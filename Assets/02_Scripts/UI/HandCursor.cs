using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SushiDojo.UI
{
    /// <summary>
    /// Hides the OS cursor and drives a UI Image to the mouse position instead.
    /// Put this on an Image under a Screen Space - Overlay canvas with a high sort order.
    /// The Image's pivot is the click hotspot (e.g. fingertip).
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public class HandCursor : MonoBehaviour
    {
        [SerializeField] Sprite idleSprite;
        [SerializeField] Sprite pressedSprite;
        [SerializeField, Tooltip("Size multiplier on the sprite's pixel size, so both sprites keep the same scale.")]
        float scale = 0.15f;
        [SerializeField] bool hideSystemCursor = true;

        RectTransform rect;
        RectTransform canvasRect;
        Image image;
        Canvas canvas;

        const string PrefabPath = "HandCursor";

        /// <summary>Spawns Resources/HandCursor.prefab on game start so no scene setup is needed.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            if (FindAnyObjectByType<HandCursor>() != null) return;
            GameObject prefab = Resources.Load<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[HandCursor] Resources/{PrefabPath}.prefab not found.");
                return;
            }
            DontDestroyOnLoad(Instantiate(prefab));
        }

        void Awake()
        {
            rect = (RectTransform)transform;
            image = GetComponent<Image>();
            image.raycastTarget = false;
            canvas = GetComponentInParent<Canvas>().rootCanvas;
            canvasRect = (RectTransform)canvas.transform;
            SetSprite(idleSprite);
        }

        void OnEnable()
        {
            if (hideSystemCursor) Cursor.visible = false;
        }

        void OnDisable()
        {
            Cursor.visible = true;
        }

        void OnApplicationFocus(bool hasFocus)
        {
            // Unity restores the cursor when focus is regained, so hide it again.
            if (hasFocus && hideSystemCursor && enabled) Cursor.visible = false;
        }

        void LateUpdate()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 screenPos = mouse.position.ReadValue();
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, cam, out Vector2 local))
                rect.anchoredPosition = local;

            if (pressedSprite != null)
                SetSprite(mouse.leftButton.isPressed ? pressedSprite : idleSprite);
        }

        void SetSprite(Sprite sprite)
        {
            if (sprite == null || image.sprite == sprite) return;
            image.sprite = sprite;
            rect.sizeDelta = sprite.rect.size * scale;
        }
    }
}
