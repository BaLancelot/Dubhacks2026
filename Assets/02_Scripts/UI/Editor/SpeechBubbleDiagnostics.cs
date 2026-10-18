using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SushiDojo.UI.EditorTools
{
    /// <summary>Logs everything that decides whether a UI speech bubble shows up, for each one in the open scenes.</summary>
    public static class SpeechBubbleDiagnostics
    {
        [MenuItem("Tools/UI/Diagnose UI Speech Bubbles")]
        static void Diagnose()
        {
            UISpeechBubble[] bubbles = Object.FindObjectsByType<UISpeechBubble>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (bubbles.Length == 0)
            {
                Debug.LogWarning("[Bubble Diagnose] No UISpeechBubble in the open scenes.");
                return;
            }

            foreach (UISpeechBubble bubble in bubbles)
            {
                bubble.SetAllDirty();
                Canvas.ForceUpdateCanvases();
                Debug.Log(Report(bubble), bubble);
            }
        }

        static string Report(UISpeechBubble bubble)
        {
            var log = new StringBuilder();
            log.AppendLine($"[Bubble Diagnose] {Path(bubble.transform)}");
            log.AppendLine($"  activeInHierarchy={bubble.gameObject.activeInHierarchy} enabled={bubble.enabled}");

            Canvas canvas = bubble.canvas;
            log.AppendLine(canvas == null
                ? "  canvas=NONE (not under a Canvas -> nothing renders)"
                : $"  canvas={Path(canvas.rootCanvas.transform)} mode={canvas.rootCanvas.renderMode} enabled={canvas.rootCanvas.enabled} sortOrder={canvas.rootCanvas.sortingOrder}");

            RectTransform rect = bubble.rectTransform;
            log.AppendLine($"  rect={rect.rect.size} lossyScale={rect.lossyScale} siblingIndex={rect.GetSiblingIndex()}");
            log.AppendLine($"  color={bubble.color} texture={(bubble.mainTexture != null ? bubble.mainTexture.name : "null")} material={(bubble.materialForRendering != null ? bubble.materialForRendering.name : "null")}");
            log.AppendLine($"  vertices={bubble.LastVertexCount} cull={bubble.canvasRenderer.cull} rendererAlpha={bubble.canvasRenderer.GetAlpha()} inheritedAlpha={bubble.canvasRenderer.GetInheritedAlpha()}");

            for (Transform t = bubble.transform; t != null; t = t.parent)
            {
                if (t.TryGetComponent(out CanvasGroup group))
                    log.AppendLine($"  CanvasGroup on {t.name}: alpha={group.alpha} ignoreParent={group.ignoreParentGroups}");
                if (t != bubble.transform && t.TryGetComponent(out RectMask2D mask2D))
                    log.AppendLine($"  RectMask2D on {t.name} rect={((RectTransform)t).rect}");
                if (t != bubble.transform && t.TryGetComponent(out Mask mask))
                    log.AppendLine($"  Mask on {t.name} showGraphic={mask.showMaskGraphic}");
                if (!t.gameObject.activeSelf) log.AppendLine($"  INACTIVE: {t.name}");
            }

            foreach (TMP_Text text in bubble.GetComponentsInChildren<TMP_Text>(true))
                log.AppendLine($"  text {text.name}: '{text.text}' chars={text.textInfo?.characterCount} color={text.color} size={text.fontSize} font={(text.font != null ? text.font.name : "null")} active={text.gameObject.activeInHierarchy}");

            UISpeechBubbleOutline outline = bubble.GetComponentInChildren<UISpeechBubbleOutline>(true);
            log.AppendLine(outline == null ? "  outline=none" : $"  outline rect={outline.rectTransform.rect.size} active={outline.gameObject.activeInHierarchy}");
            return log.ToString();
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
