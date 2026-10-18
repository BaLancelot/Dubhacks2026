using SushiDojo.Cards;
using UnityEngine;

namespace SushiDojo.UI
{
    /// <summary>
    /// Reacts when the local player throws food off the board (food waste = minus points):
    /// every time, the player's own score bubble shakes and flashes red while the score drops;
    /// the first time in the game, the opponent's bubble also calls it out.
    /// Empty references are found in the scene.
    /// </summary>
    public class FoodWasteReaction : MonoBehaviour
    {
        [SerializeField] IngredientPlacementZone placementZone;
        [Tooltip("The bubble showing the local player's score.")]
        [SerializeField] BubbleScoreText myScore;
        [Tooltip("The bubble showing the opponent's score.")]
        [SerializeField] BubbleScoreText opponentBubble;

        [Header("First Waste Message")]
        [SerializeField, TextArea] string firstWasteMessage = "food waste!\nminus point.";
        [SerializeField, Min(0.1f)] float messageDuration = 2.5f;

        bool hasShownMessage;

        void Awake()
        {
            if (placementZone == null) placementZone = FindFirstObjectByType<IngredientPlacementZone>();
            if (myScore == null || opponentBubble == null) FindBubbles();
        }

        void OnEnable()
        {
            if (placementZone != null) placementZone.IngredientsDiscarded += HandleDiscarded;
        }

        void OnDisable()
        {
            if (placementZone != null) placementZone.IngredientsDiscarded -= HandleDiscarded;
        }

        void HandleDiscarded(int discardedCount)
        {
            if (myScore != null) myScore.PlayPenalty();

            NetworkMatchTimer timer = FindFirstObjectByType<NetworkMatchTimer>();
            if (timer != null && (timer.IsMatchSettling || timer.HasMatchEnded)) return;

            if (hasShownMessage || opponentBubble == null) return;
            hasShownMessage = true;
            opponentBubble.ShowMessage(firstWasteMessage, messageDuration);
        }

        void FindBubbles()
        {
            foreach (BubbleScoreText bubble in FindObjectsByType<BubbleScoreText>(FindObjectsSortMode.None))
            {
                if (bubble.ShowsLocalScore) myScore ??= bubble;
                else opponentBubble ??= bubble;
            }
        }
    }
}
