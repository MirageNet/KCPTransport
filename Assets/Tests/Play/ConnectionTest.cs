using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using Cysharp.Threading.Tasks;
using System;
using System.IO;

using UnityEngine;
using Random = UnityEngine.Random;
using System.Threading.Tasks;

namespace Mirror.KCP
{
    public class ConnectionTest
    {
        public const int PORT = 7896;

        KcpTransport transport;
        IConnection clientConnection;
        IConnection serverConnection;

        [UnitySetUp]
        public IEnumerator Setup() => UniTask.ToCoroutine(async () =>
        {
            var transportGo = new GameObject("kcpTransport", typeof(KcpTransport));

            transport = transportGo.GetComponent<KcpTransport>();

            transport.Port = PORT;

            await transport.ListenAsync();

            Task<IConnection> acceptTask = transport.AcceptAsync();
            Task<IConnection> connectTask = transport.ConnectAsync(new Uri("kcp://localhost:7896"));

            serverConnection = await acceptTask;
            clientConnection = await connectTask;
        });

        [TearDown]
        public void TearDown()
        {
            transport.Disconnect();
            clientConnection?.Disconnect();
            serverConnection?.Disconnect();

            UnityEngine.Object.Destroy(transport.gameObject);
        }

        // A Test behaves as an ordinary method
        [Test]
        public void Connect()
        {
            Assert.That(clientConnection, Is.Not.Null);
            Assert.That(serverConnection, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SendDataFromClient() => UniTask.ToCoroutine(async () =>
        {
            byte[] data = new byte[] { (byte)Random.Range(1, 255) };
            await clientConnection.SendAsync(new ArraySegment<byte>(data));

            var buffer = new MemoryStream();
            await serverConnection.ReceiveAsync(buffer);
            Assert.That(buffer.ToArray(), Is.EquivalentTo(data));
        });

        [UnityTest]
        public IEnumerator SendDataFromServer() => UniTask.ToCoroutine(async () =>
        {
            byte[] data = new byte[] { (byte)Random.Range(1, 255) };
            await serverConnection.SendAsync(new ArraySegment<byte>(data));

            var buffer = new MemoryStream();
            await clientConnection.ReceiveAsync(buffer);
            Assert.That(buffer.ToArray(), Is.EquivalentTo(data));
        });


        [UnityTest]
        public IEnumerator DisconnectFromServer() => UniTask.ToCoroutine(async () =>
        {
            serverConnection.Disconnect();

            var buffer = new MemoryStream();
            bool more = await clientConnection.ReceiveAsync(buffer);

            Assert.That(more, Is.False, "Receive should return false when the connection is disconnected");
        });
    }
}
