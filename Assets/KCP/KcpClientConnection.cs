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
            udpClient = new UdpClient(remoteEndpoint.AddressFamily);
            udpClient.Connect(remoteEndpoint);
            open = true;

            SetupKcp();
            _ = ReceiveLoopAsync();

            await Handshake();
        }

        async Task ReceiveLoopAsync()
        {
            try
            {
                while (udpClient.Client != null)
                {
                    UdpReceiveResult result = await udpClient.ReceiveAsync();
                    // send it to the proper connection
                    RawInput(result.Buffer);
                }
            }
            catch (ObjectDisposedException)
            {
                // connection was closed.  no problem
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        protected override void Dispose()
        {
            udpClient.Close();
        }

        protected override void RawSend(byte[] data, int length)
        {
            udpClient.Send(data, length);
        }
    }
}
