using System;
using System.Collections.Generic;
using SushiDojo.Ingredients;
using UnityEngine;

namespace SushiDojo.Conveyor
{
    /// <summary>
    /// Straight belt moving right to left one slot per step.
    /// Items spawn at the right entry, get revealed at the center slot, and are destroyed past the left exit.
    /// </summary>
    public class ConveyorBelt : MonoBehaviour
    {
        enum State { Idle, Moving, Holding }

        static readonly Color SlotGizmoColor = Color.white;
        static readonly Color CenterGizmoColor = Color.yellow;
        static readonly Color EntryGizmoColor = Color.green;
        static readonly Color ExitGizmoColor = Color.red;

        [Header("Setup")]
        [SerializeField] BeltItem itemPrefab;
        [Tooltip("Parent for spawned items. Falls back to this transform.")]
        [SerializeField] Transform itemRoot;
        [SerializeField] List<IngredientData> ingredients = new();

        [Header("Layout")]
        [Tooltip("Slots on each side of the center. Visible slots = 2N + 1. Read on Start.")]
        [SerializeField, Min(1)] int sideSlotCount = 2;
        [SerializeField, Min(0.01f)] float slotSpacing = 1.5f;

        [Header("Belt Body")]
        [Tooltip("Its local X scale is set to the belt length. Child meshes should be 1 unit long on X.")]
        [SerializeField] Transform beltBody;
        [Tooltip("Belt length beyond the visible slots, in world units.")]
        [SerializeField, Min(0f)] float beltExtraLength;
        [Tooltip("World length of one texture repeat along the belt. Used by BeltTexture faces.")]
        [SerializeField, Min(0.01f)] float textureTileLength = 1.5f;
        [Tooltip("Slot box height in the scene gizmo.")]
        [SerializeField, Min(0.01f)] float gizmoSlotHeight = 1.5f;

        [Header("Timing")]
        [SerializeField, Min(0.01f)] float moveDuration = 0.2f;
        [SerializeField, Min(0f)] float holdDuration = 1f;
        [Tooltip("0..1 progress of one step. Values above 1 overshoot.")]
        [SerializeField] AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Reveal")]
        [Tooltip("On: items keep plate + ingredient after passing the center. Off: they get covered again.")]
        [SerializeField] bool keepRevealedAfterCenter = true;

        [Header("Spawning")]
        [Tooltip("Spawn one item every N steps. 1 = every slot filled.")]
        [SerializeField, Min(1)] int spawnEveryNSteps = 1;
        [SerializeField] bool prefillOnStart = true;
        [SerializeField] bool runOnStart = true;
        [SerializeField] bool useFixedSeed;
        [SerializeField] int seed;

        public event Action<IngredientData> OnCenterRevealed;
        /// <summary>(playerId, ingredient) — fired when a player takes the center ingredient.</summary>
        public event Action<int, IngredientData> OnCenterTaken;

        public int VisibleSlotCount => sideSlotCount * 2 + 1;
        public bool IsRunning { get; private set; }
        /// <summary>Total distance the belt has moved, in world units. Drives texture scrolling.</summary>
        public float TravelDistance => (completedSteps + currentMoveProgress) * slotSpacing;
        public float TextureTileLength => textureTileLength;
        public float BeltLength => VisibleSlotCount * slotSpacing + beltExtraLength;
        public bool CanTakeCenter =>
            state == State.Holding && CenterItem != null && CenterItem.IsRevealed && !CenterItem.IsTaken;

        // Lane layout: [exit] [visible slots ...] [entry]
        int LaneLength => VisibleSlotCount + 2;
        int ExitIndex => 0;
        int EntryIndex => LaneLength - 1;
        int CenterIndex => sideSlotCount + 1;
        BeltItem CenterItem => lane[CenterIndex];
        Transform ItemRoot => itemRoot != null ? itemRoot : transform;

        BeltItem[] lane;
        Vector3[] moveFrom;
        System.Random rng;
        State state = State.Idle;
        float stateTimer;
        int stepCount;
        int completedSteps;
        float currentMoveProgress;

        void Awake()
        {
            ApplyBodyLength();
        }

        void Start()
        {
            rng = useFixedSeed ? new System.Random(seed) : new System.Random();
            lane = new BeltItem[LaneLength];
            moveFrom = new Vector3[LaneLength];

            if (TotalSpawnWeight() <= 0f)
                Debug.LogWarning($"[{nameof(ConveyorBelt)}] No ingredient with spawn weight > 0 registered.", this);

            if (prefillOnStart) Prefill();
            if (runOnStart) StartBelt();
        }

        void Update()
        {
            switch (state)
            {
                case State.Moving: TickMove(); break;
                case State.Holding: TickHold(); break;
            }
        }

        public void StartBelt()
        {
            if (IsRunning) return;
            IsRunning = true;
            if (state == State.Idle) EnterHold();
        }

