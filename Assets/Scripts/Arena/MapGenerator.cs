using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class MapGenerator : MonoBehaviour
{
    private const string WallsChildName = "Walls";
    private const string EnemiesChildName = "Enemies";

    private enum WallFill { Sliced, Tiled }

    [Header("Size")]
    [SerializeField] private int width = 30;
    [SerializeField] private int height = 30;
    [SerializeField] private int borderTiles = 8;
    [SerializeField] private int seed;
    [SerializeField] private bool generateOnStart = true;

    [Header("Ground")]
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private TileBase groundTile;
    [SerializeField] private TileBase[] grassTiles;

    [Header("Grass")]
    [SerializeField] private float grassNoiseScale = 0.12f;
    [Range(0f, 1f)] [SerializeField] private float grassThreshold = 0.5f;
    [Range(0f, 0.3f)] [SerializeField] private float grassEdgeJitter = 0.06f;

    [Header("Walls")]
    [SerializeField] private WallFill wallFill = WallFill.Sliced;
    [SerializeField] private Sprite fenceHorizontal;
    [SerializeField] private Sprite fenceVertical;
    [SerializeField] private Sprite fenceCorner;
    [SerializeField] private Vector3 fenceScale = new Vector3(2, 2, 2);
    [SerializeField] private float postSpacing = 4f;
    [SerializeField] private float wallThickness = 0.6f;
    [SerializeField] private int wallSortingOrder;

    [Header("Characters")]
    [SerializeField] private GameObject characterPrefab;
    [SerializeField] private int enemyCount = 6;
    [SerializeField] private Transform player;
    [SerializeField] private bool randomizePlayerSpawn = true;
    [SerializeField] private Vector2 enemySpeedRatio = new Vector2(0.72f, 0.94f);
    [SerializeField] private float clearanceFromPlayer = 10f;
    [SerializeField] private float clearanceFromEnemies = 3f;
    [SerializeField] private float edgeMargin = 2f;
    [SerializeField] private int placementAttempts = 24;

    [Header("Bounds")]
    [SerializeField] private PolygonCollider2D mapBounds;

    private Transform wallsRoot;
    private Transform enemiesRoot;

    void Start()
    {
        if (generateOnStart)
            Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (tilemap == null || groundTile == null)
        {
            Debug.LogError(
                "Map Generator needs a Tilemap and a ground tile before it can build anything.",
                this
            );
            return;
        }

        if (seed != 0)
            Random.InitState(seed);

        BuildGround(
            out Vector2 min,
            out Vector2 max,
            out Vector2 apronMin,
            out Vector2 apronMax
        );

        BuildWalls(min, max);
        FitCollider(mapBounds, min, max);
        FitScratchCard(apronMin, apronMax);

        if (Application.isPlaying)
            PlaceCharacters(min, max);
    }

    private void BuildGround(
        out Vector2 min,
        out Vector2 max,
        out Vector2 apronMin,
        out Vector2 apronMax
    )
    {
        int playX = -width / 2;
        int playY = -height / 2;

        int apron = Mathf.Max(0, borderTiles);
        int originX = playX - apron;
        int originY = playY - apron;
        int columns = width + apron * 2;
        int rows = height + apron * 2;

        float offsetX = Random.Range(0f, 10000f);
        float offsetY = Random.Range(0f, 10000f);

        TileBase[] tiles = new TileBase[columns * rows];

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                float noise = Mathf.PerlinNoise(
                    offsetX + x * grassNoiseScale,
                    offsetY + y * grassNoiseScale
                );

                noise += Random.Range(
                    -grassEdgeJitter,
                    grassEdgeJitter
                );

                tiles[x + y * columns] =
                    noise > grassThreshold
                        ? PickGrass()
                        : groundTile;
            }
        }

        tilemap.ClearAllTiles();

        tilemap.SetTilesBlock(
            new BoundsInt(
                originX,
                originY,
                0,
                columns,
                rows,
                1
            ),
            tiles
        );

        min = tilemap.CellToWorld(
            new Vector3Int(playX, playY, 0)
        );

        max = tilemap.CellToWorld(
            new Vector3Int(playX + width, playY + height, 0)
        );

        apronMin = tilemap.CellToWorld(
            new Vector3Int(originX, originY, 0)
        );

        apronMax = tilemap.CellToWorld(
            new Vector3Int(
                originX + columns,
                originY + rows,
                0
            )
        );
    }

    private TileBase PickGrass()
    {
        if (grassTiles == null || grassTiles.Length == 0)
            return groundTile;

        return grassTiles[Random.Range(0, grassTiles.Length)];
    }

    private void BuildWalls(Vector2 min, Vector2 max)
    {
        wallsRoot = ResetChild(
            wallsRoot,
            WallsChildName
        );

        BuildSide(
            "Wall Bottom",
            fenceHorizontal,
            new Vector2(min.x, min.y),
            new Vector2(max.x, min.y),
            true,
            Vector2.down,
            false
        );

        BuildSide(
            "Wall Top",
            fenceHorizontal,
            new Vector2(min.x, max.y),
            new Vector2(max.x, max.y),
            true,
            Vector2.up,
            false
        );

        BuildSide(
            "Wall Left",
            fenceVertical,
            new Vector2(min.x, min.y),
            new Vector2(min.x, max.y),
            false,
            Vector2.left,
            false
        );

        BuildSide(
            "Wall Right",
            fenceVertical,
            new Vector2(max.x, min.y),
            new Vector2(max.x, max.y),
            false,
            Vector2.right,
            true
        );

        BuildPost(
            wallsRoot,
            "Corner SW",
            new Vector2(min.x, min.y),
            false
        );

        BuildPost(
            wallsRoot,
            "Corner SE",
            new Vector2(max.x, min.y),
            true
        );

        BuildPost(
            wallsRoot,
            "Corner NW",
            new Vector2(min.x, max.y),
            false
        );

        BuildPost(
            wallsRoot,
            "Corner NE",
            new Vector2(max.x, max.y),
            true
        );
    }

    private void BuildSide(
        string name,
        Sprite rail,
        Vector2 from,
        Vector2 to,
        bool horizontal,
        Vector2 outward,
        bool postsFlipX
    )
    {
        GameObject side = new GameObject(name);
        side.transform.SetParent(wallsRoot, false);
        side.transform.position = (from + to) * 0.5f;

        float span = Mathf.Abs(
            horizontal
                ? to.x - from.x
                : to.y - from.y
        );

        BuildRail(
            side.transform,
            "Rail",
            rail,
            (from + to) * 0.5f,
            span,
            horizontal
        );

        int steps =
            postSpacing > 0.01f
                ? Mathf.RoundToInt(span / postSpacing)
                : 1;

        for (int i = 1; i < steps; i++)
        {
            BuildPost(
                side.transform,
                "Post " + i,
                Vector2.Lerp(
                    from,
                    to,
                    (float)i / steps
                ),
                postsFlipX
            );
        }

        BoxCollider2D box =
            side.AddComponent<BoxCollider2D>();

        box.size = new Vector2(
            horizontal
                ? span
                : wallThickness,
            horizontal
                ? wallThickness
                : span
        );

        box.offset =
            outward * (wallThickness * 0.5f);
    }

    private void BuildPost(
        Transform parent,
        string name,
        Vector2 position,
        bool flipX
    )
    {
        if (fenceCorner == null)
            return;

        GameObject go = new GameObject(name);

        go.transform.SetParent(
            parent,
            false
        );

        go.transform.localScale =
            fenceScale;

        go.transform.position =
            position;

        SpriteRenderer renderer =
            go.AddComponent<SpriteRenderer>();

        renderer.sprite =
            fenceCorner;

        renderer.flipX =
            flipX;

        renderer.sortingOrder =
            wallSortingOrder + 1;
    }

    private void BuildRail(
        Transform side,
        string name,
        Sprite sprite,
        Vector2 centre,
        float length,
        bool horizontal
    )
    {
        if (sprite == null || length <= 0f)
            return;

        GameObject go = new GameObject(name);

        go.transform.SetParent(
            side,
            false
        );

        go.transform.localScale =
            fenceScale;

        go.transform.position =
            centre;

        SpriteRenderer renderer =
            go.AddComponent<SpriteRenderer>();

        renderer.sprite =
            sprite;

        renderer.sortingOrder =
            wallSortingOrder;

        if (wallFill == WallFill.Tiled)
        {
            renderer.drawMode =
                SpriteDrawMode.Tiled;

            renderer.tileMode =
                SpriteTileMode.Continuous;
        }
        else
        {
            renderer.drawMode =
                SpriteDrawMode.Sliced;
        }

        Vector2 piece =
            sprite.bounds.size;

        renderer.size = new Vector2(
            horizontal
                ? length / fenceScale.y
                : piece.x,
            horizontal
                ? piece.y
                : length / fenceScale.x
        );
    }

    private void FitScratchCard(
        Vector2 min,
        Vector2 max
    )
    {
        ArenaScratchCard card =
            FindAnyObjectByType<ArenaScratchCard>();

        if (card == null)
            return;

        Bounds arena = new Bounds();

        arena.SetMinMax(
            new Vector3(min.x, min.y, 0f),
            new Vector3(max.x, max.y, 0f)
        );

        card.FitTo(
            arena,
            GroundTexture()
        );
    }

    private Texture GroundTexture()
    {
        return groundTile is Tile drawn &&
               drawn.sprite != null
            ? drawn.sprite.texture
            : null;
    }

    private void FitCollider(
        PolygonCollider2D collider,
        Vector2 min,
        Vector2 max
    )
    {
        if (collider == null)
            return;

        Vector2 lo =
            collider.transform.InverseTransformPoint(min);

        Vector2 hi =
            collider.transform.InverseTransformPoint(max);

        collider.pathCount = 1;

        collider.SetPath(
            0,
            new[]
            {
                lo,
                new Vector2(hi.x, lo.y),
                hi,
                new Vector2(lo.x, hi.y)
            }
        );
    }

    private void PlaceCharacters(
        Vector2 min,
        Vector2 max
    )
    {
        SpawnBox(
            min,
            max,
            out Vector2 lo,
            out Vector2 hi
        );

        if (
            randomizePlayerSpawn &&
            player != null
        )
        {
            PlaceBody(
                player,
                new Vector2(
                    Random.Range(lo.x, hi.x),
                    Random.Range(lo.y, hi.y)
                )
            );
        }

        SpawnEnemies(lo, hi);
    }

    private void SpawnEnemies(
        Vector2 lo,
        Vector2 hi
    )
    {
        enemiesRoot = ResetChild(
            enemiesRoot,
            EnemiesChildName
        );

        if (characterPrefab == null)
        {
            if (enemyCount > 0)
            {
                Debug.LogWarning(
                    "Map Generator has no character prefab, so the arena is built empty.",
                    this
                );
            }

            return;
        }

        enemiesRoot.gameObject.SetActive(false);

        for (int i = 0; i < enemyCount; i++)
        {
            GameObject body =
                Instantiate(
                    characterPrefab,
                    FindSpawn(lo, hi),
                    Quaternion.identity,
                    enemiesRoot
                );

            body.name =
                "Enemy " + (i + 1);

            EnemyAI ai =
                EnemyAI.TakeOver(body);

            if (ai != null)
                ai.SpeedFactorRange =
                    enemySpeedRatio;
        }

        enemiesRoot.gameObject.SetActive(true);
    }

    private void SpawnBox(
        Vector2 min,
        Vector2 max,
        out Vector2 lo,
        out Vector2 hi
    )
    {
        lo =
            min + Vector2.one * edgeMargin;

        hi =
            max - Vector2.one * edgeMargin;

        if (lo.x > hi.x)
            lo.x = hi.x =
                (min.x + max.x) * 0.5f;

        if (lo.y > hi.y)
            lo.y = hi.y =
                (min.y + max.y) * 0.5f;
    }

    private static void PlaceBody(
        Transform body,
        Vector2 position
    )
    {
        body.position =
            position;

        if (body.TryGetComponent(
            out Rigidbody2D rb
        ))
        {
            rb.position =
                position;

            rb.linearVelocity =
                Vector2.zero;
        }
    }

    private Vector2 FindSpawn(
        Vector2 lo,
        Vector2 hi
    )
    {
        Vector2 spot = Vector2.zero;

        for (
            int attempt = 0;
            attempt < Mathf.Max(
                1,
                placementAttempts
            );
            attempt++
        )
        {
            spot = new Vector2(
                Random.Range(lo.x, hi.x),
                Random.Range(lo.y, hi.y)
            );

            if (
                player != null &&
                Vector2.Distance(
                    spot,
                    player.position
                ) < clearanceFromPlayer
            )
            {
                continue;
            }

            if (IsCrowded(spot))
                continue;

            return spot;
        }

        return spot;
    }

    private bool IsCrowded(
        Vector2 spot
    )
    {
        for (
            int i = 0;
            i < enemiesRoot.childCount;
            i++
        )
        {
            if (
                Vector2.Distance(
                    spot,
                    enemiesRoot.GetChild(i).position
                ) < clearanceFromEnemies
            )
            {
                return true;
            }
        }

        return false;
    }

    private Transform ResetChild(
        Transform known,
        string name
    )
    {
        Transform existing =
            known != null
                ? known
                : transform.Find(name);

        if (existing != null)
        {
            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
        }

        GameObject go =
            new GameObject(name);

        go.transform.SetParent(
            transform,
            false
        );

        return go.transform;
    }

    private void Update()
    {
        if (
            Keyboard.current.rKey
                .wasPressedThisFrame
        )
        {
            SceneManager.LoadScene(
                SceneManager.GetActiveScene()
                    .buildIndex
            );
        }
    }
}

