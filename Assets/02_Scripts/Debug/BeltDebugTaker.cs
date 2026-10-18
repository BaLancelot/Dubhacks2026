using SushiDojo.Conveyor;
using SushiDojo.Ingredients;
using UnityEngine;

namespace SushiDojo.DebugTools
{
    /// <summary>Logs belt reveal and take events. Taking is click-only.</summary>
    public class BeltDebugTaker : MonoBehaviour
    {
        [SerializeField] ConveyorBelt belt;
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

        void HandleRevealed(IngredientData data)
        {
            if (logReveals) Debug.Log($"[BeltDebug] Revealed: {data.DisplayName}");
        }

        void HandleTaken(int takerId, IngredientData data)
        {
            Debug.Log($"[BeltDebug] Player {takerId} took: {data.DisplayName}");
        }
    }
}
