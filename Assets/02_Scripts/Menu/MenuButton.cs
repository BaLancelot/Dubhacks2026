using SushiDojo.Sound;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SushiDojo.Menu
{
    /// <summary>Hover grow + hover/click sounds for a menu button. The click sound plays on press, not release.</summary>
    [RequireComponent(typeof(Button))]
    public class MenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        [SerializeField] SoundCue hoverCue;
        [SerializeField] SoundCue clickCue;
        [SerializeField, Min(1f)] float hoverScale = 1.08f;
        [Tooltip("How fast the scale follows the hover state.")]
        [SerializeField, Min(0f)] float scaleSpeed = 15f;

        Button button;
        bool hovered;

        void Awake()
        {
            button = GetComponent<Button>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && button.IsInteractable()) SoundManager.Play(clickCue);
        }

        void Update()
        {
            float target = hovered && button.IsInteractable() ? hoverScale : 1f;
            float blend = 1f - Mathf.Exp(-scaleSpeed * Time.unscaledDeltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * target, blend);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            if (button.IsInteractable()) SoundManager.Play(hoverCue);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
        }
    }
}
