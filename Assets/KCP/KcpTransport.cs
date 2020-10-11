#region Statements

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

#endregion

namespace Mirror.KCP
{
    public class KcpTransport : Transport
    {
        #region Fields

        UdpClient listener;

        [Header("Transport Configuration")]
        [SerializeField] public  ushort Port = 7777;
        [SerializeField] private string _bindAddress = "localhost";

        readonly Dictionary<IPEndPoint, KcpServerConnection> connectedClients = new Dictionary<IPEndPoint, KcpServerConnection>();
        readonly Channel<KcpServerConnection> acceptedConnections = Channel.CreateSingleConsumerUnbounded<KcpServerConnection>();

        // hand shake message
        internal static readonly ArraySegment<byte> Hello = new ArraySegment<byte>(new byte[] { 0 });

        #endregion

        public override IEnumerable<string> Scheme => new[] { "kcp" };

        #region Server

        /// <summary>
        ///     Open up the port and listen for connections
        ///     Use in servers.
        /// </summary>
        /// <exception>If we cannot start the transport</exception>
        /// <returns></returns>
        public override Task ListenAsync()
        {
            listener = new UdpClient(AddressFamily.InterNetworkV6);
            listener.Client.DualMode = true;
            listener.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, Port));

            _ = ReadLoop();
            return Task.CompletedTask;
        }

        private async Task ReadLoop()
        {
            try
            {
                var endPoint = new IPEndPoint(IPAddress.Any, 0);
                while (listener.Client != null)
                {
                    UdpReceiveResult result = await listener.ReceiveAsync();
                    // send it to the proper connection
                    RawInput(result.RemoteEndPoint, result.Buffer);
                }
            }
            catch (ObjectDisposedException)
            {
                // the listener has been closed
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                listener.Close();
            }
        }

        private void RawInput(IPEndPoint endpoint, byte[] data)
        {
            // is this a new connection?                    
            if (!connectedClients.TryGetValue(endpoint, out KcpServerConnection connection))
            {
                // add it to a queue
                connection = new KcpServerConnection(listener, endpoint);
                acceptedConnections.Writer.TryWrite(connection);
                connectedClients.Add(endpoint, connection);
            }

            connection.RawInput(data);
        }

        /// <summary>
        ///     Stop listening to the port
        /// </summary>
        public override void Disconnect()
        {
            listener?.Close();
        }

        /// <summary>
        ///     Accepts a connection from a client.
        ///     After ListenAsync completes,  clients will queue up until you call AcceptAsync
        ///     then you get the connection to the client
        /// </summary>
        /// <returns>The connection to a client</returns>
        public override async Task<IConnection> AcceptAsync()
        {
            KcpServerConnection connection = await acceptedConnections.Reader.ReadAsync();

            await connection.HandShake();

            return connection;
        }

        /// <summary>
        ///     Retrieves the address of this server.
        ///     Useful for network discovery
        /// </summary>
        /// <returns>the url at which this server can be reached</returns>
        public override IEnumerable<Uri> ServerUri()
        {
            {
                var builder = new UriBuilder
                {
                    Scheme = "kcp",
                    Host = _bindAddress,
                    Port = Port
                };
                return new[] { builder.Uri };
            }
        }

        #endregion

        #region Client

        /// <summary>
        ///     Determines if this transport is supported in the current platform
        /// </summary>
        /// <returns>true if the transport works in this platform</returns>
        public override bool Supported => Application.platform != RuntimePlatform.WebGLPlayer;

        /// <summary>
        ///     Connect to a server located at a provided uri
        /// </summary>
        /// <param name="uri">address of the server to connect to</param>
        /// <returns>The connection to the server</returns>
        /// <exception>If connection cannot be established</exception>
        public override async Task<IConnection> ConnectAsync(Uri uri)
        {
            var client = new KcpClientConnection();

            await client.ConnectAsync(uri.Host, (ushort)uri.Port);
            return client;
        }

        #endregion
    }
}
