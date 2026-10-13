using ScratchCardAsset;
using UnityEngine;

// The plugin scratches in texture pixels reached from a screen point, while the
// game only ever knows where things are in the world. Everything that carves the
// arena goes through here so that translation lives in one place.
[RequireComponent(typeof(ScratchCardManager))]
public class ArenaScratchCard : MonoBehaviour
{
    [SerializeField] private bool allowPointerScratching;

    [Header("Ground")]
    [Tooltip("The unscratched ground, tiled across the card. A single tile is enough -- it repeats, " +
             "so it needs its Wrap Mode set to Repeat rather than Clamp. Left empty, the map " +
             "generator hands over the tile it laid the field with.")]
    [SerializeField] private Texture groundTexture;

    [Tooltip("How much ground one copy of that texture covers, in world units. Matching the tilemap's " +
             "cell size is what lines the two up.")]
    [SerializeField] private float groundTileWorldSize = 0.96f;

    [Tooltip("Tints the ground, and goes above 1 to brighten it. Every provided tile is the same earth " +
             "colour, so a card left white would uncover ground its own shade and nothing would read as " +
             "scratched. The default lifts the card to the reference's pale sand and leaves the tilemap " +
             "below it as the darker earth a blade uncovers, so neither has to be recoloured by hand.")]
    [ColorUsage(false, true)]
    [SerializeField] private Color groundTint = new Color(1.7f, 1.75f, 1.2f);

    [Header("Canvas")]
    [Tooltip("Scratch resolution: canvas pixels per world unit. Raising it sharpens the edge a blade " +
             "leaves, and costs memory across the whole field.")]
    [SerializeField] private float surfacePixelsPerUnit = 50f;

    [Tooltip("Ceiling on either canvas axis. A field large enough to hit it is covered at lower " +
             "resolution rather than failing to allocate.")]
    [SerializeField] private int maxCanvasSize = 4096;

    [Tooltip("How much ground one brush stamp clears, in world units. Re-solved into the plugin's " +
             "pixel-based brush size whenever the card is fitted.")]
    [SerializeField] private float brushWorldWidth = 1.5f;

    // Blades are spawned mid-run, long after the card is placed, so they look the
    // surface up instead of carrying a reference wired in the inspector.
    public static ArenaScratchCard Instance { get; private set; }

    private static readonly int GroundTextureId = Shader.PropertyToID("_GroundTex");
    private static readonly int GroundTilingId = Shader.PropertyToID("_GroundTiling");
    private static readonly int GroundColorId = Shader.PropertyToID("_GroundColor");

    private ScratchCardManager manager;
    private Renderer surfaceRenderer;
    private Camera cardCamera;

    // Built here rather than imported, so they are ours to clean up.
    private Texture2D canvasTexture;
    private Sprite canvasSprite;

    // The arena the card was last fitted to, or none if nothing has fitted it.
    private Vector3? fittedSize;

    // Said once. The card is re-fitted whenever the arena is rebuilt, and a console
    // filling up with the same line is a console nobody reads.
    private bool warnedAboutGround;

    public bool Ready { get; private set; }
    public ScratchCardManager Manager => manager;

    void Awake()
    {
        Instance = this;
        manager = GetComponent<ScratchCardManager>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ReleaseCanvas();
    }

    void Start()
    {
        if (manager.Card == null)
        {
            Debug.LogError("Scratch Card Manager has no Card assigned, so the arena cannot be scratched.", this);
            return;
        }

        // The manager would set itself up in its own Start, but a blade may be
        // sweeping this very frame. Init() is what that Start calls, and the
        // manager skips its own once this has run.
        if (!manager.Card.Initialized)
        {
            manager.Init();
        }

        // Left alone, the plugin carves wherever the mouse or a finger happens to
        // be. Only what moves through the arena is meant to cut this surface.
        manager.InputEnabled = allowPointerScratching;

        cardCamera = manager.MainCamera != null ? manager.MainCamera : Camera.main;
        surfaceRenderer = manager.Card.SurfaceTransform != null
            ? manager.Card.SurfaceTransform.GetComponent<Renderer>()
            : null;

        Ready = manager.Card.Initialized && cardCamera != null;

        // A generator running in the same Start can beat the card to it, and back
        // then there was no surface material to write the ground onto. Now there is.
        if (fittedSize.HasValue)
        {
            FitGround(fittedSize.Value);
            FitBrush(fittedSize.Value, canvasSprite);
        }
        else
        {
            // Nothing fitted the card, so it is covering whatever the builder laid
            // out. The ground still has to be tiled to the size it actually is.
            FitGround(surfaceRenderer != null ? surfaceRenderer.bounds.size : Vector3.one);
        }
    }

