using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using SushiDojo.Conveyor;

public class NetworkMatchTimer : NetworkBehaviour
{
    [Header("Match Settings")]
    [SerializeField] private float matchDuration = 20f;
    [SerializeField, Min(1f)] private float settleTimeout = 60f;
    [SerializeField] private ConveyorBelt conveyor;
    [Header("References")]
    [SerializeField] private NetworkScoreboard scoreboard;

    private NetworkVariable<double> endTime = new(0);
    private NetworkVariable<bool> matchStarted = new(false);
    private NetworkVariable<bool> matchSettling = new(false);
    private NetworkVariable<bool> matchEnded = new(false);
    private NetworkVariable<bool> wasteThrow = new(false);
    // 0 = waiting on both players, 1 = nothing to throw, 2 = play the waste throw.
    private NetworkVariable<int> wastePlan = new(0);

    // -1 = No winner yet
    //  0 = Tie
    //  1 = Host wins
    //  2 = Client wins
    private NetworkVariable<int> winner = new(-1);
    private NetworkVariable<int> replayVotes = new(0);
    private readonly HashSet<ulong> replayVoters = new();
    private readonly HashSet<ulong> settleCompleters = new();
    private readonly HashSet<ulong> throwReady = new();
    private readonly Dictionary<ulong, int> wasteCounts = new();
    private double settleDeadline;
    private bool localReplayRequested;
    private bool localSettleReported;
    private bool localThrowReady;
    private bool localWasteReported;

    public event Action OnMatchSettling;
    public event Action OnWasteThrow;
    public event Action OnMatchEnded;

    public bool WasteThrowStarted => IsSpawned && wasteThrow.Value;
    public bool WastePlanReady => IsSpawned && wastePlan.Value != 0;
    public bool WastePlanSkipped => IsSpawned && wastePlan.Value == 1;

    public bool HasMatchStarted => IsSpawned && matchStarted.Value;

    public bool IsMatchSettling =>
        IsSpawned && matchSettling.Value && !matchEnded.Value;

    public bool HasMatchEnded =>
        IsSpawned && matchEnded.Value;

    public bool IsMatchRunning =>
        IsSpawned &&
        matchStarted.Value &&
        !matchSettling.Value &&
        !matchEnded.Value &&
        TimeRemaining > 0f;

    public int Winner => winner.Value;

    public float TimeRemaining
    {
        get
        {
            if (!IsSpawned || !matchStarted.Value)
                return matchDuration;

            if (matchEnded.Value || matchSettling.Value)
                return 0f;

            double remaining =
                endTime.Value - NetworkManager.ServerTime.Time;

            return (float)Math.Max(0, remaining);
        }
    }

    public override void OnNetworkSpawn()
    {
        matchSettling.OnValueChanged += OnSettlingChanged;
        wasteThrow.OnValueChanged += OnWasteThrowChanged;
        matchEnded.OnValueChanged += OnEndStateChanged;
        if (IsServer)
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

        if (matchSettling.Value && !matchEnded.Value)
            OnMatchSettling?.Invoke();
        if (wasteThrow.Value && !matchEnded.Value)
            OnWasteThrow?.Invoke();
        if (matchEnded.Value)
            OnMatchEnded?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        matchSettling.OnValueChanged -= OnSettlingChanged;
        wasteThrow.OnValueChanged -= OnWasteThrowChanged;
        matchEnded.OnValueChanged -= OnEndStateChanged;
        if (NetworkManager != null)
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        RemoveReplayVote(clientId);
        if (!IsServer || !matchSettling.Value || matchEnded.Value) return;
        settleCompleters.Remove(clientId);
        throwReady.Remove(clientId);
        wasteCounts.Remove(clientId);
        TryDecideWastePlan();
        TryStartThrow();
        TryFinishSettle();
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

        matchSettling.Value = false;
        matchEnded.Value = false;
        winner.Value = -1;
        settleCompleters.Clear();
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Debug: '-' ends the match right away (either player can press it).
        Keyboard keyboard = Keyboard.current;
        if (IsSpawned && IsMatchRunning && keyboard != null &&
            (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame))
            DebugEndMatchRpc();
#endif

        // Only the server decides when the match ends.
        if (!IsSpawned || !IsServer)
            return;

        if (matchStarted.Value &&
            !matchSettling.Value &&
            !matchEnded.Value &&
            TimeRemaining <= 0f)
        {
            BeginSettle();
            return;
        }

        if (matchSettling.Value &&
            !matchEnded.Value &&
            NetworkManager.ServerTime.Time >= settleDeadline)
        {
            Debug.LogWarning("TIMER: Settle timed out; ending match.");
            EndMatch();
        }
    }

