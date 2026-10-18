
using System;
using Unity.Netcode;
using UnityEngine;
using SushiDojo.Conveyor;
using SushiDojo.Ingredients;

public class NetworkConveyor : NetworkBehaviour
{
    [SerializeField] private ConveyorBelt conveyor;
    [SerializeField] private NetworkMatchTimer matchTimer;
    // Fired locally when this player wins an ingredient.
    public event Action<IngredientData> OnIngredientAwarded;

    // Shared count for debugging successful claims.
    public NetworkVariable<int> TotalClaims = new(0);

    // Host-controlled ingredient slots.
    private NetworkList<int> slots = new();
    private bool slotsChanged;

    public override void OnNetworkSpawn()
    {
        if (conveyor == null)
        {
            Debug.LogError("Conveyor reference is missing!");
            return;
        }

        if (matchTimer != null)
        {
            matchTimer.OnMatchEnded += StopConveyorForMatch;
        }

        // Both host and client can request ingredient claims.
        conveyor.OnCenterClicked += RequestTakeIngredient;

        if (IsServer)
        {
            int count = conveyor.VisibleSlotCount + 2;

            for (int i = 0; i < count; i++)
                slots.Add(-1);

            conveyor.StartBelt();

            Debug.Log("Host started the conveyor!");
        }
        else
        {
            slots.OnListChanged += OnSlotsChanged;
            slotsChanged = true;

            Debug.Log("Client waiting for conveyor updates.");
        }
    }

    public override void OnNetworkDespawn()
    {
        slots.OnListChanged -= OnSlotsChanged;

        if (conveyor != null)
        {
            conveyor.OnCenterClicked -= RequestTakeIngredient;
        }
    }

    private void OnSlotsChanged(NetworkListEvent<int> change)
    {
        slotsChanged = true;
    }

    private void StopConveyorForMatch()
{
    if (conveyor != null)
    {
        conveyor.FreezeBelt();
        Debug.Log("Conveyor stopped: Match over!");
    }
}
    private void Update()
    {
        if (!IsSpawned || conveyor == null)
            return;
        if (matchTimer != null && matchTimer.HasMatchEnded)
            return;
        if (IsServer)
        {
            // Host determines which ingredients occupy each slot.
            for (int i = 0; i < slots.Count; i++)
            {
                int ingredient =
                    conveyor.GetIngredientIndexAtSlot(i);

                if (slots[i] != ingredient)
                    slots[i] = ingredient;
            }
        }
        else if (slotsChanged)
        {
            if (slots.Count != conveyor.VisibleSlotCount + 2)
                return;

            slotsChanged = false;

            int[] snapshot = new int[slots.Count];

            for (int i = 0; i < slots.Count; i++)
                snapshot[i] = slots[i];

            conveyor.ApplyRemoteSlots(snapshot);
        }
    }

    // Called when the local player clicks the revealed ingredient.
    public void RequestTakeIngredient()
    {
        if (matchTimer != null && !matchTimer.IsMatchRunning)
            return;
        if (!IsSpawned || !IsClient)
            return;

        TakeIngredientRpc();
    }

    // Claim validation always happens on the server.
    [Rpc(SendTo.Server)]
    private void TakeIngredientRpc(RpcParams rpcParams = default)
    {
        if (matchTimer != null && !matchTimer.IsMatchRunning)
            return;
        if (conveyor == null)
            return;

        if (conveyor.TryTakeCenter(out IngredientData ingredient))
        {
            ulong playerId = rpcParams.Receive.SenderClientId;

            int ingredientIndex =
                conveyor.GetIngredientIndex(ingredient);

            if (ingredientIndex < 0)
            {
                Debug.LogError(
                    "Claimed ingredient is not registered!"
                );

                return;
            }

            TotalClaims.Value++;

            Debug.Log(
                $"Player {playerId} claimed {ingredient.name}"
            );

            // Only the player who won receives this confirmation.
            ConfirmIngredientRpc(
                ingredientIndex,
                RpcTarget.Single(
                    playerId,
                    RpcTargetUse.Temp
                )
            );
        }
    }

    // Runs only on the player who successfully claimed the food.
    [Rpc(SendTo.SpecifiedInParams)]
    private void ConfirmIngredientRpc(
        int ingredientIndex,
        RpcParams rpcParams = default)
    {
        if (conveyor == null)
            return;

        IngredientData ingredient =
            conveyor.GetIngredientByIndex(ingredientIndex);

        if (ingredient == null)
        {
            Debug.LogError(
                "Received an unknown ingredient!"
            );

            return;
        }

        Debug.Log(
            $"LOCAL PLAYER RECEIVED: {ingredient.name}"
        );

        // Teammate's local inventory system can subscribe to this.
        OnIngredientAwarded?.Invoke(ingredient);
    }
}