    /// <summary>
    /// Clears a stroke between two world points, widening the brush by brushScale.
    /// </summary>
    public void ScratchLine(Vector3 worldStart, Vector3 worldEnd, float brushScale = 1f, float pressure = 1f)
    {
        if (!TryGetTexturePosition(worldStart, out Vector2 start)) return;
        if (!TryGetTexturePosition(worldEnd, out Vector2 end)) return;

        // Brush width belongs to the card rather than to the caller, so every
        // scratcher restates its own width against the configured base.
        manager.Card.BrushSize = Mathf.Max(0.001f, manager.BrushSize * brushScale);
        manager.Card.ScratchLine(start, end, pressure, pressure);
    }

    /// <summary>
    /// How much ground a single brush stamp clears. The brush is sized in texture
    /// pixels, so its reach depends on how far the card is stretched over the
    /// arena; callers space their strokes by this to avoid leaving stripes.
    /// </summary>
    public float GetBrushWorldSize(float brushScale = 1f)
    {
        if (!Ready || manager.BrushTexture == null || surfaceRenderer == null) return 0f;

        float textureWidth = manager.Card.ScratchData.TextureSize.x;
        if (textureWidth <= 0f) return 0f;

        return manager.BrushTexture.width * manager.BrushSize * brushScale / textureWidth
               * surfaceRenderer.bounds.size.x;
    }

    /// <summary>
    /// Lays the card over a newly generated arena of any size. The canvas is rebuilt
    /// to the arena's own proportions rather than stretched to them, because the
    /// surface sprite is not a picture of the ground -- it is the scratch canvas, and
    /// its pixel dimensions decide both the mask's resolution and the brush's shape.
    /// The ground it draws is tiled from a texture, so that stays put too.
    /// </summary>
    /// <param name="ground">
    /// The earth the field was laid with, used when nothing is wired into Ground
    /// Texture. The card and the tilemap beneath it have to show the same ground, so
    /// the builder simply hands over the tile it used rather than that being wired
    /// up by hand in a scene that may not survive to be saved.
    /// </param>
    public void FitTo(Bounds arena, Texture ground = null)
    {
        // Reachable from an editor tool, where nothing has woken up yet.
        if (manager == null) manager = GetComponent<ScratchCardManager>();
        if (manager == null || manager.Card == null) return;

        if (groundTexture == null && ground != null) groundTexture = ground;

        Transform surface = manager.Card.SurfaceTransform;
        if (surface == null) return;

        SpriteRenderer renderer = surface.GetComponent<SpriteRenderer>();
        if (renderer == null) return;

        if (arena.size.x <= 0f || arena.size.y <= 0f)
        {
            Debug.LogWarning("Asked to cover an arena with no area, so the card is left where it was.", this);
            return;
        }

        transform.position = new Vector3(arena.center.x, arena.center.y, transform.position.z);

        Sprite canvas = BuildCanvas(arena.size);
        if (canvas == null) return;

        // Assigned through the manager rather than onto the renderer, because a
        // canvas of a new size needs the mask render texture rebuilt around it. The
        // property does that itself once the card is running, and before then it is
        // enough that Init picks the sprite up when it gets there.
        manager.ScratchSurfaceSprite = canvas;

        // Whole pixels do not divide the arena exactly, so this is the last of the
        // rounding rather than the stretch it used to be: both axes land within a
        // pixel of 1, which is what keeps a round brush round.
        Vector3 sprite = canvas.bounds.size;
        surface.localScale = new Vector3(arena.size.x / sprite.x, arena.size.y / sprite.y, 1f);

        // The cached renderer is what brush sizes are measured against, and its
        // world size has just changed underneath them.
        surfaceRenderer = renderer;

        fittedSize = arena.size;
        FitBrush(arena.size, canvas);
        FitGround(arena.size);

        // The shadow's reach is stored in UV, so it has to be re-solved against a
        // surface that has just changed size or the band would scale with the field.
        ScratchInnerShadow shadow = GetComponent<ScratchInnerShadow>();
        if (shadow != null) shadow.Rebuild();
    }

