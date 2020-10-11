using System;
using System.Collections.Generic;

namespace Mirror.KCP
{
    public class KCP
    {
        public const int IKCP_RTO_NDL = 30;  // no delay min rto
        public const int IKCP_RTO_MIN = 100; // normal min rto
        public const int IKCP_RTO_DEF = 200;
        public const int IKCP_RTO_MAX = 60000;
        public const int IKCP_CMD_PUSH = 81; // cmd: push data
        public const int IKCP_CMD_ACK = 82; // cmd: ack
        public const int IKCP_CMD_WASK = 83; // cmd: window probe (ask)
        public const int IKCP_CMD_WINS = 84; // cmd: window size (tell)
        public const int IKCP_ASK_SEND = 1;  // need to send IKCP_CMD_WASK
        public const int IKCP_ASK_TELL = 2;  // need to send IKCP_CMD_WINS
        public const int IKCP_WND_SND = 32;
        public const int IKCP_WND_RCV = 32;
        public const int IKCP_MTU_DEF = 1400;
        public const int IKCP_ACK_FAST = 3;
        public const int IKCP_INTERVAL = 100;
        public const int IKCP_OVERHEAD = 24;
        public const int IKCP_DEADLINK = 20;
        public const int IKCP_THRESH_INIT = 2;
        public const int IKCP_THRESH_MIN = 2;
        public const int IKCP_PROBE_INIT = 7000;   // 7 secs to probe window size
        public const int IKCP_PROBE_LIMIT = 120000; // up to 120 secs to probe window
        public const int IKCP_SN_OFFSET = 12;

        private static readonly DateTime refTime = DateTime.Now;

        internal struct ackItem
        {
            internal uint serialNumber;
            internal uint timestamp;
        }

        // kcp members.
        readonly uint conv; uint mtu;
        uint snd_una; uint snd_nxt; uint rcv_nxt;
        uint ssthresh;
        uint rx_rttval; uint rx_srtt;
        uint rx_rto; uint rx_minrto;
        uint cwnd; uint probe;
        uint interval; uint ts_flush;
        bool noDelay; bool updated;
        uint ts_probe; uint probe_wait;
        uint incr;

        int fastresend;
        int nocwnd; bool streamEnabled;
        readonly List<Segment> sendQueue = new List<Segment>(16);
        readonly List<Segment> receiveQueue = new List<Segment>(16);
        readonly List<Segment> sendBuffer = new List<Segment>(16);
        readonly List<Segment> receiveBuffer = new List<Segment>(16);
        readonly List<ackItem> ackList = new List<ackItem>(16);

        byte[] buffer;
        int reserved;
        readonly Action<byte[], int> output; // buffer, size

        public uint SendWindowMax { get; private set; }
        public uint ReceiveWindowMax { get; private set; }
        public uint RmtWnd { get; private set; }
        public uint Mss { get; private set; }

        // get how many packet is waiting to be sent
        public int WaitSnd { get { return sendBuffer.Count + sendQueue.Count; } } 

        // internal time.
        public uint CurrentMS => (uint)DateTime.Now.Subtract(refTime).TotalMilliseconds;

        // create a new kcp control object, 'conv' must equal in two endpoint
        // from the same connection.
        public KCP(uint conv_, Action<byte[], int> output_)
        {
            conv = conv_;
            SendWindowMax = IKCP_WND_SND;
            ReceiveWindowMax = IKCP_WND_RCV;
            RmtWnd = IKCP_WND_RCV;
            mtu = IKCP_MTU_DEF;
            Mss = mtu - IKCP_OVERHEAD;
            rx_rto = IKCP_RTO_DEF;
            rx_minrto = IKCP_RTO_MIN;
            interval = IKCP_INTERVAL;
            ts_flush = IKCP_INTERVAL;
            ssthresh = IKCP_THRESH_INIT;
            buffer = new byte[mtu];
            output = output_;
        }

        // check the size of next message in the recv queue
        public int PeekSize()
        {

            if (receiveQueue.Count == 0) return -1;

            Segment seq = receiveQueue[0];

            if (seq.frg == 0) return seq.data.ReadableBytes;

            if (receiveQueue.Count < seq.frg + 1) return -1;

            int length = 0;

            foreach (Segment item in receiveQueue)
            {
                length += item.data.ReadableBytes;
                if (item.frg == 0)
                    break;
            }

            return length;
        }


