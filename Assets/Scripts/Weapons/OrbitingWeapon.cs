using UnityEngine;
using UnityEngine.Pool;

public class OrbitingWeapon : MonoBehaviour
{
    [SerializeField] private float spriteRotationOffset = 0f;
    [SerializeField] private int damage = 1;
    [SerializeField] private float knockbackForce = 12f;
    [SerializeField] private float rearrangeSpeed = 180f;
    [SerializeField, Range(0f, 0.95f)] private float maxSlowdownFraction = 0.8f;

    [Header("Spawn")]
    [Tooltip("Seconds the blade takes to reach full size when it is picked up. Zero skips the effect.")]
    [SerializeField] private float spawnScaleDuration = 0.18f;

    [Tooltip("Size over that window, as a fraction of the prefab's own scale. The bump past 1 is what makes it pop rather than merely appear.")]
    [SerializeField] private AnimationCurve spawnScaleCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.65f, 1.15f),
        new Keyframe(1f, 1f));

    private Vector3 baseScale;
    private float spawnElapsed;
    private bool grown;
    private Collider2D hitbox;

    private Transform center;
    private float radius;
    private float currentOffset;
    private float targetOffset;
    private bool offsetInitialized;
    private Rigidbody2D rb;
    private WeaponOrbitController owner;

    private CharacterStats ownerStats;
    private bool clashed;
    private bool retired;
    private IObjectPool<OrbitingWeapon> pool;

    public Transform Center => center;
    public float CurrentOffset => currentOffset;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        hitbox = GetComponent<Collider2D>();


        baseScale = transform.localScale;
        transform.localScale = Vector3.zero;


        if (hitbox != null) hitbox.enabled = false;
    }

    public void Init(Transform orbitCenter, float orbitRadius, WeaponOrbitController orbitOwner)
    {
        center = orbitCenter;
        radius = orbitRadius;
        owner = orbitOwner;
        ownerStats = orbitOwner != null ? orbitOwner.GetComponent<CharacterStats>() : null;
    }

    void OnDestroy()
    {
        if (owner != null) owner.RemoveWeapon(this);
    }


    public void SetPool(IObjectPool<OrbitingWeapon> owningPool)
    {
        pool = owningPool;
    }

    public void Retire()
    {

        if (retired) return;
        retired = true;

        if (owner != null) owner.RemoveWeapon(this);
        owner = null;
        ownerStats = null;
        center = null;

        if (pool != null) pool.Release(this);
        else Destroy(gameObject);
    }


    public void ResetForUse()
    {
        retired = false;
        clashed = false;
        grown = false;
        spawnElapsed = 0f;
        offsetInitialized = false;
        currentOffset = 0f;
        targetOffset = 0f;

        transform.localScale = Vector3.zero;
        if (hitbox != null) hitbox.enabled = false;
        if (rb != null) rb.simulated = true;

        ScratchTrail trail = GetComponent<ScratchTrail>();
        if (trail != null) trail.enabled = true;

        WeaponClash clash = GetComponent<WeaponClash>();
        if (clash != null) clash.Cancel();

        enabled = true;
    }

    public void SetTargetOffset(float targetOffsetAngle)
    {
        targetOffset = targetOffsetAngle;

        if (!offsetInitialized)
        {
            currentOffset = targetOffsetAngle;
            offsetInitialized = true;
        }
    }

    void LateUpdate()
    {
        GrowIn();

        if (center == null || owner == null) return;

        float maxStep = rearrangeSpeed * Time.deltaTime;

        if (Mathf.DeltaAngle(currentOffset, targetOffset) > 0f)
        {
            maxStep = Mathf.Min(maxStep, owner.SpinSpeed * maxSlowdownFraction * Time.deltaTime);
        }

        currentOffset = Mathf.MoveTowardsAngle(currentOffset, targetOffset, maxStep);

        float totalAngle = owner.SpinPhase + currentOffset;
        float rad = totalAngle * Mathf.Deg2Rad;
        Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;

        transform.position = (Vector2)center.position + offset;
        transform.rotation = Quaternion.Euler(0f, 0f, totalAngle + spriteRotationOffset);
    }


    private void GrowIn()
    {
        if (grown) return;

        spawnElapsed += Time.deltaTime;
        float t = spawnScaleDuration > 0f ? Mathf.Clamp01(spawnElapsed / spawnScaleDuration) : 1f;

        if (t < 1f)
        {
            transform.localScale = baseScale * spawnScaleCurve.Evaluate(t);
            return;
        }

        grown = true;
        transform.localScale = baseScale;
        if (hitbox != null) hitbox.enabled = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (clashed) return;

        if (owner != null && other.transform.IsChildOf(owner.transform)) return;

        CharacterStats character = other.GetComponent<CharacterStats>();
        if (character != null)
        {
            character.TakeDamage(damage, transform.position, knockbackForce, ownerStats);
            return;
        }

        OrbitingWeapon otherWeapon = other.GetComponent<OrbitingWeapon>();
        if (otherWeapon != null && !otherWeapon.clashed && otherWeapon.Center != center)
        {
            Clash(otherWeapon);
        }
    }


    private void Clash(OrbitingWeapon other)
    {
        Vector2 here = transform.position;
        Vector2 there = other.transform.position;


        Vector2 axis = here - there;
        axis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Random.insideUnitCircle.normalized;

        WeaponClash mine = GetComponent<WeaponClash>();
        WeaponClash theirs = other.GetComponent<WeaponClash>();

        Detach();
        other.Detach();

        Vector2 contact = (here + there) * 0.5f;
        WeaponClash source = mine != null ? mine : theirs;
        if (source != null)
        {
            source.EmitSparks(contact, axis);
            source.PlayClash(contact);
        }


        if (mine != null) mine.Fly(axis, 1f); else Retire();
        if (theirs != null) theirs.Fly(-axis, -1f); else other.Retire();
    }


    private void Detach()
    {
        clashed = true;


        if (owner != null) owner.RemoveWeapon(this);
        owner = null;
        ownerStats = null;
        center = null;

        if (hitbox != null) hitbox.enabled = false;

        ScratchTrail trail = GetComponent<ScratchTrail>();
        if (trail != null) trail.enabled = false;


        if (rb != null) rb.simulated = false;

        enabled = false;
    }
}