    // A solid canvas sized to the arena's own proportions. Nothing samples its
    // contents -- the shader draws the ground from a tiled texture instead -- so it
    // is allocated single-channel and never drawn into.
    //
    // Its dimensions are the whole point. The plugin reads TextureSize off the
    // sprite's rect and steps the brush in texture pixels, so a canvas whose aspect
    // does not match the field's turns a circular scratch into an ellipse.
    private Sprite BuildCanvas(Vector3 arenaSize)
    {
        int width = CanvasAxis(arenaSize.x);
        int height = CanvasAxis(arenaSize.y);

        // The clamp bites the long axis first, which would skew the aspect the
        // canvas exists to preserve, so the short one is brought down to match.
        float aspect = arenaSize.x / arenaSize.y;
        if (width >= maxCanvasSize) height = CanvasSize(width / aspect);
        if (height >= maxCanvasSize) width = CanvasSize(height * aspect);

        if (canvasSprite != null &&
            Mathf.Approximately(canvasSprite.rect.width, width) &&
            Mathf.Approximately(canvasSprite.rect.height, height))
        {
            return canvasSprite;
        }

        ReleaseCanvas();

        canvasTexture = new Texture2D(width, height, TextureFormat.Alpha8, false)
        {
            name = "Arena Scratch Canvas"
        };

        // Filled opaque rather than left as it was allocated. Nothing samples it,
        // but a fresh texture is all zeros, and zero alpha is what several things
        // downstream read as "no sprite here".
        byte[] opaque = new byte[width * height];
        for (int i = 0; i < opaque.Length; i++) opaque[i] = 255;
        canvasTexture.SetPixelData(opaque, 0);
        canvasTexture.Apply(false, false);

        // FullRect, emphatically: the default packs the mesh to the sprite's opaque
        // pixels, which for a canvas that only exists as a rectangle of a certain
        // size is an invitation to get back no rectangle at all.
        canvasSprite = Sprite.Create(canvasTexture, new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f), surfacePixelsPerUnit, 0, SpriteMeshType.FullRect);
        canvasSprite.name = "Arena Scratch Canvas";

        // Nothing reads the sprite's pixels, and saying so up front spares the
        // plugin a full GetPixels of an arena-sized texture every time it is fitted.
        manager.ScratchSurfaceSpriteHasAlpha = false;

        return canvasSprite;
    }

    private int CanvasAxis(float worldSize) => CanvasSize(worldSize * surfacePixelsPerUnit);

    // Compressed formats want whole blocks of four.
    private int CanvasSize(float pixels)
    {
        int rounded = Mathf.RoundToInt(pixels / 4f) * 4;
        return Mathf.Clamp(rounded, 4, maxCanvasSize);
    }

    // The brush is stepped in canvas pixels, so the size that clears a given stretch
    // of ground depends on how many pixels the canvas spends per world unit -- which
    // has just changed. Solved on x; the canvas matches the arena's aspect, so y
    // comes out the same.
    private void FitBrush(Vector3 arenaSize, Sprite canvas)
    {
        if (canvas == null || arenaSize.x <= 0f) return;
        if (manager.BrushTexture == null || manager.BrushTexture.width <= 0) return;

        manager.BrushSize = brushWorldWidth * canvas.rect.width
                            / (manager.BrushTexture.width * arenaSize.x);
    }

    // Tiles rather than stretches, so one copy of the texture always covers the same
    // patch of ground however large the field around it is.
    private void FitGround(Vector3 arenaSize)
    {
        Material material = manager.Card.SurfaceMaterial;
        if (material == null) return;

        if (groundTexture != null)
        {
            material.SetTexture(GroundTextureId, groundTexture);
        }
        else if (!warnedAboutGround)
        {
            // The shader falls back to a white texture, so the card is still there:
            // it is just a flat sheet of tint with no earth drawn on it, which reads
            // as the ground having vanished rather than as a missing reference.
            warnedAboutGround = true;
            Debug.LogWarning("The scratch card has no ground texture, so the arena is covered in flat " +
                             "colour instead of earth. Assign one to Ground Texture, or give Map " +
                             "Generator a ground tile for it to take one from.", this);
        }

        material.SetColor(GroundColorId, groundTint);

        float tile = Mathf.Max(0.0001f, groundTileWorldSize);
        material.SetVector(GroundTilingId, new Vector4(arenaSize.x / tile, arenaSize.y / tile, 0f, 0f));
    }

    // Map Generator's Generate is on a context menu as well as a round start, so
    // this has to survive being called with the game not running, where Destroy
    // does nothing and would leak the canvas it was asked to free.
    private void ReleaseCanvas()
    {
        DestroyObject(canvasSprite);
        DestroyObject(canvasTexture);

        canvasSprite = null;
        canvasTexture = null;
    }

    private static void DestroyObject(Object target)
    {
        if (target == null) return;

        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }

    /// <summary>
    /// Restores the card to its unscratched state, for the start of a new round.
    /// </summary>
    public void ResetCard()
    {
        if (Ready) manager.ClearScratchCard();
    }

    private bool TryGetTexturePosition(Vector3 worldPosition, out Vector2 texturePosition)
    {
        if (!Ready)
        {
            texturePosition = Vector2.zero;
            return false;
        }

        // The card lies flat and square to an orthographic camera, so casting a
        // world point out to the screen and back onto the surface lands on itself.
        texturePosition = manager.Card.ScratchData.GetScratchPosition(cardCamera.WorldToScreenPoint(worldPosition));
        return true;
    }
}
