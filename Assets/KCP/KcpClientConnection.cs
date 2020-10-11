using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

namespace Mirror.KCP
{
    public class KcpClientConnection : KcpConnection
    {
        #region Client
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

        private async Task Handshake()
        {

            // send a greeting and see if the server replies
            await SendAsync(KcpTransport.Hello);
            var stream = new MemoryStream();
            if (!await ReceiveAsync(stream))
            {
                throw new SocketException((int)SocketError.SocketError);
            }
        }

        private async Task ReceiveLoopAsync()
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
        #endregion


        /// <summary>
        ///     Disconnect this connection
        /// </summary>
        public override void Disconnect()
        {
            base.Disconnect();
            udpClient.Close();
        }

        protected override void RawSend(byte[] data, int length)
        {
            udpClient.Send(data, length);
        }
    }
}