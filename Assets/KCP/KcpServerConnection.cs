using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

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
    }
}
