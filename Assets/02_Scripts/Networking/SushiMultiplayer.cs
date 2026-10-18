using System;
using System.Threading.Tasks;
using SushiDojo.Menu;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using Unity.Services.Lobbies;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runs before NetworkManager so a scene's duplicate cannot become another persistent manager.
[DefaultExecutionOrder(-2000)]
public class SushiMultiplayer : MonoBehaviour
{
    public static SushiMultiplayer Instance { get; private set; }
    [SerializeField] string gameplaySceneName = "SampleScene";
    [SerializeField] string menuSceneName = "MainMenu";
    [SerializeField] NetworkConveyor networkConveyor;
    [SerializeField] NetworkMatchTimer matchTimer;
    NetworkManager manager;
    MainMenu menu;
    ISession session;
    Task initialization;
    bool operationInProgress;
    bool returningToMenu;
    bool loadingMatch;
    bool guestLobbyOpened;
    bool subscribed;
    NetworkSceneManager loadingSceneManager;
    string status = "Connecting to services...";

    public string Status => status;
    public string RoomCode => session?.Code ?? "";
    public bool IsBusy => operationInProgress || returningToMenu;
    public bool ConnectionLost { get; private set; }
    public bool HasOpponent { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        Instance = this;
        manager = GetComponent<NetworkManager>();
        DontDestroyOnLoad(gameObject);
        Application.targetFrameRate = 60;
        Application.runInBackground = true;
        SceneManager.sceneLoaded += SceneLoaded;
    }

    async void Start()
    {
        if (Instance != this) return;
        manager.OnClientConnectedCallback += Connected;
        manager.OnClientDisconnectCallback += Disconnected;
        subscribed = true;
        initialization = InitializeServices();
        BindMenu();
        try { await initialization; } catch { /* The menu displays the initialization error; connect can retry. */ }
    }

