using SushiDojo.Ingredients;
using UnityEngine;

namespace SushiDojo.Board
{
    [RequireComponent(typeof(BoxCollider))]
    public class BoardIngredient : MonoBehaviour
    {
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        [SerializeField] SpriteRenderer ingredientRenderer;
        [SerializeField] Sprite fallbackSprite;
        [SerializeField, Min(0.01f)] float fitSize = 0.7f;
        [SerializeField, Min(0.01f)] float colliderThickness = 0.12f;
        [SerializeField, Min(0f)] float discardUpwardImpulse = 2.5f;
        [SerializeField, Min(0f)] float discardPlanarImpulse = 1.5f;
        [SerializeField, Min(0f)] float discardTorque = 3.5f;
        [SerializeField, Min(0f)] float fadeDelay = 0.35f;
        [SerializeField, Min(0f)] float fadeDuration = 0.6f;
        [Tooltip("Turn the sprite to face the camera every frame instead of lying flat on the board.")]
        [SerializeField] bool faceCamera = true;
        [Tooltip("While facing the camera, lift the sprite by this fraction of its height so it stands on the board. 0.5 = bottom edge on the surface.")]
        [SerializeField, Range(0f, 1f)] float standLift = 0.4f;

        BoxCollider ingredientCollider;
        MaterialPropertyBlock materialProperties;
        Color initialColor;
        float discardTimer;
        bool isDiscarding;
        Vector3 rendererRestLocalPosition;

        public IngredientData Data { get; private set; }

        void Awake()
        {
            ingredientCollider = GetComponent<BoxCollider>();
            ingredientCollider.enabled = false;
            materialProperties = new MaterialPropertyBlock();
            if (ingredientRenderer != null) rendererRestLocalPosition = ingredientRenderer.transform.localPosition;
        }

        void LateUpdate()
        {
            if (!faceCamera || ingredientRenderer == null || ingredientRenderer.sprite == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            Transform view = cam.transform;
            Transform sprite = ingredientRenderer.transform;
            float height = ingredientRenderer.sprite.bounds.size.y * sprite.lossyScale.y;
            sprite.SetPositionAndRotation(
                transform.TransformPoint(rendererRestLocalPosition) + view.up * (height * standLift),
                view.rotation);
        }

        void Update()
        {
            if (!isDiscarding) return;

            discardTimer += Time.deltaTime;
            if (discardTimer < fadeDelay) return;

            float fadeProgress = fadeDuration > 0f
                ? Mathf.Clamp01((discardTimer - fadeDelay) / fadeDuration)
                : 1f;
            Color color = initialColor;
            color.a *= 1f - fadeProgress;
            ingredientRenderer.GetPropertyBlock(materialProperties);
            materialProperties.SetFloat(AlphaId, color.a);
            ingredientRenderer.SetPropertyBlock(materialProperties);

            if (fadeProgress >= 1f)
                Destroy(gameObject);
        }

        public bool Initialize(IngredientData data, int sortingOrderOffset)
        {
            if (data == null) throw new System.ArgumentNullException(nameof(data));
            Data = data;
            if (ingredientRenderer == null)
            {
                Debug.LogError($"{nameof(BoardIngredient)} requires an ingredient renderer.", this);
                return false;
            }
            Sprite displaySprite = data.Sprite != null ? data.Sprite : fallbackSprite;
            if (displaySprite == null)
            {
                Debug.LogError(
                    $"{nameof(BoardIngredient)} needs a fallback sprite when '{data.DisplayName}' has no sprite.",
                    this);
                return false;
            }

            ingredientRenderer.sprite = displaySprite;
            ingredientRenderer.sortingOrder += sortingOrderOffset;
            initialColor = ingredientRenderer.color;

            Vector2 spriteSize = displaySprite.bounds.size;
            float longestSide = Mathf.Max(spriteSize.x, spriteSize.y);
            if (longestSide <= 0f)
            {
                Debug.LogError($"Sprite for ingredient '{data.DisplayName}' has an invalid size.", data);
                return false;
            }

            float scale = fitSize / longestSide;
            ingredientRenderer.transform.localScale = Vector3.one * scale;
            ingredientRenderer.transform.localRotation =
                Quaternion.AngleAxis(180f, Vector3.up) * Quaternion.Euler(-90f, 0f, 0f);
            ingredientCollider = ingredientCollider != null ? ingredientCollider : GetComponent<BoxCollider>();
            ingredientCollider.size = new Vector3(
                spriteSize.x * scale,
                colliderThickness,
                spriteSize.y * scale);
            return true;
        }

        public void LaunchForDiscard(Vector3 boardUp)
        {
            if (isDiscarding) return;

            isDiscarding = true;
            transform.SetParent(null, true);
            ingredientCollider.enabled = true;
            Rigidbody body = gameObject.AddComponent<Rigidbody>();
            body.mass = 0.2f;
            body.linearDamping = 0.1f;
            body.angularDamping = 0.2f;
            body.useGravity = true;

            Vector2 planarImpulse = Random.insideUnitCircle * discardPlanarImpulse;
            Vector3 impulse = boardUp * discardUpwardImpulse +
                transform.right * planarImpulse.x +
                transform.forward * planarImpulse.y;
            body.AddForce(impulse, ForceMode.Impulse);
            body.AddTorque(Random.insideUnitSphere * discardTorque, ForceMode.Impulse);
        }
    }
}
