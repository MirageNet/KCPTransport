#region Statements

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using Mirror;

#endregion

namespace Mirror.KCP
{
    public sealed class Server
    {
        private Socket _socket;

        #region Class Specific

        /// <summary>
        ///     Start up the server.
        /// </summary>
        public async Task Start(ushort port)
        {
            UdpClient listener = new UdpClient(port);
            var groupEP = new IPEndPoint(IPAddress.Any, port);

            /*
            try
            {
                while (true)
                {
                    Console.WriteLine("Waiting for broadcast");
                    byte[] bytes = listener.Receive(ref groupEP);

                    // is this a new connection?
                       // if yes,  create a connection object, and keep a list of the connections
                       // add it to a queue
                    // else,  find the connection for that endpoint,  and forward the message to it
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception($"Server failed to startup. Exception: {ex}"));
            }
            */
        }

        /// <summary>
        ///     Start accepting a connection that has come in.
        /// </summary>
        /// <returns></returns>
        public Task<IConnection> AcceptAsync()
        {
            // pop a connection from the queue,  and return it.
            return Task.FromResult(new KcpConnection(null) as IConnection);
        }


        /// <summary>
        ///     Shutdown the server and stop listening for connections.
        /// </summary>
        public void Shutdown()
        {
           
        }

        #endregion
    }
}
