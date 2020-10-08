#region Statements

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

#endregion

namespace Mirror.KCP
{
    public sealed class Server
    {
        private Socket _socket;
        private TcpListener _socketListener;

        #region Class Specific

        /// <summary>
        ///     Start up the server.
        /// </summary>
        public async Task Start(string address, ushort port)
        {
            try
            {
                Debug.Log("Starting up server.");

                IPHostEntry hostEntry = await Dns.GetHostEntryAsync(address);

                if (hostEntry.AddressList.Length == 0)
                {
                    throw new Exception("Unable to resolve host: " + address);
                }

                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);

                if (true)
                    _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.NoDelay, true);

                IPHostEntry host = await Dns.GetHostEntryAsync(address);
                IPAddress ipAddress = IPAddress.Parse(host.AddressList[1].ToString());
                IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

                _socket.Bind(localEndPoint);

                _socketListener = new TcpListener(localEndPoint);
                _socketListener.Start(100);

                Debug.Log("Server started finished starting up.");
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
        public async Task<Socket> AcceptAsync()
        {
            if (_socketListener == null) return null;

            Socket client = await _socketListener.AcceptSocketAsync();

            Debug.Log($"Incoming connection: {client.RemoteEndPoint}");

            return client;
        }
        /// <summary>
        ///     Shutdown the server and stop listening for connections.
        /// </summary>
        public void Shutdown()
        {
            _socketListener.Stop();
            _socket.Close();
            _socket.Dispose();
            _socket = null;
        }

        #endregion
    }
}
