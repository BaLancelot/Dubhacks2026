using System;
using UnityEngine;

namespace SushiDojo.Board
{
    /// <summary>
    /// Once thrown it can't be grabbed again. It fades out so that it's gone exactly
    /// <see cref="returnDelay"/> seconds after release, snaps back to its starting pose, then fades in.
    /// Also recovers it right away if it ends up too far from home.
    /// Renderers need a shader with an _Alpha property (e.g. SushiDojo/FakeLit).
    /// </summary>
    [RequireComponent(typeof(ThrowableBody), typeof(Rigidbody))]
    public class ThrowRespawn : MonoBehaviour
    {
        enum State { Home, Thrown, FadingOut, Hidden, FadingIn }

        static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        [Tooltip("Seconds from release until it's reset to its starting pose.")]
        [SerializeField, Min(0f)] float returnDelay = 1.4f;
        [Tooltip("Recover immediately when farther than this from home (lost or fell through).")]
        [SerializeField, Min(0f)] float maxDistanceFromHome = 30f;

        [Header("Fade")]
        [Tooltip("Ends exactly at Return Delay.")]
        [SerializeField, Min(0f)] float fadeOutDuration = 0.3f;
        [Tooltip("Invisible time at home before fading back in.")]
        [SerializeField, Min(0f)] float hiddenDuration = 0.1f;
        [SerializeField, Min(0f)] float fadeInDuration = 0.25f;

        public event Action OnReset;
        public event Action OnReturnedHome;

        ThrowableBody throwable;
        Rigidbody body;
        Renderer[] renderers;
        MaterialPropertyBlock block;
        Vector3 homeLocalPosition;
        Quaternion homeLocalRotation;
        State state = State.Home;
        float stateTimer;

        float FadeOutStart => Mathf.Max(returnDelay - fadeOutDuration, 0f);

        void Awake()
        {
            throwable = GetComponent<ThrowableBody>();
            body = GetComponent<Rigidbody>();
            renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            homeLocalPosition = transform.localPosition;
            homeLocalRotation = transform.localRotation;
        }

        void OnEnable()
        {
            throwable.Released += HandleReleased;
        }

        void OnDisable()
        {
            throwable.Released -= HandleReleased;
        }

        void Update()
        {
            stateTimer += Time.deltaTime;

            if ((state == State.Thrown || state == State.FadingOut) && IsLost())
            {
                EnterHidden();
                return;
            }

            switch (state)
            {
                case State.Thrown: TickThrown(); break;
                case State.FadingOut: TickFadeOut(); break;
                case State.Hidden: TickHidden(); break;
                case State.FadingIn: TickFadeIn(); break;
            }
        }

        void HandleReleased()
        {
            throwable.IsGrabbable = false;
            Enter(State.Thrown);
        }

        void TickThrown()
        {
            if (stateTimer >= FadeOutStart) Enter(State.FadingOut);
        }

        void TickFadeOut()
        {
            SetAlpha(1f - Progress(fadeOutDuration));
            if (stateTimer >= fadeOutDuration) EnterHidden();
        }

        void TickHidden()
        {
            if (stateTimer >= hiddenDuration) Enter(State.FadingIn);
        }

        void TickFadeIn()
        {
            SetAlpha(Progress(fadeInDuration));
            if (stateTimer < fadeInDuration) return;

            SetAlpha(1f);
            body.isKinematic = false;
            throwable.IsGrabbable = true;
            Enter(State.Home);
            OnReturnedHome?.Invoke();
        }

        void EnterHidden()
        {
            SetAlpha(0f);
            ReturnHome();
            Enter(State.Hidden);
            OnReset?.Invoke();
        }

        void ReturnHome()
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;

            Transform parent = transform.parent;
            Vector3 position = parent != null ? parent.TransformPoint(homeLocalPosition) : homeLocalPosition;
            Quaternion rotation = parent != null ? parent.rotation * homeLocalRotation : homeLocalRotation;
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
        }

        bool IsLost()
        {
            Transform parent = transform.parent;
            Vector3 home = parent != null ? parent.TransformPoint(homeLocalPosition) : homeLocalPosition;
            return Vector3.Distance(body.position, home) > maxDistanceFromHome;
        }

        void Enter(State next)
        {
            state = next;
            stateTimer = 0f;
        }

        float Progress(float duration)
        {
            return duration > 0f ? Mathf.Clamp01(stateTimer / duration) : 1f;
        }

        void SetAlpha(float alpha)
        {
            foreach (Renderer target in renderers)
            {
                target.GetPropertyBlock(block);
                block.SetFloat(AlphaId, alpha);
                target.SetPropertyBlock(block);
            }
        }
    }
}
