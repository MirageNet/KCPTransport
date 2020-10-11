using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mirror.KCP
{
    public abstract class KcpConnection : IConnection
    {
        protected UdpClient udpClient;
        protected IPEndPoint remoteEndpoint;
        protected KCPTransport.KCP kcp;
        protected bool open;

        protected KcpConnection()
        {
        }

        protected void SetupKcp()
        {
            kcp = new KCPTransport.KCP(0, RawSend);
            kcp.NoDelay(0, 10, 2, 1);
            kcp.SetStreamMode(true);
            _ = Tick();
        }

        async UniTask Tick()
        {
            try
            {
                while (open)
                {
                    kcp.Update();
                    uint sleepTime = kcp.Check();

                    if (sleepTime > 0)
                    {
                        await UniTask.Delay((int)sleepTime);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // fine,  socket was closed,  no more ticking needed
            }
            catch(Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        volatile bool isWaiting = false;

        readonly AutoResetUniTaskCompletionSource dataAvailable = AutoResetUniTaskCompletionSource.Create();

        internal void RawInput(byte[] buffer)
        {
            kcp.Input(buffer, 0, buffer.Length, true, false);

            if (isWaiting && kcp.PeekSize() > 0)
            {
                // we just got a full message
                // Let the receivers know
                dataAvailable.TrySetResult();
            }
        }

        protected abstract void RawSend(byte[] data, int length);

        public Task SendAsync(ArraySegment<byte> data)
        {
            int result = kcp.Send(data.Array, data.Offset, data.Count);

            if (result < 0)
                return Task.FromException(new SocketException((int)SocketError.SocketError));

            return Task.CompletedTask;
        }

        /// <summary>
        ///     reads a message from connection
        /// </summary>
        /// <param name="buffer">buffer where the message will be written</param>
        /// <returns>true if we got a message, false if we got disconnected</returns>
        public async Task<bool> ReceiveAsync(MemoryStream buffer)
        {
            int msgSize = kcp.PeekSize();

            if (msgSize < 0)
            {
                isWaiting = true;
                await dataAvailable.Task;
                isWaiting = false;
                msgSize = kcp.PeekSize();
            }

            if (msgSize <=0 )
            {
                // disconnected
                return false;
            }

            // we have some data,  return it
            buffer.SetLength(msgSize);
            buffer.Position = 0;
            buffer.TryGetBuffer(out ArraySegment<byte> data);
            kcp.Recv(data.Array, data.Offset, data.Count);

            return true;
        }

        /// <summary>
        ///     Disconnect this connection
        /// </summary>
        public virtual void Disconnect()
        {
            open = false;
        }

        /// <summary>
        ///     the address of endpoint we are connected to
        ///     Note this can be IPEndPoint or a custom implementation
        ///     of EndPoint, which depends on the transport
        /// </summary>
        /// <returns></returns>
        public EndPoint GetEndPointAddress()
        {
            return remoteEndpoint;
        }
    }
}