        /// <summary>Stops after the current move finishes. Center can no longer be taken.</summary>
        public void StopBelt()
        {
            IsRunning = false;
            if (state == State.Holding) state = State.Idle;
        }

        /// <summary>First caller during the hold wins. Only the ingredient is removed; plate and lid stay.</summary>
        public bool TryTakeCenter(int playerId, out IngredientData data)
        {
            data = null;
            if (!CanTakeCenter) return false;

            BeltItem item = CenterItem;
            data = item.Data;
            item.TakeIngredient();

            OnCenterTaken?.Invoke(playerId, data);
            return true;
        }

        void Prefill()
        {
            // Match the spacing items would have if they had been spawning all along.
            for (int i = EntryIndex - spawnEveryNSteps; i > ExitIndex; i -= spawnEveryNSteps)
                Spawn(i);
        }

        void EnterHold()
        {
            state = State.Holding;
            stateTimer = 0f;
            RevealCenter();
        }

        void TickHold()
        {
            stateTimer += Time.deltaTime;
            if (stateTimer >= holdDuration) BeginStep();
        }

        void BeginStep()
        {
            ReleaseCenter();

            if (stepCount % spawnEveryNSteps == 0) Spawn(EntryIndex);
            stepCount++;

            for (int i = ExitIndex + 1; i < LaneLength; i++)
                lane[i - 1] = lane[i];
            lane[EntryIndex] = null;

            for (int i = 0; i < LaneLength; i++)
                if (lane[i] != null) moveFrom[i] = lane[i].transform.localPosition;

            state = State.Moving;
            stateTimer = 0f;
        }

        void TickMove()
        {
            stateTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(stateTimer / moveDuration);
            float eased = moveCurve.Evaluate(progress);
            currentMoveProgress = eased;

            for (int i = 0; i < LaneLength; i++)
                if (lane[i] != null)
                    lane[i].transform.localPosition = Vector3.LerpUnclamped(moveFrom[i], SlotPosition(i), eased);

            if (progress >= 1f) EndStep();
        }

        void EndStep()
        {
            for (int i = 0; i < LaneLength; i++)
                if (lane[i] != null) lane[i].transform.localPosition = SlotPosition(i);

            completedSteps++;
            currentMoveProgress = 0f;

            if (lane[ExitIndex] != null)
            {
                Destroy(lane[ExitIndex].gameObject);
                lane[ExitIndex] = null;
            }

            if (IsRunning) EnterHold();
            else state = State.Idle;
        }

        void RevealCenter()
        {
            BeltItem item = CenterItem;
            if (item == null) return;
            item.Reveal();
            OnCenterRevealed?.Invoke(item.Data);
        }

        void ReleaseCenter()
        {
            BeltItem item = CenterItem;
            if (item == null) return;

            if (keepRevealedAfterCenter) item.LeaveCenter();
            else item.Cover();
        }

        void Spawn(int laneIndex)
        {
            IngredientData data = PickIngredient();
            if (data == null) return;

            BeltItem item = Instantiate(itemPrefab, ItemRoot);
            item.transform.localPosition = SlotPosition(laneIndex);
            item.Init(data);
            lane[laneIndex] = item;
        }

        IngredientData PickIngredient()
        {
            float total = TotalSpawnWeight();
            if (total <= 0f) return null;

            double roll = rng.NextDouble() * total;
            IngredientData lastValid = null;
            foreach (IngredientData data in ingredients)
            {
                if (data == null || data.SpawnWeight <= 0f) continue;
                lastValid = data;
                roll -= data.SpawnWeight;
                if (roll < 0d) return data;
            }
            return lastValid;
        }

        float TotalSpawnWeight()
        {
            float total = 0f;
            foreach (IngredientData data in ingredients)
                if (data != null) total += data.SpawnWeight;
            return total;
        }

        Vector3 SlotPosition(int laneIndex)
        {
            return new Vector3((laneIndex - CenterIndex) * slotSpacing, 0f, 0f);
        }

        void ApplyBodyLength()
        {
            if (beltBody == null) return;

            Vector3 scale = beltBody.localScale;
            scale.x = BeltLength;
            beltBody.localScale = scale;
        }

        void OnValidate()
        {
            ApplyBodyLength();
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = ItemRoot.localToWorldMatrix;
            Vector3 slotSize = new Vector3(slotSpacing, gizmoSlotHeight, 0f);

            for (int i = 0; i < LaneLength; i++)
            {
                if (i == ExitIndex) Gizmos.color = ExitGizmoColor;
                else if (i == EntryIndex) Gizmos.color = EntryGizmoColor;
                else if (i == CenterIndex) Gizmos.color = CenterGizmoColor;
                else Gizmos.color = SlotGizmoColor;

                Gizmos.DrawWireCube(SlotPosition(i), slotSize);
            }
        }
    }
}
