using SushiDojo.Effects;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SushiDojo.DebugTools
{
    /// <summary>Test-only: press a key to replay the start-of-game wipe (black, then leaves sweep it away).</summary>
    public class LeafTransitionDebug : MonoBehaviour
    {
        [SerializeField] LeafTransition transition;
        [SerializeField] Key playKey = Key.Digit2;

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || transition == null) return;
            if (keyboard[playKey].wasPressedThisFrame) transition.RevealFromBlack();
        }
    }
}
