
using Unity.Netcode;
using UnityEngine;
using SushiDojo.Conveyor;
using SushiDojo.Ingredients;

public class NetworkConveyor : NetworkBehaviour
{
    [SerializeField] private ConveyorBelt conveyor;

    public NetworkVariable<int> TotalClaims = new(0);

    private NetworkList<int> slots = new();
    private bool slotsChanged;

    public override void OnNetworkSpawn()
    {
        if (conveyor == null)
        {
            Debug.LogError("Conveyor reference is missing!");
            return;
        }
        conveyor.OnCenterClicked += RequestTakeIngredient;

        if (IsServer)
        {
            // One entry for every lane position,
            // including the entry and exit slots.
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

    private void Update()
    {
        if (!IsSpawned || conveyor == null)
            return;

        if (IsServer)
        {
            // Read the host's conveyor slots.
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

    public void RequestTakeIngredient()
    {
        if (!IsSpawned || !IsClient)
            return;

        TakeIngredientRpc();
    }

    [Rpc(SendTo.Server)]
    private void TakeIngredientRpc(RpcParams rpcParams = default)
    {
        if (conveyor == null)
            return;

        if (conveyor.TryTakeCenter(out IngredientData ingredient))
        {
            TotalClaims.Value++;

            ulong playerId = rpcParams.Receive.SenderClientId;

            Debug.Log(
                $"Player {playerId} claimed {ingredient.name}"
            );
        }
    }
}
