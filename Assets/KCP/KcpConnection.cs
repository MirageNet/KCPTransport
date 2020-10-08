using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Mirror.KCP
{
    public sealed class KcpConnection : IConnection
    {
        #region Implementation of IConnection

        public Task SendAsync(ArraySegment<byte> data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// reads a message from connection
        /// </summary>
        /// <param name="buffer">buffer where the message will be written</param>
        /// <returns>true if we got a message, false if we got disconnected</returns>
        public Task<bool> ReceiveAsync(MemoryStream buffer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Disconnect this connection
        /// </summary>
        public void Disconnect()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// the address of endpoint we are connected to
        /// Note this can be IPEndPoint or a custom implementation
        /// of EndPoint, which depends on the transport
        /// </summary>
        /// <returns></returns>
        public EndPoint GetEndPointAddress()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
