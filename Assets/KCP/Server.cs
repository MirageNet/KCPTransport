#region Statements

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using Mirror;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

#endregion

namespace Mirror.KCP
{
    public sealed class Server
    {
        private Socket _socket;
        UdpClient listener;

        Dictionary<IPEndPoint, IConnection> connectedClients = new Dictionary<IPEndPoint, IConnection>();
        Channel<KcpConnection> acceptedConnections = Channel.CreateSingleConsumerUnbounded<KcpConnection>();

        #region Class Specific

        /// <summary>
        ///     Start up the server.
        /// </summary>
        public async Task Start(ushort port)
        {
            listener = new UdpClient(port);
            var groupEP = new IPEndPoint(IPAddress.Any, port);

            try
            {
                while (true)
                {
                    Console.WriteLine("Waiting for broadcast");
                    UdpReceiveResult result = await listener.ReceiveAsync();

                    // is this a new connection?                    
                    if(connectedClients.ContainsKey(result.RemoteEndPoint))
                    {
                        // if yes,  create a connection object, and keep a list of the connections

                    }
                    else
                    {
                        // add it to a queue
                        KcpConnection newConn = new KcpConnection(null);
                        acceptedConnections.Writer.TryWrite(newConn);
                        connectedClients.Add(result.RemoteEndPoint, newConn);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception($"Server failed to startup. Exception: {ex}"));
            }
        }

        /// <summary>
        ///     Start accepting a connection that has come in.
        /// </summary>
        /// <returns></returns>
        public async Task<IConnection> AcceptAsync()
        {
            // pop a connection from the queue,  and return it.
            KcpConnection newConnection = await acceptedConnections.Reader.ReadAsync();
            return newConnection;
        }

        /// <summary>
        ///     Shutdown the server and stop listening for connections.
        /// </summary>
        public void Shutdown()
        {
            listener.Close();
        }

        #endregion
    }
}
