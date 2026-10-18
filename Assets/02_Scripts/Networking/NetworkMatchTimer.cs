
using System;
using Unity.Netcode;
using UnityEngine;

public class NetworkMatchTimer : NetworkBehaviour
{
    [Header("Match Settings")]
    [SerializeField] private float matchDuration = 180f;

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

    public event Action OnMatchEnded;

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

        if (matchEnded.Value)
            OnMatchEnded?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        matchEnded.OnValueChanged -= OnEndStateChanged;
    }

    public void StartMatch()
    {
        if (!IsSpawned || !IsServer || matchStarted.Value)
            return;

        if (scoreboard == null || !scoreboard.IsSpawned)
        {
            Debug.LogError("NetworkScoreboard not assigned or ready!");
            return;
        }

        winner.Value = -1;

        endTime.Value =
            NetworkManager.ServerTime.Time + matchDuration;

        matchStarted.Value = true;

        Debug.Log("Three-minute match started!");
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
            new Rect(Screen.width / 2f - 110, 15, 220, 160)
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
