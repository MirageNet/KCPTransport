using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using Cysharp.Threading.Tasks;

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
            await server.Start(PORT);

            client = new KcpConnection(null);

            await client.Connect("localhost", PORT);
            IConnection connection = await server.AcceptAsync();

            Assert.That(connection, Is.Not.Null);
        });

    }
}
