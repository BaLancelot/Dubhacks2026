using SushiDojo.Effects;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SushiDojo.DebugTools
{
    /// <summary>Test-only: press a key to play the dust cloud over the chopping board (or the screen center).</summary>
    public class DustCloudDebug : MonoBehaviour
    {
        static readonly Vector2 ViewportCenter = new(0.5f, 0.5f);

        [SerializeField] DustCloud cloud;
        [SerializeField] Key playKey = Key.Digit1;
        [Tooltip("Falls back to Camera.main.")]
        [SerializeField] Camera targetCamera;
        [Tooltip("Cloud appears over this. Empty = the chopping board in the scene, or the screen center if none.")]
        [SerializeField] Transform spawnTarget;

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || cloud == null) return;
            if (!keyboard[playKey].wasPressedThisFrame) return;

            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            Transform target = spawnTarget != null ? spawnTarget : FindBoard();

            if (target != null)
            {
                cloud.PlayOver(target.position, cam);
                return;
            }

            if (cam != null)
            {
                Vector3 center = cam.ViewportToWorldPoint(new Vector3(ViewportCenter.x, ViewportCenter.y,
                    cam.WorldToViewportPoint(cloud.transform.position).z));
                cloud.transform.position = center;
            }
            cloud.Play();
        }

        static Transform FindBoard()
        {
            ChoppingBoardMesh board = FindFirstObjectByType<ChoppingBoardMesh>();
            return board != null ? board.transform : null;
        }
    }
}