    private void BeginSettle()
    {
        if (!IsServer || matchSettling.Value || matchEnded.Value)
            return;

        settleCompleters.Clear();
        throwReady.Clear();
        wasteCounts.Clear();
        wasteThrow.Value = false;
        wastePlan.Value = 0;
        settleDeadline = NetworkManager.ServerTime.Time + settleTimeout;
        matchSettling.Value = true;
        Debug.Log("TIMER: Time up — settling leftover recipes as waste.");
    }

    public void NotifyWasteCount(int count)
    {
        if (!IsSpawned || !IsClient || !IsMatchSettling || localWasteReported)
            return;

        localWasteReported = true;
        NotifyWasteCountRpc(Mathf.Max(0, count));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void NotifyWasteCountRpc(int count, RpcParams rpcParams = default)
    {
        if (!IsServer || !matchSettling.Value || matchEnded.Value || wastePlan.Value != 0)
            return;

        ulong sender = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.ConnectedClients.ContainsKey(sender))
            return;

        wasteCounts[sender] = Mathf.Max(0, count);
        TryDecideWastePlan();
    }

    private void TryDecideWastePlan()
    {
        if (!IsServer || !matchSettling.Value || matchEnded.Value || wastePlan.Value != 0)
            return;

        bool anyWaste = false;
        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
        {
            if (!wasteCounts.TryGetValue(clientId, out int count))
                return;
            if (count > 0)
                anyWaste = true;
        }

        wastePlan.Value = anyWaste ? 2 : 1;
    }

    public void NotifyReadyToThrow()
    {
        if (!IsSpawned || !IsClient || !IsMatchSettling || localThrowReady)
            return;

        localThrowReady = true;
        NotifyReadyToThrowRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void NotifyReadyToThrowRpc(RpcParams rpcParams = default)
    {
        if (!IsServer || !matchSettling.Value || matchEnded.Value || wasteThrow.Value)
            return;

        ulong sender = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.ConnectedClients.ContainsKey(sender))
            return;

        throwReady.Add(sender);
        TryStartThrow();
    }

    private void TryStartThrow()
    {
        if (!IsServer || !matchSettling.Value || matchEnded.Value || wasteThrow.Value)
            return;

        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
        {
            if (!throwReady.Contains(clientId))
                return;
        }

        wasteThrow.Value = true;
    }

    public void NotifyLocalSettleComplete()
    {
        if (!IsSpawned || !IsClient || !IsMatchSettling || localSettleReported)
            return;

        localSettleReported = true;
        NotifySettleCompleteRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void NotifySettleCompleteRpc(RpcParams rpcParams = default)
    {
        if (!IsServer || !matchSettling.Value || matchEnded.Value)
            return;

        ulong sender = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.ConnectedClients.ContainsKey(sender))
            return;

        settleCompleters.Add(sender);
        TryFinishSettle();
    }

    private void TryFinishSettle()
    {
        if (!IsServer || !matchSettling.Value || matchEnded.Value)
            return;

        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
        {
            if (!settleCompleters.Contains(clientId))
                return;
        }

        EndMatch();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void DebugEndMatchRpc()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("DEBUG: match ended early with '-'.");
        EndMatch();
#endif
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

    private void OnSettlingChanged(bool previous, bool current)
    {
        if (current && !previous)
        {
            localSettleReported = false;
            localThrowReady = false;
            localWasteReported = false;
            Debug.Log("Match settling on this player!");
            OnMatchSettling?.Invoke();
        }
    }

    private void OnWasteThrowChanged(bool previous, bool current)
    {
        if (current && !previous)
            OnWasteThrow?.Invoke();
    }

    private void OnEndStateChanged(bool previous, bool current)
    {
        if (current && !previous)
        {
            Debug.Log("Match ended on this player!");
            OnMatchEnded?.Invoke();
        }
    }

    public int ReplayVoteCount => replayVotes.Value;
    public bool LocalReplayRequested => localReplayRequested;

    public void RequestPlayAgain()
    {
        if (!IsSpawned || !IsClient || !HasMatchEnded || localReplayRequested) return;
        if (SushiMultiplayer.Instance == null || !SushiMultiplayer.Instance.HasOpponent ||
            SushiMultiplayer.Instance.ConnectionLost) return;
        localReplayRequested = true;
        VotePlayAgainRpc();
    }
}
