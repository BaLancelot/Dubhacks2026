using SushiDojo.Sound;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace SushiDojo.Menu
{
    /// <summary>
    /// Shows a 3D player model inside the UI: a hidden camera far off-stage renders the model
    /// into a RenderTexture that this RawImage displays. The model boings in and jiggles on the beat.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class LobbyPlayerPreview : MonoBehaviour
    {
        [SerializeField] GameObject playerPrefab;
        [Tooltip("World spot far from the menu where the model and its camera live.")]
        [SerializeField] Vector3 stagePosition = new(1000f, 0f, 0f);
        [SerializeField, Min(64)] int textureSize = 512;

        [Header("Camera")]
        [SerializeField, Min(0.1f)] float cameraDistance = 6f;
        [Tooltip("Height the camera looks at (model is about 2 units tall).")]
        [SerializeField] float lookHeight = 1f;
        [SerializeField, Range(5f, 90f)] float fieldOfView = 30f;
        [Tooltip("Model Y rotation. 0 = facing the camera.")]
        [SerializeField] float modelYaw;

        [Header("Animation")]
        [SerializeField, Min(0.01f)] float popDuration = 0.45f;
        [SerializeField] AnimationCurve popCurve = new(
            new Keyframe(0f, 0f), new Keyframe(0.4f, 1.2f), new Keyframe(0.7f, 0.93f), new Keyframe(1f, 1f));
        [Tooltip("Jelly hit on every beat while shown. 0 = none.")]
        [SerializeField, Min(0f)] float beatJiggle = 4f;

        RawImage image;
        RenderTexture texture;
        Camera previewCamera;
        GameObject player;
        ChefImpactJelly jelly;
        float popTimer = -1f;
        int lastBeat = int.MinValue;

        public bool IsShown { get; private set; }

        void Awake()
        {
            image = GetComponent<RawImage>();
            image.enabled = false;
        }

        void OnDestroy()
        {
            if (previewCamera != null) Destroy(previewCamera.gameObject);
            if (player != null) Destroy(player);
            if (texture != null) texture.Release();
        }

        public void Show()
        {
            if (IsShown) return;
            EnsureStage();
            IsShown = true;
            image.enabled = true;
            previewCamera.enabled = true;
            player.SetActive(true);
            popTimer = 0f;
            transform.localScale = Vector3.zero;
            if (jelly != null) jelly.Impact(Vector3.up, beatJiggle * 1.5f);
        }

        public void Hide()
        {
            IsShown = false;
            image.enabled = false;
            if (previewCamera != null) previewCamera.enabled = false;
            if (player != null) player.SetActive(false);
        }

        void Update()
        {
            if (!IsShown) return;

            if (popTimer >= 0f)
            {
                popTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(popTimer / popDuration);
                transform.localScale = Vector3.one * popCurve.Evaluate(progress);
                if (progress >= 1f) popTimer = -1f;
            }

            int beat = Mathf.FloorToInt(BeatClock.Beats());
            if (beat != lastBeat)
            {
                if (lastBeat != int.MinValue && jelly != null && beatJiggle > 0f) jelly.Impact(Vector3.up, beatJiggle);
                lastBeat = beat;
            }
        }

        void EnsureStage()
        {
            if (texture == null)
            {
                texture = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32) { name = "LobbyPlayerPreview" };
                image.texture = texture;
            }

            if (previewCamera == null)
            {
                var camObject = new GameObject("LobbyPreviewCamera");
                previewCamera = camObject.AddComponent<Camera>();
                previewCamera.clearFlags = CameraClearFlags.SolidColor;
                previewCamera.backgroundColor = Color.clear;
                previewCamera.fieldOfView = fieldOfView;
                previewCamera.nearClipPlane = 0.1f;
                previewCamera.farClipPlane = cameraDistance * 3f;
                previewCamera.targetTexture = texture;
                previewCamera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

                Vector3 look = stagePosition + Vector3.up * lookHeight;
                camObject.transform.position = look + Vector3.back * cameraDistance;
                camObject.transform.LookAt(look);
            }

            if (player == null && playerPrefab != null)
            {
                player = Instantiate(playerPrefab, stagePosition, Quaternion.Euler(0f, modelYaw, 0f));
                player.name = "LobbyPlayer";
                // Stand still on the stage; no physics in the lobby.
                if (player.TryGetComponent(out Rigidbody body)) body.isKinematic = true;
                jelly = player.GetComponent<ChefImpactJelly>();
            }
        }
    }
}
