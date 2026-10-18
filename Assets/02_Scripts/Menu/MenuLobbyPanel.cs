using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SushiDojo.Menu
{
    /// <summary>
    /// Lobby screen for a 1:1 match: room code, the other player's model once they're in, and GAME START.
    /// Networking code drives it through SetRoomCode / SetOpponentJoined.
    /// </summary>
    public class MenuLobbyPanel : MonoBehaviour
    {
        [SerializeField] TMP_Text roomCodeText;
        [SerializeField] TMP_Text waitingText;
        [SerializeField] LobbyPlayerPreview opponentPreview;
        [SerializeField] Button startButton;
        [SerializeField] Button backButton;

        [Header("Text")]
        [SerializeField] string hostWaitingMessage = "WAITING FOR PLAYER";
        [SerializeField] string guestWaitingMessage = "WAITING FOR HOST";
        [Tooltip("Seconds per animated dot.")]
        [SerializeField, Min(0.05f)] float dotInterval = 0.4f;

        [Header("Debug")]
        [Tooltip("Editor/dev builds: toggles the other player joining/leaving.")]
        [SerializeField] Key debugToggleOpponentKey = Key.J;

        public event Action StartPressed;
        public event Action BackPressed;

        public bool IsHost { get; private set; }
        public bool OpponentJoined { get; private set; }

        public bool AllowDebugInput { get; set; } = true;
        bool networkBusy;
        string networkStatus;
        int openedFrame;
        int roomCodeSetFrame = -1;
        bool hasRoomCode;

        public void SetNetworkState(string message, bool busy)
        {
            networkStatus = message;
            networkBusy = busy;
            waitingText.enabled = busy || !OpponentJoined || !IsHost;
            startButton.interactable = IsHost && OpponentJoined && !busy;
        }


        void Awake()
        {
            startButton.onClick.AddListener(() => StartPressed?.Invoke());
            backButton.onClick.AddListener(() => BackPressed?.Invoke());
        }

        /// <summary>Resets the lobby. Only the host gets the GAME START button.</summary>
        public void Open(bool isHost)
        {
            IsHost = isHost;
            openedFrame = Time.frameCount;
            networkBusy = true;
            networkStatus = isHost ? "Creating room..." : "Connecting to host...";
            waitingText.text = networkStatus;
            SetRoomCode("");
            SetOpponentJoined(false);
        }

        public void SetRoomCode(string code)
        {
            string displayedCode = string.IsNullOrEmpty(code) ? "" : code;
            if (roomCodeText.text != displayedCode)
            {
                roomCodeText.text = displayedCode;
                roomCodeSetFrame = Time.frameCount;
            }
            hasRoomCode = displayedCode.Length > 0;
            roomCodeText.enabled = hasRoomCode && !OpponentJoined;
            if (!hasRoomCode) roomCodeSetFrame = -1;
        }

        /// <summary>The other player joined (true) or left (false).</summary>
        public void SetOpponentJoined(bool joined)
        {
            OpponentJoined = joined;
            roomCodeText.enabled = hasRoomCode && !joined;
            waitingText.enabled = !joined;
            if (joined) opponentPreview.Show();
            else opponentPreview.Hide();

            startButton.gameObject.SetActive(IsHost);
            startButton.interactable = joined && !networkBusy;
        }

        void Update()
        {
            if (waitingText.enabled)
            {
                int dots = Mathf.FloorToInt(Time.time / dotInterval) % 4;
                // Give the room-code text a rendered frame before announcing that the room is ready.
                bool codeDisplayed = hasRoomCode && Time.frameCount > roomCodeSetFrame;
                string message = !string.IsNullOrEmpty(networkStatus) && networkBusy
                    ? networkStatus : IsHost && !codeDisplayed
                        ? "Creating room" : IsHost ? hostWaitingMessage : guestWaitingMessage;
                waitingText.text = message.TrimEnd('.') + new string('.', dots);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Keyboard keyboard = Keyboard.current;
            // Skip the opening frame so the key that opened the lobby doesn't also toggle it.
            if (AllowDebugInput && keyboard != null && Time.frameCount != openedFrame && keyboard[debugToggleOpponentKey].wasPressedThisFrame)
                SetOpponentJoined(!OpponentJoined);
#endif
        }
    }
}
