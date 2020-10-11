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
        public KcpServerConnection(UdpClient udpClient, IPEndPoint remoteEndpoint) : base(udpClient)
        {
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
            Debug.Log("Server send hello");
            await SendAsync(KcpTransport.Hello);

            Debug.Log("Server waiting for hello");
            if (!await ReceiveAsync(memoryStream))
            {
                Debug.Log("Server did not get anything");
                throw new SocketException((int)SocketError.SocketError);
            }
            Debug.Log("Server received hello");
        }
    }
}
