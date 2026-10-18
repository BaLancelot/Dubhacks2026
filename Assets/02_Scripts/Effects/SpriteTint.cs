using UnityEngine;

namespace SushiDojo.Effects
{
    /// <summary>
    /// Per-object tint for sprites using the SushiDojo/SpriteColorTint material.
    /// Values go through a MaterialPropertyBlock, so every object can have its own color without extra materials.
    /// Updates live in the editor.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(SpriteRenderer))]
    public class SpriteTint : MonoBehaviour
    {
        const string TintShaderName = "SushiDojo/SpriteColorTint";

        static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        static readonly int TintAmountId = Shader.PropertyToID("_TintAmount");
        static readonly int KeepShadingId = Shader.PropertyToID("_KeepShading");
        static readonly int BrightnessId = Shader.PropertyToID("_Brightness");

        [Tooltip("Swapped in when the sprite isn't already on the tint shader.")]
        [SerializeField] Material tintMaterial;
        [SerializeField] Color tintColor = new(1f, 0.5f, 0.5f, 1f);
        [Tooltip("0 = original sprite, 1 = fully painted with the tint color.")]
        [SerializeField, Range(0f, 1f)] float tintAmount = 1f;
        [Tooltip("0 = flat color, 1 = the sprite's own light and dark show through.")]
        [SerializeField, Range(0f, 1f)] float keepShading = 1f;
        [SerializeField, Range(0f, 3f)] float brightness = 1f;

        SpriteRenderer spriteRenderer;
        MaterialPropertyBlock block;

        public Color TintColor
        {
            get => tintColor;
            set { tintColor = value; Apply(); }
        }

        public float TintAmount
        {
            get => tintAmount;
            set { tintAmount = Mathf.Clamp01(value); Apply(); }
        }

        void OnEnable()
        {
            Apply();
        }

        void OnValidate()
        {
            Apply();
        }

        void OnDisable()
        {
            if (spriteRenderer != null) spriteRenderer.SetPropertyBlock(null);
        }

        void Apply()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            block ??= new MaterialPropertyBlock();

            Material current = spriteRenderer.sharedMaterial;
            if (tintMaterial != null && (current == null || current.shader.name != TintShaderName))
                spriteRenderer.sharedMaterial = tintMaterial;

            spriteRenderer.GetPropertyBlock(block);
            block.SetColor(TintColorId, tintColor);
            block.SetFloat(TintAmountId, tintAmount);
            block.SetFloat(KeepShadingId, keepShading);
            block.SetFloat(BrightnessId, brightness);
            spriteRenderer.SetPropertyBlock(block);
        }

#if UNITY_EDITOR
        // Picks up the shared tint material when the component is added.
        void Reset()
        {
            tintMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/01_Arts/Materials/SpriteColorTint.mat");
            Apply();
        }
#endif
    }
}
