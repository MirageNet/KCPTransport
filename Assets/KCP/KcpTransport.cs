using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mirror.KCP
{
    public class KcpTransport : Transport
    {
        Socket socket;

        [Header("Transport Configuration")]
        public ushort Port = 7777;

        [SerializeField] private string _bindAddress = "localhost";

        readonly Dictionary<EndPoint, KcpServerConnection> connectedClients = new Dictionary<EndPoint, KcpServerConnection>();
        readonly Channel<KcpServerConnection> acceptedConnections = Channel.CreateSingleConsumerUnbounded<KcpServerConnection>();

        public override IEnumerable<string> Scheme => new[] { "kcp" };

        #region listening
        readonly byte[] buffer = new byte[1500];
        /// <summary>
        ///     Open up the port and listen for connections
        ///     Use in servers.
        /// </summary>
        /// <exception>If we cannot start the transport</exception>
        /// <returns></returns>
        public override Task ListenAsync()
        {
            socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
            socket.DualMode = true;
            socket.Bind(new IPEndPoint(IPAddress.IPv6Any, Port));

            ReadLoop();

            return Task.CompletedTask;
        }

        void ReadLoop()
        {
            var saea = new SocketAsyncEventArgs();
            saea.Completed += ReceivedPacket;

            ReceiveFrom(saea);
        }

        private void ReceiveFrom(SocketAsyncEventArgs saea)
        {
            var newClientEP = new IPEndPoint(IPAddress.IPv6Any, 0);
            saea.RemoteEndPoint = newClientEP;
            saea.SetBuffer(buffer, 0, buffer.Length);

            if (!socket.ReceiveFromAsync(saea))
                ReceivedPacket(null, saea);
        }

        private void ReceivedPacket(object sender, SocketAsyncEventArgs saea)
        {
            if (saea.SocketError == SocketError.Success)
            {
                RawInput(saea.RemoteEndPoint, saea.Buffer, saea.Count);
                ReceiveFrom(saea);
            }
        }

        void RawInput(EndPoint endpoint, byte[] data, int msgLength)
        {
            // is this a new connection?                    
            if (!connectedClients.TryGetValue(endpoint, out KcpServerConnection connection))
            {
                // add it to a queue
                connection = new KcpServerConnection(socket, endpoint);
                acceptedConnections.Writer.TryWrite(connection);
                connectedClients.Add(endpoint, connection);
            }

            connection.RawInput(data, msgLength);
        }
        #endregion

        /// <summary>
        ///     Stop listening to the port
        /// </summary>
        public override void Disconnect()
        {
            // disconnect all connections and stop listening to the port
            /*foreach (KcpServerConnection connection in connectedClients.Values)
            {
                connection.Disconnect();
            }*/

            socket?.Close();
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

            await connection.Handshake();

            return connection;
        }

        /// <summary>
        ///     Retrieves the address of this server.
        ///     Useful for network discovery
        /// </summary>
        /// <returns>the url at which this server can be reached</returns>
        public override IEnumerable<Uri> ServerUri()
        {
            var builder = new UriBuilder
            {
                Scheme = "kcp",
                Host = _bindAddress,
                Port = Port
            };
            return new[] { builder.Uri };
        }

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

            ushort port = (ushort)(uri.IsDefaultPort? Port : uri.Port);

            await client.ConnectAsync(uri.Host, port);
            return client;
        }
    }
}
