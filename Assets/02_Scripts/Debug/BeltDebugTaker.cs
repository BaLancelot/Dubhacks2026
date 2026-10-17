using SushiDojo.Conveyor;
using SushiDojo.Ingredients;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SushiDojo.DebugTools
{
    /// <summary>Test-only: press a key to take the center ingredient and log belt events.</summary>
    public class BeltDebugTaker : MonoBehaviour
    {
        [SerializeField] ConveyorBelt belt;
        [SerializeField] Key takeKey = Key.Space;
        [SerializeField] bool logReveals = true;

        void OnEnable()
        {
            if (belt == null) return;
            belt.OnCenterRevealed += HandleRevealed;
            belt.OnCenterTaken += HandleTaken;
        }

        void OnDisable()
        {
            if (belt == null) return;
            belt.OnCenterRevealed -= HandleRevealed;
            belt.OnCenterTaken -= HandleTaken;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || belt == null) return;
            if (!keyboard[takeKey].wasPressedThisFrame) return;

            if (!belt.TryTakeCenter(out _))
                Debug.Log("[BeltDebug] Nothing to take.");
        }

        void HandleRevealed(IngredientData data)
        {
            if (logReveals) Debug.Log($"[BeltDebug] Revealed: {data.DisplayName}");
        }

        void HandleTaken(IngredientData data)
        {
            Debug.Log($"[BeltDebug] Took: {data.DisplayName}");
        }
    }
}
