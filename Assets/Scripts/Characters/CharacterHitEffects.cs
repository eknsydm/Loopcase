using UnityEngine;


[RequireComponent(typeof(CharacterStats))]
public class CharacterHitEffects : MonoBehaviour
{
    [Header("Impact frame")]
    [Tooltip("Material on the Loopcase/2D/Sprite-Flash shader. Left empty, the flash is skipped and the sparks still play.")]
    [SerializeField] private Material flashMaterial;

    [Tooltip("Seconds the body stays white. Two or three frames reads as a hit; much longer reads as a bug.")]
    [SerializeField] private float flashDuration = 0.07f;

    [Header("Sparks")]
    [SerializeField] private Material particleMaterial;
    [SerializeField] private int particleCount = 12;
    [SerializeField] private float particleSpeed = 7f;
    [SerializeField] private float particleLifetime = 0.35f;
    [SerializeField] private float particleSize = 0.15f;
    [SerializeField] private Color particleColor = Color.white;
    [SerializeField] private float particleGravity = 1.2f;

    [Tooltip("Half-angle of the spray around the direction of the blow. 180 throws sparks in every direction.")]
    [SerializeField, Range(0f, 180f)] private float sprayArc = 55f;

    [Tooltip("How far back toward the attacker the burst starts, so sparks come off the surface rather than the pivot.")]
    [SerializeField] private float contactOffset = 0.35f;

    private CharacterStats stats;
    private SpriteRenderer[] bodyRenderers;
    private Material[] restMaterials;
    private ParticleSystem sparks;
    private float flashUntil;
    private bool flashing;

    void Awake()
    {
        stats = GetComponent<CharacterStats>();


        bodyRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        restMaterials = new Material[bodyRenderers.Length];
        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            restMaterials[i] = bodyRenderers[i].sharedMaterial;
        }

        sparks = BuildSparks();
    }

    void OnEnable()
    {
        stats.Hit += OnHit;
    }

    void OnDisable()
    {
        stats.Hit -= OnHit;


        SetFlash(false);
    }

    void Update()
    {
        if (flashing && Time.time >= flashUntil) SetFlash(false);
    }

    void OnDestroy()
    {
        if (sparks != null) Destroy(sparks.gameObject);
    }

    private void OnHit(Vector2 sourcePosition)
    {
        SetFlash(true);
        flashUntil = Time.time + flashDuration;

        Vector2 away = (Vector2)transform.position - sourcePosition;


        away = away.sqrMagnitude > 0.0001f ? away.normalized : Random.insideUnitCircle.normalized;

        Burst((Vector2)transform.position - away * contactOffset, away);
    }

    private void SetFlash(bool on)
    {
        if (flashMaterial == null || flashing == on) return;

        flashing = on;
        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] == null) continue;

            bodyRenderers[i].sharedMaterial = on ? flashMaterial : restMaterials[i];
        }
    }

    private void Burst(Vector2 origin, Vector2 direction)
    {
        if (sparks == null) return;

        float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        ParticleSystem.EmitParams particle = new ParticleSystem.EmitParams();

        for (int i = 0; i < particleCount; i++)
        {
            float angle = (baseAngle + Random.Range(-sprayArc, sprayArc)) * Mathf.Deg2Rad;
            Vector2 heading = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            particle.position = origin;
            particle.velocity = heading * particleSpeed * Random.Range(0.45f, 1f);
            particle.startLifetime = particleLifetime * Random.Range(0.6f, 1f);
            particle.startSize = particleSize * Random.Range(0.6f, 1.3f);
            particle.startColor = particleColor;

            sparks.Emit(particle, 1);
        }
    }


    private ParticleSystem BuildSparks()
    {

        GameObject holder = new GameObject($"{name} HitSparks");

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


        if (bodyRenderers.Length > 0 && bodyRenderers[0] != null)
        {
            psRenderer.sortingLayerID = bodyRenderers[0].sortingLayerID;
            psRenderer.sortingOrder = bodyRenderers[0].sortingOrder + 1;
        }

        ps.Play();
        return ps;
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
