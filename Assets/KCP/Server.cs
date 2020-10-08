#region Statements

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using KcpProject;

#endregion

namespace Mirror.KCP
{
    public sealed class Server
    {
        private KcpProject.KCP _kcp;
        private ByteBuffer _receiveBuffer = ByteBuffer.Allocate(1024 * 32);
        private Socket _socket;
        internal ConcurrentQueue<IPEndPoint> QueuedConnections = new ConcurrentQueue<IPEndPoint>();
        private uint mNextUpdateTime = 0;

        #region Class Specific

        /// <summary>
        ///     Start up the server and start listening for connections.
        /// </summary>
        public async Task Start(string address, ushort port)
        {
            IPHostEntry hostEntry = await Dns.GetHostEntryAsync(address);

            if (hostEntry.AddressList.Length == 0)
            {
                throw new Exception("Unable to resolve host: " + address);
            }

            IPAddress endPoint = hostEntry.AddressList[0];

            _socket = new Socket(endPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

            await _socket.ConnectAsync(endPoint, port);

            _kcp = new KcpProject.KCP((uint)new Random().Next(1, int.MaxValue), Send);
            _kcp.NoDelay(1, 10, 2, 1);
            _kcp.SetStreamMode(true);
            _receiveBuffer.Clear();

            _ = Task.Run(Tick);
        }

        private void Tick()
        {
            while (_socket != null)
            {
                if (0 != mNextUpdateTime && _kcp.CurrentMS < mNextUpdateTime)
                {
                    continue;
                }

                _kcp.Update();
                mNextUpdateTime = _kcp.Check();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="dataLength"></param>
        private void Send(byte[] data, int dataLength)
        {
            _socket?.Send(data, dataLength, SocketFlags.None);
        }

        /// <summary>
        ///     Shutdown the server and stop listening for connections.
        /// </summary>
        public void Shutdown()
        {
            _socket?.Close();
            _socket = null;
            _receiveBuffer.Clear();
        }

        #endregion
    }
}