        public int Recv(byte[] buffer)
        {
            return Recv(buffer, 0, buffer.Length);
        }

        // Receive data from kcp state machine
        //
        // Return number of bytes read.
        //
        // Return -1 when there is no readable data.
        //
        // Return -2 if len(buffer) is smaller than kcp.PeekSize().
        public int Recv(byte[] buffer, int index, int length)
        {
            int peekSize = PeekSize();
            if (peekSize < 0)
                return -1;

            if (peekSize > length)
                return -2;

            bool fastRecover = (receiveQueue.Count >= ReceiveWindowMax);

            // merge fragment.
            int count = 0;
            int n = index;
            foreach (Segment seg in receiveQueue)
            {
                // copy fragment data into buffer.
                Buffer.BlockCopy(seg.data.RawBuffer, seg.data.ReaderIndex, buffer, n, seg.data.ReadableBytes);
                n += seg.data.ReadableBytes;

                count++;
                uint fragment = seg.frg;
                Segment.Put(seg);
                if (fragment == 0) break;
            }

            if (count > 0)
            {
                receiveQueue.RemoveRange(0, count);
            }

            // move available data from rcv_buf -> rcv_queue
            count = 0;
            foreach (Segment seg in receiveBuffer)
            {
                if (seg.sn == rcv_nxt && receiveQueue.Count + count < ReceiveWindowMax)
                {
                    receiveQueue.Add(seg);
                    rcv_nxt++;
                    count++;
                }
                else
                {
                    break;
                }
            }

            if (count > 0)
            {
                receiveBuffer.RemoveRange(0, count);
            }

            // fast recover
            if (receiveQueue.Count < ReceiveWindowMax && fastRecover)
            {
                // ready to send back IKCP_CMD_WINS in ikcp_flush
                // tell remote my window size
                probe |= IKCP_ASK_TELL;
            }

            return n - index;
        }

        public int Send(byte[] buffer)
        {
            return Send(buffer, 0, buffer.Length);
        }

        // user/upper level send, returns below zero for error
        public int Send(byte[] buffer, int index, int length)
        {
            if (length == 0) return -1;

            if (streamEnabled)
            {
                int sendQueueSize = sendQueue.Count;
                if (sendQueueSize > 0)
                {
                    Segment seg = sendQueue[sendQueueSize - 1];
                    if (seg.data.ReadableBytes < Mss)
                    {
                        int capacity = (int)(Mss - seg.data.ReadableBytes);
                        int writen = Math.Min(capacity, length);
                        seg.data.WriteBytes(buffer, index, writen);
                        index += writen;
                        length -= writen;
                    }
                }
            }

            if (length == 0)
                return 0;

            int count;
            if (length <= Mss)
                count = 1;
            else
                count = (int)((length + Mss - 1) / Mss);

            if (count > 255) return -2;

            if (count == 0) count = 1;

            // fragment
            for (int i = 0; i < count; i++)
            {
                int size = Math.Min(length, (int)Mss);

                var seg = Segment.Get(size);
                seg.data.WriteBytes(buffer, index, size);
                index += size;
                length -= size;

                seg.frg = (!streamEnabled ? (byte)(count - i - 1) : 0U);
                sendQueue.Add(seg);
            }

            return 0;
        }

        // update ack.
        void UpdateAck(int rtt)
        {
            // https://tools.ietf.org/html/rfc6298
            if (rx_srtt == 0)
            {
                rx_srtt = (uint)rtt;
                rx_rttval = (uint)rtt >> 1;
            }
            else
            {
                uint delta = (uint)Math.Abs(rtt - rx_srtt);
                rx_srtt += delta >> 3;

                if (rtt < rx_srtt - rx_rttval)
                {
                    // if the new RTT sample is below the bottom of the range of
                    // what an RTT measurement is expected to be.
                    // give an 8x reduced weight versus its normal weighting
                    rx_rttval += (delta - rx_rttval) >> 5;
                }
                else
                {
                    rx_rttval += (delta - rx_rttval) >> 2;
                }
            }

            uint rto = rx_srtt + Math.Max(interval, rx_rttval << 2);
            rx_rto = Utils.Clamp(rto, rx_minrto, IKCP_RTO_MAX);
        }

        void ShrinkBuf()
        {
            snd_una = sendBuffer.Count > 0 ? sendBuffer[0].sn : snd_nxt;
        }

