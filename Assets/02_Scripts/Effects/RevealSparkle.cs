using UnityEngine;

namespace SushiDojo.Effects
{
    /// <summary>
    /// Burst of sprites flying outward from this transform.
    /// Sparkle sprites are pre-placed children; a random amount of them is used per play.
    /// </summary>
    public class RevealSparkle : MonoBehaviour
    {
        const float FullCircle = 360f;

        [SerializeField] SpriteRenderer[] sparkles;

        [Header("Count")]
        [SerializeField, Min(0)] int minCount = 4;
        [SerializeField, Min(0)] int maxCount = 7;

        [Header("Motion")]
        [SerializeField, Min(0.01f)] float duration = 0.5f;
        [SerializeField] Vector2 distanceRange = new(0.6f, 1f);
        [Tooltip("Random angle offset per sparkle, in degrees.")]
        [SerializeField, Range(0f, 180f)] float angleJitter = 15f;
        [Tooltip("Longest side of each sparkle sprite, in world units.")]
        [SerializeField] Vector2 sizeRange = new(0.15f, 0.3f);
        [Tooltip("Degrees per second. Direction is random per sparkle.")]
        [SerializeField] float spinSpeed = 180f;

        [Header("Curves (0..1 over duration)")]
        [SerializeField] AnimationCurve distanceCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField] AnimationCurve alphaCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        Vector3[] directions;
        float[] distances;
        float[] sizes;
        float[] spins;
        Color[] baseColors;
        float[] unitScales;
        int activeCount;
        float timer;
        bool isPlaying;

        void Awake()
        {
            int count = sparkles.Length;
            directions = new Vector3[count];
            distances = new float[count];
            sizes = new float[count];
            spins = new float[count];
            baseColors = new Color[count];
            unitScales = new float[count];

            for (int i = 0; i < count; i++)
            {
                baseColors[i] = sparkles[i].color;
                unitScales[i] = UnitScale(sparkles[i].sprite);
            }

            HideAll();
        }

        void Update()
        {
            if (!isPlaying) return;

            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            Apply(progress);

            if (progress >= 1f)
            {
                isPlaying = false;
                HideAll();
            }
        }

        public void Play()
        {
            int upper = Mathf.Min(maxCount, sparkles.Length);
            int lower = Mathf.Min(minCount, upper);
            activeCount = Random.Range(lower, upper + 1);

            float angleStep = FullCircle / Mathf.Max(activeCount, 1);
            float startAngle = Random.Range(0f, FullCircle);

            for (int i = 0; i < sparkles.Length; i++)
            {
                bool active = i < activeCount;
                sparkles[i].enabled = active;
                if (!active) continue;

                float angle = (startAngle + angleStep * i + Random.Range(-angleJitter, angleJitter)) * Mathf.Deg2Rad;
                directions[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                distances[i] = Random.Range(distanceRange.x, distanceRange.y);
                sizes[i] = Random.Range(sizeRange.x, sizeRange.y);
                spins[i] = Random.value < 0.5f ? -spinSpeed : spinSpeed;
            }

            timer = 0f;
            isPlaying = true;
            Apply(0f);
        }

        void Apply(float progress)
        {
            float distance = distanceCurve.Evaluate(progress);
            float scale = scaleCurve.Evaluate(progress);
            float alpha = alphaCurve.Evaluate(progress);

            for (int i = 0; i < activeCount; i++)
            {
                Transform sparkle = sparkles[i].transform;
                sparkle.localPosition = directions[i] * (distances[i] * distance);
                sparkle.localScale = Vector3.one * (sizes[i] * scale * unitScales[i]);
                sparkle.localRotation = Quaternion.Euler(0f, 0f, spins[i] * timer);

                Color color = baseColors[i];
                color.a *= alpha;
                sparkles[i].color = color;
            }
        }

        /// <summary>Scale that makes the sprite's longest side 1 world unit.</summary>
        static float UnitScale(Sprite sprite)
        {
            if (sprite == null) return 1f;
            Vector2 size = sprite.bounds.size;
            float longestSide = Mathf.Max(size.x, size.y);
            return longestSide > 0f ? 1f / longestSide : 1f;
        }

        void HideAll()
        {
            foreach (SpriteRenderer sparkle in sparkles)
                sparkle.enabled = false;
        }
    }
}
