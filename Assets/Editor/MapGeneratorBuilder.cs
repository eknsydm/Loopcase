using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Tilemaps;

// The generator needs a dozen references before it will build anything, and every
// one of them already exists at a known path. This wires them rather than leaving
// a row of empty inspector slots to fill by hand.
public static class MapGeneratorBuilder
{
    private const string TilePath = "Assets/Sprites/tile/tiles/";
    private const string SpritePath = "Assets/Sprites/tile/";

    private const string GroundTile = TilePath + "tile-ground_0.asset";
    private static readonly string[] GrassTiles =
    {
        TilePath + "tile-grass-2_0.asset",
        TilePath + "tile-grass-9_0.asset"
    };

    [MenuItem("Tools/Loopcase/Build Map Generator")]
    private static void Build()
    {
        MapGenerator generator = Object.FindAnyObjectByType<MapGenerator>();
        if (generator == null)
        {
            GameObject root = new GameObject("Map Generator");
            Undo.RegisterCreatedObjectUndo(root, "Build Map Generator");
            generator = root.AddComponent<MapGenerator>();
        }

        SerializedObject serialized = new SerializedObject(generator);

        // The tilemap the scene already paints on, so a generated arena replaces the
        // hand-drawn one rather than sitting beside it.
        Tilemap tilemap = Object.FindAnyObjectByType<Tilemap>();
        if (tilemap == null) Debug.LogWarning("No Tilemap in the scene; assign one on the Map Generator.");
        serialized.FindProperty("tilemap").objectReferenceValue = tilemap;

        serialized.FindProperty("groundTile").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<TileBase>(GroundTile);

        SerializedProperty grass = serialized.FindProperty("grassTiles");
        grass.arraySize = GrassTiles.Length;
        for (int i = 0; i < GrassTiles.Length; i++)
        {
            grass.GetArrayElementAtIndex(i).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TileBase>(GrassTiles[i]);
        }

        serialized.FindProperty("fenceHorizontal").objectReferenceValue =
            LoadSprite("map-decoration-fence-connector-horizontal");
        serialized.FindProperty("fenceVertical").objectReferenceValue =
            LoadSprite("map-decoration-fence-connector-vertical");
        serialized.FindProperty("fenceCorner").objectReferenceValue =
            LoadSprite("map-decoration-fence-corner");

        GameObject bounds = GameObject.Find("MapBounds");
        serialized.FindProperty("mapBounds").objectReferenceValue =
            bounds != null ? bounds.GetComponent<PolygonCollider2D>() : null;

        serialized.FindProperty("cameraBounds").objectReferenceValue = BuildCameraBounds();

        // The one body in the scene nothing is driving is the player, the same test
        // the enemy AI makes when it goes looking for something to chase.
        CharacterStats player = Object.FindObjectsByType<CharacterStats>(FindObjectsSortMode.None)
            .FirstOrDefault(stats => stats.GetComponent<EnemyAI>() == null);
        serialized.FindProperty("player").objectReferenceValue =
            player != null ? player.transform : null;

        serialized.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
        Selection.activeGameObject = generator.gameObject;

        Debug.Log("Map Generator wired up. Set the enemy prefab, then use its Generate context menu " +
                  "to preview the ground and fence without entering play mode.", generator);
    }

    // The camera is allowed to drift past the fence, which the collectable spawner
    // must not be. They were reading the same collider, so the confiner is moved onto
    // one of its own and MapBounds goes back to meaning just the play area.
    private static PolygonCollider2D BuildCameraBounds()
    {
        GameObject go = GameObject.Find("CameraBounds");
        if (go == null)
        {
            go = new GameObject("CameraBounds");
            Undo.RegisterCreatedObjectUndo(go, "Build Map Generator");
        }

        PolygonCollider2D shape = go.GetComponent<PolygonCollider2D>();
        if (shape == null) shape = go.AddComponent<PolygonCollider2D>();

        // Nothing should collide with it; it is a shape for the confiner to read.
        shape.isTrigger = true;

        CinemachineConfiner2D confiner = Object.FindAnyObjectByType<CinemachineConfiner2D>();
        if (confiner == null)
        {
            Debug.LogWarning("No Cinemachine Confiner 2D in the scene; point one at CameraBounds by hand.");
            return shape;
        }

        confiner.BoundingShape2D = shape;
        EditorUtility.SetDirty(confiner);
        return shape;
    }

    // A fence texture may be sliced into several sprites or imported as one. Multiple
    // mode names the cut-out piece with a _0 suffix; single mode leaves it named after
    // the texture, so both spellings are tried before falling back to whatever is there.
    private static Sprite LoadSprite(string textureName)
    {
        string path = SpritePath + textureName + ".png";
        List<Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();

        if (sprites.Count == 0)
        {
            Debug.LogWarning($"No sprite found in {path}; assign the fence sprites by hand.");
            return null;
        }

        return sprites.FirstOrDefault(s => s.name == textureName + "_0")
               ?? sprites.FirstOrDefault(s => s.name == textureName)
               ?? sprites[0];
    }
}
