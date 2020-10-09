using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using Cysharp.Threading.Tasks;
using System;
using System.IO;

using UnityEngine;
using Random = UnityEngine.Random;

namespace Mirror.KCP
{
    public class ConnectionTest
    {
        public const int PORT = 7896;

        Server server;
        KcpConnection client;

        // A Test behaves as an ordinary method
        [UnityTest]
        public IEnumerator CanEstablishConnections() => UniTask.ToCoroutine(async () =>
        {
            server = new Server();
            _ = server.Start(PORT);

            client = new KcpConnection(null);

            await client.Connect("localhost", PORT);
            IConnection connection = await server.AcceptAsync();

            Assert.That(connection, Is.Not.Null);

            server.Shutdown();
        });


        [UnityTest]
        public IEnumerator CanSendData() => UniTask.ToCoroutine(async () =>
        {
            server = new Server();
            _ = server.Start(PORT);

            client = new KcpConnection(null);

            await client.Connect("localhost", PORT);
            IConnection connection = await server.AcceptAsync();
            byte[] data = new byte[] { (byte)Random.Range(1,255) };
            await client.SendAsync(new ArraySegment<byte>(data));

            var buffer = new MemoryStream();
            await connection.ReceiveAsync(buffer);
            Assert.That(buffer.ToArray(), Is.EquivalentTo(data));

            server.Shutdown();
        });
    }
}