        void ParseAck(uint sn)
        {
            if (sn < snd_una || sn >= snd_nxt) return;

            foreach (Segment seg in sendBuffer)
            {
                if (sn == seg.sn)
                {
                    // mark and free space, but leave the segment here,
                    // and wait until `una` to delete this, then we don't
                    // have to shift the segments behind forward,
                    // which is an expensive operation for large window
                    seg.acked = 1;
                    break;
                }
                if (sn < seg.sn)
                    break;
            }
        }

        void ParseFastrack(uint sn, uint ts)
        {
            if (sn < snd_una || sn >= snd_nxt)
                return;

            foreach (Segment seg in sendBuffer)
            {
                if (sn < seg.sn)
                    break;
                else if (sn != seg.sn && seg.ts <= ts)
                    seg.fastack++;
            }
        }

        void ParseUna(uint una)
        {
            int count = 0;
            foreach (Segment seg in sendBuffer)
            {
                if (una >seg.sn)
                {
                    count++;
                    Segment.Put(seg);
                }
                else
                    break;
            }

            if (count > 0)
                sendBuffer.RemoveRange(0, count);
        }

        void AckPush(uint sn, uint ts)
        {
            ackList.Add(new ackItem { serialNumber = sn, timestamp = ts });
        }

        bool ParseData(Segment newseg)
        {
            uint sn = newseg.sn;
            if (sn >= rcv_nxt + ReceiveWindowMax || sn < rcv_nxt)
                return true;

            int n = receiveBuffer.Count - 1;
            int insert_idx = 0;
            bool repeat = false;
            for (int i = n; i >= 0; i--)
            {
                Segment seg = receiveBuffer[i];
                if (seg.sn == sn)
                {
                    repeat = true;
                    break;
                }

                if (sn > seg.sn)
                {
                    insert_idx = i + 1;
                    break;
                }
            }

            if (!repeat)
            {
                if (insert_idx == n + 1)
                    receiveBuffer.Add(newseg);
                else
                    receiveBuffer.Insert(insert_idx, newseg);
            }

            // move available data from rcv_buf -> rcv_queue
            int count = 0;
            foreach (Segment seg in receiveBuffer)
            {
                if (seg.sn == rcv_nxt && receiveQueue.Count + count < ReceiveWindowMax)
                {
                    rcv_nxt++;
                    count++;
                }
                else
                {
                    break;
                }
            }

            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                    receiveQueue.Add(receiveBuffer[i]);
                receiveBuffer.RemoveRange(0, count);
            }
            return repeat;
        }

