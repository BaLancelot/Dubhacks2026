
using Unity.Netcode;
using UnityEngine;

public class NetworkScoreboard : NetworkBehaviour
{
    // Scores visible to both players.
    [SerializeField] private NetworkMatchTimer matchTimer;
    public NetworkVariable<int> HostScore = new(0);
    public NetworkVariable<int> ClientScore = new(0);

    // Score calculated on this player's computer.
    private int localScore = 0;

    public override void OnNetworkSpawn()
    {
        localScore = IsHost
            ? HostScore.Value
            : ClientScore.Value;

        Debug.Log("Network scoreboard ready!");
    }

    // Called by the local recipe system after crafting sushi.
    public void SetLocalScore(int newScore)
    {
        if (matchTimer != null && !matchTimer.IsMatchRunning)
            return;
        if (!IsSpawned || !IsClient)
            return;

        localScore = Mathf.Max(0, newScore);

        SubmitScoreRpc(localScore);
    }

    // The server records the score reported by each player.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SubmitScoreRpc(
        int score,
        RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        score = Mathf.Max(0, score);

        if (senderId == NetworkManager.ServerClientId)
        {
            HostScore.Value = score;
        }
        else
        {
            ClientScore.Value = score;
        }

        Debug.Log($"Player {senderId} score: {score}");
    }

    // Temporary UI for testing.
    private void OnGUI()
    {
        if (!IsSpawned)
            return;

        GUILayout.BeginArea(
            new Rect(20, Screen.height - 165, 280, 155)
        );

        GUILayout.Label("SUSHI SHOWDOWN SCORES");
        GUILayout.Label("Host Score: " + HostScore.Value);
        GUILayout.Label("Client Score: " + ClientScore.Value);
        GUILayout.Label("My Score: " + localScore);

        if (GUILayout.Button("+1 Point (Test)", GUILayout.Height(35)))
        {
            SetLocalScore(localScore + 1);
        }

        GUILayout.EndArea();
    }
}
