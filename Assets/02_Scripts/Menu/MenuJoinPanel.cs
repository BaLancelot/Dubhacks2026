using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SushiDojo.Menu
{
    /// <summary>Join screen: type a room code and connect. MainMenu slides it in and out.</summary>
    public class MenuJoinPanel : MonoBehaviour
    {
        [SerializeField] TMP_InputField codeInput;
        [SerializeField] Button connectButton;
        [SerializeField] Button backButton;
        [SerializeField] TMP_Text statusText;

        [Header("Debug")]
        [Tooltip("Editor/dev builds: pretend the join succeeded.")]
        [SerializeField] Key debugJoinSuccessKey = Key.J;

        /// <summary>The player asked to join with this room code.</summary>
        public event Action<string> Submitted;
        public event Action BackPressed;
        /// <summary>Debug only: fired by the debug key to fake a successful join.</summary>
        public event Action DebugJoinSucceeded;

        bool waiting;

        void Awake()
        {
            connectButton.onClick.AddListener(Submit);
            backButton.onClick.AddListener(() => BackPressed?.Invoke());
        }

        /// <summary>Resets the screen and puts the cursor in the code field.</summary>
        public void Open()
        {
            waiting = false;
            codeInput.text = "";
            codeInput.interactable = true;
            connectButton.interactable = true;
            SetStatus("");
            EventSystem.current?.SetSelectedGameObject(codeInput.gameObject);
            codeInput.ActivateInputField();
        }

        public void SetStatus(string message) => statusText.text = message;

        /// <summary>Call when the connection attempt failed so the player can try again.</summary>
        public void ShowError(string message)
        {
            waiting = false;
            codeInput.interactable = true;
            connectButton.interactable = true;
            SetStatus(message);
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (!waiting && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)) Submit();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (keyboard[debugJoinSuccessKey].wasPressedThisFrame && !codeInput.isFocused) DebugJoinSucceeded?.Invoke();
#endif
        }

        void Submit()
        {
            if (waiting) return;

            string code = codeInput.text.Trim().ToUpperInvariant();
            if (code.Length == 0)
            {
                SetStatus("Enter a room code");
                codeInput.ActivateInputField();
                return;
            }

            waiting = true;
            codeInput.interactable = false;
            connectButton.interactable = false;
            SetStatus("Connecting...");
            Submitted?.Invoke(code);
        }
    }
}
