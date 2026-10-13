using System.Collections.Generic;
using System.IO;
using ScratchCardAsset;
using ScratchCardAsset.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// The plugin's own menu drops in a small demo card. This builds one sized to the
// play area instead: pointed at the game camera, sorted under the characters, and
// backed by a surface texture whose aspect matches the arena so that a round brush
// still clears a round patch of ground.
public static class ArenaScratchCardBuilder
{
    private const string SurfaceAssetPath = "Assets/ScratchCard/Generated/ArenaScratchSurface.png";

    private const string InnerShadowShaderPath = "Assets/Shaders/ScratchCardInnerShadow.shader";
    private const string InnerShadowShaderName = "Loopcase/ScratchCardInnerShadow";

    private const float SurfacePixelsPerUnit = 50f; // scratch resolution across the arena
    private const float SpritePixelsPerUnit = 100f; // matches the rest of the project's art
    private const int MaxSurfaceSize = 4096;

    // The card is one stretched sprite, so a field has to be baked rather than
    // tiled by the material. One tile covers one grid cell, which keeps the grass
    // at the same scale as the dirt it is mown down to.
    private const float SurfaceTileWorldSize = 0.96f;

    private static readonly string[] SurfaceTilePaths =
    {
        "Assets/Sprites/tile/tile-grass-2.png",
        "Assets/Sprites/tile/tile-grass-9.png"
    };

    // Two ways to dress the ground, both baked down into the same surface texture.
    // The tileset is the project's own 96 pixel art recoloured green; the grass
    // texture is a single drawing repeated across the arena. Switch this to change
    // what the card looks like before it is scratched.
    private enum SurfaceSource { RecolouredTileset, GrassTexture }
    private static readonly SurfaceSource Surface = SurfaceSource.GrassTexture;

    // A drawing wide enough to read as a field rather than as one repeated tuft,
    // so it covers more ground per copy than a tile does. Larger shows more of the
    // drawing at once; smaller repeats sooner and reads as finer blades.
    private const string GrassTexturePath = "Assets/Sprites/Cartoon_green_texture_grass.jpg";
    private const float GrassTextureWorldSize = 8f;

    // That tileset is a brown earth palette. Remapping its brightness onto a green
    // ramp keeps the hand-drawn tufts and outlines but turns the ground into grass,
    // so the untinted dirt underneath is what a blade uncovers. Set this false to
    // tile the art in its own colours instead.
    private static readonly bool RecolourSurface = true;
    private static readonly Color SurfaceShadow = new Color(0.20f, 0.35f, 0.14f);
    private static readonly Color SurfaceHighlight = new Color(0.55f, 0.78f, 0.30f);
    private const float ArenaPadding = 2f;
    private const float BrushWorldWidth = 1.5f; // ground cleared by one brush stamp
    private const int SortingOrder = -1; // above the ground, below the characters

    [MenuItem("Tools/Loopcase/Build Arena Scratch Card")]
    private static void Build()
    {
        ArenaScratchCard existing = Object.FindAnyObjectByType<ArenaScratchCard>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.LogWarning("The scene already has an arena scratch card. Delete it first to rebuild.", existing);
            return;
        }

        Bounds arena = MeasureArena();
        Sprite surface = CreateSurfaceSprite(arena.size);
        if (surface == null) return;

        // Built inactive: the manager reaches for its card the moment it wakes, so
        // nothing may run until every reference is in place.
        GameObject root = new GameObject("Arena Scratch Card");
        root.SetActive(false);
        root.transform.position = new Vector3(arena.center.x, arena.center.y, 0f);

        GameObject cardObject = new GameObject("Card");
        cardObject.transform.SetParent(root.transform, false);

        GameObject surfaceObject = new GameObject("Surface");
        surfaceObject.transform.SetParent(cardObject.transform, false);

        SpriteRenderer surfaceRenderer = surfaceObject.AddComponent<SpriteRenderer>();
        surfaceRenderer.sprite = surface;
        surfaceRenderer.sortingOrder = SortingOrder;

        Vector3 spriteSize = surface.bounds.size;
        surfaceObject.transform.localScale = new Vector3(
            arena.size.x / spriteSize.x, arena.size.y / spriteSize.y, 1f);

        ScratchCard card = cardObject.AddComponent<ScratchCard>();
        card.SurfaceTransform = surfaceObject.transform;
        card.RenderTextureQuality = Quality.High;

