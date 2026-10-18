using SushiDojo.Board;
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

        void Awake()
        {
            if (belt == null) belt = FindFirstObjectByType<ConveyorBelt>();
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
