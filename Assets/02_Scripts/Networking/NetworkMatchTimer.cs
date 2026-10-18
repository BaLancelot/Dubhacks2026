
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using SushiDojo.Conveyor;

public class NetworkMatchTimer : NetworkBehaviour
{
    [Header("Match Settings")]
    [SerializeField] private float matchDuration = 180f;
    [SerializeField] private ConveyorBelt conveyor;
    [Header("References")]
    [SerializeField] private NetworkScoreboard scoreboard;

    private NetworkVariable<double> endTime = new(0);
    private NetworkVariable<bool> matchStarted = new(false);
    private NetworkVariable<bool> matchEnded = new(false);

    // -1 = No winner yet
    //  0 = Tie
    //  1 = Host wins
    //  2 = Client wins
    private NetworkVariable<int> winner = new(-1);
    private NetworkVariable<int> replayVotes = new(0);
    private readonly HashSet<ulong> replayVoters = new();
    private bool localReplayRequested;

    public event Action OnMatchEnded;

    public bool HasMatchStarted => IsSpawned && matchStarted.Value;

    public bool HasMatchEnded =>
        IsSpawned && matchEnded.Value;

    public bool IsMatchRunning =>
        IsSpawned &&
        matchStarted.Value &&
        !matchEnded.Value &&
        TimeRemaining > 0f;

    public int Winner => winner.Value;

    public float TimeRemaining
    {
        get
        {
            if (!IsSpawned || !matchStarted.Value)
                return matchDuration;

            if (matchEnded.Value)
                return 0f;

            double remaining =
                endTime.Value - NetworkManager.ServerTime.Time;

            return (float)Math.Max(0, remaining);
        }
    }

    public override void OnNetworkSpawn()
    {
        matchEnded.OnValueChanged += OnEndStateChanged;
        if (IsServer)
            NetworkManager.OnClientDisconnectCallback += RemoveReplayVote;

        if (matchEnded.Value)
            OnMatchEnded?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        matchEnded.OnValueChanged -= OnEndStateChanged;
        if (NetworkManager != null)
            NetworkManager.OnClientDisconnectCallback -= RemoveReplayVote;
    }

    private void RemoveReplayVote(ulong clientId)
    {
        if (replayVoters.Remove(clientId)) replayVotes.Value = replayVoters.Count;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void VotePlayAgainRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!HasMatchEnded || !NetworkManager.ConnectedClients.ContainsKey(sender)) return;
        replayVoters.Add(sender);
        replayVotes.Value = replayVoters.Count;

        if (NetworkManager.ConnectedClientsIds.Count != 2 || replayVoters.Count != 2) return;
        foreach (ulong player in NetworkManager.ConnectedClientsIds)
            if (!replayVoters.Contains(player)) return;

        SushiMultiplayer multiplayer = NetworkManager.GetComponent<SushiMultiplayer>();
        if (multiplayer != null) multiplayer.StartRematch();
    }

    public void StartMatch()
{
    if (!IsSpawned || !IsServer || matchStarted.Value)
        return;

    if (scoreboard == null || !scoreboard.IsSpawned)
    {
        Debug.LogError("Scoreboard is not ready!");
        return;
    }

    endTime.Value =
        NetworkManager.ServerTime.Time + matchDuration;

    matchStarted.Value = true;

    if (conveyor == null)
    {
        Debug.LogError("TIMER: Conveyor reference is missing!");
        return;
    }

    conveyor.StartBelt();

    Debug.Log(
        $"TIMER: Started conveyor {conveyor.gameObject.name}, " +
        $"IsRunning={conveyor.IsRunning}"
    );
}

    private void Update()
    {
        // Only the server decides when the match ends.
        if (!IsSpawned || !IsServer)
            return;

        if (matchStarted.Value &&
            !matchEnded.Value &&
            TimeRemaining <= 0f)
        {
            EndMatch();
        }
    }

    private void EndMatch()
    {
        if (!IsServer || matchEnded.Value)
            return;

        int hostScore = scoreboard.HostScore.Value;
        int clientScore = scoreboard.ClientScore.Value;

        if (hostScore > clientScore)
        {
            winner.Value = 1;
            Debug.Log("Player 1 wins!");
        }
        else if (clientScore > hostScore)
        {
            winner.Value = 2;
            Debug.Log("Player 2 wins!");
        }
        else
        {
            winner.Value = 0;
            Debug.Log("It's a tie!");
        }

        // Set this last so other scripts know
        // the final winner has already been decided.
        matchEnded.Value = true;

        Debug.Log("Match finished!");
    }

    private void OnEndStateChanged(bool previous, bool current)
    {
        if (current && !previous)
        {
            Debug.Log("Match ended on this player!");
            OnMatchEnded?.Invoke();
        }
    }

    private void OnGUI()
    {
        if (!IsSpawned)
            return;

        GUILayout.BeginArea(
            new Rect(Screen.width / 2f - 110, 15, 220, 230)
        );

        if (!matchStarted.Value)
        {
            GUILayout.Label("Waiting for match...");

            if (IsServer &&
                GUILayout.Button("Start Match"))
            {
                StartMatch();
            }
        }
        else if (HasMatchEnded)
        {
            GUILayout.Label("TIME'S UP!");

            if (winner.Value == 1)
                GUILayout.Label("PLAYER 1 WINS!");
            else if (winner.Value == 2)
                GUILayout.Label("PLAYER 2 WINS!");
            else if (winner.Value == 0)
                GUILayout.Label("IT'S A TIE!");

            if (scoreboard != null)
            {
                GUILayout.Label(
                    $"Final Score: {scoreboard.HostScore.Value} - " +
                    $"{scoreboard.ClientScore.Value}"
                );
            }

            GUILayout.Label($"Play again: {replayVotes.Value}/2 players");
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && IsClient && !localReplayRequested;
            if (GUILayout.Button(localReplayRequested ? "Waiting for other player..." : "Play Again"))
            {
                localReplayRequested = true;
                VotePlayAgainRpc();
            }
            GUI.enabled = previousEnabled;
        }
        else
        {
            int seconds = Mathf.CeilToInt(TimeRemaining);

            int minutes = seconds / 60;
            int remainingSeconds = seconds % 60;

            GUILayout.Label(
                $"Time: {minutes:00}:{remainingSeconds:00}"
            );
        }

        GUILayout.EndArea();
    }
}
