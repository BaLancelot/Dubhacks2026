using System;
using System.Collections;
using System.Collections.Generic;
using SushiDojo.Effects;
using SushiDojo.Sound;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace SushiDojo.Menu
{
    /// <summary>
    /// Main menu flow:
    /// urgent zoom onto the covered dish (props are dark silhouettes) -> bang, the lights snap on and
    /// it bursts open with the title centered ->
    /// the lid drops back while the camera puts the dish on the left -> buttons slide in on the right ->
    /// the dish opens and closes on every BGM beat.
    /// HOST / JOIN slide the buttons out to the left and bring the lobby / join panel in from the right.
    /// Networking hooks in through the events below and the Lobby / Join panels.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [Header("Scene")]
        [Tooltip("Orthographic. Empty = Camera.main.")]
        [SerializeField] Camera cam;
        [SerializeField] MenuDish dish;
        [Tooltip("Each child slides in from the right, top to bottom.")]
        [SerializeField] RectTransform buttonPanel;
        [SerializeField] CanvasGroup buttonGroup;
        [SerializeField] string gameSceneName = "SampleScene";
        [Tooltip("Lets the click sound play before the scene changes.")]
        [SerializeField, Min(0f)] float startGameDelay = 0.25f;

        [Header("Intro Zoom")]
        [SerializeField, Min(0.01f)] float startSize = 7f;
        [SerializeField, Min(0.01f)] float zoomSize = 1.8f;
        [SerializeField, Min(0f)] float startDelay = 0.2f;
        [SerializeField, Min(0.01f)] float zoomDuration = 0.9f;
        [Tooltip("Where the dish sits on screen during the zoom and the bang. 0 = bottom, 0.5 = middle, 1 = top.")]
        [SerializeField, Range(0f, 1f)] float zoomDishScreenY = 0.4f;
        [Tooltip("0..1 zoom progress. Ease-in keeps it speeding up into the bang.")]
        [SerializeField] AnimationCurve zoomCurve = new(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 3f, 3f));
        [Tooltip("Dish rattle (0..1) over zoom progress.")]
        [SerializeField] AnimationCurve rattleCurve = AnimationCurve.EaseInOut(0f, 0.2f, 1f, 1f);

        [Header("Lights")]
        [Tooltip("Props start as dark silhouettes and light up on the bang.")]
        [SerializeField] bool startDark = true;
        [Tooltip("Multiplied into each prop's color while dark.")]
        [SerializeField] Color darkTint = new(0.05f, 0.05f, 0.08f, 1f);
        [Tooltip("Background sprites: not darkened at the start, dimmed to Background Show Tint on the bang. Empty = the object named Background.")]
        [SerializeField] SpriteRenderer[] keepLit;
        [Tooltip("Background color once the spotlight is on. Darker = the beam stands out more.")]
        [SerializeField] Color backgroundShowTint = new(0.4f, 0.36f, 0.42f, 1f);
        [Tooltip("How fast the lights come on. Small = snap.")]
        [SerializeField, Min(0f)] float lightsOnDuration = 0.06f;
        [SerializeField] SoundCue lightsOnCue;

        [Header("Spotlight Beam")]
        [SerializeField] bool showBeam = true;
        [Tooltip("Adds the beam's color onto what's behind it, like real light. Off = normal see-through blending.")]
        [SerializeField] bool beamAdditive = true;
        [SerializeField] Color beamColor = new(1f, 0.95f, 0.8f, 0.55f);
        [Tooltip("Bottom center of the beam, relative to the dish (world units).")]
        [SerializeField] Vector2 beamOffset = new(0f, -0.35f);
        [SerializeField, Min(0.1f)] float beamHeight = 8f;
        [SerializeField, Min(0.01f)] float beamTopWidth = 0.5f;
        [SerializeField, Min(0.01f)] float beamBottomWidth = 2.2f;
        [Tooltip("Widens the cone's angle by scaling the bottom width. 1.1 = 10% wider.")]
        [SerializeField, Min(0.1f)] float beamSpread = 1.1f;
        [Tooltip("Leans the beam left/right (degrees) around its bottom point.")]
        [SerializeField, Range(-60f, 60f)] float beamTilt;
        [Tooltip("Size of the light pool on the floor under the dish.")]
        [SerializeField] Vector2 beamPoolSize = new(2.6f, 0.7f);
        [SerializeField] string beamSortingLayer = "Default";
        [Tooltip("Cone order: behind the props, in front of the background.")]
        [SerializeField] int beamSortingOrder = -5;
        [Tooltip("Pool order. Same as the table; it sits just in front of the table by depth.")]
        [SerializeField] int beamPoolSortingOrder;
        [Tooltip("Subtle shimmer once on. 0 = steady.")]
        [SerializeField, Range(0f, 0.5f)] float beamBreathe = 0.12f;

        [Header("Bang")]
        [SerializeField] SoundCue bangCue;
        [SerializeField, Min(0f)] float shakeStrength = 0.25f;
        [SerializeField, Min(0.01f)] float shakeDuration = 0.4f;
        [Tooltip("Camera size kicks out by this much on the bang, then settles back.")]
        [SerializeField, Min(0f)] float bangPunch = 0.35f;
        [Tooltip("How long the open dish stays centered before moving aside.")]
        [SerializeField, Min(0f)] float heroHold = 1.2f;

        [Header("Menu Pose")]
        [SerializeField, Min(0.01f)] float menuSize = 2.6f;
        [Tooltip("Where the dish sits on screen. 0 = left edge, 1 = right edge.")]
        [SerializeField, Range(0f, 1f)] float dishScreenX = 0.28f;
        [Tooltip("0 = bottom, 0.5 = middle, 1 = top.")]
        [SerializeField, Range(0f, 1f)] float menuDishScreenY = 0.5f;
        [SerializeField, Min(0.01f)] float moveDuration = 0.6f;
        [SerializeField] AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("The lid drops back onto the dish over this long while the camera moves.")]
        [SerializeField, Min(0.01f)] float introCloseDuration = 0.35f;
        [SerializeField] SoundCue introCloseCue;

        [Header("Menu Backdrop")]
        [Tooltip("When the dish moves aside, the spotlight fades out and the background returns to its normal color over this long.")]
        [SerializeField, Min(0f)] float lightsOffDuration = 0.6f;
        [Tooltip("Background and titles are multiplied by this once the menu shows. Gray = a tone darker; white = original.")]
        [SerializeField] Color menuBackdropTint = new(0.75f, 0.75f, 0.75f, 1f);
        [Tooltip("Big title halves behind the menu. They slide in once the lid has closed: left from the left, right from the right. Empty = none.")]
        [SerializeField] SpriteRenderer titleLeft;
        [SerializeField] SpriteRenderer titleRight;
        [Tooltip("Where each title's center rests on screen. (0,0) = bottom left, (1,1) = top right.")]
        [SerializeField] Vector2 titleLeftViewport = new(0.35f, 0.72f);
        [SerializeField] Vector2 titleRightViewport = new(0.65f, 0.28f);
        [Tooltip("Each title's width as a fraction of the screen width. Above 1 bleeds off the screen edges.")]
        [SerializeField, Range(0.1f, 2f)] float titleWidth = 1f;
        [SerializeField, Min(0.01f)] float titleSlideDuration = 1.6f;
        [Tooltip("The right title starts this much later than the left one.")]
        [SerializeField, Min(0f)] float titleRightDelay = 0.15f;
        [Tooltip("0 = off screen, 1 = in place.")]
        [SerializeField] AnimationCurve titleSlideCurve = new(new Keyframe(0f, 0f, 0f, 3f), new Keyframe(1f, 1f, 0f, 0f));

        [Header("Buttons")]
        [SerializeField] float buttonSlideDistance = 700f;
        [SerializeField, Min(0.01f)] float buttonSlideDuration = 0.35f;
        [SerializeField, Min(0f)] float buttonStagger = 0.08f;
        [Tooltip("0 = off to the right, 1 = in place. Values above 1 overshoot.")]
        [SerializeField] AnimationCurve buttonSlideCurve = new(new Keyframe(0f, 0f), new Keyframe(0.7f, 1.05f), new Keyframe(1f, 1f));

        [Header("Host / Join")]
        [SerializeField] MenuJoinPanel joinPanel;
        [SerializeField] MenuLobbyPanel lobbyPanel;
        [SerializeField] float panelSlideDistance = 900f;
        [SerializeField, Min(0.01f)] float panelSlideDuration = 0.35f;
        [Tooltip("0 = off screen, 1 = in place. Values above 1 overshoot.")]
        [SerializeField] AnimationCurve panelSlideCurve = new(new Keyframe(0f, 0f), new Keyframe(0.7f, 1.05f), new Keyframe(1f, 1f));
        [SerializeField] SoundCue panelSlideCue;

        [Header("Beat Loop")]
        [SerializeField, Min(1f)] float bpm = 136.4f;
        [Tooltip("BGM clip time (seconds) of the first beat. Same as ChefBeatBounce.")]
        [SerializeField] float firstBeatTime = 0.21f;
        [Tooltip("1 = open or close every beat, 2 = every other beat.")]
        [SerializeField, Min(1)] int beatsPerToggle = 1;
        [SerializeField, Min(0.01f)] float beatCloseDuration = 0.06f;
        [SerializeField] SoundCue beatOpenCue;
        [SerializeField] SoundCue beatCloseCue;

        RectTransform[] buttons;
        Vector2[] buttonRestPositions;
        Vector3 camBasePosition;
        float camBaseSize;
        float shakeTimer;
        bool inMenuPose;
        bool looping;
        bool leaving;
        int lastBeat;
        SpriteRenderer[] darkened;
        Color[] litColors;
        SpriteRenderer[] backgrounds;
        Color[] backgroundColors;
        SpotlightBeam beam;
        Vector2 buttonPanelRest;
        RectTransform currentScreen;
        bool switching;
        float titleTimer = -1f;
        bool showTitlesOnClose;
        Color titleLeftColor = Color.white;
        Color titleRightColor = Color.white;

        /// <summary>HOST was pressed. Start hosting, then call Lobby.SetRoomCode / SetOpponentJoined.</summary>
        public event Action HostRequested;
        /// <summary>A room code was entered. Connect, then call EnterGuestLobby() or Join.ShowError().</summary>
        public event Action<string> JoinRequested;
        /// <summary>The host pressed GAME START. If nothing listens, the game scene loads directly.</summary>
        public event Action StartRequested;
        /// <summary>The player backed out of the lobby / join screen. Stop hosting or disconnect.</summary>
        public event Action LeftLobby;

        public bool IsTransitioning => switching || leaving;
        public MenuLobbyPanel Lobby => lobbyPanel;
        public MenuJoinPanel Join => joinPanel;

        void Awake()
        {
            if (cam == null) cam = Camera.main;

            int count = buttonPanel.childCount;
            buttons = new RectTransform[count];
            buttonRestPositions = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                buttons[i] = (RectTransform)buttonPanel.GetChild(i);
                buttonRestPositions[i] = buttons[i].anchoredPosition;
                PlaceButton(i, 0f);
            }
            SetButtonsInteractable(false);
            buttonGroup.alpha = 0f;
            buttonPanelRest = buttonPanel.anchoredPosition;
            currentScreen = buttonPanel;
            HidePanel(joinPanel);
            HidePanel(lobbyPanel);

            // Before Darken() changes them.
            if (titleLeft != null) titleLeftColor = titleLeft.color;
            if (titleRight != null) titleRightColor = titleRight.color;

            if (startDark) Darken();
            if (showBeam) CreateBeam();
            SetTitlesVisible(false);
        }

        void OnEnable()
        {
            dish.OnClosed += HandleDishClosed;
            if (joinPanel != null)
            {
                joinPanel.Submitted += HandleJoinSubmitted;
                joinPanel.BackPressed += BackToMenu;
                joinPanel.DebugJoinSucceeded += EnterGuestLobbyWithHost;
            }
            if (lobbyPanel != null)
            {
                lobbyPanel.StartPressed += HandleStartPressed;
                lobbyPanel.BackPressed += BackToMenu;
            }
        }

        void OnDisable()
        {
            dish.OnClosed -= HandleDishClosed;
            if (joinPanel != null)
            {
                joinPanel.Submitted -= HandleJoinSubmitted;
                joinPanel.BackPressed -= BackToMenu;
                joinPanel.DebugJoinSucceeded -= EnterGuestLobbyWithHost;
            }
            if (lobbyPanel != null)
            {
                lobbyPanel.StartPressed -= HandleStartPressed;
                lobbyPanel.BackPressed -= BackToMenu;
            }
        }

        IEnumerator Start()
        {
            dish.SetClosed();
            camBaseSize = startSize;
            camBasePosition = ZoomCameraPosition();
            if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

            yield return Zoom();
            Bang();
            if (heroHold > 0f) yield return new WaitForSeconds(heroHold);

            showTitlesOnClose = true;
            dish.Close(introCloseDuration);
            StartCoroutine(LightsOff());
            yield return MoveToMenuPose();
            yield return SlideButtonsIn();

            lastBeat = CurrentBeat();
            looping = true;
        }

        void Update()
        {
            if (!looping) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) BackToMenu();

            int beat = CurrentBeat();
            if (beat == lastBeat) return;
            lastBeat = beat;

            if (dish.IsOpen)
            {
                dish.Close(beatCloseDuration);
            }
            else
            {
                dish.Open();
                SoundManager.Play(beatOpenCue);
            }
        }

        void LateUpdate()
        {
            // Keeps the dish on the left when the window is resized.
            if (inMenuPose) camBasePosition = MenuCameraPosition();

            float shake = 0f;
            if (shakeTimer > 0f)
            {
                shakeTimer = Mathf.Max(shakeTimer - Time.deltaTime, 0f);
                shake = shakeTimer / shakeDuration;
                shake *= shake;
            }

            cam.transform.position = camBasePosition + (Vector3)(Random.insideUnitCircle * (shakeStrength * shake));
            cam.orthographicSize = camBaseSize + bangPunch * shake;

            UpdateTitles();
        }

        public void StartGame()
        {
            if (leaving) return;
            StartCoroutine(LoadGame());
        }

        /// <summary>HOST button: open the lobby as host and ask networking to start hosting.</summary>
        public void HostGame()
        {
            if (!HasPanel(lobbyPanel) || !SwitchTo((RectTransform)lobbyPanel.transform)) return;
            lobbyPanel.Open(isHost: true);
            HostRequested?.Invoke();
        }

        /// <summary>JOIN button: open the room code screen.</summary>
        public void JoinGame()
        {
            if (!HasPanel(joinPanel) || !SwitchTo((RectTransform)joinPanel.transform)) return;
            joinPanel.Open();
        }

        /// <summary>Networking calls this once the guest is connected: shows the lobby as guest.</summary>
        public void EnterGuestLobby()
        {
            if (!HasPanel(lobbyPanel) || !SwitchTo((RectTransform)lobbyPanel.transform)) return;
            lobbyPanel.Open(isHost: false);
        }

        /// <summary>Back to the main buttons (BACK button or Esc).</summary>
        public void BackToMenu()
        {
            if (currentScreen == buttonPanel || !SwitchTo(buttonPanel)) return;
            LeftLobby?.Invoke();
        }

        public void OpenOptions()
        {
            Debug.Log($"[{nameof(MainMenu)}] Options menu is not made yet.", this);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        IEnumerator Zoom()
        {
            for (float t = 0f; t < zoomDuration; t += Time.deltaTime)
            {
                float progress = t / zoomDuration;
                camBaseSize = Mathf.LerpUnclamped(startSize, zoomSize, zoomCurve.Evaluate(progress));
                camBasePosition = ZoomCameraPosition();
                dish.Rattle = rattleCurve.Evaluate(progress);
                yield return null;
            }
            camBaseSize = zoomSize;
            camBasePosition = ZoomCameraPosition();
            dish.Rattle = 0f;
        }

        void Bang()
        {
            if (darkened != null) StartCoroutine(LightsOn());
            if (beam != null) beam.TurnOn();
            dish.Burst();
            SoundManager.Play(bangCue);
            shakeTimer = shakeDuration;
        }

        void Darken()
        {
            var lit = new HashSet<SpriteRenderer>();
            if (keepLit != null && keepLit.Length > 0)
            {
                foreach (SpriteRenderer sr in keepLit) if (sr != null) lit.Add(sr);
            }
            else
            {
                GameObject background = GameObject.Find("Background");
                if (background != null && background.TryGetComponent(out SpriteRenderer bg)) lit.Add(bg);
            }
            // Sparkles drive their own color.
            foreach (RevealSparkle sparkle in FindObjectsByType<RevealSparkle>(FindObjectsSortMode.None))
                lit.UnionWith(sparkle.GetComponentsInChildren<SpriteRenderer>(true));

            var targets = new List<SpriteRenderer>();
            foreach (SpriteRenderer sr in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                if (!lit.Contains(sr)) targets.Add(sr);

            backgrounds = new List<SpriteRenderer>(lit).FindAll(sr => sr != null && sr.GetComponentInParent<RevealSparkle>() == null).ToArray();
            backgroundColors = new Color[backgrounds.Length];
            for (int i = 0; i < backgrounds.Length; i++) backgroundColors[i] = backgrounds[i].color;

            darkened = targets.ToArray();
            litColors = new Color[darkened.Length];
            for (int i = 0; i < darkened.Length; i++)
            {
                litColors[i] = darkened[i].color;
                darkened[i].color = DarkColor(litColors[i]);
            }
        }

        void CreateBeam()
        {
            Vector3 bottom = dish.transform.position + (Vector3)beamOffset;
            bottom.z = dish.transform.position.z;
            beam = SpotlightBeam.Create(null, bottom, beamColor, beamHeight, beamTopWidth, beamBottomWidth * beamSpread, beamTilt,
                beamPoolSize, beamSortingLayer, beamSortingOrder, beamPoolSortingOrder, beamBreathe, beamAdditive);
        }

        IEnumerator LightsOn()
        {
            SoundManager.Play(lightsOnCue);
            for (float t = 0f; t < lightsOnDuration; t += Time.deltaTime)
            {
                SetLight(t / lightsOnDuration);
                yield return null;
            }
            SetLight(1f);
            darkened = null;
        }

        void SetLight(float amount)
        {
            for (int i = 0; i < darkened.Length; i++)
                if (darkened[i] != null) darkened[i].color = Color.Lerp(DarkColor(litColors[i]), litColors[i], amount);

            // The background sinks into darkness as the spotlight comes on.
            for (int i = 0; i < backgrounds.Length; i++)
                if (backgrounds[i] != null)
                    backgrounds[i].color = Color.Lerp(backgroundColors[i], Tint(backgroundColors[i], backgroundShowTint), amount);
        }

        /// <summary>The spotlight goes out and the dimmed background comes back to its own color.</summary>
        IEnumerator LightsOff()
        {
            if (beam != null) beam.TurnOff(lightsOffDuration);
            if (backgrounds == null) yield break;

            var from = new Color[backgrounds.Length];
            for (int i = 0; i < backgrounds.Length; i++)
                if (backgrounds[i] != null) from[i] = backgrounds[i].color;

            for (float t = 0f; t < lightsOffDuration; t += Time.deltaTime)
            {
                for (int i = 0; i < backgrounds.Length; i++)
                    if (backgrounds[i] != null) backgrounds[i].color = Color.Lerp(from[i], MenuBackdropColor(i), t / lightsOffDuration);
                yield return null;
            }

            for (int i = 0; i < backgrounds.Length; i++)
                if (backgrounds[i] != null) backgrounds[i].color = MenuBackdropColor(i);
        }

        Color MenuBackdropColor(int backgroundIndex) => Tint(backgroundColors[backgroundIndex], menuBackdropTint);

        void ShowTitles()
        {
            if (titleLeft != null) titleLeft.color = Tint(titleLeftColor, menuBackdropTint);
            if (titleRight != null) titleRight.color = Tint(titleRightColor, menuBackdropTint);
            titleTimer = 0f;
            SetTitlesVisible(true);
            UpdateTitles();
        }

        void SetTitlesVisible(bool visible)
        {
            if (titleLeft != null) titleLeft.enabled = visible;
            if (titleRight != null) titleRight.enabled = visible;
        }

        /// <summary>Keeps the titles fixed on screen (they follow the camera) while they slide in from the sides.</summary>
        void UpdateTitles()
        {
            if (titleTimer < 0f) return;
            titleTimer += Time.deltaTime;

            float leftProgress = titleSlideCurve.Evaluate(Mathf.Clamp01(titleTimer / titleSlideDuration));
            float rightProgress = titleSlideCurve.Evaluate(Mathf.Clamp01((titleTimer - titleRightDelay) / titleSlideDuration));

            // Start fully off screen: center half a title width past the edge.
            float offLeft = -titleWidth * 0.5f;
            float offRight = 1f + titleWidth * 0.5f;
            PlaceTitle(titleLeft, new Vector2(Mathf.LerpUnclamped(offLeft, titleLeftViewport.x, leftProgress), titleLeftViewport.y));
            PlaceTitle(titleRight, new Vector2(Mathf.LerpUnclamped(offRight, titleRightViewport.x, rightProgress), titleRightViewport.y));
        }

        void PlaceTitle(SpriteRenderer title, Vector2 viewport)
        {
            if (title == null || title.sprite == null) return;

            float screenHeight = camBaseSize * 2f;
            float screenWidth = screenHeight * cam.aspect;
            Vector3 position = camBasePosition + new Vector3((viewport.x - 0.5f) * screenWidth, (viewport.y - 0.5f) * screenHeight, 0f);
            position.z = title.transform.position.z;
            title.transform.position = position;
            title.transform.localScale = Vector3.one * (titleWidth * screenWidth / title.sprite.bounds.size.x);
        }

        Color DarkColor(Color lit) => Tint(lit, darkTint);

        static Color Tint(Color color, Color tint) => new(color.r * tint.r, color.g * tint.g, color.b * tint.b, color.a);

        IEnumerator MoveToMenuPose()
        {
            Vector3 fromPosition = camBasePosition;
            float fromSize = camBaseSize;
            for (float t = 0f; t < moveDuration; t += Time.deltaTime)
            {
                float eased = moveCurve.Evaluate(t / moveDuration);
                camBasePosition = Vector3.LerpUnclamped(fromPosition, MenuCameraPosition(), eased);
                camBaseSize = Mathf.LerpUnclamped(fromSize, menuSize, eased);
                yield return null;
            }
            camBaseSize = menuSize;
            inMenuPose = true;
        }

        IEnumerator SlideButtonsIn()
        {
            buttonGroup.alpha = 1f;
            float total = buttonSlideDuration + buttonStagger * (buttons.Length - 1);
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                for (int i = 0; i < buttons.Length; i++)
                    PlaceButton(i, (t - i * buttonStagger) / buttonSlideDuration);
                yield return null;
            }

            for (int i = 0; i < buttons.Length; i++) PlaceButton(i, 1f);
            SetButtonsInteractable(true);
        }

        IEnumerator LoadGame()
        {
            leaving = true;
            looping = false;
            SetButtonsInteractable(false);
            if (startGameDelay > 0f) yield return new WaitForSeconds(startGameDelay);
            SceneManager.LoadScene(gameSceneName);
        }

        void HandleJoinSubmitted(string code)
        {
            if (JoinRequested != null) JoinRequested.Invoke(code);
            else joinPanel.SetStatus("Connecting... (no network yet: press J to fake it)");
        }

        void HandleStartPressed()
        {
            if (StartRequested != null) StartRequested.Invoke();
            else StartGame();
        }

        /// <summary>Debug: fake a successful join. The host is already in the room.</summary>
        void EnterGuestLobbyWithHost()
        {
            EnterGuestLobby();
            lobbyPanel.SetOpponentJoined(true);
        }

        bool HasPanel(Component panel)
        {
            if (panel != null) return true;
            Debug.LogWarning($"[{nameof(MainMenu)}] Join/Lobby panels are missing. Run Tools > Menu > Create Host and Join Panels, then save the scene.", this);
            return false;
        }

        static void HidePanel(Component panel)
        {
            if (panel != null) panel.gameObject.SetActive(false);
        }

        /// <summary>Slides the current screen out and the next one in. False if a switch is already running.</summary>
        bool SwitchTo(RectTransform next)
        {
            if (switching || leaving || next == null || next == currentScreen) return false;
            StartCoroutine(SwitchRoutine(currentScreen, next));
            currentScreen = next;
            return true;
        }

        IEnumerator SwitchRoutine(RectTransform from, RectTransform to)
        {
            switching = true;
            SoundManager.Play(panelSlideCue);

            // Going deeper: the old screen exits left and the new one enters from the right. Going back: the reverse.
            float exitDirection = to != buttonPanel ? -1f : 1f;
            Vector2 fromRest = RestPosition(from);
            Vector2 toRest = RestPosition(to);

            if (from == buttonPanel) SetButtonsInteractable(false);
            SetPanelInteractable(from, false);
            to.gameObject.SetActive(true);
            SetPanelInteractable(to, false);
            SetScreenAlpha(to, 0f);

            for (float t = 0f; t < panelSlideDuration; t += Time.deltaTime)
            {
                float progress = t / panelSlideDuration;
                float eased = panelSlideCurve.Evaluate(progress);
                from.anchoredPosition = fromRest + Vector2.right * (exitDirection * panelSlideDistance * Mathf.Clamp01(eased));
                to.anchoredPosition = toRest + Vector2.right * (-exitDirection * panelSlideDistance * (1f - eased));
                // The leaving screen fades out as it slides away; the arriving one fades in.
                SetScreenAlpha(from, 1f - progress);
                SetScreenAlpha(to, progress);
                yield return null;
            }

            SetScreenAlpha(from, 0f);
            SetScreenAlpha(to, 1f);
            to.anchoredPosition = toRest;
            if (from == buttonPanel)
            {
                from.anchoredPosition = fromRest + Vector2.right * (exitDirection * panelSlideDistance);
            }
            else
            {
                from.anchoredPosition = fromRest;
                from.gameObject.SetActive(false);
            }

            if (to == buttonPanel) SetButtonsInteractable(true);
            SetPanelInteractable(to, true);
            switching = false;
        }

        /// <summary>Where a screen sits when shown. Panels keep the position they were built at.</summary>
        Vector2 RestPosition(RectTransform screen) => screen == buttonPanel ? buttonPanelRest : PanelRest(screen);

        readonly Dictionary<RectTransform, Vector2> panelRests = new();

        Vector2 PanelRest(RectTransform panel)
        {
            if (!panelRests.TryGetValue(panel, out Vector2 rest)) panelRests[panel] = rest = panel.anchoredPosition;
            return rest;
        }

        static void SetScreenAlpha(RectTransform screen, float alpha)
        {
            if (screen.TryGetComponent(out CanvasGroup group)) group.alpha = Mathf.Clamp01(alpha);
        }

        static void SetPanelInteractable(RectTransform screen, bool interactable)
        {
            if (screen.TryGetComponent(out CanvasGroup group))
            {
                group.interactable = interactable;
                group.blocksRaycasts = interactable;
            }
        }

        void HandleDishClosed()
        {
            SoundManager.Play(looping ? beatCloseCue : introCloseCue);

            // The titles come in only once the intro lid has landed.
            if (!showTitlesOnClose) return;
            showTitlesOnClose = false;
            ShowTitles();
        }

        void PlaceButton(int index, float progress)
        {
            float slide = 1f - buttonSlideCurve.Evaluate(Mathf.Clamp01(progress));
            buttons[index].anchoredPosition = buttonRestPositions[index] + Vector2.right * (buttonSlideDistance * slide);
        }

        void SetButtonsInteractable(bool interactable)
        {
            buttonGroup.interactable = interactable;
            buttonGroup.blocksRaycasts = interactable;
        }

        int CurrentBeat()
        {
            float time = SoundManager.TryGetBgmTime(out float bgmTime) ? bgmTime : Time.time;
            return Mathf.FloorToInt((time - firstBeatTime) * bpm / 60f / beatsPerToggle);
        }

        /// <summary>Camera position that keeps the dish at Zoom Dish Screen Y at the current zoom.</summary>
        Vector3 ZoomCameraPosition() => FramedCameraPosition(new Vector2(0.5f, zoomDishScreenY), camBaseSize);

        /// <summary>Camera position that puts the dish at Dish Screen X / Menu Dish Screen Y at the menu zoom.</summary>
        Vector3 MenuCameraPosition() => FramedCameraPosition(new Vector2(dishScreenX, menuDishScreenY), menuSize);

        /// <summary>Camera position that shows the dish at the given viewport point (0..1) with this orthographic size.</summary>
        Vector3 FramedCameraPosition(Vector2 dishViewport, float size)
        {
            Vector3 dishPosition = dish.transform.position;
            Vector2 offset = new((0.5f - dishViewport.x) * 2f * size * cam.aspect, (0.5f - dishViewport.y) * 2f * size);
            return new Vector3(dishPosition.x + offset.x, dishPosition.y + offset.y, cam.transform.position.z);
        }
    }
}
