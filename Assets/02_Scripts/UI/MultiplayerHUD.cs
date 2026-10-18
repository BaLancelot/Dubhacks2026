using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SushiDojo.UI
{
    // Presentation only: the network timer and scoreboard remain the source of truth.
    public class MultiplayerHUD : MonoBehaviour
    {
        [Tooltip("Optional. Scores now show in the speech bubbles.")]
        [SerializeField] TMP_Text localScore;
        [Tooltip("Optional. Scores now show in the speech bubbles.")]
        [SerializeField] TMP_Text opponentScore;
        [SerializeField] TMP_Text timerText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] GameObject resultsPanel;
        [SerializeField] TMP_Text resultTitle;
        [SerializeField] TMP_Text resultDetails;
        [SerializeField] TMP_Text replayStatus;
        [SerializeField] Button replayButton;
        [SerializeField] Button returnButton;
        [SerializeField] Button menuButton;
        NetworkMatchTimer timer;
        NetworkScoreboard scoreboard;

        void Awake()
        {
            replayButton.onClick.AddListener(Replay);
            returnButton.onClick.AddListener(ReturnToMenu);
            menuButton.onClick.AddListener(ReturnToMenu);
            resultsPanel.SetActive(false);
        }

        void Replay() { if (timer != null) timer.RequestPlayAgain(); }
        void ReturnToMenu()
        {
            if (SushiMultiplayer.Instance != null) SushiMultiplayer.Instance.LeaveRoom();
        }

        void Update()
        {
            if (timer == null) timer = FindFirstObjectByType<NetworkMatchTimer>();
            if (scoreboard == null) scoreboard = FindFirstObjectByType<NetworkScoreboard>();
            SushiMultiplayer multiplayer = SushiMultiplayer.Instance;
            bool connected = multiplayer != null && !multiplayer.ConnectionLost;
            bool ended = timer != null && timer.HasMatchEnded;
            bool running = timer != null && timer.IsMatchRunning;
            bool scoresReady = scoreboard != null && scoreboard.IsSpawned;
            if (localScore != null)
                localScore.text = scoresReady ? $"YOU  {scoreboard.DisplayedLocalScore}" : "YOU  0";
            if (opponentScore != null)
                opponentScore.text = scoresReady ? $"OPPONENT  {scoreboard.DisplayedOpponentScore}" : "OPPONENT  0";
            int seconds = timer != null ? Mathf.CeilToInt(timer.TimeRemaining) : 180;
            timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
            statusText.text = multiplayer == null ? "Open MainMenu to host or join" :
                (running && connected && !multiplayer.IsBusy ? "" : multiplayer.Status);
            bool disconnected = multiplayer != null && multiplayer.ConnectionLost;
            resultsPanel.SetActive(ended || disconnected);
            menuButton.gameObject.SetActive(!running && !ended && !disconnected);
            menuButton.interactable = multiplayer != null && !multiplayer.IsBusy;
            if (!ended && !disconnected) return;

            bool localWon = timer != null && scoreboard != null &&
                timer.Winner == (scoreboard.IsHost ? 1 : 2);
            resultTitle.text = disconnected ? "PLAYER DISCONNECTED" :
                timer.Winner == 0 ? "IT'S A TIE" : localWon ? "YOU WIN!" : "OPPONENT WINS";
            resultDetails.text = scoresReady
                ? $"YOU {scoreboard.LocalScore}  —  OPPONENT {scoreboard.OpponentScore}\n" +
                  $"Recipe points: {scoreboard.LocalPoints} / {scoreboard.OpponentPoints}\n" +
                  $"Waste penalty: {scoreboard.LocalWaste} / {scoreboard.OpponentWaste}"
                : "The connection was lost.";
            replayStatus.text = disconnected ? "Return to the main menu to reconnect." :
                $"Play again: {timer.ReplayVoteCount}/2 ready";
            replayButton.gameObject.SetActive(ended && !disconnected);
            replayButton.interactable = connected && multiplayer.HasOpponent && !multiplayer.IsBusy &&
                timer != null && !timer.LocalReplayRequested;
            returnButton.interactable = multiplayer != null && !multiplayer.IsBusy;
        }
    }
}
