using System;
using SushiDojo.Effects;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SushiDojo.Menu
{
    /// <summary>
    /// The covered dish on the main menu.
    /// Closed: dome only. Open: plate + food + lid hanging in the air (its scene pose).
    /// The intro burst reveals the title (top word, then bottom word) instead of food.
    /// </summary>
    public class MenuDish : MonoBehaviour
    {
        [Header("Renderers")]
        [Tooltip("Squash and stretch is applied to this transform.")]
        [SerializeField] Transform visual;
        [SerializeField] SpriteRenderer coverRenderer;
        [SerializeField] SpriteRenderer plateRenderer;
        [SerializeField] SpriteRenderer foodRenderer;
        [Tooltip("Its scene pose is the open pose.")]
        [SerializeField] SpriteRenderer openLidRenderer;
        [SerializeField] RevealSparkle sparkle;

        [Header("Food")]
        [Tooltip("The first one pops out in the intro. Each later open picks a different one at random.")]
        [SerializeField] Sprite[] foods;
        [Tooltip("Longest side of the food sprite is scaled to this size (local units).")]
        [SerializeField, Min(0.01f)] float foodFitSize = 1.1f;
        [Tooltip("Food scale multiplier during a pop. Should end at 1.")]
        [SerializeField] AnimationCurve foodPopCurve = new(new Keyframe(0f, 0f), new Keyframe(0.6f, 1.25f), new Keyframe(1f, 1f));

        [Header("Title (intro open)")]
        [SerializeField] Sprite titleTop;
        [SerializeField] Sprite titleBottom;
        [Tooltip("Scale of the title sprites (local units per sprite unit).")]
        [SerializeField, Min(0.001f)] float titleScale = 0.16f;
        [Tooltip("Center of the whole title, relative to the food position.")]
        [SerializeField] Vector2 titleOffset = new(0f, 0.35f);
        [Tooltip("Each word's center relative to the title center, in sprite units (matches Title.png).")]
        [SerializeField] Vector2 titleTopPosition = new(-0.46f, 1.445f);
        [SerializeField] Vector2 titleBottomPosition = new(0.99f, -2.245f);
        [Tooltip("The bottom word pops this long after the top word.")]
        [SerializeField, Min(0f)] float titleStagger = 0.18f;
        [SerializeField, Min(0.01f)] float titlePopDuration = 0.55f;
        [Tooltip("Scale and travel from the dish over the pop. Overshoot past 1 for the boing.")]
        [SerializeField] AnimationCurve titlePopCurve = new(
            new Keyframe(0f, 0f), new Keyframe(0.35f, 1.25f), new Keyframe(0.6f, 0.92f), new Keyframe(0.8f, 1.04f), new Keyframe(1f, 1f));
        [Tooltip("Jelly squash/stretch while popping.")]
        [SerializeField, Min(0f)] float titleWobble = 0.18f;

        [Header("Burst (intro open)")]
        [Tooltip("Lid flies to its open pose plus this offset and waits there (off screen) until the dish closes.")]
        [SerializeField] Vector2 burstLidOffset = new(1.5f, 5f);
        [Tooltip("Lid Z rotation at the end of the flight, in degrees.")]
        [SerializeField] float burstLidSpin = -540f;
        [SerializeField, Min(0.01f)] float burstLidDuration = 0.6f;
        [SerializeField] AnimationCurve burstLidCurve = new(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));
        [SerializeField, Min(0.01f)] float burstFoodDuration = 0.5f;
        [Tooltip("How high the food jumps out of the dish (local units).")]
        [SerializeField, Min(0f)] float burstFoodJump = 0.8f;
        [SerializeField, Min(0f)] float burstSquash = 0.3f;

        [Header("Beat Open / Close")]
        [Tooltip("Lid pose on the plate, relative to its open pose. Start of an open, end of a close.")]
        [SerializeField] Vector2 lidClosedOffset = new(-0.15f, -0.55f);
        [SerializeField] float lidClosedAngle = -15f;
        [SerializeField, Min(0.01f)] float lidOpenDuration = 0.12f;
        [Tooltip("0 = closed pose, 1 = open pose. Values above 1 overshoot.")]
        [SerializeField] AnimationCurve lidOpenCurve = new(new Keyframe(0f, 0f), new Keyframe(0.7f, 1.15f), new Keyframe(1f, 1f));
        [Tooltip("0 = where the lid was, 1 = on the plate. Ease-in so it slams down.")]
        [SerializeField] AnimationCurve lidCloseCurve = new(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 2f));
        [SerializeField, Min(0.01f)] float foodPopDuration = 0.18f;
        [SerializeField, Min(0f)] float openSquash = 0.08f;
        [SerializeField, Min(0f)] float closeSquash = 0.12f;
        [SerializeField, Min(0.01f)] float squashDuration = 0.25f;

        [Header("Rattle")]
        [SerializeField] float rattleAngle = 6f;
        [SerializeField] float rattleHop = 0.06f;
        [Tooltip("Radians per second.")]
        [SerializeField] float rattleSpeed = 45f;

        /// <summary>Fired when the lid lands and the dish is closed again.</summary>
        public event Action OnClosed;

        public bool IsOpen { get; private set; }
        /// <summary>0..1. Shakes the closed dome like something inside wants out.</summary>
        public float Rattle { get; set; }

        Vector3 visualBaseScale;
        Vector3 coverRestPosition;
        Quaternion coverRestRotation;
        Vector3 lidRestPosition;
        Quaternion lidRestRotation;
        Vector3 foodRestPosition;
        Vector3 foodBaseScale = Vector3.one;
        int foodIndex;

        SpriteRenderer titleTopRenderer;
        SpriteRenderer titleBottomRenderer;
        bool showingTitle;
        float titleTimer;

        bool lidMoving;
        bool closeOnLand;
        float lidTimer;
        float lidDuration;
        AnimationCurve lidCurve;
        Vector2 lidFromOffset;
        Vector2 lidToOffset;
        Vector2 lidOffset;
        float lidFromAngle;
        float lidToAngle;
        float lidAngle;

        bool foodPopping;
        float foodTimer;
        float foodDuration;
        float foodJump;

        float squashTimer;
        float squashAmount;

        void Awake()
        {
            visualBaseScale = visual.localScale;
            coverRestPosition = coverRenderer.transform.localPosition;
            coverRestRotation = coverRenderer.transform.localRotation;
            lidRestPosition = openLidRenderer.transform.localPosition;
            lidRestRotation = openLidRenderer.transform.localRotation;
            foodRestPosition = foodRenderer.transform.localPosition;
            titleTopRenderer = CreateTitleRenderer("TitleTop", titleTop, 0);
            titleBottomRenderer = CreateTitleRenderer("TitleBottom", titleBottom, 1);
            ShowFood(0);
            SetClosed();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            TickLid(dt);
            TickFood(dt);
            TickTitle(dt);
            TickSquash(dt);
            ApplyRattle();
        }

        public void SetClosed()
        {
            IsOpen = false;
            lidMoving = false;
            closeOnLand = false;
            foodPopping = false;
            showingTitle = false;
            foodRenderer.transform.localPosition = foodRestPosition;
            foodRenderer.transform.localScale = foodBaseScale;
            SetVisible(open: false);
        }

        /// <summary>Intro open: the lid blasts off screen and the title boings out, one word after the other.</summary>
        public void Burst()
        {
            Rattle = 0f;
            showingTitle = titleTopRenderer != null || titleBottomRenderer != null;
            OpenWith(burstLidOffset, burstLidSpin, burstLidDuration, burstLidCurve);
            if (showingTitle) StartTitlePop();
            else StartFoodPop(burstFoodDuration, burstFoodJump);
            Squash(burstSquash);
            if (sparkle != null) sparkle.Play();
        }

        /// <summary>Beat open: the lid pops up to its open pose over the next food.</summary>
        public void Open()
        {
            if (IsOpen) return;

            ShowFood(NextFoodIndex());
            OpenWith(Vector2.zero, 0f, lidOpenDuration, lidOpenCurve);
            StartFoodPop(foodPopDuration, 0f);
            Squash(openSquash);
        }

        /// <summary>The lid drops from wherever it is onto the plate. OnClosed fires when it lands.</summary>
        public void Close(float duration)
        {
            if (!IsOpen) return;

            // Unwind at most half a turn, even after the burst spin.
            float fromAngle = lidClosedAngle + Mathf.DeltaAngle(lidClosedAngle, lidAngle);
            StartLid(lidOffset, fromAngle, lidClosedOffset, lidClosedAngle, duration, lidCloseCurve);
            closeOnLand = true;
        }

        void OpenWith(Vector2 toOffset, float toAngle, float duration, AnimationCurve curve)
        {
            IsOpen = true;
            SetVisible(open: true);
            StartLid(lidClosedOffset, lidClosedAngle, toOffset, toAngle, duration, curve);
            closeOnLand = false;
        }

        void StartLid(Vector2 fromOffset, float fromAngle, Vector2 toOffset, float toAngle, float duration, AnimationCurve curve)
        {
            lidFromOffset = fromOffset;
            lidFromAngle = fromAngle;
            lidToOffset = toOffset;
            lidToAngle = toAngle;
            lidDuration = duration;
            lidCurve = curve;
            lidTimer = 0f;
            lidMoving = true;
            ApplyLid(fromOffset, fromAngle);
        }

        void TickLid(float dt)
        {
            if (!lidMoving) return;

            lidTimer += dt;
            float progress = Progress(lidTimer, lidDuration);
            float t = lidCurve.Evaluate(progress);
            ApplyLid(Vector2.LerpUnclamped(lidFromOffset, lidToOffset, t), Mathf.LerpUnclamped(lidFromAngle, lidToAngle, t));
            if (progress < 1f) return;

            lidMoving = false;
            if (!closeOnLand) return;

            SetClosed();
            Squash(closeSquash);
            OnClosed?.Invoke();
        }

        void ApplyLid(Vector2 offset, float angle)
        {
            lidOffset = offset;
            lidAngle = angle;
            Transform lid = openLidRenderer.transform;
            lid.localPosition = lidRestPosition + (Vector3)offset;
            lid.localRotation = Quaternion.Euler(0f, 0f, angle) * lidRestRotation;
        }

        void StartFoodPop(float duration, float jump)
        {
            foodDuration = duration;
            foodJump = jump;
            foodTimer = 0f;
            foodPopping = true;
            foodRenderer.transform.localScale = Vector3.zero;
        }

        void TickFood(float dt)
        {
            if (!foodPopping) return;

            foodTimer += dt;
            float progress = Progress(foodTimer, foodDuration);
            Transform food = foodRenderer.transform;
            food.localScale = foodBaseScale * foodPopCurve.Evaluate(progress);
            food.localPosition = foodRestPosition + Vector3.up * (foodJump * Mathf.Sin(progress * Mathf.PI));
            if (progress < 1f) return;

            foodPopping = false;
            food.localScale = foodBaseScale;
            food.localPosition = foodRestPosition;
        }

        SpriteRenderer CreateTitleRenderer(string objectName, Sprite sprite, int orderOffset)
        {
            if (sprite == null) return null;

            GameObject go = new(objectName);
            go.transform.SetParent(foodRenderer.transform.parent, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = foodRenderer.sharedMaterial;
            sr.sortingLayerID = foodRenderer.sortingLayerID;
            sr.sortingOrder = foodRenderer.sortingOrder + orderOffset;
            sr.enabled = false;
            return sr;
        }

        void StartTitlePop()
        {
            titleTimer = 0f;
            PlaceTitleWord(titleTopRenderer, titleTopPosition, 0f);
            PlaceTitleWord(titleBottomRenderer, titleBottomPosition, 0f);
        }

        void TickTitle(float dt)
        {
            if (!showingTitle) return;

            titleTimer += dt;
            PlaceTitleWord(titleTopRenderer, titleTopPosition, titleTimer / titlePopDuration);
            PlaceTitleWord(titleBottomRenderer, titleBottomPosition, (titleTimer - titleStagger) / titlePopDuration);
        }

        /// <summary>Springs a word out of the dish to its spot in the title, with a jelly wobble.</summary>
        void PlaceTitleWord(SpriteRenderer word, Vector2 wordPosition, float progress)
        {
            if (word == null) return;

            progress = Mathf.Clamp01(progress);
            float t = titlePopCurve.Evaluate(progress);
            Vector3 target = foodRestPosition + (Vector3)(titleOffset + wordPosition * titleScale);
            float wobble = titleWobble * (1f - progress) * Mathf.Sin(progress * Mathf.PI * 4f);

            word.transform.localPosition = Vector3.LerpUnclamped(foodRestPosition, target, t);
            word.transform.localScale = new Vector3(1f + wobble, 1f - wobble, 1f) * (titleScale * t);
        }

        void Squash(float amount)
        {
            squashAmount = amount;
            squashTimer = squashDuration;
        }

        void TickSquash(float dt)
        {
            if (squashTimer <= 0f) return;

            squashTimer = Mathf.Max(squashTimer - dt, 0f);
            float progress = 1f - squashTimer / squashDuration;
            float s = squashAmount * (1f - progress) * Mathf.Cos(progress * Mathf.PI * 3f);
            visual.localScale = Vector3.Scale(visualBaseScale, new Vector3(1f + s, 1f - s, 1f));
        }

        void ApplyRattle()
        {
            Transform cover = coverRenderer.transform;
            if (Rattle <= 0f)
            {
                cover.localPosition = coverRestPosition;
                cover.localRotation = coverRestRotation;
                return;
            }

            float phase = Time.time * rattleSpeed;
            cover.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase) * rattleAngle * Rattle) * coverRestRotation;
            cover.localPosition = coverRestPosition + Vector3.up * (Mathf.Abs(Mathf.Sin(phase * 0.5f)) * rattleHop * Rattle);
        }

        void SetVisible(bool open)
        {
            coverRenderer.enabled = !open;
            plateRenderer.enabled = open;
            foodRenderer.enabled = open && !showingTitle;
            openLidRenderer.enabled = open;
            if (titleTopRenderer != null) titleTopRenderer.enabled = open && showingTitle;
            if (titleBottomRenderer != null) titleBottomRenderer.enabled = open && showingTitle;
        }

        int NextFoodIndex()
        {
            if (foods == null || foods.Length < 2) return 0;

            int next = Random.Range(0, foods.Length - 1);
            return next >= foodIndex ? next + 1 : next;
        }

        void ShowFood(int index)
        {
            if (foods == null || foods.Length == 0) return;

            foodIndex = index;
            foodRenderer.sprite = foods[index];
            FitFoodToSize();
        }

        void FitFoodToSize()
        {
            Sprite sprite = foodRenderer.sprite;
            if (sprite == null) return;

            Vector2 size = sprite.bounds.size;
            float longestSide = Mathf.Max(size.x, size.y);
            if (longestSide <= 0f) return;

            foodBaseScale = Vector3.one * (foodFitSize / longestSide);
            foodRenderer.transform.localScale = foodBaseScale;
        }

        static float Progress(float elapsed, float duration)
        {
            return duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
        }
    }
}
