
using SushiDojo.Cards;
using Unity.Netcode;
using UnityEngine;

public class NetworkScoreboard : NetworkBehaviour
{
    [SerializeField] private NetworkMatchTimer matchTimer;
    public NetworkVariable<int> HostScore = new(0);
    public NetworkVariable<int> ClientScore = new(0);
    public NetworkVariable<int> HostPoints = new(0);
    public NetworkVariable<int> ClientPoints = new(0);
    public NetworkVariable<int> HostWaste = new(0);
    public NetworkVariable<int> ClientWaste = new(0);

    const float ScoreChangeDuration = 0.55f;
    int previousLocalScore;
    int previousOpponentScore;
    int localScoreStart;
    int opponentScoreStart;
    float localScoreChangeTimer;
    float opponentScoreChangeTimer;
    bool scoreDisplayInitialized;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            HostScore.Value = 0;
            ClientScore.Value = 0;
            HostPoints.Value = 0;
            ClientPoints.Value = 0;
            HostWaste.Value = 0;
            ClientWaste.Value = 0;
        }

        previousLocalScore = LocalScore;
        previousOpponentScore = OpponentScore;
        localScoreStart = previousLocalScore;
        opponentScoreStart = previousOpponentScore;
        scoreDisplayInitialized = true;
        Debug.Log("Network scoreboard ready!");
    }

    public int LocalScore => IsHost ? HostScore.Value : ClientScore.Value;
    public int OpponentScore => IsHost ? ClientScore.Value : HostScore.Value;

    void Update()
    {
        if (!IsSpawned) return;

        int localScore = LocalScore;
        int opponentScore = OpponentScore;
        if (!scoreDisplayInitialized)
        {
            previousLocalScore = localScore;
            previousOpponentScore = opponentScore;
            scoreDisplayInitialized = true;
        }
        else
        {
            if (localScore != previousLocalScore)
            {
                localScoreStart = previousLocalScore;
                localScoreChangeTimer = ScoreChangeDuration;
                previousLocalScore = localScore;
            }

            if (opponentScore != previousOpponentScore)
            {
                opponentScoreStart = previousOpponentScore;
                opponentScoreChangeTimer = ScoreChangeDuration;
                previousOpponentScore = opponentScore;
            }
        }

        localScoreChangeTimer = Mathf.Max(0f, localScoreChangeTimer - Time.unscaledDeltaTime);
        opponentScoreChangeTimer = Mathf.Max(0f, opponentScoreChangeTimer - Time.unscaledDeltaTime);
    }

    public void RecordRecipeCompleted(int ingredientCount)
    {
        if (!CanSubmitScore() || ingredientCount < 1 ||
            ingredientCount > RecipeData.IngredientSlotCount)
            return;

        RecordRecipeCompletedRpc(ingredientCount);
    }

    public void RecordDiscardedIngredients(int ingredientCount)
    {
        if (!CanSubmitScore() || ingredientCount < 1 ||
            ingredientCount > RecipeData.IngredientSlotCount)
            return;

        RecordDiscardedIngredientsRpc(ingredientCount);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RecordRecipeCompletedRpc(
        int ingredientCount,
        RpcParams rpcParams = default)
    {
        if (!CanServerRecord(ingredientCount))
            return;

        ulong senderId = rpcParams.Receive.SenderClientId;

        if (senderId == NetworkManager.ServerClientId)
        {
            HostPoints.Value += ingredientCount;
            HostScore.Value += ingredientCount;
        }
        else
        {
            ClientPoints.Value += ingredientCount;
            ClientScore.Value += ingredientCount;
        }

        Debug.Log($"Player {senderId} completed a {ingredientCount}-ingredient menu.");
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RecordDiscardedIngredientsRpc(
        int ingredientCount,
        RpcParams rpcParams = default)
    {
        if (!CanServerRecord(ingredientCount))
            return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId == NetworkManager.ServerClientId)
        {
            HostWaste.Value += ingredientCount;
            HostScore.Value -= ingredientCount;
        }
        else
        {
            ClientWaste.Value += ingredientCount;
            ClientScore.Value -= ingredientCount;
        }

        Debug.Log($"Player {senderId} discarded {ingredientCount} ingredients.");
    }

    bool CanSubmitScore() =>
        IsSpawned &&
        IsClient &&
        (matchTimer == null || matchTimer.IsMatchRunning);

    bool CanServerRecord(int ingredientCount) =>
        IsServer &&
        IsSpawned &&
        ingredientCount >= 1 &&
        ingredientCount <= RecipeData.IngredientSlotCount &&
        (matchTimer == null || matchTimer.IsMatchRunning);

    public int DisplayedLocalScore => localScoreChangeTimer > 0f
        ? GetAnimatedScore(localScoreStart, LocalScore, localScoreChangeTimer) : LocalScore;
    public int DisplayedOpponentScore => opponentScoreChangeTimer > 0f
        ? GetAnimatedScore(opponentScoreStart, OpponentScore, opponentScoreChangeTimer) : OpponentScore;
    public int LocalPoints => IsHost ? HostPoints.Value : ClientPoints.Value;
    public int OpponentPoints => IsHost ? ClientPoints.Value : HostPoints.Value;
    public int LocalWaste => IsHost ? HostWaste.Value : ClientWaste.Value;
    public int OpponentWaste => IsHost ? ClientWaste.Value : HostWaste.Value;

    static int GetAnimatedScore(int startScore, int targetScore, float timer)
    {
        float progress = 1f - Mathf.Clamp01(timer / ScoreChangeDuration);
        return Mathf.RoundToInt(Mathf.Lerp(startScore, targetScore, progress));
    }
}
