using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Mirror.KCP
{
    [Category("Performance")]
    [Category("Benchmark")]
    public class MultipleClients
    {
        const string ScenePath = "Assets/Tests/Performance/Scene.unity";
        const string MonsterPath = "Assets/Tests/Performance/Monster.prefab";
        const int Warmup = 50;
        const int MeasureCount = 256;

        const int ClientCount = 50;
        const int MonsterCount = 10;

        public NetworkServer server;
        public Transport transport;

        public NetworkIdentity monsterPrefab;


        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // load scene
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters { loadSceneMode = LoadSceneMode.Additive });
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(scene);

            monsterPrefab = AssetDatabase.LoadAssetAtPath<NetworkIdentity>(MonsterPath);
            // load host
            server = Object.FindObjectOfType<NetworkServer>();

            server.Authenticated.AddListener(conn => server.SetClientReady(conn));

            System.Threading.Tasks.Task task = server.ListenAsync();

            while (!task.IsCompleted)
                yield return null;

            transport = Object.FindObjectOfType<Transport>();

            // connect from a bunch of clients
            for (int i = 0; i< ClientCount; i++)
                yield return StartClient(i, transport);

            // spawn a bunch of monsters
            for (int i = 0; i < MonsterCount; i++)
                SpawnMonster(i);

            // wait until all monsters are spawned
            while (Object.FindObjectsOfType<MonsterBehavior>().Count() < MonsterCount * (ClientCount + 1))
            {
                yield return new WaitForSeconds(0.1f);
            }
        }

        private IEnumerator StartClient(int i, Transport transport)
        {
            var clientGo = new GameObject($"Client {i}", typeof(NetworkClient));
            NetworkClient client = clientGo.GetComponent<NetworkClient>();
            client.Transport = transport;

            client.RegisterPrefab(monsterPrefab.gameObject);
            client.ConnectAsync("localhost");
            while (!client.IsConnected)
                yield return null;
        }

        private void SpawnMonster(int i)
        {
            NetworkIdentity monster = GameObject.Instantiate(monsterPrefab);

            monster.GetComponent<MonsterBehavior>().MonsterId = i;
            monster.gameObject.name = $"Monster {i}";
            server.Spawn(monster.gameObject);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // shutdown
            server.Disconnect();
            yield return null;

            // unload scene
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        [Performance]
        public IEnumerator SyncMonsters()
        {
            yield return Measure.Frames().MeasurementCount(MeasureCount).WarmupCount(Warmup).Run();
        }
    }
}