        // Input when you received a low level packet (eg. UDP packet), call it
        // regular indicates a regular packet has received(not from FEC)
        // 
        // 'ackNoDelay' will trigger immediate ACK, but surely it will not be efficient in bandwidth
        public int Input(byte[] data, int index, int size, bool regular, bool ackNoDelay)
        {
            uint s_una = snd_una;
            if (size < IKCP_OVERHEAD) return -1;

            int offset = index;
            uint latest = 0;
            int flag = 0;

            while (true)
            {
                uint ts = 0;
                uint sn = 0;
                uint length = 0;
                uint una = 0;
                uint conv_ = 0;

                ushort wnd = 0;
                byte cmd = 0;
                byte frg = 0;

                if (size - (offset - index) < IKCP_OVERHEAD) break;

                offset += Utils.Decode32U(data, offset, ref conv_);

                if (conv != conv_) return -1;

                offset += Utils.Decode8u(data, offset, ref cmd);
                offset += Utils.Decode8u(data, offset, ref frg);
                offset += Utils.Decode16U(data, offset, ref wnd);
                offset += Utils.Decode32U(data, offset, ref ts);
                offset += Utils.Decode32U(data, offset, ref sn);
                offset += Utils.Decode32U(data, offset, ref una);
                offset += Utils.Decode32U(data, offset, ref length);

                if (size - (offset - index) < length) return -2;

                switch (cmd)
                {
                    case IKCP_CMD_PUSH:
                    case IKCP_CMD_ACK:
                    case IKCP_CMD_WASK:
                    case IKCP_CMD_WINS:
                        break;
                    default:
                        return -3;
                }

                // only trust window updates from regular packets. i.e: latest update
                if (regular)
                {
                    RmtWnd = wnd;
                }

                ParseUna(una);
                ShrinkBuf();

                if (IKCP_CMD_ACK == cmd)
                {
                    ParseAck(sn);
                    ParseFastrack(sn, ts);
                    flag |= 1;
                    latest = ts;
                }
                else if (IKCP_CMD_PUSH == cmd)
                {
                    if (sn < rcv_nxt + ReceiveWindowMax)
                    {
                        AckPush(sn, ts);
                        if (sn >= rcv_nxt)
                        {
                            var seg = Segment.Get((int)length);
                            seg.conv = conv_;
                            seg.cmd = cmd;
                            seg.frg = frg;
                            seg.wnd = wnd;
                            seg.ts = ts;
                            seg.sn = sn;
                            seg.una = una;
                            seg.data.WriteBytes(data, offset, (int)length);
                            _ = ParseData(seg);
                        }
                    }
                }
                else if (IKCP_CMD_WASK == cmd)
                {
                    // ready to send back IKCP_CMD_WINS in Ikcp_flush
                    // tell remote my window size
                    probe |= IKCP_ASK_TELL;
                }
                else if (IKCP_CMD_WINS == cmd)
                {
                    // do nothing
                }
                else
                {
                    return -3;
                }

                offset += (int)length;
            }

            // update rtt with the latest ts
            // ignore the FEC packet
            if (flag != 0 && regular)
            {
                uint current = CurrentMS;
                if (current >= latest)
                {
                    UpdateAck(Utils.TimeDiff(current, latest));
                }
            }

            // cwnd update when packet arrived
            if (nocwnd == 0)
            {
                if (snd_una > s_una)
                {
                    if (cwnd < RmtWnd)
                    {
                        uint _mss = Mss;
                        if (cwnd < ssthresh)
                        {
                            cwnd++;
                            incr += _mss;
                        }
                        else
                        {
                            if (incr < _mss)
                            {
                                incr = _mss;
                            }
                            incr += (_mss * _mss) / incr + (_mss) / 16;
                            if ((cwnd + 1) * _mss <= incr)
                            {
                                if (_mss > 0)
                                    cwnd = (incr + _mss - 1) / _mss;
                                else
                                    cwnd = incr + _mss - 1;
                            }
                        }
                        if (cwnd > RmtWnd)
                        {
                            cwnd = RmtWnd;
                            incr = RmtWnd * _mss;
                        }
                    }
                }
            }

            // ack immediately
            if (ackNoDelay && ackList.Count > 0)
            {
                Flush(true);
            }

            return 0;
        }

        ushort WndUnused()
        {
            if (receiveQueue.Count < ReceiveWindowMax)
                return (ushort)(ReceiveWindowMax - receiveQueue.Count);
            return 0;
        }

