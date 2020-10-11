using System;
using System.IO;
using System.Linq;
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
        protected KCP kcp;
        protected bool open;

        private int lastSend = 0;

        internal static readonly ArraySegment<byte> Hello = new ArraySegment<byte>(new byte[] { 0 });
        private static readonly ArraySegment<byte> Goodby = new ArraySegment<byte>(new byte[] { 1 });

        protected KcpConnection()
        {
        }

        protected void SetupKcp()
        {
            kcp = new KCP(0, RawSend);
            // normal:  false, 40, 2, 1
            // fast:    false, 30, 2, 1
            // fast2:   true, 20, 2, 1
            // fast3:   true, 10, 2, 1
            kcp.SetNoDelay(false, 40, 2, 1);
            _ = Tick();
        }

        private async UniTask Tick()
        {
            try
            {
                while (open)
                {
                    kcp.Update();

                    int check = kcp.Check();

                    // call every 10 ms unless check says we can wait longer
                    if (check < 10)
                        check = 10;

                    await UniTask.Delay(check);
                }
            }
            catch (ObjectDisposedException)
            {
                // fine,  socket was closed,  no more ticking needed
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                open = false;
                dataAvailable.TrySetResult();
                Dispose();
            }
        }

        protected virtual void Dispose()
        {
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
            kcp.Send(data.Array, data.Offset, data.Count);
            return Task.CompletedTask;
        }

        /// <summary>
        ///     reads a message from connection
        /// </summary>
        /// <param name="buffer">buffer where the message will be written</param>
        /// <returns>true if we got a message, false if we got disconnected</returns>
        public async Task<bool> ReceiveAsync(MemoryStream buffer)
        {
            if (!open)
                return false;

            int msgSize = kcp.PeekSize();

            if (msgSize < 0)
            {
                isWaiting = true;
                await dataAvailable.Task;
                isWaiting = false;
                msgSize = kcp.PeekSize();
            }

            if (!open)
                return false;

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

            // if we receive a disconnect message,  then close everything
            
            var dataSegment = new ArraySegment<byte>(buffer.GetBuffer(), 0, msgSize);
            if (dataSegment.SequenceEqual(Goodby))
            {
                open = false;
                return false;
            }

            return true;
        }

        internal async Task Handshake()
        {
            // send a greeting and see if the server replies
            await SendAsync(Hello);
            var stream = new MemoryStream();
            if (!await ReceiveAsync(stream))
            {
                throw new SocketException((int)SocketError.SocketError);
            }
        }

        /// <summary>
        ///     Disconnect this connection
        /// </summary>
        public virtual void Disconnect()
        {
            // send a disconnect message and disconnect
            if (open)
            {
                _ = SendAsync(Goodby);
                kcp.Flush(false);
            }
            open = false;

            // EOF is now available
            dataAvailable.TrySetResult();
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
