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
        public ushort port = 7896;

        KcpTransport transport;
        IConnection clientConnection;
        IConnection serverConnection;

        [UnitySetUp]
        public IEnumerator Setup() => UniTask.ToCoroutine(async () =>
        {
            // each test goes in a different port
            // that way the transports can take some time to cleanup
            // without interfering with each other.
            port++;

            var transportGo = new GameObject("kcpTransport", typeof(KcpTransport));

            transport = transportGo.GetComponent<KcpTransport>();

            transport.Port = port;

            await transport.ListenAsync();

            Task<IConnection> acceptTask = transport.AcceptAsync();
            var uriBuilder = new UriBuilder()
            {
                Host = "localhost",
                Scheme = "kcp",
                Port = port
            };

            Task<IConnection> connectTask = transport.ConnectAsync(uriBuilder.Uri);

            serverConnection = await acceptTask;
            clientConnection = await connectTask;
        });

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            transport.Disconnect();
            clientConnection?.Disconnect();
            serverConnection?.Disconnect();

            UnityEngine.Object.Destroy(transport.gameObject);
            // wait a frame so object will be destroyed

            yield return null;
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

        [UnityTest]
        public IEnumerator DisconnectFromClient() => UniTask.ToCoroutine(async () =>
        {
            clientConnection.Disconnect();

            var buffer = new MemoryStream();
            bool more = await serverConnection.ReceiveAsync(buffer);

            Assert.That(more, Is.False, "Receive should return false when the connection is disconnected");
        });

        [UnityTest]
        public IEnumerator DisconnectServerFromIdle() => UniTask.ToCoroutine(async () =>
        {
            var buffer = new MemoryStream();
            bool more = await serverConnection.ReceiveAsync(buffer);

            Assert.That(more, Is.False, "After some time of no activity, the server should disconnect");
        });

        [UnityTest]
        public IEnumerator DisconnectClientFromIdle() => UniTask.ToCoroutine(async () =>
        {
            // after certain amount of time with no messages, it should disconnect
            var buffer = new MemoryStream();
            bool more = await clientConnection.ReceiveAsync(buffer);

            Assert.That(more, Is.False, "After some time of no activity, the client should disconnect");
        });

    }
}
