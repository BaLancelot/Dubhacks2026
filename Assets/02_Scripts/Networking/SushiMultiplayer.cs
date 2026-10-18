
using System;
using UnityEngine;
using Unity.Netcode;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;

public class SushiMultiplayer : MonoBehaviour
{
    [SerializeField] private NetworkConveyor networkConveyor;

    private ISession session;
    private string joinCode = "";
    private string status = "Not connected";
    private bool servicesReady;
    private bool operationInProgress;
    private NetworkManager observedManager;

    private bool CanConnect => servicesReady && !operationInProgress && session == null &&
        (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening);

    void Update()
    {
        if (observedManager == NetworkManager.Singleton) return;
        UnsubscribeNetworkEvents();
        observedManager = NetworkManager.Singleton;
        if (observedManager == null) return;
        observedManager.OnClientConnectedCallback += HandleConnected;
        observedManager.OnClientDisconnectCallback += HandleDisconnected;
    }

    void OnDestroy() => UnsubscribeNetworkEvents();

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
        if (observedManager == null || clientId != observedManager.LocalClientId) return;
        status = "Network disconnected. Leave Room before retrying.";
        Debug.LogWarning($"Network disconnected: {observedManager.DisconnectReason}");
    }

    async void Start()
    {
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
        operationInProgress = true;
        status = "Leaving room...";
        try
        {
            if (session != null) await session.LeaveAsync();
            session = null;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
            status = "Left room. Ready to host or join";
        }
        catch (Exception e)
        {
            Debug.LogError($"Leaving failed: {e}");
            status = "Failed to leave room. Try Leave Room again.";
        }
        finally { operationInProgress = false; }
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(Screen.width - 270, 20, 250, 430));

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

        if (session != null)
        {
            GUI.enabled = previousEnabled && !operationInProgress;
            if (GUILayout.Button("Leave Room")) LeaveRoom();
            GUI.enabled = previousEnabled;
        }

        GUILayout.Space(10);

        if (session != null)
        {
            GUILayout.Label("Room Code: " + session.Code);
        }

        GUILayout.Label("Status: " + status);

        GUILayout.Space(10);

        // Ingredient claiming
        if (GUILayout.Button("Claim Ingredient", GUILayout.Height(40)))
        {
            if (networkConveyor != null)
            {
                networkConveyor.RequestTakeIngredient();
            }
            else
            {
                Debug.LogWarning("NetworkConveyor is not assigned!");
            }
        }

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