        // flush pending data
        public uint Flush(bool ackOnly)
        {
            var seg = Segment.Get(32);
            seg.conv = conv;
            seg.cmd = IKCP_CMD_ACK;
            seg.wnd = WndUnused();
            seg.una = rcv_nxt;

            int writeIndex = reserved;

            Action<int> makeSpace = (space) =>
            {
                if (writeIndex + space > mtu)
                {
                    output(buffer, writeIndex);
                    writeIndex = reserved;
                }
            };

            Action flushBuffer = () =>
            {
                if (writeIndex > reserved)
                {
                    output(buffer, writeIndex);
                }
            };

            // flush acknowledges
            for (int i = 0; i < ackList.Count; i++)
            {
                makeSpace(KCP.IKCP_OVERHEAD);
                ackItem ack = ackList[i];
                if (ack.serialNumber >= rcv_nxt || ackList.Count - 1 == i)
                {
                    seg.sn = ack.serialNumber;
                    seg.ts = ack.timestamp;
                    writeIndex += seg.Encode(buffer, writeIndex);
                }
            }
            ackList.Clear();

            // flash remain ack segments
            if (ackOnly)
            {
                flushBuffer();
                return interval;
            }

            uint current = 0;
            // probe window size (if remote window size equals zero)
            if (RmtWnd == 0)
            {
                current = CurrentMS;
                if (probe_wait == 0)
                {
                    probe_wait = IKCP_PROBE_INIT;
                    ts_probe = current + probe_wait;
                }
                else
                {
                    if (current >= ts_probe)
                    {
                        if (probe_wait < IKCP_PROBE_INIT)
                            probe_wait = IKCP_PROBE_INIT;
                        probe_wait += probe_wait / 2;
                        if (probe_wait > IKCP_PROBE_LIMIT)
                            probe_wait = IKCP_PROBE_LIMIT;
                        ts_probe = current + probe_wait;
                        probe |= IKCP_ASK_SEND;
                    }
                }
            }
            else
            {
                ts_probe = 0;
                probe_wait = 0;
            }

            // flush window probing commands
            if ((probe & IKCP_ASK_SEND) != 0)
            {
                seg.cmd = IKCP_CMD_WASK;
                makeSpace(IKCP_OVERHEAD);
                writeIndex += seg.Encode(buffer, writeIndex);
            }

            if ((probe & IKCP_ASK_TELL) != 0)
            {
                seg.cmd = IKCP_CMD_WINS;
                makeSpace(IKCP_OVERHEAD);
                writeIndex += seg.Encode(buffer, writeIndex);
            }

            probe = 0;

            // calculate window size
            uint cwnd_ = Math.Min(SendWindowMax, RmtWnd);
            if (nocwnd == 0)
                cwnd_ = Math.Min(cwnd, cwnd_);

            // sliding window, controlled by snd_nxt && sna_una+cwnd
            int newSegsCount = 0;
            for (int k = 0; k < sendQueue.Count; k++)
            {
                if (snd_nxt >= snd_una + cwnd_)
                    break;

                Segment newseg = sendQueue[k];
                newseg.conv = conv;
                newseg.cmd = IKCP_CMD_PUSH;
                newseg.sn = snd_nxt;
                sendBuffer.Add(newseg);
                snd_nxt++;
                newSegsCount++;
            }

            if (newSegsCount > 0)
            {
                sendQueue.RemoveRange(0, newSegsCount);
            }

            // calculate resent
            uint resent = (uint)fastresend;
            if (fastresend <= 0) resent = 0xffffffff;

            // check for retransmissions
            current = CurrentMS;
            ulong change = 0; ulong lostSegs = 0;
            int minrto = (int)interval;

            for (int k = 0; k < sendBuffer.Count; k++)
            {
                Segment segment = sendBuffer[k];
                bool needSend = false;
                if (segment.acked == 1)
                    continue;
                if (segment.xmit == 0)  // initial transmit
                {
                    needSend = true;
                    segment.rto = rx_rto;
                    segment.resendts = current + segment.rto;
                }
                else if (segment.fastack >= resent) // fast retransmit
                {
                    needSend = true;
                    segment.fastack = 0;
                    segment.rto = rx_rto;
                    segment.resendts = current + segment.rto;
                    change++;
                }
                else if (segment.fastack > 0 && newSegsCount == 0) // early retransmit
                {
                    needSend = true;
                    segment.fastack = 0;
                    segment.rto = rx_rto;
                    segment.resendts = current + segment.rto;
                    change++;
                }
                else if (current >= segment.resendts) // RTO
                {
                    needSend = true;
                    if (!noDelay)
                        segment.rto += rx_rto;
                    else
                        segment.rto += rx_rto / 2;
                    segment.fastack = 0;
                    segment.resendts = current + segment.rto;
                    lostSegs++;
                }

                if (needSend)
                {
                    current = CurrentMS;
                    segment.xmit++;
                    segment.ts = current;
                    segment.wnd = seg.wnd;
                    segment.una = seg.una;

                    int need = IKCP_OVERHEAD + segment.data.ReadableBytes;
                    makeSpace(need);
                    writeIndex += segment.Encode(buffer, writeIndex);
                    Buffer.BlockCopy(segment.data.RawBuffer, segment.data.ReaderIndex, buffer, writeIndex, segment.data.ReadableBytes);
                    writeIndex += segment.data.ReadableBytes;
                }

                // get the nearest rto
                int _rto = Utils.TimeDiff(segment.resendts, current);
                if (_rto > 0 && _rto < minrto)
                {
                    minrto = _rto;
                }
            }

            // flash remain segments
            flushBuffer();

            // cwnd update
            if (nocwnd == 0)
            {
                // update ssthresh
                // rate halving, https://tools.ietf.org/html/rfc6937
                if (change > 0)
                {
                    uint inflght = snd_nxt - snd_una;
                    ssthresh = inflght / 2;
                    if (ssthresh < IKCP_THRESH_MIN)
                        ssthresh = IKCP_THRESH_MIN;
                    cwnd = ssthresh + resent;
                    incr = cwnd * Mss;
                }

                // congestion control, https://tools.ietf.org/html/rfc5681
                if (lostSegs > 0)
                {
                    ssthresh = cwnd / 2;
                    if (ssthresh < IKCP_THRESH_MIN)
                        ssthresh = IKCP_THRESH_MIN;
                    cwnd = 1;
                    incr = Mss;
                }

                if (cwnd < 1)
                {
                    cwnd = 1;
                    incr = Mss;
                }
            }

            return (uint)minrto;
        }

