using System;
using System.Net;
using System.Net.Sockets;

namespace KCPTransport
{
    class UDPSession
    {
        Socket mSocket;
        KCP mKCP;

        readonly ByteBuffer mRecvBuffer = ByteBuffer.Allocate(1024 * 32);
        uint mNextUpdateTime;

        public bool IsConnected { get { return mSocket != null && mSocket.Connected; } }
        public bool WriteDelay { get; set; }
        public bool AckNoDelay { get; set; }

        public IPEndPoint RemoteAddress { get; private set; }
        public IPEndPoint LocalAddress { get; private set; }

        public void Connect(string host, int port)
        {
            IPHostEntry hostEntry = Dns.GetHostEntry(host);
            if (hostEntry.AddressList.Length == 0)
            {
                throw new ArgumentException("Unable to resolve host: " + host);
            }
            IPAddress endpoint = hostEntry.AddressList[0];
            mSocket = new Socket(endpoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            mSocket.Connect(endpoint, port);
            RemoteAddress = (IPEndPoint)mSocket.RemoteEndPoint;
            LocalAddress = (IPEndPoint)mSocket.LocalEndPoint;
            mKCP = new KCP((uint)(new Random().Next(1, int.MaxValue)), rawSend);
            // normal:  false, 40, 2, 1
            // fast:    false, 30, 2, 1
            // fast2:   false, 20, 2, 1
            // fast3:   false, 10, 2, 1
            mKCP.SetNoDelay(false, 30, 2, 1);
            mKCP.SetStreamMode(true);
            mRecvBuffer.Clear();
        }

        public void Close()
        {
            if (mSocket != null)
            {
                mSocket.Close();
                mSocket = null;
                mRecvBuffer.Clear();
            }
        }

        void rawSend(byte[] data, int length)
        {
            mSocket?.Send(data, length, SocketFlags.None);
        }

        public int Send(byte[] data, int index, int length)
        {
            if (mSocket == null)
                return -1;

            int waitsnd = mKCP.WaitSnd;
            if (waitsnd < mKCP.SendWindowMax && waitsnd < mKCP.RmtWnd)
            {
                int sendBytes = 0;
                do
                {
                    int n = Math.Min((int)mKCP.Mss, length - sendBytes);
                    mKCP.Send(data, index + sendBytes, n);
                    sendBytes += n;
                } while (sendBytes < length);

                waitsnd = mKCP.WaitSnd;
                if (waitsnd >= mKCP.SendWindowMax || waitsnd >= mKCP.RmtWnd || !WriteDelay)
                {
                    mKCP.Flush(false);
                }

                return length;
            }

            return 0;
        }

        public int Recv(byte[] data, int index, int length)
        {
            // remaining part from last time
            if (mRecvBuffer.ReadableBytes > 0)
            {
                int recvBytes = Math.Min(mRecvBuffer.ReadableBytes, length);
                Buffer.BlockCopy(mRecvBuffer.RawBuffer, mRecvBuffer.ReaderIndex, data, index, recvBytes);
                mRecvBuffer.ReaderIndex += recvBytes;
                // reset read/write pointer after reading
                if (mRecvBuffer.ReaderIndex == mRecvBuffer.WriterIndex)
                {
                    mRecvBuffer.Clear();
                }
                return recvBytes;
            }

            if (mSocket == null)
                return -1;

            if (!mSocket.Poll(0, SelectMode.SelectRead))
            {
                return 0;
            }

            int rn;
            try
            {
                rn = mSocket.Receive(mRecvBuffer.RawBuffer, mRecvBuffer.WriterIndex, mRecvBuffer.WritableBytes, SocketFlags.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                rn = -1;
            }

            if (rn <= 0)
            {
                return rn;
            }
            mRecvBuffer.WriterIndex += rn;

            int inputN = mKCP.Input(mRecvBuffer.RawBuffer, mRecvBuffer.ReaderIndex, mRecvBuffer.ReadableBytes, true, AckNoDelay);
            if (inputN < 0)
            {
                mRecvBuffer.Clear();
                return inputN;
            }
            mRecvBuffer.Clear();

            // read all complete messages
            while (true)
            {
                int size = mKCP.PeekSize();
                if (size <= 0) break;

                mRecvBuffer.EnsureWritableBytes(size);

                int n = mKCP.Recv(mRecvBuffer.RawBuffer, mRecvBuffer.WriterIndex, size);
                if (n > 0) mRecvBuffer.WriterIndex += n;
            }

            // there's data to be received
            return mRecvBuffer.ReadableBytes > 0 ? Recv(data, index, length) : 0;
        }

        public void Update()
        {
            if (mSocket == null)
                return;

            if (mNextUpdateTime == 0 || mKCP.CurrentMS >= mNextUpdateTime)
            {
                mKCP.Update();
                mNextUpdateTime = mKCP.Check();
            }
        }
    }
}
