using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

namespace Mirror.KCP
{
    public class KcpClientConnection : KcpConnection
    {

        readonly byte[] buffer = new byte[1500];

        /// <summary>
        /// Client connection,  does not share the UDP client with anyone
        /// so we can set up our own read loop
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        public KcpClientConnection() : base() 
        {
        }

        internal async Task ConnectAsync(string host, ushort port)
        {
            IPAddress[] ipAddress = await Dns.GetHostAddressesAsync(host);
            if (ipAddress.Length < 1)
                throw new SocketException((int)SocketError.HostNotFound);

            remoteEndpoint = new IPEndPoint(ipAddress[0], port);
            socket = new Socket(remoteEndpoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(remoteEndpoint);
            SetupKcp();

            ReceiveLoop();

            await Handshake();
        }

        void ReceiveLoop()
        {
            socket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref remoteEndpoint, ReceiveFrom, null);
        }

        private void ReceiveFrom(IAsyncResult ar)
        {
            try
            {
                int msgLength = socket.EndReceiveFrom(ar, ref remoteEndpoint);
                RawInput(buffer, msgLength);
                socket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref remoteEndpoint, ReceiveFrom, null);
            }
            catch (ObjectDisposedException)
            {
                // fine,  the socket has been closed, we can stop
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        protected override void Dispose()
        {
            socket.Close();
        }

        protected override void RawSend(byte[] data, int length)
        {
            socket.Send(data, length, SocketFlags.None);
        }
    }
}
