using System;
using System.Collections.Generic;
using UnityEngine;

namespace SushiDojo.Board
{
    /// <summary>
    /// Once thrown it can't be grabbed again. It fades out so that it's gone exactly
    /// <see cref="returnDelay"/> seconds after release, snaps back to its starting pose, then fades in.
    /// Passengers (e.g. food on the board) fade and reset together with it.
    /// Also recovers it right away if it ends up too far from home.
    /// Mesh renderers need a shader with an _Alpha property (e.g. SushiDojo/FakeLit);
    /// sprite renderers fade through their color.
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
        [Tooltip("Fade together with this body. Ones still on the board come back with it; ones that flew off disappear. " +
                 "Keep them under the same table frame, not under this body.")]
        [SerializeField] Rigidbody[] passengers;
        [Tooltip("A passenger counts as on the board up to this height above its top (board local Y).")]
        [SerializeField, Min(0f)] float onBoardHeight = 1f;

        [Header("Fade")]
        [Tooltip("Ends exactly at Return Delay.")]
        [SerializeField, Min(0f)] float fadeOutDuration = 0.3f;
        [Tooltip("Invisible time at home before fading back in.")]
        [SerializeField, Min(0f)] float hiddenDuration = 0.1f;
        [SerializeField, Min(0f)] float fadeInDuration = 0.25f;

        public event Action OnReset;
        public event Action OnReturnedHome;

        ThrowableBody throwable;
        BoxCollider boardBounds;
        Rigidbody[] bodies;
        Vector3[] homeLocalPositions;
        Quaternion[] homeLocalRotations;
        Renderer[] renderers;
        Color[] spriteBaseColors;
        SpriteBillboard[] billboards;
        MaterialPropertyBlock block;
        State state = State.Home;
        float stateTimer;

        float FadeOutStart => Mathf.Max(returnDelay - fadeOutDuration, 0f);
        Rigidbody Self => bodies[0];

        void Awake()
        {
            throwable = GetComponent<ThrowableBody>();
            boardBounds = GetComponent<BoxCollider>();
            block = new MaterialPropertyBlock();

            var bodyList = new List<Rigidbody> { GetComponent<Rigidbody>() };
            foreach (Rigidbody passenger in passengers)
                if (passenger != null) bodyList.Add(passenger);
            bodies = bodyList.ToArray();

            homeLocalPositions = new Vector3[bodies.Length];
            homeLocalRotations = new Quaternion[bodies.Length];
            var rendererList = new List<Renderer>();
            var billboardList = new List<SpriteBillboard>();
            for (int i = 0; i < bodies.Length; i++)
            {
                homeLocalPositions[i] = bodies[i].transform.localPosition;
                homeLocalRotations[i] = bodies[i].transform.localRotation;
                rendererList.AddRange(bodies[i].GetComponentsInChildren<Renderer>());
                billboardList.AddRange(bodies[i].GetComponentsInChildren<SpriteBillboard>());
            }

            renderers = rendererList.ToArray();
            billboards = billboardList.ToArray();
            spriteBaseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] is SpriteRenderer sprite) spriteBaseColors[i] = sprite.color;
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
            foreach (Rigidbody target in bodies)
                if (target.gameObject.activeSelf) target.isKinematic = false;
            throwable.IsGrabbable = true;
            Enter(State.Home);
            OnReturnedHome?.Invoke();
        }

        void EnterHidden()
        {
            SetAlpha(0f);

            // Decide before the board moves: passengers that left the board are gone.
            for (int i = 1; i < bodies.Length; i++)
                if (bodies[i].gameObject.activeSelf && !IsOnBoard(bodies[i]))
                    bodies[i].gameObject.SetActive(false);

            for (int i = 0; i < bodies.Length; i++)
                if (bodies[i].gameObject.activeSelf) ReturnHome(i);
            foreach (SpriteBillboard billboard in billboards) billboard.ResetRoll();
            Enter(State.Hidden);
            OnReset?.Invoke();
        }

        void ReturnHome(int index)
        {
            Rigidbody target = bodies[index];
            target.linearVelocity = Vector3.zero;
            target.angularVelocity = Vector3.zero;
            target.isKinematic = true;

            Transform parent = target.transform.parent;
            Vector3 position = parent != null ? parent.TransformPoint(homeLocalPositions[index]) : homeLocalPositions[index];
            Quaternion rotation = parent != null ? parent.rotation * homeLocalRotations[index] : homeLocalRotations[index];
            target.position = position;
            target.rotation = rotation;
            target.transform.SetPositionAndRotation(position, rotation);
        }

        bool IsOnBoard(Rigidbody passenger)
        {
            if (boardBounds == null) return true;

            Vector3 local = transform.InverseTransformPoint(passenger.position) - boardBounds.center;
            Vector3 half = boardBounds.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x
                && Mathf.Abs(local.z) <= half.z
                && local.y >= 0f
                && local.y <= half.y + onBoardHeight;
        }

        bool IsLost()
        {
            Transform parent = transform.parent;
            Vector3 home = parent != null ? parent.TransformPoint(homeLocalPositions[0]) : homeLocalPositions[0];
            return Vector3.Distance(Self.position, home) > maxDistanceFromHome;
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
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is SpriteRenderer sprite)
                {
                    Color color = spriteBaseColors[i];
                    color.a *= alpha;
                    sprite.color = color;
                    continue;
                }

                renderers[i].GetPropertyBlock(block);
                block.SetFloat(AlphaId, alpha);
                renderers[i].SetPropertyBlock(block);
            }
        }
    }
}
