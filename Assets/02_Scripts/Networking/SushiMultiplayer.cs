
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

    async void Start()
    {
        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

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
    }

    public async void JoinGame(string code)
    {
        try
        {
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);

            status = "Joined room!";

            Debug.Log("Successfully joined room!");
        }
        catch (Exception e)
        {
            Debug.LogError($"Joining failed: {e}");
            status = "Failed to join room!";
        }
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(20, 20, 250, 350));

        GUILayout.Label("Sushi Showdown Multiplayer");

        if (GUILayout.Button("Host Game", GUILayout.Height(40)))
        {
            status = "Creating room...";
            HostGame();
        }

        GUILayout.Space(10);
        GUILayout.Label("Join Code:");

        joinCode = GUILayout.TextField(joinCode);

        if (GUILayout.Button("Join Game", GUILayout.Height(40)))
        {
            status = "Joining room...";
            JoinGame(joinCode.Trim());
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
