
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;

public class SushiMultiplayer : MonoBehaviour
{
    [SerializeField] private NetworkConveyor networkConveyor;
    [SerializeField] private NetworkMatchTimer matchTimer;

    private ISession session;
    private string joinCode = "";
    private string status = "Not connected";
    private bool servicesReady;
    private bool operationInProgress;
    private NetworkManager observedManager;
    private string gameplayScenePath;
    private bool returningToLobby;
    private bool rematchLoading;
    private NetworkSceneManager rematchSceneManager;

    void Awake()
    {
        // Give both local player instances a bounded rendering budget.
        // Uncapped Game views compete for the same CPU/GPU in Multiplayer Play Mode.
        Application.targetFrameRate = 60;
        Application.runInBackground = true;
    }

    private bool CanConnect => servicesReady && !operationInProgress && session == null &&
        (NetworkManager.Singleton == null ||
         (!NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.ShutdownInProgress));

    private void ConfigureTransport()
    {
        Application.runInBackground = true;
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || manager.IsListening) return;
        if (manager.NetworkConfig.NetworkTransport is UnityTransport transport)
        {
            // Virtual-player windows can process accumulated packets in bursts.
            // The default 128-packet queue overflowed before Relay disconnected.
            transport.MaxPacketQueueSize = Math.Max(transport.MaxPacketQueueSize, 1024);
        }
    }

    void Update()
    {
        // This manager survives the synchronized rematch scene load.
        if (networkConveyor == null)
            networkConveyor = FindFirstObjectByType<NetworkConveyor>();
        if (matchTimer == null)
            matchTimer = FindFirstObjectByType<NetworkMatchTimer>();
        if (observedManager == NetworkManager.Singleton) return;
        UnsubscribeNetworkEvents();
        observedManager = NetworkManager.Singleton;
        if (observedManager == null) return;
        observedManager.OnClientConnectedCallback += HandleConnected;
        observedManager.OnClientDisconnectCallback += HandleDisconnected;
    }

    void OnDestroy()
    {
        UnsubscribeNetworkEvents();
        if (rematchSceneManager != null)
            rematchSceneManager.OnSceneEvent -= HandleRematchSceneEvent;
    }

    public void StartRematch()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer || rematchLoading || operationInProgress) return;
        if (matchTimer == null || !matchTimer.HasMatchEnded) return;

        rematchLoading = true;
        operationInProgress = true;
        status = "Both players ready. Loading next match...";
        rematchSceneManager = manager.SceneManager;
        rematchSceneManager.OnSceneEvent += HandleRematchSceneEvent;
        SceneEventProgressStatus result = rematchSceneManager.LoadScene(gameplayScenePath, LoadSceneMode.Single);
        if (result != SceneEventProgressStatus.Started)
        {
            rematchSceneManager.OnSceneEvent -= HandleRematchSceneEvent;
            rematchLoading = false;
            operationInProgress = false;
            status = $"Could not load next match: {result}. Return to Lobby to retry.";
            Debug.LogError(status);
        }
    }

    private void HandleRematchSceneEvent(SceneEvent sceneEvent)
    {
        if (!rematchLoading || sceneEvent.SceneEventType != SceneEventType.LoadEventCompleted) return;
        rematchSceneManager.OnSceneEvent -= HandleRematchSceneEvent;
        rematchLoading = false;
        operationInProgress = false;
        networkConveyor = FindFirstObjectByType<NetworkConveyor>();
        matchTimer = FindFirstObjectByType<NetworkMatchTimer>();

        if (sceneEvent.ClientsThatTimedOut.Count != 0 || sceneEvent.ClientsThatCompleted.Count != 2 ||
            NetworkManager.Singleton.ConnectedClientsIds.Count != 2 || matchTimer == null)
        {
            status = "A player did not finish loading. Return to Lobby to retry.";
            return;
        }

        matchTimer.StartMatch();
        status = "Next match started!";
    }

    void UnsubscribeNetworkEvents()
    {
        if (observedManager == null) return;
        observedManager.OnClientConnectedCallback -= HandleConnected;
        observedManager.OnClientDisconnectCallback -= HandleDisconnected;
    }

    void HandleConnected(ulong clientId)
    {
        if (observedManager != null && clientId == observedManager.LocalClientId)
            status = observedManager.IsHost ? "Hosting room!" : "Connected to host!";
    }

    void HandleDisconnected(ulong clientId)
    {
        if (returningToLobby) return;
        if (observedManager == null || clientId != observedManager.LocalClientId) return;
        status = "Network disconnected. Leave Room before retrying.";
        Debug.LogWarning($"Network disconnected: {observedManager.DisconnectReason}");
    }

    async void Start()
    {
        gameplayScenePath = networkConveyor != null
            ? networkConveyor.gameObject.scene.path
            : SceneManager.GetActiveScene().path;
        if (matchTimer == null)
            matchTimer = FindFirstObjectByType<NetworkMatchTimer>();
        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            servicesReady = true;
            status = "Ready to host or join";
            Debug.Log("Unity Services initialized!");
        }
        catch (Exception e)
        {
            Debug.LogError($"Initialization failed: {e}");
            status = "Initialization failed!";
        }
    }

    public async void HostGame()
    {
        if (!CanConnect) return;
        ConfigureTransport();
        operationInProgress = true;
        status = "Creating room...";
        try
        {
            var options = new SessionOptions
            {
                MaxPlayers = 2
            }.WithRelayNetwork();

            session = await MultiplayerService.Instance.CreateSessionAsync(options);

            status = "Hosting room!";

            Debug.Log($"Room created! Join code: {session.Code}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Hosting failed: {e}");
            status = "Failed to host room!";
        }
        finally { operationInProgress = false; }
    }

    public async void JoinGame(string code)
    {
        if (!CanConnect) return;
        ConfigureTransport();
        code = code?.Trim();
        if (string.IsNullOrEmpty(code))
        {
            status = "Enter a join code";
            return;
        }
        operationInProgress = true;
        status = "Joining room...";
        try
        {
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);

            status = NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient
                ? "Connected to host!"
                : "Joined room; connecting to host...";

            Debug.Log("Successfully joined room!");
        }
        catch (Exception e)
        {
            Debug.LogError($"Joining failed: {e}");
            status = "Failed to join room!";
        }
        finally { operationInProgress = false; }
    }

    public async void LeaveRoom()
    {
        if (operationInProgress) return;
        // Leaving is available before a match or after it finishes/disconnects.
        if (matchTimer != null && matchTimer.IsMatchRunning &&
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
            return;
        operationInProgress = true;
        returningToLobby = true;
        status = "Leaving room...";
        try
        {
            // Capture the gameplay scene before disposing the persistent network manager.
            string scenePath = gameplayScenePath;
            if (string.IsNullOrEmpty(scenePath))
                throw new InvalidOperationException("Cannot return to lobby: gameplay scene is unknown.");

            if (session != null && session.State != SessionState.Deleted)
                await session.LeaveAsync();
            session = null;
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null)
            {
                if (manager.IsListening && !manager.ShutdownInProgress)
                    manager.Shutdown();

                // Shutdown completes on a subsequent frame. Starting another session
                // sooner can reuse the old transport and network object registrations.
                while (manager != null && (manager.IsListening || manager.ShutdownInProgress))
                    await Task.Yield();

                UnsubscribeNetworkEvents();
                if (manager != null) Destroy(manager.gameObject);
                await Task.Yield();
            }

            // Recreate the timer, scoreboard, conveyor, recipes and hand together.
            // NetworkManager persists across scene loads, so it must be removed above.
            await SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
        }
        catch (Exception e)
        {
            Debug.LogError($"Leaving failed: {e}");
            status = "Failed to leave room. Try Leave Room again.";
        }
        finally
        {
            operationInProgress = false;
            returningToLobby = false;
        }
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(20, 20, 250, 430));

        GUILayout.Label("Sushi Showdown Multiplayer");

        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && CanConnect;
        if (GUILayout.Button("Host Game", GUILayout.Height(40)))
        {
            HostGame();
        }

        GUILayout.Space(10);
        GUILayout.Label("Join Code:");

        joinCode = GUILayout.TextField(joinCode);

        GUI.enabled = previousEnabled && CanConnect;
        if (GUILayout.Button("Join Game", GUILayout.Height(40)))
        {
            JoinGame(joinCode.Trim());
        }
        GUI.enabled = previousEnabled;

        bool matchFinished = matchTimer != null && matchTimer.HasMatchEnded;
        bool disconnected = session != null && NetworkManager.Singleton != null &&
            !NetworkManager.Singleton.IsConnectedClient && !operationInProgress;
        if (session != null && (matchTimer == null || !matchTimer.HasMatchStarted || matchFinished || disconnected))
        {
            GUI.enabled = previousEnabled && !operationInProgress;
            if (GUILayout.Button(matchFinished || disconnected ? "Return to Lobby" : "Leave Room"))
                LeaveRoom();
            GUI.enabled = previousEnabled;
        }

        GUILayout.Space(10);

        if (session != null)
        {
            GUILayout.Label("Room Code: " + session.Code);
        }

        GUILayout.Label("Status: " + status);

        GUILayout.Space(10);

        if (networkConveyor != null && networkConveyor.IsSpawned)
        {
            GUILayout.Label(
                "Total Claims: " + networkConveyor.TotalClaims.Value
            );
        }

        GUILayout.Space(10);

        // Networking debug information
        if (NetworkManager.Singleton != null)
        {
            GUILayout.Label(
                "NGO Server: " + NetworkManager.Singleton.IsServer
            );

            GUILayout.Label(
                "NGO Client: " + NetworkManager.Singleton.IsClient
            );

            GUILayout.Label(
                "NGO Connected: " +
                NetworkManager.Singleton.IsConnectedClient
            );
        }

        GUILayout.EndArea();
    }
}