    async Task InitializeServices()
    {
        try
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            if (!operationInProgress) status = "Ready to host or join";
        }
        catch (Exception e)
        {
            status = "Could not connect to services. Please try again.";
            Debug.LogException(e);
            throw;
        }
    }

    void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        networkConveyor = FindFirstObjectByType<NetworkConveyor>();
        matchTimer = FindFirstObjectByType<NetworkMatchTimer>();
        BindMenu();
    }

    void BindMenu()
    {
        UnbindMenu();
        menu = FindFirstObjectByType<MainMenu>();
        if (menu == null) return;
        menu.HostRequested += HostGame;
        menu.JoinRequested += JoinGame;
        menu.StartRequested += StartGame;
        menu.LeftLobby += LeaveRoom;
        menu.Lobby.AllowDebugInput = false;
        menu.Join.AllowDebugInput = false;
    }

    void UnbindMenu()
    {
        if (menu == null) return;
        menu.HostRequested -= HostGame;
        menu.JoinRequested -= JoinGame;
        menu.StartRequested -= StartGame;
        menu.LeftLobby -= LeaveRoom;
        menu = null;
    }

    void Update()
    {
        if (Instance != this || manager == null) return;
        if (menu == null) return;
        if (!guestLobbyOpened && session != null && manager.IsConnectedClient &&
            !manager.IsHost && !menu.IsTransitioning && !returningToMenu)
        {
            menu.EnterGuestLobby();
            guestLobbyOpened = true;
        }
        if (menu.Lobby.gameObject.activeInHierarchy)
        {
            menu.Lobby.SetRoomCode(RoomCode);
            if (menu.Lobby.OpponentJoined != HasOpponent)
                menu.Lobby.SetOpponentJoined(HasOpponent);
            menu.Lobby.SetNetworkState(status, IsBusy || session == null || !manager.IsConnectedClient);
        }
    }

    void Connected(ulong id)
    {
        HasOpponent = manager.IsServer ? manager.ConnectedClientsIds.Count == 2 : manager.IsConnectedClient;
        ConnectionLost = false;
        status = HasOpponent ? "Both players connected" :
            string.IsNullOrEmpty(RoomCode) ? "Creating room..." : "Waiting for player";
    }

    void Disconnected(ulong id)
    {
        if (returningToMenu) return;
        HasOpponent = false;
        bool inGame = SceneManager.GetActiveScene().name == gameplaySceneName;
        ConnectionLost = inGame || id == manager.LocalClientId;
        status = ConnectionLost ? "Player disconnected. Return to the main menu to reconnect." : "Waiting for player";
        if (networkConveyor != null && inGame) networkConveyor.StopLocalPresentation();
        if (menu != null && !manager.IsHost) menu.Join.ShowError(status);
    }

    public void HostGame() => Connect(null);
    public void JoinGame(string code) => Connect(code);

    async void Connect(string code)
    {
        if (IsBusy || session != null || manager.ShutdownInProgress)
        {
            if (menu != null && code != null) menu.Join.ShowError("Please wait for the current connection to finish.");
            return;
        }
        operationInProgress = true;
        guestLobbyOpened = false;
        ConnectionLost = false;
        status = code == null ? "Creating room..." : "Joining room...";
        try
        {
            if (initialization == null || initialization.IsFaulted)
                initialization = InitializeServices();
            await initialization;
            if (returningToMenu) return;
            if (manager.NetworkConfig.NetworkTransport is UnityTransport transport)
                transport.MaxPacketQueueSize = Math.Max(transport.MaxPacketQueueSize, 1024);
            if (code == null)
                session = await MultiplayerService.Instance.CreateSessionAsync(
                    new SessionOptions { MaxPlayers = 2 }.WithRelayNetwork());
            else
            {
                code = code.Trim().ToUpperInvariant();
                if (code.Length == 0) throw new ArgumentException("Enter a room code.");
                session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
            }
            HasOpponent = manager.IsHost ? manager.ConnectedClientsIds.Count == 2 : manager.IsConnectedClient;
            status = HasOpponent ? "Both players connected" : manager.IsHost ? "Waiting for player" : "Connecting to host...";
        }
        catch (Exception e)
        {
            status = code == null ? "Could not create room. Go back and retry." :
                IsRoomFull(e) ? "Full!" : "Could not join room.\nCheck the code and try again.";
            Debug.LogException(e);
            if (menu != null && code != null) menu.Join.ShowError(status);
            if (manager.IsListening && session == null) manager.Shutdown();
        }
        finally { operationInProgress = false; }
    }

    static bool IsRoomFull(Exception error)
    {
        // Session operations wrap the Lobby service exception; retain its specific reason.
        for (Exception current = error; current != null; current = current.InnerException)
        {
            if (current is LobbyServiceException lobby && lobby.Reason == LobbyExceptionReason.LobbyFull)
                return true;
            if (current is RequestFailedException request && request.ErrorCode == (int)LobbyExceptionReason.LobbyFull)
                return true;
        }
        return false;
    }

    public void StartGame()
    {
        if (IsBusy || session == null || !manager.IsHost || manager.ConnectedClientsIds.Count != 2) return;
        LoadMatch();
    }

    public void StartRematch()
    {
        if (IsBusy || !manager.IsServer || manager.ConnectedClientsIds.Count != 2 ||
            matchTimer == null || !matchTimer.HasMatchEnded) return;
        LoadMatch();
    }

    void LoadMatch()
    {
        operationInProgress = true;
        loadingMatch = true;
        status = "Loading match...";
        loadingSceneManager = manager.SceneManager;
        loadingSceneManager.OnSceneEvent += MatchSceneEvent;
        var result = loadingSceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
        if (result != SceneEventProgressStatus.Started)
        {
            FinishLoading();
            status = $"Could not load match ({result}). Return to the menu and retry.";
            ConnectionLost = true;
        }
    }

    void MatchSceneEvent(SceneEvent sceneEvent)
    {
        if (!loadingMatch || sceneEvent.SceneEventType != SceneEventType.LoadEventCompleted) return;
        FinishLoading();
        matchTimer = FindFirstObjectByType<NetworkMatchTimer>();
        if (sceneEvent.ClientsThatTimedOut.Count != 0 || sceneEvent.ClientsThatCompleted.Count != 2 ||
            manager.ConnectedClientsIds.Count != 2 || matchTimer == null || !matchTimer.IsSpawned)
        {
            status = "A player did not finish loading. Return to the main menu and retry.";
            ConnectionLost = true;
            return;
        }
        matchTimer.StartMatch();
        status = "Match started";
    }

    void FinishLoading()
    {
        if (loadingSceneManager != null) loadingSceneManager.OnSceneEvent -= MatchSceneEvent;
        loadingSceneManager = null;
        loadingMatch = false;
        operationInProgress = false;
    }

    public async void LeaveRoom()
    {
        if (returningToMenu) return;
        returningToMenu = true;
        status = "Returning to main menu...";
        // Back during a Relay request waits for its result, then disposes that room too.
        if (loadingMatch) FinishLoading();
        while (operationInProgress && this != null) await Task.Yield();
        if (this == null) return;
        try
        {
            if (session != null && session.State != SessionState.Deleted) await session.LeaveAsync();
        }
        catch (Exception e) { Debug.LogWarning($"Session cleanup failed; disconnecting locally: {e.Message}"); }
        session = null;
        if (manager != null)
        {
            if (manager.IsListening && !manager.ShutdownInProgress) manager.Shutdown();
            while (manager != null && (manager.IsListening || manager.ShutdownInProgress)) await Task.Yield();
        }
        if (SceneManager.GetActiveScene().name == menuSceneName)
        {
            // MainMenu.BackToMenu already slides back to the buttons. Keep that
            // scene and its completed intro alive while resetting the connection.
            HasOpponent = false;
            ConnectionLost = false;
            guestLobbyOpened = false;
            networkConveyor = null;
            matchTimer = null;
            returningToMenu = false;
            status = "Ready to host or join";
            if (menu != null)
            {
                menu.Lobby.SetRoomCode("");
                menu.Lobby.SetOpponentJoined(false);
            }
            return;
        }

        UnbindMenu();
        string scene = menuSceneName;
        Destroy(gameObject);
        await Task.Yield();
        await SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        UnbindMenu();
        SceneManager.sceneLoaded -= SceneLoaded;
        if (manager != null && subscribed)
        {
            manager.OnClientConnectedCallback -= Connected;
            manager.OnClientDisconnectCallback -= Disconnected;
        }
        if (loadingSceneManager != null) loadingSceneManager.OnSceneEvent -= MatchSceneEvent;
        Instance = null;
    }
}