        ScratchCardManager manager = root.AddComponent<ScratchCardManager>();
        ConfigureManager(manager, card, surfaceRenderer, surface, arena.size.x);
        card.BrushSize = manager.BrushSize;

        ArenaScratchCard arenaCard = root.AddComponent<ArenaScratchCard>();
        AssignGroundTexture(arenaCard);
        ScratchInnerShadow shadow = root.AddComponent<ScratchInnerShadow>();
        AssignInnerShadowShader(shadow);
        root.SetActive(true);

        // Builds the masked material now rather than at the next domain reload, so
        // the card reads as a card in the scene view straight away.
        manager.InitSurfaceMaterial();

        // After the manager, so whichever of the two ended up creating the material
        // gets the shadow shader put on it. Called rather than left to the
        // component's own Awake, because that has already run by now.
        shadow.Rebuild();

        Undo.RegisterCreatedObjectUndo(root, "Build Arena Scratch Card");
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;

        Debug.Log($"Arena scratch card covers {arena.size.x:0.#} x {arena.size.y:0.#} units at " +
                  $"{surface.rect.width} x {surface.rect.height} px. Anything carrying a ScratchTrail now cuts it.", root);
    }

    // For a card that is already in the scene. Build only ever runs once, and it
    // refuses to touch an existing card, so without this the only way to get the
    // shadow onto one is to delete it and lay it out again.
    [MenuItem("Tools/Loopcase/Add Scratch Inner Shadow")]
    private static void AddInnerShadow()
    {
        ArenaScratchCard arenaCard = Object.FindAnyObjectByType<ArenaScratchCard>();
        if (arenaCard == null)
        {
            Debug.LogWarning("No arena scratch card in the scene to shade. " +
                             "Build one with Tools > Loopcase > Build Arena Scratch Card first.");
            return;
        }

        ScratchInnerShadow shadow = arenaCard.GetComponent<ScratchInnerShadow>();
        if (shadow == null)
        {
            shadow = Undo.AddComponent<ScratchInnerShadow>(arenaCard.gameObject);
        }
        else
        {
            Undo.RecordObject(shadow, "Add Scratch Inner Shadow");
        }

        AssignInnerShadowShader(shadow);

        // A card built before the ground was tiled has nothing in that field, and
        // the shader draws white without it.
        Undo.RecordObject(arenaCard, "Add Scratch Inner Shadow");
        AssignGroundTexture(arenaCard);

        shadow.Rebuild();

        EditorSceneManager.MarkSceneDirty(arenaCard.gameObject.scene);
        Selection.activeGameObject = arenaCard.gameObject;

        Debug.Log("Scratched ground now takes a shadow along its upper edges. " +
                  "Tune the band on the Scratch Inner Shadow component.", arenaCard);
    }

    // Re-bakes the surface and drops it onto the card already in the scene, so that
    // trying another texture does not cost the card its place, its camera, or the
    // brush size solved for the arena.
    [MenuItem("Tools/Loopcase/Rebuild Arena Scratch Surface")]
    private static void RebuildSurface()
    {
        ArenaScratchCard arenaCard = Object.FindAnyObjectByType<ArenaScratchCard>();
        if (arenaCard == null)
        {
            Debug.LogWarning("No arena scratch card in the scene to redress. Build one first.");
            return;
        }

        ScratchCardManager manager = arenaCard.GetComponent<ScratchCardManager>();
        if (manager == null || manager.Card == null || manager.Card.SurfaceTransform == null)
        {
            Debug.LogError("The arena scratch card is missing its manager or its surface.", arenaCard);
            return;
        }

        SpriteRenderer surfaceRenderer = manager.Card.SurfaceTransform.GetComponent<SpriteRenderer>();
        if (surfaceRenderer == null)
        {
            Debug.LogError("The card's surface has no Sprite Renderer to redress.", arenaCard);
            return;
        }

        Bounds arena = MeasureArena();
        Sprite surface = CreateSurfaceSprite(arena.size);
        if (surface == null) return;

        Undo.RecordObject(manager, "Rebuild Arena Scratch Surface");
        Undo.RecordObject(surfaceRenderer, "Rebuild Arena Scratch Surface");
        Undo.RecordObject(surfaceRenderer.transform, "Rebuild Arena Scratch Surface");
        FitSurface(manager, surfaceRenderer, surface, arena);

        // The mask material still holds the old sprite, so rebuilding it is what
        // actually puts the new ground on screen.
        manager.InitSurfaceMaterial();

        // A redress can land the card on a differently sized arena, and the shadow's
        // reach is stored in UV, so it has to be re-solved against the new surface
        // or the band would scale with the ground instead of staying put.
        ScratchInnerShadow shadow = arenaCard.GetComponent<ScratchInnerShadow>();
        if (shadow != null)
        {
            Undo.RecordObject(shadow, "Rebuild Arena Scratch Surface");
            AssignInnerShadowShader(shadow);
            shadow.Rebuild();
        }

        EditorSceneManager.MarkSceneDirty(arenaCard.gameObject.scene);
        Selection.activeGameObject = arenaCard.gameObject;

        Debug.Log($"Arena scratch surface rebaked from {Surface} at " +
                  $"{surface.rect.width} x {surface.rect.height} px.", arenaCard);
    }

    // A rebaked sprite carries its own pixel size, so the surface has to be
    // stretched back over the arena and the brush re-solved against it.
    private static void FitSurface(ScratchCardManager manager, SpriteRenderer surfaceRenderer,
        Sprite surface, Bounds arena)
    {
        surfaceRenderer.sprite = surface;

        Vector3 spriteSize = surface.bounds.size;
        surfaceRenderer.transform.localScale = new Vector3(
            arena.size.x / spriteSize.x, arena.size.y / spriteSize.y, 1f);

        SerializedObject serialized = new SerializedObject(manager);
        serialized.FindProperty("scratchSurfaceSprite").objectReferenceValue = surface;
        serialized.FindProperty("brushSize").floatValue =
            BrushSizeFor(manager.BrushTexture, surface, arena.size.x);
        serialized.ApplyModifiedProperties();

        manager.Card.BrushSize = manager.BrushSize;
    }

    // Every serialized field is written before the object wakes. The manager's own
    // property setters rebuild materials as a side effect, which a half-built card
    // cannot survive, and the shader references have no setters at all.
    private static void ConfigureManager(ScratchCardManager manager, ScratchCard card,
        SpriteRenderer surfaceRenderer, Sprite surface, float arenaWidth)
    {
        Texture brush = LoadBrushTexture();
        Camera camera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        if (camera == null)
        {
            Debug.LogWarning("No camera found; assign one to the Scratch Card Manager before playing.");
        }

        SerializedObject serialized = new SerializedObject(manager);
        serialized.FindProperty("Card").objectReferenceValue = card;
        serialized.FindProperty("RenderType").enumValueIndex = (int)ScratchCardRenderType.SpriteRenderer;
        serialized.FindProperty("spriteRendererCard").objectReferenceValue = surfaceRenderer;
        serialized.FindProperty("mainCamera").objectReferenceValue = camera;
        serialized.FindProperty("scratchSurfaceSprite").objectReferenceValue = surface;

        // The surface is fully opaque, which spares the texture a Read/Write flag
        // and stops the plugin from sampling it back off the GPU every scratch.
        serialized.FindProperty("scratchSurfaceSpriteHasAlpha").boolValue = false;

        serialized.FindProperty("brushTexture").objectReferenceValue = brush;
        serialized.FindProperty("brushSize").floatValue = BrushSizeFor(brush, surface, arenaWidth);
        serialized.FindProperty("brushOpacity").floatValue = 1f;

        // Pointer input would carve the card from wherever the mouse sits.
        serialized.FindProperty("inputEnabled").boolValue = false;
        serialized.FindProperty("checkCanvasRaycasts").boolValue = false;

        serialized.FindProperty("maskShader").objectReferenceValue = Shader.Find("ScratchCard/Mask");
        serialized.FindProperty("brushShader").objectReferenceValue = Shader.Find("ScratchCard/Brush");
        serialized.FindProperty("maskProgressShader").objectReferenceValue = Shader.Find("ScratchCard/MaskProgress");
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // The brush is stamped in texture pixels, so its size has to be solved back
    // from how wide a patch of ground it should clear.
    private static float BrushSizeFor(Texture brush, Sprite surface, float arenaWidth)
    {
        if (brush == null || brush.width <= 0 || arenaWidth <= 0f) return 1f;
        return BrushWorldWidth * surface.rect.width / (brush.width * arenaWidth);
    }

    // The ground is tiled by the shader now rather than baked into the surface, so
    // the card needs the tile itself. Its Wrap Mode has to be Repeat or the tiling
    // clamps to one stretched copy, which is the very thing this replaced.
    private static void AssignGroundTexture(ArenaScratchCard arenaCard)
    {
        Texture2D ground = null;
        foreach (string path in SurfaceTilePaths)
        {
            ground = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (ground != null) break;
        }

        if (ground == null)
        {
            Debug.LogWarning("No ground tile found, so the card draws plain white until one is " +
                             "assigned to Arena Scratch Card. Check the paths in SurfaceTilePaths.");
            return;
        }

        SetWrapModeRepeat(ground);

        SerializedObject serialized = new SerializedObject(arenaCard);
        serialized.FindProperty("groundTexture").objectReferenceValue = ground;
        serialized.FindProperty("groundTileWorldSize").floatValue = SurfaceTileWorldSize;
        serialized.FindProperty("surfacePixelsPerUnit").floatValue = SurfacePixelsPerUnit;
        serialized.FindProperty("brushWorldWidth").floatValue = BrushWorldWidth;
        serialized.ApplyModifiedProperties();
    }

    private static void SetWrapModeRepeat(Texture2D texture)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer == null || importer.wrapMode == TextureWrapMode.Repeat) return;

        importer.wrapMode = TextureWrapMode.Repeat;
        importer.SaveAndReimport();
    }

    // Written through SerializedObject so the reference is saved with the scene.
    // Serializing it matters beyond convenience: the shader is reached by name at
    // runtime otherwise, and Shader.Find only finds what something already refers
    // to, which in a build is nothing.
    private static void AssignInnerShadowShader(ScratchInnerShadow shadow)
    {
        Shader innerShadow = AssetDatabase.LoadAssetAtPath<Shader>(InnerShadowShaderPath);
        if (innerShadow == null) innerShadow = Shader.Find(InnerShadowShaderName);

        if (innerShadow == null)
        {
            Debug.LogWarning($"{InnerShadowShaderName} was not found, so the card's scratch edges stay flat. " +
                             $"Expected it at {InnerShadowShaderPath}.");
            return;
        }

        SerializedObject serialized = new SerializedObject(shadow);
        serialized.FindProperty("shader").objectReferenceValue = innerShadow;
        serialized.ApplyModifiedProperties();
    }

    private static Texture LoadBrushTexture()
    {
        string[] candidates =
        {
            "Assets/Circle.png",
            "Assets/ScratchCard/Demo/Textures/Circle.png",
            "Assets/ScratchCard/Demo/Textures/Brush_1.png"
        };

        foreach (string path in candidates)
        {
            Texture brush = AssetDatabase.LoadAssetAtPath<Texture>(path);
            if (brush != null) return brush;
        }

        Debug.LogWarning("No brush texture found; assign one to the Scratch Card Manager before playing.");
        return null;
    }

    // The painted tilemap wins: it is the whole field the camera can see, apron and
    // all, and matching it exactly is what keeps the card the same shape as the
    // ground under it. Measured off the cells rather than the renderer, which
    // reports whatever its chunks happen to have loaded.
    //
    // Failing that the arena's own MapBounds, then whatever is selected, then a
    // plain default so the menu always produces something to drag around. Those are
    // the play area rather than the whole field, so they keep their own padding.
    private static Bounds MeasureArena()
    {
        Tilemap tilemap = Object.FindAnyObjectByType<Tilemap>();
        if (tilemap != null)
        {
            tilemap.CompressBounds();
            BoundsInt cells = tilemap.cellBounds;
            if (cells.size.x > 0 && cells.size.y > 0)
            {
                Bounds field = new Bounds();
                field.SetMinMax(tilemap.CellToWorld(cells.min), tilemap.CellToWorld(cells.max));
                return field;
            }
        }

        Bounds bounds;
        if (TryGetBounds(GameObject.Find("MapBounds"), out bounds) ||
            TryGetBounds(Selection.activeGameObject, out bounds))
        {
            bounds.Expand(new Vector3(ArenaPadding * 2f, ArenaPadding * 2f, 0f));
            return bounds;
        }

        Debug.LogWarning("No tilemap or MapBounds found; using a default 50 x 50 area. " +
                         "Move and scale the Surface to fit.");
        return new Bounds(Vector3.zero, new Vector3(50f, 50f, 0f));
    }

    private static bool TryGetBounds(GameObject source, out Bounds bounds)
    {
        bounds = default;
        if (source == null) return false;

        Collider2D collider = source.GetComponent<Collider2D>();
        if (collider != null)
        {
            bounds = collider.bounds;
            return true;
        }

        Renderer renderer = source.GetComponent<Renderer>();
        if (renderer != null)
        {
            bounds = renderer.bounds;
            return true;
        }

        return false;
    }

    private static Sprite CreateSurfaceSprite(Vector3 arenaSize)
    {
        if (arenaSize.x <= 0f || arenaSize.y <= 0f)
        {
            Debug.LogError("The arena has no area, so no scratch surface could be generated.");
            return null;
        }

        // A tile has to land on a whole number of pixels or the field drifts out of
        // step with itself, so the resolution is derived from the cell rather than
        // the other way round, shrinking the cell until the arena fits a texture.
        float tileWorldSize = Surface == SurfaceSource.GrassTexture
            ? GrassTextureWorldSize
            : SurfaceTileWorldSize;

        int cell = Mathf.Max(4, Mathf.RoundToInt(tileWorldSize * SurfacePixelsPerUnit));
        while (cell > 4 && Mathf.Max(arenaSize.x, arenaSize.y) * (cell / tileWorldSize) > MaxSurfaceSize)
        {
            cell--;
        }

        float pixelsPerUnit = cell / tileWorldSize;

        // Held to the arena's own aspect ratio, so that stretching the sprite over
        // the ground does not stretch the brush along with it.
        int width = ToMultipleOfFour(arenaSize.x * pixelsPerUnit);
        int height = ToMultipleOfFour(arenaSize.y * pixelsPerUnit);

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(GenerateSurfacePixels(width, height, cell));
        texture.Apply();

        Directory.CreateDirectory(Path.GetDirectoryName(SurfaceAssetPath));
        File.WriteAllBytes(SurfaceAssetPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.Refresh();
        ImportAsOpaqueSprite(SurfaceAssetPath);
        return AssetDatabase.LoadAssetAtPath<Sprite>(SurfaceAssetPath);
    }

    // Compressed formats want dimensions in whole blocks of four.
    private static int ToMultipleOfFour(float pixels)
    {
        int rounded = Mathf.RoundToInt(pixels / 4f) * 4;
        return Mathf.Clamp(rounded, 4, MaxSurfaceSize);
    }

    private static Color32[] GenerateSurfacePixels(int width, int height, int cell)
    {
        if (Surface == SurfaceSource.GrassTexture)
        {
            return GenerateGrassPixels(width, height, cell);
        }

        List<Color32[]> tiles = new List<Color32[]>();
        foreach (string path in SurfaceTilePaths)
        {
            Texture2D source = LoadSourceTexture(path);
            if (source == null) continue;

            tiles.Add(ResampleTile(source, cell, RecolourSurface));
            Object.DestroyImmediate(source);
        }

        if (tiles.Count == 0)
        {
            Debug.LogWarning("No surface tiles were found, so the card falls back to plain foil. " +
                             "Check the paths in SurfaceTilePaths.");
            return GenerateFoilPixels(width, height);
        }

        // The tile backgrounds are flat, so a flipped or wrapped copy still meets its
        // neighbours seamlessly and only the tuft moves. Shifting each cell by up to
        // a third is what stops a field of one drawing reading as rows.
        int span = Mathf.Max(1, cell / 3);

        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int hash = CellHash(x / cell, y / cell);
                Color32[] tile = tiles[hash % tiles.Count];

                int tx = (hash & 4) != 0 ? cell - 1 - x % cell : x % cell;
                int ty = (hash & 8) != 0 ? cell - 1 - y % cell : y % cell;
                tx = (tx + (hash >> 4) % span - span / 2 + cell) % cell;
                ty = (ty + (hash >> 12) % span - span / 2 + cell) % cell;
                Color32 texel = tile[ty * cell + tx];

                // Broad patches of lighter and darker growth over the top of that.
                float shade = 0.90f + 0.20f * Mathf.PerlinNoise(x * 0.004f, y * 0.004f);
                pixels[y * width + x] = new Color32(
                    (byte)Mathf.Min(255f, texel.r * shade),
                    (byte)Mathf.Min(255f, texel.g * shade),
                    (byte)Mathf.Min(255f, texel.b * shade), 255);
            }
        }
        return pixels;
    }

    // One drawing repeated. Its edges were never drawn to meet, so every other
    // copy is mirrored: a reflection always matches its neighbour exactly, which
    // hides the seam at the cost of a symmetry the shading below breaks up again.
    private static Color32[] GenerateGrassPixels(int width, int height, int cell)
    {
        Texture2D source = LoadSourceTexture(GrassTexturePath);
        if (source == null)
        {
            Debug.LogWarning($"No grass texture at {GrassTexturePath}, so the card falls back to " +
                             "plain foil. Check GrassTexturePath.");
            return GenerateFoilPixels(width, height);
        }

        // Kept in its own colours; the tileset needs remapping to green, this does not.
        Color32[] tile = ResampleTile(source, cell, false);
        Object.DestroyImmediate(source);

        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            int ty = (y / cell) % 2 != 0 ? cell - 1 - y % cell : y % cell;
            for (int x = 0; x < width; x++)
            {
                int tx = (x / cell) % 2 != 0 ? cell - 1 - x % cell : x % cell;
                Color32 texel = tile[ty * cell + tx];

                // Broad patches of lighter and darker growth over the top of that.
                float shade = 0.90f + 0.20f * Mathf.PerlinNoise(x * 0.004f, y * 0.004f);
                pixels[y * width + x] = new Color32(
                    (byte)Mathf.Min(255f, texel.r * shade),
                    (byte)Mathf.Min(255f, texel.g * shade),
                    (byte)Mathf.Min(255f, texel.b * shade), 255);
            }
        }
        return pixels;
    }

    private static int CellHash(int cellX, int cellY)
    {
        return (cellX * 73856093 ^ cellY * 19349663) & 0x7fffffff;
    }

    // Box filtered rather than point sampled: a 96 pixel tile squeezed into a 48
    // pixel cell drops every other row of a drawn outline if it is merely sampled.
    private static Color32[] ResampleTile(Texture2D source, int cell, bool recolour)
    {
        Color32[] texels = source.GetPixels32();
        int sourceWidth = source.width;
        int sourceHeight = source.height;
        Color32[] resampled = new Color32[cell * cell];

        for (int y = 0; y < cell; y++)
        {
            int y0 = y * sourceHeight / cell;
            int y1 = Mathf.Max(y0 + 1, (y + 1) * sourceHeight / cell);
            for (int x = 0; x < cell; x++)
            {
                int x0 = x * sourceWidth / cell;
                int x1 = Mathf.Max(x0 + 1, (x + 1) * sourceWidth / cell);

                int r = 0, g = 0, b = 0, count = 0;
                for (int sy = y0; sy < y1; sy++)
                {
                    for (int sx = x0; sx < x1; sx++)
                    {
                        Color32 texel = texels[sy * sourceWidth + sx];
                        r += texel.r;
                        g += texel.g;
                        b += texel.b;
                        count++;
                    }
                }

                resampled[y * cell + x] = Recolour(new Color32(
                    (byte)(r / count), (byte)(g / count), (byte)(b / count), 255), recolour);
            }
        }
        return resampled;
    }

    private static Color32 Recolour(Color32 source, bool recolour)
    {
        if (!recolour) return source;

        float luminance = (0.299f * source.r + 0.587f * source.g + 0.114f * source.b) / 255f;
        Color grass = Color.Lerp(SurfaceShadow, SurfaceHighlight, luminance);
        return new Color32((byte)(grass.r * 255f), (byte)(grass.g * 255f), (byte)(grass.b * 255f), 255);
    }

    // Read straight off disk rather than through the importer, so a tile does not
    // need its Read/Write flag turned on just to be baked into the card once.
    private static Texture2D LoadSourceTexture(string path)
    {
        if (!File.Exists(path)) return null;

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (texture.LoadImage(File.ReadAllBytes(path))) return texture;

        Object.DestroyImmediate(texture);
        return null;
    }

    // Only reached when no tile art is found: a plain grain to cut through, so the
    // card is still buildable in a project that has no ground art yet.
    private static Color32[] GenerateFoilPixels(int width, int height)
    {
        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float grain = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.16f
                              + Mathf.PerlinNoise(x * 0.63f, y * 0.63f) * 0.07f;
                byte level = (byte)(Mathf.Clamp01(0.55f + grain) * 255f);
                pixels[y * width + x] = new Color32(level, level, (byte)Mathf.Min(255, level + 6), 255);
            }
        }
        return pixels;
    }

    private static void ImportAsOpaqueSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = SpritePixelsPerUnit;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = false;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = MaxSurfaceSize;
        // Background art rather than a mask, so block compression costs nothing
        // visible and saves the arena-sized texture most of its memory.
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();
    }
}
