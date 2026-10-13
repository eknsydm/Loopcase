using ScratchCardAsset;
using UnityEngine;

// Puts the reference video's shadow on the arena card: a dark band inside the top
// rim of everything that has been scratched away, so a cleared patch reads as ground
// dug out below the surface rather than a flat hole cut in it.
//
// The drawing itself is all in Loopcase/ScratchCardInnerShadow. This exists for the
// two things a shader cannot do for itself -- getting that shader onto the card
// without anyone having to wire a material up by hand, and keeping the shadow the
// same width on the ground whatever size the arena and its texture happen to be.
//
// Sits on the same object as ArenaScratchCard.
//
// Runs in edit mode as well, because ScratchCardManager does: the card is a card in
// the scene view before anyone presses play, and a shadow you cannot see until the
// game is running is a shadow you cannot dial in against the reference.
[ExecuteAlways]
[RequireComponent(typeof(ScratchCardManager))]
[DisallowMultipleComponent]
public class ScratchInnerShadow : MonoBehaviour
{
    [Tooltip("Left empty, it is looked up by name. Assigning it is what guarantees the shader survives into a build.")]
    [SerializeField] private Shader shader;

    [Header("Shadow")]
    [SerializeField] private Color color = new Color(0.20f, 0.11f, 0.05f, 1f);

    [Tooltip("Opacity of the band at its darkest.")]
    [SerializeField, Range(0f, 1f)] private float strength = 0.9f;

    [Tooltip("How far the shadow reaches down into a hole, in world units. The reference sits at roughly a sixth of a character's width.")]
    [SerializeField] private float distance = 0.15f;

    [Tooltip("Where the light comes from. Straight up matches the reference; the shadow thins to nothing on edges that face this way.")]
    [SerializeField] private Vector2 direction = Vector2.up;

    [Tooltip("Low values give the reference's near-solid band with a short tail. 1 spreads an even gradient across the whole distance.")]
    [SerializeField, Range(0.02f, 1f)] private float softness = 0.35f;

    private const string ShaderName = "Loopcase/ScratchCardInnerShadow";

    private static readonly int ColorId = Shader.PropertyToID("_ShadowColor");
    private static readonly int StrengthId = Shader.PropertyToID("_ShadowStrength");
    private static readonly int DistanceId = Shader.PropertyToID("_ShadowDistance");
    private static readonly int DirectionId = Shader.PropertyToID("_ShadowDir");
    private static readonly int SoftnessId = Shader.PropertyToID("_ShadowSoftness");

    private ScratchCardManager manager;

    // Awake, because ScratchCardManager builds itself a plain masked material in its
    // own Start and only when the card has none. Claiming the slot first is what
    // makes this work without touching the plugin or the card builder.
    void Awake()
    {
        if (!Resolve()) return;

        Claim();
    }

    // Pulled out of Awake because OnValidate can fire before it, and in edit mode
    // can fire without it ever having run on this instance.
    private bool Resolve()
    {
        if (manager == null) manager = GetComponent<ScratchCardManager>();
        if (shader == null) shader = Shader.Find(ShaderName);

        if (shader != null) return true;

        Debug.LogError($"{ShaderName} was not found, so the arena keeps its flat scratch edges. " +
                       "Assign the shader on Scratch Inner Shadow.", this);
        return false;
    }

    void Start()
    {
        // The surface is stretched over the arena by the builder, and the card sizes
        // its mask off that, so the world-to-UV conversion is only trustworthy once
        // everything has had its Awake.
        Apply();
    }

    // Swapping the shader rather than replacing the material keeps whatever the
    // plugin has already put on it -- the surface texture, the atlas offset, and the
    // mask render texture once that exists. Every one of those property names is
    // shared with ScratchCard/Mask, so nothing needs re-binding.
    private void Claim()
    {
        if (manager.Card == null)
        {
            Debug.LogWarning("Scratch Card Manager has no Card, so there is no surface to shade.", this);
            return;
        }

        if (manager.Card.SurfaceMaterial == null)
        {
            manager.Card.SurfaceMaterial = new Material(shader) { name = "Arena Scratch Surface" };
            return;
        }

        if (manager.Card.SurfaceMaterial.shader != shader)
        {
            manager.Card.SurfaceMaterial.shader = shader;
        }
    }

    /// <summary>
    /// Takes the card's material over and pushes every setting onto it, without
    /// depending on Awake having run. The card builder calls this after the manager
    /// has had its turn, so the shader ends up on the material whichever of the two
    /// created it.
    /// </summary>
    public void Rebuild()
    {
        if (!Resolve()) return;

        Claim();
        Apply();
    }

    /// <summary>
    /// Pushes the settings onto the card's material, converting the shadow's reach
    /// from world units into the UV step the shader samples the mask by.
    /// </summary>
    public void Apply()
    {
        if (manager == null || manager.Card == null) return;

        Material material = manager.Card.SurfaceMaterial;
        if (material == null || material.shader != shader) return;

        material.SetColor(ColorId, color);
        material.SetFloat(StrengthId, strength);
        material.SetFloat(SoftnessId, softness);

        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        material.SetVector(DirectionId, new Vector4(dir.x, dir.y, 0f, 0f));

        // The mask is stretched across the surface, so one UV of V is the surface's
        // full world height however many pixels that turned out to be. Measuring it
        // off the renderer means rebuilding the card at a different size does not
        // silently rescale the shadow.
        material.SetFloat(DistanceId, distance / Mathf.Max(SurfaceWorldHeight(), 0.0001f));
    }

    private float SurfaceWorldHeight()
    {
        Transform surface = manager.Card.SurfaceTransform;
        if (surface == null) return 1f;

        Renderer renderer = surface.GetComponent<Renderer>();
        return renderer != null ? renderer.bounds.size.y : 1f;
    }

    // So the band can be dialled in against the reference with it on screen, which
    // is the only way to judge it.
    void OnValidate()
    {
        Rebuild();
    }
}
