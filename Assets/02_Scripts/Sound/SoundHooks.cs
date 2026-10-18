using SushiDojo.Board;
using SushiDojo.Cards;
using SushiDojo.Conveyor;
using SushiDojo.Effects;
using SushiDojo.Ingredients;
using UnityEngine;

namespace SushiDojo.Sound
{
    /// <summary>
    /// Hooks sounds onto existing gameplay events so no gameplay code needs to call audio.
    /// Empty targets are found in the scene; empty cues stay silent.
    /// </summary>
    public class SoundHooks : MonoBehaviour
    {
        [Header("Conveyor")]
        [SerializeField] ConveyorBelt belt;
        [SerializeField] SoundCue beltStep;
        [SerializeField] SoundCue coverOpen;
        [SerializeField] SoundCue ingredientTaken;

        [Tooltip("This player won an ingredient from the belt.")]
        [SerializeField] SoundCue ingredientCollected;

        [Header("Cards")]
        [SerializeField] IngredientHandView hand;
        [SerializeField] IngredientPlacementZone placementZone;
        [SerializeField] SoundCue cardHover;
        [SerializeField] SoundCue cardSelect;
        [Tooltip("A card dropped on the board becomes an ingredient. One is picked at random.")]
        [SerializeField] SoundCue[] cardPlaced;

        [Header("Cooking Cloud")]
        [SerializeField] DustCloud dustCloud;
        [SerializeField] SoundCue cloudStart;
        [SerializeField] SoundCue cloudCovered;
        [SerializeField] SoundCue cloudSparkle;

        [Header("Chopping Board")]
        [SerializeField] ThrowableBody board;
        [SerializeField] ThrowRespawn boardRespawn;
        [SerializeField] SoundCue boardGrab;
        [SerializeField] SoundCue boardThrow;
        [Tooltip("Throw sound only plays at or above this throw speed (units/s). Gentle releases stay silent.")]
        [SerializeField, Min(0f)] float minBoardThrowSpeed = 5f;
        [SerializeField] SoundCue boardReturn;

        NetworkConveyor networkConveyor;
        bool cardsBound;
        float nextBindAttempt;

        void Awake()
        {
            if (belt == null) belt = FindFirstObjectByType<ConveyorBelt>();
            networkConveyor = FindFirstObjectByType<NetworkConveyor>();
            if (dustCloud == null) dustCloud = FindRootCloud();
            if (board == null) board = FindFirstObjectByType<ThrowableBody>();
            if (boardRespawn == null && board != null) boardRespawn = board.GetComponent<ThrowRespawn>();
        }

        void OnEnable()
        {
            if (belt != null)
            {
                belt.OnStepStarted += HandleBeltStep;
                belt.OnCenterRevealed += HandleCoverOpen;
                belt.OnCenterTaken += HandleIngredientTaken;
            }
            if (dustCloud != null)
            {
                dustCloud.OnStarted += HandleCloudStart;
                dustCloud.OnCovered += HandleCloudCovered;
                dustCloud.OnSparkle += HandleCloudSparkle;
            }
            if (board != null)
            {
                board.Grabbed += HandleBoardGrab;
                board.Released += HandleBoardThrow;
            }
            if (boardRespawn != null) boardRespawn.OnReturnedHome += HandleBoardReturn;
            if (networkConveyor != null) networkConveyor.OnIngredientAwarded += HandleIngredientCollected;
            BindCards();
        }

        void Update()
        {
            // The card hand can be created after this (virtual players spawn their UI late), so keep looking.
            if (cardsBound || Time.unscaledTime < nextBindAttempt) return;
            nextBindAttempt = Time.unscaledTime + 1f;
            BindCards();
        }

        void BindCards()
        {
            if (cardsBound) return;
            if (hand == null) hand = FindFirstObjectByType<IngredientHandView>();
            if (placementZone == null) placementZone = FindFirstObjectByType<IngredientPlacementZone>();
            if (hand == null || placementZone == null) return;

            hand.CardHovered += HandleCardHover;
            hand.CardPicked += HandleCardSelect;
            placementZone.IngredientPlaced += HandleCardPlaced;
            cardsBound = true;
        }

        void UnbindCards()
        {
            if (!cardsBound) return;
            if (hand != null)
            {
                hand.CardHovered -= HandleCardHover;
                hand.CardPicked -= HandleCardSelect;
            }
            if (placementZone != null) placementZone.IngredientPlaced -= HandleCardPlaced;
            cardsBound = false;
        }

        void OnDisable()
        {
            if (belt != null)
            {
                belt.OnStepStarted -= HandleBeltStep;
                belt.OnCenterRevealed -= HandleCoverOpen;
                belt.OnCenterTaken -= HandleIngredientTaken;
            }
            if (dustCloud != null)
            {
                dustCloud.OnStarted -= HandleCloudStart;
                dustCloud.OnCovered -= HandleCloudCovered;
                dustCloud.OnSparkle -= HandleCloudSparkle;
            }
            if (board != null)
            {
                board.Grabbed -= HandleBoardGrab;
                board.Released -= HandleBoardThrow;
            }
            if (boardRespawn != null) boardRespawn.OnReturnedHome -= HandleBoardReturn;
            if (networkConveyor != null) networkConveyor.OnIngredientAwarded -= HandleIngredientCollected;
            UnbindCards();
        }

        void HandleIngredientCollected(IngredientData _) => SoundManager.Play(ingredientCollected);
        void HandleCardHover(IngredientCardUI _) => SoundManager.Play(cardHover);
        void HandleCardSelect(IngredientCardUI _) => SoundManager.Play(cardSelect);
        void HandleCardPlaced(IngredientData _)
        {
            if (cardPlaced != null && cardPlaced.Length > 0) SoundManager.Play(cardPlaced[Random.Range(0, cardPlaced.Length)]);
        }

        void HandleBeltStep() => SoundManager.Play(beltStep);
        void HandleCoverOpen(IngredientData _) => SoundManager.Play(coverOpen);
        void HandleIngredientTaken(int playerId, IngredientData _) => SoundManager.Play(ingredientTaken);
        void HandleCloudStart() => SoundManager.Play(cloudStart);
        void HandleCloudCovered() => SoundManager.Play(cloudCovered);
        void HandleCloudSparkle() => SoundManager.Play(cloudSparkle);
        void HandleBoardGrab() => SoundManager.Play(boardGrab);
        void HandleBoardThrow()
        {
            if (board.LastThrowSpeed >= minBoardThrowSpeed) SoundManager.Play(boardThrow);
        }
        void HandleBoardReturn() => SoundManager.Play(boardReturn);

        /// <summary>The cloud that isn't just a linked layer (e.g. the fog child).</summary>
        static DustCloud FindRootCloud()
        {
            foreach (DustCloud cloud in FindObjectsByType<DustCloud>(FindObjectsSortMode.None))
                if (cloud.transform.parent == null || cloud.transform.parent.GetComponentInParent<DustCloud>() == null)
                    return cloud;
            return null;
        }
    }
}
