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
        });

        [TearDown]
        public void TearDown()
        {
            transport.Disconnect();
            clientConnection?.Disconnect();
            serverConnection?.Disconnect();

            GameObject.Destroy(transport.gameObject);
        }

        // A Test behaves as an ordinary method
        [UnityTest]
        public IEnumerator CanEstablishConnections() => UniTask.ToCoroutine(async () =>
        {
            Task<IConnection> acceptTask = transport.AcceptAsync();
            Task<IConnection> connectTask = transport.ConnectAsync(new Uri("kcp://localhost:7896"));

            serverConnection = await acceptTask;
            clientConnection = await connectTask;

            Assert.That(clientConnection, Is.Not.Null);
            Assert.That(serverConnection, Is.Not.Null);
        });

        [UnityTest]
        public IEnumerator CanSendData() => UniTask.ToCoroutine(async () =>
        {
            Task<IConnection> acceptTask = transport.AcceptAsync();
            Task<IConnection> connectTask = transport.ConnectAsync(new Uri("kcp://localhost:7896"));

            serverConnection = await acceptTask;
            clientConnection = await connectTask;

            byte[] data = new byte[] { (byte)Random.Range(1, 255) };
            await clientConnection.SendAsync(new ArraySegment<byte>(data));

            var buffer = new MemoryStream();
            await serverConnection.ReceiveAsync(buffer);
            Assert.That(buffer.ToArray(), Is.EquivalentTo(data));

            transport.Disconnect();
        });
    }
}
