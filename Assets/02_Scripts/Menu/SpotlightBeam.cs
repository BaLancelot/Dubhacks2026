using System.Collections;
using UnityEngine;

namespace SushiDojo.Menu
{
    /// <summary>
    /// A fake spotlight: a soft cone sprite coming down from above plus a light pool on the floor.
    /// Textures are generated in code, so no art is needed. Starts off; call TurnOn().
    /// Additive mode adds the beam's color onto what's behind it, like real light.
    /// </summary>
    public class SpotlightBeam : MonoBehaviour
    {
        const int ConeWidth = 128;
        const int ConeHeight = 256;
        const int PoolSize = 128;
        const string AdditiveMaterialPath = "SpriteAdditive";

        SpriteRenderer cone;
        SpriteRenderer pool;
        Color color;
        float breathe;
        float alpha;
        bool on;

        /// <summary>Builds the beam with its bottom center at this object's position.</summary>
        public static SpotlightBeam Create(Transform parent, Vector3 localBottom, Color color, float height,
            float topWidth, float bottomWidth, float tilt, Vector2 poolSize, string sortingLayer, int sortingOrder, int poolSortingOrder,
            float breathe, bool additive)
        {
            Material material = null;
            if (additive)
            {
                material = Resources.Load<Material>(AdditiveMaterialPath);
                if (material == null) Debug.LogWarning($"[SpotlightBeam] Resources/{AdditiveMaterialPath}.mat not found. Using normal blending.");
            }

            GameObject go = new("SpotlightBeam");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localBottom;

            var beam = go.AddComponent<SpotlightBeam>();
            beam.color = color;
            beam.breathe = breathe;

            float spriteWidth = (float)ConeWidth / ConeHeight;
            beam.cone = CreateRenderer(go.transform, "Cone",
                Sprite.Create(CreateConeTexture(topWidth / bottomWidth), new Rect(0, 0, ConeWidth, ConeHeight),
                    new Vector2(0.5f, 0f), ConeHeight),
                sortingLayer, sortingOrder, material);
            beam.cone.transform.localScale = new Vector3(bottomWidth / spriteWidth, height, 1f);
            // Swings the beam around its bottom point, so it still lands on the dish.
            beam.cone.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

            beam.pool = CreateRenderer(go.transform, "Pool",
                Sprite.Create(CreatePoolTexture(), new Rect(0, 0, PoolSize, PoolSize), new Vector2(0.5f, 0.5f), PoolSize),
                sortingLayer, poolSortingOrder, material);
            beam.pool.transform.localScale = new Vector3(poolSize.x, poolSize.y, 1f);
            // Equal sorting orders fall back to depth: this keeps the pool on the table but under the dish.
            beam.pool.transform.localPosition = new Vector3(0f, 0f, 0.015f);

            beam.SetAlpha(0f);
            return beam;
        }

        /// <summary>Snaps on with a quick flicker, like a stage light being thrown.</summary>
        public void TurnOn()
        {
            if (on) return;
            on = true;
            StartCoroutine(Flicker());
        }

        /// <summary>Fades the beam out over the given time.</summary>
        public void TurnOff(float duration)
        {
            on = false;
            StopAllCoroutines();
            StartCoroutine(FadeOut(duration));
        }

        void Update()
        {
            if (!on || alpha < 1f || breathe <= 0f) return;
            // Slow, subtle shimmer so the light feels alive.
            SetAlpha(1f - breathe * (0.5f + 0.5f * Mathf.Sin(Time.time * 2.3f)), keep: true);
        }

        IEnumerator Flicker()
        {
            SetAlpha(1f);
            yield return new WaitForSeconds(0.05f);
            SetAlpha(0.25f);
            yield return new WaitForSeconds(0.05f);
            SetAlpha(1f);
        }

        IEnumerator FadeOut(float duration)
        {
            float from = cone.color.a / Mathf.Max(color.a, Mathf.Epsilon);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                SetAlpha(Mathf.Lerp(from, 0f, t / duration));
                yield return null;
            }
            SetAlpha(0f);
        }

        void SetAlpha(float value, bool keep = false)
        {
            if (!keep) alpha = value;
            Color c = color;
            c.a *= value;
            cone.color = c;
            pool.color = c;
        }

        static SpriteRenderer CreateRenderer(Transform parent, string name, Sprite sprite, string sortingLayer, int sortingOrder,
            Material material)
        {
            GameObject go = new(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (material != null) sr.sharedMaterial = material;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        /// <summary>Soft-edged trapezoid, narrow at the top, brightest near the lamp.</summary>
        static Texture2D CreateConeTexture(float topRatio)
        {
            var pixels = new Color32[ConeWidth * ConeHeight];
            for (int y = 0; y < ConeHeight; y++)
            {
                float v = (float)y / (ConeHeight - 1);
                float halfWidth = 0.5f * Mathf.Lerp(1f, topRatio, v);
                float soft = halfWidth * 0.6f;
                float vertical = Mathf.SmoothStep(0f, 1f, v / 0.2f) * Mathf.Lerp(0.45f, 1f, v);
                for (int x = 0; x < ConeWidth; x++)
                {
                    float d = Mathf.Abs((x + 0.5f) / ConeWidth - 0.5f);
                    float a = Mathf.SmoothStep(0f, 1f, (halfWidth - d) / soft) * vertical;
                    pixels[y * ConeWidth + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                }
            }
            return CreateTexture(ConeWidth, ConeHeight, pixels);
        }

        /// <summary>Round soft glow, squashed into an ellipse by its scale.</summary>
        static Texture2D CreatePoolTexture()
        {
            var pixels = new Color32[PoolSize * PoolSize];
            for (int y = 0; y < PoolSize; y++)
            for (int x = 0; x < PoolSize; x++)
            {
                float dx = (x + 0.5f) / PoolSize * 2f - 1f, dy = (y + 0.5f) / PoolSize * 2f - 1f;
                float a = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * PoolSize + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            return CreateTexture(PoolSize, PoolSize, pixels);
        }

        static Texture2D CreateTexture(int width, int height, Color32[] pixels)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
