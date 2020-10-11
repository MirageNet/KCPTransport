using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

namespace Mirror.KCP
{
    public class KcpServerConnection : KcpConnection
    {
        public KcpServerConnection(UdpClient udpClient, IPEndPoint remoteEndpoint) 
        {
            this.udpClient = udpClient;
            this.remoteEndpoint = remoteEndpoint;
            open = true;
            SetupKcp();
        }

        protected override void RawSend(byte[] data, int length)
        {
            udpClient.Send(data, length, remoteEndpoint);
        }


        public async Task HandShake()
        {
            // Send a greeting during handshake
            var memoryStream = new MemoryStream();
            await SendAsync(KcpTransport.Hello);

            if (!await ReceiveAsync(memoryStream))
            {
                throw new SocketException((int)SocketError.SocketError);
            }
        }
    }
}
