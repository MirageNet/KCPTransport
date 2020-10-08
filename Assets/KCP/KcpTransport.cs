#region Statements

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

#endregion

namespace Mirror.KCP
{
    public class KcpTransport : Transport
    {
        #region Fields

        [Header("Transport Configuration")]
        [SerializeField] private ushort _port = 7777;
        [SerializeField] private string _bindAddress = "localhost";

        private Server _server;

        #endregion

        #region Overrides of Transport

        public override IEnumerable<string> Scheme => new[] { "kcp" };

        /// <summary>
        ///     Open up the port and listen for connections
        ///     Use in servers.
        /// </summary>
        /// <exception>If we cannot start the transport</exception>
        /// <returns></returns>
        public override async Task ListenAsync()
        {
            _server = new Server();

            await _server.Start(_bindAddress, _port);
        }

        /// <summary>
        ///     Stop listening to the port
        /// </summary>
        public override void Disconnect()
        {
            _server?.Shutdown();
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
        public override Task<IConnection> ConnectAsync(Uri uri)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Accepts a connection from a client.
        ///     After ListenAsync completes,  clients will queue up until you call AcceptAsync
        ///     then you get the connection to the client
        /// </summary>
        /// <returns>The connection to a client</returns>
        public override async Task<IConnection> AcceptAsync()
        {
            while (_server != null)
            {
                while (_server.QueuedConnections.TryDequeue(out IPEndPoint client))
                {
                    return new KcpConnection();
                }

                await Task.Delay(1);
            }

            return null;
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
                    Port = _port
                };
                return new[] { builder.Uri };
            }
        }

        #endregion
    }
}
