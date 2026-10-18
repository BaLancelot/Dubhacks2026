using System.Collections.Generic;
using UnityEngine;

// Put this on the Rigidbody root, with the imported model under Visual.
[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
public sealed class ChefImpactJelly : MonoBehaviour
{
    public Transform visual;
    [Range(1f, 10f)] public float frequency = 3.5f;
    [Range(0.05f, 1f)] public float damping = 0.24f;
    [Range(0f, 2f)] public float impactStrength = 0.45f;
    [Range(0.05f, 0.5f)] public float maxSquash = 0.32f;
    public float minimumImpactSpeed = 0.35f;
    struct Bone { public Transform t; public Quaternion rest; public float weight; }
    readonly List<Bone> bones = new List<Bone>();
    Vector3 restScale, lean, leanVelocity;
    float squash, squashVelocity;
    bool ready;

    void Awake()
    {
        if (!visual || visual == transform) { Debug.LogError("Assign a child Visual transform to ChefImpactJelly.", this); enabled = false; return; }
        restScale = visual.localScale;
        foreach (Transform t in visual.GetComponentsInChildren<Transform>(true))
        {
            float w = t.name == "Hat_1" ? .55f : t.name == "Hat_2" ? .8f : t.name == "Hat_3" ? 1.1f : t.name.StartsWith("Hand_") ? .8f : t.name == "Body_2" ? .2f : t.name == "Body_3" ? .25f : 0;
            if (w > 0) bones.Add(new Bone { t=t, rest=t.localRotation, weight=w });
        }
        ready = true;
    }
    void OnCollisionEnter(Collision collision)
    {
        if (collision.contactCount == 0) return;
        Vector3 normal = collision.GetContact(0).normal;
        float speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, normal));
        if (speed >= minimumImpactSpeed) Impact(normal, speed);
    }
    // Also callable by your combat system, CharacterController, or custom hit logic.
    public void Impact(Vector3 worldNormal, float speed)
    {
        if (!ready || !isActiveAndEnabled || speed <= 0 || worldNormal.sqrMagnitude < 0.0001f) return;
        float impulse = Mathf.Clamp(speed * impactStrength, 0, 6);
        squashVelocity = Mathf.Clamp(squashVelocity - impulse, -8, 8);
        Vector3 direction = transform.InverseTransformDirection(worldNormal.normalized);
        Vector3 axis = Vector3.Cross(Vector3.up, direction);
        // A vertical hit still gives the tall hat a small secondary wobble.
        if (axis.sqrMagnitude < .001f) axis = Vector3.forward * .2f;
        leanVelocity = Vector3.ClampMagnitude(leanVelocity + axis * impulse * .7f, 8);
    }
    [ContextMenu("Test impact (Play Mode)")]
    void TestImpact() { if (Application.isPlaying) Impact(transform.up, 5); }
    void LateUpdate()
    {
        if (!ready) return;
        float remaining = Mathf.Min(Time.deltaTime, .1f);
        float omega = 2 * Mathf.PI * frequency;
        while (remaining > 0)
        {
            float dt = Mathf.Min(remaining, 1f / 240f); remaining -= dt;
            squashVelocity += (-omega*omega*squash - 2*damping*omega*squashVelocity)*dt;
            squash += squashVelocity*dt;
            if (Mathf.Abs(squash) > maxSquash) { squash=Mathf.Clamp(squash,-maxSquash,maxSquash); if (squash*squashVelocity > 0) squashVelocity=0; }
            float hatOmega = omega * .68f;
            leanVelocity += (-hatOmega*hatOmega*lean - 2*damping*hatOmega*leanVelocity)*dt;
            lean = Vector3.ClampMagnitude(lean + leanVelocity*dt, .45f);
        }
        float height = 1 + squash;
        float width = 1 / Mathf.Sqrt(height);
        visual.localScale = Vector3.Scale(restScale, new Vector3(width,height,width));
        Vector3 worldAxis = transform.TransformDirection(lean);
        foreach (Bone b in bones)
        {
            Vector3 localAxis = b.t.parent ? b.t.parent.InverseTransformDirection(worldAxis) : worldAxis;
            b.t.localRotation = Quaternion.AngleAxis(localAxis.magnitude * Mathf.Rad2Deg * b.weight, localAxis.normalized) * b.rest;
        }
    }
    void OnDisable()
    {
        if (!ready) return;
        visual.localScale=restScale;
        foreach (Bone b in bones) if (b.t) b.t.localRotation=b.rest;
        squash=squashVelocity=0;lean=leanVelocity=Vector3.zero;
    }
}
