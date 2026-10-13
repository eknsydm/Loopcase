using UnityEngine;


[RequireComponent(typeof(OrbitingWeapon))]
public class WeaponClash : MonoBehaviour
{
    [Header("Launch")]
    [Tooltip("How hard a blade is thrown away from the point of contact. Travels at this speed the " +
             "whole way out: slowing one down risks it settling in view and never being cleaned up.")]
    [SerializeField] private float launchSpeed = 16f;

    [Tooltip("Degrees a second. The two blades are spun opposite ways, matching the way they were thrown.")]
    [SerializeField] private float launchSpin = 900f;

    [Tooltip("How far past the edge of the view a blade gets before it is dropped, as a fraction of " +
             "the screen. Enough that it is gone rather than clipped away mid-frame.")]
    [SerializeField] private float offScreenMargin = 0.25f;

    [Tooltip("Backstop for a blade that never leaves the view -- a camera travelling with it, say. " +
             "Nothing should outlive this.")]
    [SerializeField] private float maxFlightTime = 8f;

    [Header("Sound")]
    [Tooltip("The clang of two blades meeting. Left empty, the first AudioSource on the blade -- so the " +
             "HitSound hanging off the weapon prefab is found without being wired up.")]
    [SerializeField] private AudioSource hitSound;

    [Tooltip("How far a clash can be from the ear and still be heard at all. 0 means every clash in the " +
             "arena is heard wherever it happens.")]
    [SerializeField] private float hearingRange = 18f;

    [Tooltip("Where the fade starts, as a fraction of that range.")]
    [Range(0f, 1f)]
    [SerializeField] private float fullVolumeFraction = 0.35f;

    [Header("Sparks")]
    [SerializeField] private Material particleMaterial;
    [SerializeField] private int particleCount = 18;
    [SerializeField] private float particleSpeed = 9f;
    [SerializeField] private float particleLifetime = 0.4f;
    [SerializeField] private float particleSize = 0.14f;
    [SerializeField] private Color particleColor = new Color(1f, 0.95f, 0.72f, 1f);
    [SerializeField] private float particleGravity = 1.5f;

    [Tooltip("Half-angle of each spray. Sparks come off square to the blades rather than along them, " +
             "which is what reads as steel meeting steel instead of something exploding.")]
    [SerializeField, Range(0f, 180f)] private float sprayArc = 45f;


    private static ParticleSystem shared;


    private static AudioSource speaker;

    private OrbitingWeapon weapon;
    private Vector2 velocity;
    private float spin;
    private float flightEnds;
    private Camera view;
    private bool flying;

    void Awake()
    {
        weapon = GetComponent<OrbitingWeapon>();


        if (hitSound == null) hitSound = GetComponentInChildren<AudioSource>(true);
    }

    public void Cancel()
    {
        flying = false;
        velocity = Vector2.zero;
        spin = 0f;
    }

    public void Fly(Vector2 direction, float spinSign)
    {
        velocity = direction * launchSpeed;
        spin = launchSpin * spinSign;
        flightEnds = Time.time + maxFlightTime;
        view = Camera.main;
        flying = true;
    }

    public void EmitSparks(Vector2 contact, Vector2 axis)
    {
        ParticleSystem sparks = Sparks();
        if (sparks == null) return;

        Vector2 across = new Vector2(-axis.y, axis.x);
        ParticleSystem.EmitParams particle = new ParticleSystem.EmitParams();

        for (int i = 0; i < particleCount; i++)
        {
            Vector2 side = (i % 2 == 0) ? across : -across;
            float baseAngle = Mathf.Atan2(side.y, side.x) * Mathf.Rad2Deg;
            float angle = (baseAngle + Random.Range(-sprayArc, sprayArc)) * Mathf.Deg2Rad;

            particle.position = contact;
            particle.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                                * particleSpeed * Random.Range(0.45f, 1f);
            particle.startLifetime = particleLifetime * Random.Range(0.6f, 1f);
            particle.startSize = particleSize * Random.Range(0.6f, 1.3f);
            particle.startColor = particleColor;

            sparks.Emit(particle, 1);
        }
    }


    public void PlayClash(Vector2 contact)
    {
        if (hitSound == null) return;

        AudioClip clip = hitSound.clip;
        if (clip == null) return;

        float volume = Earshot.VolumeAt(contact, hearingRange, fullVolumeFraction);
        if (volume <= 0f) return;

        Speaker().PlayOneShot(clip, hitSound.volume * volume);
    }
    private static AudioSource Speaker()
    {
        if (speaker != null) return speaker;

        GameObject holder = new GameObject("Weapon Clash Audio");

        speaker = holder.AddComponent<AudioSource>();
        speaker.playOnAwake = false;

        speaker.spatialBlend = 0f;

        return speaker;
    }

    void Update()
    {
        if (!flying) return;

        transform.position += (Vector3)(velocity * Time.deltaTime);
        transform.Rotate(0f, 0f, spin * Time.deltaTime);

        if (Time.time < flightEnds && !IsOffScreen()) return;


        flying = false;
        if (weapon != null) weapon.Retire(); else Destroy(gameObject);
    }

    private bool IsOffScreen()
    {
        if (view == null) return false;

        Vector3 viewport = view.WorldToViewportPoint(transform.position);
        return viewport.x < -offScreenMargin || viewport.x > 1f + offScreenMargin
            || viewport.y < -offScreenMargin || viewport.y > 1f + offScreenMargin;
    }

    private ParticleSystem Sparks()
    {
        if (shared != null) return shared;

        GameObject holder = new GameObject("Weapon Clash Sparks");

        ParticleSystem ps = holder.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = true; 
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startLifetime = particleLifetime;
        main.startSize = particleSize;
        main.startColor = particleColor;
        main.gravityModifier = particleGravity;
        main.maxParticles = 250;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;

        ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
        fade.enabled = true;
        fade.color = new ParticleSystem.MinMaxGradient(FadeOut());

        ParticleSystem.SizeOverLifetimeModule shrink = ps.sizeOverLifetime;
        shrink.enabled = true;
        shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));

        ParticleSystemRenderer psRenderer = holder.GetComponent<ParticleSystemRenderer>();
        psRenderer.sharedMaterial = particleMaterial;


        SpriteRenderer blade = GetComponentInChildren<SpriteRenderer>();
        if (blade != null)
        {
            psRenderer.sortingLayerID = blade.sortingLayerID;
            psRenderer.sortingOrder = blade.sortingOrder + 1;
        }

        ps.Play();
        shared = ps;
        return shared;
    }

    private static Gradient FadeOut()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f) },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });

        return gradient;
    }
}
