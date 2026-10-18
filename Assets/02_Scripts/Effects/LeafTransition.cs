using System;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace SushiDojo.Effects
{
    /// <summary>
    /// UI screen transition: a black panel sweeps diagonally across the screen.
    /// Part of the leaves hug the moving edge to hide its straight line; the rest scatter
    /// off it and drift behind, so the wipe reads as a gust of leaves rather than a band.
    /// RevealFromBlack() starts fully black and wipes it away (used on game start).
    /// Play() covers, holds, then reveals. Cover() / Reveal() split it for loading in between.
    /// Leaf images are pre-placed children; each play picks random sprites, sizes and spins.
    /// </summary>
    public class LeafTransition : MonoBehaviour
    {
        enum State { Idle, Covering, Holding, Revealing }

        const float FullTurnDegrees = 360f;
        const float FullTurn = Mathf.PI * 2f;

        [Header("Parts")]
        [Tooltip("The canvas area the transition fills.")]
        [SerializeField] RectTransform screenArea;
        [Tooltip("Rotated container that moves across the screen.")]
        [SerializeField] RectTransform sweep;
        [SerializeField] RectTransform blackPanel;
        [SerializeField] Image[] leaves;
        [SerializeField] Sprite[] leafSprites;

        [Header("Game Start")]
        [Tooltip("Start the scene fully black and wipe it away.")]
        [SerializeField] bool revealOnStart = true;
        [Tooltip("Black time before the wipe starts.")]
        [SerializeField, Min(0f)] float startBlackDuration = 0.4f;

        [Header("Timing (unscaled)")]
        [SerializeField, Min(0.01f)] float coverDuration = 1.2f;
        [Tooltip("Fully black time between cover and reveal when using Play().")]
        [SerializeField, Min(0f)] float holdDuration = 0.15f;
        [SerializeField, Min(0.01f)] float revealDuration = 1.4f;
        [SerializeField] AnimationCurve coverCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] AnimationCurve revealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Sweep")]
        [Tooltip("Screen direction the panel travels (x = right, y = up).")]
        [SerializeField] Vector2 sweepDirection = new(1f, 1f);
        [Tooltip("Black panel length along the travel, as a multiple of the screen diagonal.")]
        [SerializeField, Min(1f)] float panelLengthScale = 1.1f;

        [Header("Edge Leaves (hide the panel's edge)")]
        [Tooltip("Fraction of leaves that hug the edge. The rest scatter.")]
        [SerializeField, Range(0f, 1f)] float edgeLeafRatio = 0.5f;
        [Tooltip("How far edge leaves sit from the edge, in canvas units.")]
        [SerializeField, Min(0f)] float edgeScatter = 90f;
        [Tooltip("Edge leaf size range, in canvas units.")]
        [SerializeField] Vector2 edgeSizeRange = new(250f, 450f);

        [Header("Scattered Leaves")]
        [Tooltip("Start distance from the edge, in canvas units. Positive = out over the uncovered screen.")]
        [SerializeField] Vector2 scatterRange = new(-150f, 700f);
        [Tooltip("Drift away from the edge over time, canvas units per second.")]
        [SerializeField] Vector2 scatterDriftRange = new(0f, 300f);
        [Tooltip("Sideways drift, canvas units per second (direction random).")]
        [SerializeField] Vector2 scatterSideDriftRange = new(0f, 120f);
        [Tooltip("Scattered leaf size range, in canvas units.")]
        [SerializeField] Vector2 scatterSizeRange = new(120f, 380f);

        [Header("Leaf Motion")]
        [Tooltip("Spin speed range, degrees per second. Direction is random per leaf.")]
        [SerializeField] Vector2 spinRange = new(90f, 300f);
        [Tooltip("Back-and-forth flutter along the travel, in canvas units.")]
        [SerializeField, Min(0f)] float flutterAmount = 25f;
        [Tooltip("Flutter speed, radians per second.")]
        [SerializeField, Min(0f)] float flutterSpeed = 8f;
        [Tooltip("Leaf flip (squash on X) speed range, radians per second.")]
        [SerializeField] Vector2 flipSpeedRange = new(2f, 6f);
        [Tooltip("Narrowest a flipping leaf gets (fraction of its width).")]
        [SerializeField, Range(0f, 1f)] float minFlipWidth = 0.35f;

        public event Action OnCovered;
        public event Action OnFinished;

        public bool IsPlaying => state != State.Idle;

        float[] outwardOffsets;
        float[] outwardDrifts;
        float[] lateralPositions;
        float[] sideDrifts;
        float[] sizes;
        float[] spins;
        float[] startAngles;
        float[] phases;
        float[] flipSpeeds;
        State state = State.Idle;
        bool autoReveal;
        float currentHold;
        float stateTimer;
        float elapsed;

        int EdgeLeafCount => Mathf.RoundToInt(leaves.Length * edgeLeafRatio);

        void Awake()
        {
            int count = leaves.Length;
            outwardOffsets = new float[count];
            outwardDrifts = new float[count];
            lateralPositions = new float[count];
            sideDrifts = new float[count];
            sizes = new float[count];
            spins = new float[count];
            startAngles = new float[count];
            phases = new float[count];
            flipSpeeds = new float[count];

            sweep.gameObject.SetActive(false);

            // Cover from the very first frame so the scene never flashes before the wipe.
            if (revealOnStart) RevealFromBlack();
        }

        void Update()
        {
            if (state == State.Idle) return;

            float deltaTime = Time.unscaledDeltaTime;
            stateTimer += deltaTime;

            switch (state)
            {
                case State.Covering:
                    elapsed += deltaTime;
                    if (stateTimer >= coverDuration) EnterHolding();
                    break;
                case State.Holding:
                    if (autoReveal && stateTimer >= currentHold) StartRevealing();
                    break;
                case State.Revealing:
                    elapsed += deltaTime;
                    if (stateTimer >= revealDuration) Finish();
                    break;
            }

            if (state != State.Idle) Apply();
        }

        /// <summary>Snap to fully black, wait, then wipe the black away.</summary>
        public void RevealFromBlack()
        {
            autoReveal = true;
            currentHold = startBlackDuration;
            Enter(State.Holding);
            sweep.gameObject.SetActive(true);
            Apply();
        }

        /// <summary>Cover, hold, then reveal automatically.</summary>
        public void Play()
        {
            Begin(autoRevealAfterCover: true);
        }

        /// <summary>Cover and stay black until Reveal() is called.</summary>
        public void Cover()
        {
            Begin(autoRevealAfterCover: false);
        }

        public void Reveal()
        {
            if (state == State.Holding) StartRevealing();
            else autoReveal = true;
        }

        void Begin(bool autoRevealAfterCover)
        {
            autoReveal = autoRevealAfterCover;
            currentHold = holdDuration;
            RandomizeLeaves();
            Enter(State.Covering);
            sweep.gameObject.SetActive(true);
            Apply();
        }

        void EnterHolding()
        {
            Enter(State.Holding);
            OnCovered?.Invoke();
        }

        void StartRevealing()
        {
            // Fresh leaves for the trailing edge.
            RandomizeLeaves();
            Enter(State.Revealing);
        }

        void Finish()
        {
            state = State.Idle;
            sweep.gameObject.SetActive(false);
            OnFinished?.Invoke();
        }

        void Enter(State next)
        {
            state = next;
            stateTimer = 0f;
        }

        void RandomizeLeaves()
        {
            elapsed = 0f;
            Vector2 screen = ScreenSize();
            float maxSize = Mathf.Max(edgeSizeRange.y, scatterSizeRange.y);
            float lateralHalf = screen.magnitude * 0.5f + maxSize;
            int edgeCount = EdgeLeafCount;
            int scatterCount = leaves.Length - edgeCount;

            for (int i = 0; i < leaves.Length; i++)
            {
                if (leafSprites != null && leafSprites.Length > 0)
                    leaves[i].sprite = leafSprites[Random.Range(0, leafSprites.Length)];

                // Each group spreads its leaves over evenly sized slots along the edge, so there are no gaps.
                bool isEdgeLeaf = i < edgeCount;
                int slot = isEdgeLeaf ? i : i - edgeCount;
                int slotCount = Mathf.Max(isEdgeLeaf ? edgeCount : scatterCount, 1);
                float slotSize = lateralHalf * 2f / slotCount;
                lateralPositions[i] = -lateralHalf + (slot + Random.value) * slotSize;

                if (isEdgeLeaf)
                {
                    outwardOffsets[i] = Random.Range(-edgeScatter, edgeScatter);
                    outwardDrifts[i] = 0f;
                    sideDrifts[i] = 0f;
                    sizes[i] = Random.Range(edgeSizeRange.x, edgeSizeRange.y);
                }
                else
                {
                    outwardOffsets[i] = Random.Range(scatterRange.x, scatterRange.y);
                    outwardDrifts[i] = Random.Range(scatterDriftRange.x, scatterDriftRange.y);
                    float side = Random.Range(scatterSideDriftRange.x, scatterSideDriftRange.y);
                    sideDrifts[i] = Random.value < 0.5f ? -side : side;
                    sizes[i] = Random.Range(scatterSizeRange.x, scatterSizeRange.y);
                }

                float spin = Random.Range(spinRange.x, spinRange.y);
                spins[i] = Random.value < 0.5f ? -spin : spin;
                startAngles[i] = Random.Range(0f, FullTurnDegrees);
                phases[i] = Random.Range(0f, FullTurn);
                flipSpeeds[i] = Random.Range(flipSpeedRange.x, flipSpeedRange.y);
            }
        }

        void Apply()
        {
            Vector2 screen = ScreenSize();
            float diagonal = screen.magnitude;
            float panelLength = diagonal * panelLengthScale;
            float maxSize = Mathf.Max(edgeSizeRange.y, scatterSizeRange.y);
            float maxOutward = Mathf.Max(edgeScatter, scatterRange.y + scatterDriftRange.y * Mathf.Max(coverDuration, revealDuration));
            // Fully off screen on either side, leaves included.
            float travel = (panelLength + diagonal) * 0.5f + maxOutward + maxSize;

            float position = state switch
            {
                State.Covering => Mathf.LerpUnclamped(-travel, 0f, coverCurve.Evaluate(Progress(coverDuration))),
                State.Revealing => Mathf.LerpUnclamped(0f, travel, revealCurve.Evaluate(Progress(revealDuration))),
                _ => 0f,
            };

            Vector2 direction = sweepDirection.sqrMagnitude > 0f ? sweepDirection.normalized : Vector2.right;
            sweep.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            sweep.anchoredPosition = direction * position;
            blackPanel.sizeDelta = new Vector2(panelLength, diagonal + maxSize * 2f);

            // Leaves sit on the edge that's moving across the screen, scattering out over the uncovered side.
            bool isCovering = state == State.Covering;
            float edge = (isCovering ? 1f : -1f) * panelLength * 0.5f;
            float outward = isCovering ? 1f : -1f;
            bool showLeaves = state != State.Holding;

            for (int i = 0; i < leaves.Length; i++)
            {
                leaves[i].enabled = showLeaves;
                if (!showLeaves) continue;

                float flutter = Mathf.Sin(elapsed * flutterSpeed + phases[i]) * flutterAmount;
                float along = edge + outward * (outwardOffsets[i] + outwardDrifts[i] * elapsed) + flutter;
                float across = lateralPositions[i] + sideDrifts[i] * elapsed;

                RectTransform leaf = leaves[i].rectTransform;
                leaf.anchoredPosition = new Vector2(along, across);
                leaf.localRotation = Quaternion.Euler(0f, 0f, startAngles[i] + spins[i] * elapsed);

                float flip = Mathf.Lerp(minFlipWidth, 1f, Mathf.Abs(Mathf.Cos(elapsed * flipSpeeds[i] + phases[i])));
                leaf.sizeDelta = new Vector2(sizes[i] * flip, sizes[i]);
            }
        }

        /// <summary>Canvas size; falls back to the screen size before the canvas has laid out (first frame).</summary>
        Vector2 ScreenSize()
        {
            Vector2 size = screenArea.rect.size;
            if (size.x > 0f && size.y > 0f) return size;

            float scale = screenArea.lossyScale.x > 0f ? screenArea.lossyScale.x : 1f;
            return new Vector2(Screen.width, Screen.height) / scale;
        }

        float Progress(float duration)
        {
            return Mathf.Clamp01(stateTimer / duration);
        }
    }
}
