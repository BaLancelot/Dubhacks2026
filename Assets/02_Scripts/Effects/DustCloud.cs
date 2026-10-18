using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SushiDojo.Effects
{
    /// <summary>
    /// Cartoon dust cloud: pre-placed puff sprites pop in, bubble, drift and dissolve.
    /// Listen to OnCovered to swap what's underneath (e.g. ingredients -> dish).
    /// </summary>
    public class DustCloud : MonoBehaviour
    {
        const float FullTurn = Mathf.PI * 2f;
        const float FullTurnDegrees = 360f;
        static readonly int SeedId = Shader.PropertyToID("_Seed");
        static readonly int RotationId = Shader.PropertyToID("_Rotation");

        [SerializeField] SpriteRenderer[] puffs;
        [Tooltip("Played together with this cloud (e.g. a soft fog layer behind).")]
        [SerializeField] DustCloud[] linkedLayers;

        [Header("Layout")]
        [Tooltip("Puffs start inside this ellipse (half extents, world units).")]
        [SerializeField] Vector2 spreadExtents = new(0.9f, 0.55f);
        [Tooltip("Puff diameter, world units.")]
        [SerializeField] Vector2 sizeRange = new(0.7f, 1.3f);
        [Tooltip("How far each puff drifts outward and up over its life, world units.")]
        [SerializeField] Vector2 driftRange = new(0.1f, 0.4f);

        [Header("Timing")]
        [SerializeField, Min(0.01f)] float puffLifetime = 0.9f;
        [Tooltip("Random start delay per puff. The max keeps the cloud bubbling longer.")]
        [SerializeField] Vector2 startDelayRange = new(0f, 0.4f);
        [Tooltip("When OnCovered fires, as a fraction of the total duration.")]
        [SerializeField, Range(0f, 1f)] float coveredAt = 0.45f;
        [Tooltip("Optional burst near the end.")]
        [SerializeField] RevealSparkle finishSparkle;
        [Tooltip("When the sparkle bursts, as a fraction of the total duration.")]
        [SerializeField, Range(0f, 1f)] float sparkleAt = 0.75f;

        [Header("Bubbling")]
        [Tooltip("Scale wobble amount (fraction of size).")]
        [SerializeField, Min(0f)] float wobbleAmount = 0.08f;
        [Tooltip("Scale wobble speed, radians per second.")]
        [SerializeField, Min(0f)] float wobbleSpeed = 14f;

        [Header("Rotation")]
        [Tooltip("Spin speed, degrees per second. Direction is random per puff.")]
        [SerializeField] Vector2 spinSpeedRange = new(30f, 180f);
        [SerializeField] bool randomStartAngle = true;

        [Header("Curves (0..1 over each puff's life)")]
        [SerializeField] AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("Shader dissolve. 1 = solid, 0 = gone.")]
        [SerializeField] AnimationCurve solidityCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        public event Action OnCovered;
        public event Action OnFinished;

        public bool IsPlaying { get; private set; }
        public float TotalDuration => startDelayRange.y + puffLifetime;

        Vector3[] startPositions;
        Vector3[] drifts;
        float[] sizes;
        float[] delays;
        float[] phases;
        float[] startAngles;
        float[] spinSpeeds;
        float[] unitScales;
        MaterialPropertyBlock block;
        float timer;
        bool hasFiredCovered;
        bool hasFiredSparkle;

        void Awake()
        {
            int count = puffs.Length;
            startPositions = new Vector3[count];
            drifts = new Vector3[count];
            sizes = new float[count];
            delays = new float[count];
            phases = new float[count];
            startAngles = new float[count];
            spinSpeeds = new float[count];
            unitScales = new float[count];
            block = new MaterialPropertyBlock();

            for (int i = 0; i < count; i++)
                unitScales[i] = UnitScale(puffs[i].sprite);

            HideAll();
        }

        void Update()
        {
            if (!IsPlaying) return;

            timer += Time.deltaTime;

            for (int i = 0; i < puffs.Length; i++)
                UpdatePuff(i);

            if (!hasFiredCovered && timer >= TotalDuration * coveredAt)
            {
                hasFiredCovered = true;
                OnCovered?.Invoke();
            }

            if (!hasFiredSparkle && finishSparkle != null && timer >= TotalDuration * sparkleAt)
            {
                hasFiredSparkle = true;
                finishSparkle.Play();
            }

            if (timer >= TotalDuration)
            {
                IsPlaying = false;
                HideAll();
                OnFinished?.Invoke();
            }
        }

        public void Play()
        {
            for (int i = 0; i < puffs.Length; i++)
            {
                Vector2 offset = Vector2.Scale(Random.insideUnitCircle, spreadExtents);
                Vector2 outward = offset.sqrMagnitude > 0f ? offset.normalized : Vector2.up;

                startPositions[i] = offset;
                drifts[i] = (outward + Vector2.up).normalized * Random.Range(driftRange.x, driftRange.y);
                sizes[i] = Random.Range(sizeRange.x, sizeRange.y);
                delays[i] = Random.Range(startDelayRange.x, startDelayRange.y);
                phases[i] = Random.Range(0f, FullTurn);
                startAngles[i] = randomStartAngle ? Random.Range(0f, FullTurnDegrees) : 0f;
                float spin = Random.Range(spinSpeedRange.x, spinSpeedRange.y);
                spinSpeeds[i] = Random.value < 0.5f ? -spin : spin;

                puffs[i].GetPropertyBlock(block);
                block.SetFloat(SeedId, Random.value * 100f);
                puffs[i].SetPropertyBlock(block);
                puffs[i].enabled = false;
            }

            timer = 0f;
            hasFiredCovered = false;
            hasFiredSparkle = false;
            IsPlaying = true;

            foreach (DustCloud layer in linkedLayers)
                if (layer != null && layer != this) layer.Play();
        }

        /// <summary>
        /// Moves to where the world point appears on screen (keeping this cloud's depth, so its size
        /// doesn't change with a perspective camera), then plays.
        /// </summary>
        public void PlayOver(Vector3 worldPoint, Camera cam = null)
        {
            if (cam == null) cam = Camera.main;
            if (cam != null)
            {
                Vector3 screenPoint = cam.WorldToScreenPoint(worldPoint);
                screenPoint.z = cam.WorldToScreenPoint(transform.position).z;
                transform.position = cam.ScreenToWorldPoint(screenPoint);
            }
            Play();
        }

        void UpdatePuff(int i)
        {
            SpriteRenderer puff = puffs[i];
            float life = (timer - delays[i]) / puffLifetime;
            bool isAlive = life >= 0f && life <= 1f;
            puff.enabled = isAlive;
            if (!isAlive) return;

            float wobble = 1f + Mathf.Sin(timer * wobbleSpeed + phases[i]) * wobbleAmount;
            Transform puffTransform = puff.transform;
            puffTransform.localPosition = startPositions[i] + drifts[i] * life;
            puffTransform.localScale = Vector3.one * (sizes[i] * scaleCurve.Evaluate(life) * wobble * unitScales[i]);

            float angle = startAngles[i] + spinSpeeds[i] * (timer - delays[i]);
            puffTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            puff.GetPropertyBlock(block);
            block.SetFloat(RotationId, angle * Mathf.Deg2Rad);
            puff.SetPropertyBlock(block);

            Color color = puff.color;
            color.a = Mathf.Clamp01(solidityCurve.Evaluate(life));
            puff.color = color;
        }

        void HideAll()
        {
            foreach (SpriteRenderer puff in puffs)
                puff.enabled = false;
        }

        static float UnitScale(Sprite sprite)
        {
            if (sprite == null) return 1f;
            Vector2 size = sprite.bounds.size;
            float longestSide = Mathf.Max(size.x, size.y);
            return longestSide > 0f ? 1f / longestSide : 1f;
        }
    }
}
