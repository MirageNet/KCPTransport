#region Statements

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using Random = System.Random;

#endregion

namespace Mirror.KCP
{
    public sealed class KcpConnection : IConnection
    {
        #region Fields

        private Socket _socket;
        private KCPTransport.KCP _kcp;
        private uint _nextUpdateTime = 0;

        #endregion

        #region Class Specific

        public KcpConnection(Socket handler)
        {
            if(handler == null) return;

            _socket = handler;

            _ = Task.Run(Tick);
        }

        private void Tick()
        {
            while(_socket != null)
            {
                while (0 != _nextUpdateTime && _kcp.CurrentMS < _nextUpdateTime)
                {
                    Task.Delay(100);
                }

                _kcp.Update();
                _nextUpdateTime = _kcp.Check();
            }
        }

        public async Task<IConnection> Connect(string address, ushort port)
        {
            try
            {
                IPHostEntry hostEntry = await Dns.GetHostEntryAsync(address);

                if (hostEntry.AddressList.Length == 0)
                {
                    throw new Exception("Unable to resolve host: " + address);
                }

                IPAddress endpoint = hostEntry.AddressList[0];
                _socket = new Socket(endpoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

                await _socket.ConnectAsync(endpoint, port);
                byte[] data = new byte[] { 42 };
                _socket.Send(data);
                _kcp = new KCPTransport.KCP((uint)(new Random().Next(1, int.MaxValue)), SendAsync);

                _kcp.NoDelay(0, 10, 2, 1);
                _kcp.SetStreamMode(true);

                _ = Task.Run(Tick);

                return this;
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception($"Connection failure. Exception: {ex}"));

                return null;
            }
        }

        private void SendAsync(byte[] data, int length)
        {
            _socket?.Send(data, length, SocketFlags.None);
        }

        #endregion

        #region Implementation of IConnection

        public Task SendAsync(ArraySegment<byte> data)
        {
            byte[] buffer = new byte[data.Count];

            Array.Copy(data.Array, data.Offset, buffer, 0, data.Count);

            int result = _kcp.Send(buffer);

            Debug.Log(result == 0
                ? $"Connection sent data: {BitConverter.ToString(data.Array)}"
                : $"Connection failed to send data: {BitConverter.ToString(data.Array)}");

            return result == 1 ? Task.CompletedTask : null;
        }

        /// <summary>
        ///     reads a message from connection
        /// </summary>
        /// <param name="buffer">buffer where the message will be written</param>
        /// <returns>true if we got a message, false if we got disconnected</returns>
        public Task<bool> ReceiveAsync(MemoryStream buffer)
        {
            buffer.SetLength(0);
            buffer.TryGetBuffer(out ArraySegment<byte> byteBuffer);

            while (_kcp.Recv(byteBuffer.Array) > -1)
            {
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        /// <summary>
        ///     Disconnect this connection
        /// </summary>
        public void Disconnect()
        {
            _socket?.Close();
            _socket = null;
        }

        /// <summary>
        ///     the address of endpoint we are connected to
        ///     Note this can be IPEndPoint or a custom implementation
        ///     of EndPoint, which depends on the transport
        /// </summary>
        /// <returns></returns>
        public EndPoint GetEndPointAddress()
        {
            return _socket.RemoteEndPoint;
        }

        #endregion
    }
}
