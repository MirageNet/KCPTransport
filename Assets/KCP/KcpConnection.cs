#region Statements

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using KcpProject;
using UnityEngine;
using Random = System.Random;

#endregion

namespace Mirror.KCP
{
    public sealed class KcpConnection : IConnection
    {
        #region Fields

        private Socket _socket;
        private KcpProject.KCP _kcp;
        private ByteBuffer _receiveBuffer = ByteBuffer.Allocate(1024 * 32);

        #endregion

        #region Class Specific

        public KcpConnection(Socket handler)
        {
            _socket = handler;
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

                _kcp = new KcpProject.KCP((uint)(new Random().Next(1, int.MaxValue)), SendAsync);

                _kcp.NoDelay(0, 10, 2, 1);
                _kcp.SetStreamMode(true);
                _receiveBuffer.Clear();

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
            Debug.Log("Kcp send triggered.");
        }

        #endregion

        #region Implementation of IConnection

        public Task SendAsync(ArraySegment<byte> data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     reads a message from connection
        /// </summary>
        /// <param name="buffer">buffer where the message will be written</param>
        /// <returns>true if we got a message, false if we got disconnected</returns>
        public Task<bool> ReceiveAsync(MemoryStream buffer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Disconnect this connection
        /// </summary>
        public void Disconnect()
        {
            _socket?.Close();
            _socket = null;
            _receiveBuffer.Clear();
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