        // update state (call it repeatedly, every 10ms-100ms), or you can ask
        // ikcp_check when to call it again (without ikcp_input/_send calling).
        // 'current' - current timestamp in millisec.
        public void Update()
        {
            uint current = CurrentMS;

            if (!updated)
            {
                updated = true;
                ts_flush = current;
            }

            int slap = Utils.TimeDiff(current, ts_flush);

            if (slap >= 10000 || slap < -10000)
            {
                ts_flush = current;
                slap = 0;
            }

            if (slap >= 0)
            {
                ts_flush += interval;
                if (current >= ts_flush)
                    ts_flush = current + interval;
                Flush(false);
            }
        }

        // Determine when should you invoke ikcp_update:
        // returns when you should invoke ikcp_update in millisec, if there
        // is no ikcp_input/_send calling. you can call ikcp_update in that
        // time, instead of call update repeatly.
        // Important to reduce unnacessary ikcp_update invoking. use it to
        // schedule ikcp_update (eg. implementing an epoll-like mechanism,
        // or optimize ikcp_update when handling massive kcp connections)
        public uint Check()
        {
            uint current = CurrentMS;

            uint ts_flush_ = ts_flush;
            int tm_packet = 0x7fffffff;

            if (!updated)
                return current;

            if (current >= ts_flush_ + 10000 || current < ts_flush_ - 10000)
                ts_flush_ = current;

            if (current >= ts_flush_)
                return current;

            int tm_flush_ = Utils.TimeDiff(ts_flush_, current);

            foreach (Segment seg in sendBuffer)
            {
                int diff = Utils.TimeDiff(seg.resendts, current);
                if (diff <= 0)
                    return current;
                if (diff < tm_packet)
                    tm_packet = diff;
            }

            int minimal = tm_packet;
            if (tm_packet >= tm_flush_)
                minimal = tm_flush_;
            if (minimal >= interval)
                minimal = (int)interval;

            return current + (uint)minimal;
        }

        // change MTU size, default is 1400
        public int SetMtu(int mtu_)
        {
            if (mtu_ < 50)
                return -1;
            if (reserved >= (int)(mtu - IKCP_OVERHEAD) || reserved < 0)
                return -1;

            buffer = new byte[mtu_];

            mtu = (uint)mtu_;
            Mss = mtu - IKCP_OVERHEAD - (uint)reserved;
            return 0;
        }

        // turbo mode: SetNoDelay(1, 20, 2, 1)
        // normal mode: SetNoDelay(0, 40, 0, 0)
        // interval: internal update timer interval in millisec, default is 100ms
        // resend: 0:disable fast resend(default), 1:enable fast resend
        // nc: 0:normal congestion control(default), 1:disable congestion control
        public void SetNoDelay(bool nodelay_, uint interval_, int resend_, int nc_)
        {
            if (nodelay_)
            {
                noDelay = nodelay_;
                rx_minrto = IKCP_RTO_NDL;
            }

            if (interval_ >= 0)
            {
                if (interval_ > 5000)
                    interval_ = 5000;
                else if (interval_ < 10)
                    interval_ = 10;
                interval = interval_;
            }

            if (resend_ >= 0)
                fastresend = resend_;

            if (nc_ >= 0)
                nocwnd = nc_;
        }

        // set maximum window size: sndwnd=32, rcvwnd=32 by default
        public void SetWindowSize(uint sendWindow, uint recvWindow)
        {
            if (sendWindow > 0)
                SendWindowMax = sendWindow;

            if (recvWindow > 0)
                ReceiveWindowMax = Math.Max(recvWindow, IKCP_WND_RCV);
        }

        public bool ReserveBytes(int reservedSize)
        {
            if (reservedSize >= (mtu - IKCP_OVERHEAD) || reservedSize < 0)
                return false;

            reserved = reservedSize;
            Mss = mtu - IKCP_OVERHEAD - (uint)(reservedSize);
            return true;
        }

        public void SetStreamMode(bool enabled)
        {
            streamEnabled = enabled;
        }
    }
}
