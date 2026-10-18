using UnityEditor;
using UnityEngine;

namespace SushiDojo.UI.EditorTools
{
    /// <summary>
    /// Moves the match timer out of the top header onto the left wall and removes the old top header
    /// (YOU / OPPONENT scores), since the speech bubbles show the scores now. Safe to run again.
    /// </summary>
    public static class MultiplayerHUDLayoutSetup
    {
        const string PrefabPath = "Assets/03_Prefabs/MultiplayerHUD.prefab";
        const string HeaderName = "Match Header";
        const string TimerName = "Match Timer";

        // Left wall, upper part of the screen (0..1 of the canvas).
        static readonly Vector2 TimerAnchor = new(0.1f, 0.78f);

        [MenuItem("Tools/UI/HUD: Timer On Left Wall, Remove Top Scores")]
        static void Apply()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform header = root.transform.Find(HeaderName);
                Transform timer = header != null ? header.Find(TimerName) : root.transform.Find(TimerName);

                if (timer != null)
                {
                    var rect = (RectTransform)timer;
                    rect.SetParent(root.transform, false);
                    rect.anchorMin = rect.anchorMax = TimerAnchor;
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                }
                else
                {
                    Debug.LogWarning($"[HUD] '{TimerName}' not found in {PrefabPath}.");
                }

                if (header != null) Object.DestroyImmediate(header.gameObject);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[HUD] Timer moved to the left wall; top score header removed.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
