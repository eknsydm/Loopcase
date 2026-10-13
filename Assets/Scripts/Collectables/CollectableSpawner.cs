using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class CollectableSpawner : MonoBehaviour
{
    [SerializeField] private WeaponPickup collectablePrefab;

    [Header("Timing")]
    [Tooltip("Seconds between spawns, rolled fresh every time. Widen it for a scrappier round.")]
    [SerializeField] private Vector2 intervalRange = new Vector2(1.5f, 6f);

    [Tooltip("Grace before the first one, so a round does not open in a shower of weapons.")]
    [SerializeField] private float firstSpawnDelay = 2f;

    [Header("Placement")]
    [Tooltip("Collider marking the playable area -- the same shape the camera confiner uses. " +
             "Points are tested against it, so an arena that is not a rectangle still fills correctly. " +
             "Empty falls back to the box below, centred on this object.")]
    [SerializeField] private Collider2D spawnArea;

    [SerializeField] private Vector2 fallbackAreaSize = new Vector2(40f, 41f);

    [Tooltip("How far a spawn has to land from any character, so nobody is handed a weapon for standing still.")]
    [SerializeField] private float clearanceFromCharacters = 4f;

    [Tooltip("How far from a pickup already on the floor, so one drop lands clear of the last one. " +
             "Members of the same group are spaced by their own rule below instead.")]
    [SerializeField] private float clearanceFromPickups = 2.5f;

    [Tooltip("Points tried before a spawn is skipped. A crowded map should miss a beat rather than drop a weapon in someone's lap.")]
    [SerializeField] private int placementAttempts = 24;

    [Header("Groups")]
    [Tooltip("How many pickups land together in one drop, rolled fresh each time. Both at 1 puts them " +
             "back to arriving one at a time.")]
    [SerializeField] private Vector2Int groupSize = new Vector2Int(2, 4);

    [Tooltip("How far a group's members scatter around where it landed. Each still has to find room of " +
             "its own, so a wide radius is a group that thins out into a scatter.")]
    [SerializeField] private float groupRadius = 2.5f;

    [Tooltip("How close two pickups in the same group may sit. Tighter than the gap kept between one " +
             "drop and the next -- that is what makes a group a group -- but wide enough that two " +
             "swords do not land on top of each other.")]
    [SerializeField] private float spacingWithinGroup = 1.2f;

    [Header("Population")]
    [Tooltip("Spawning pauses while this many pickups are already out, death drops included, so a quiet stretch does not carpet the floor.")]
    [SerializeField] private int maxAlive = 24;

    [Tooltip("How many pickups to build up front, so the first spawns of a round do not each cost an Instantiate.")]
    [SerializeField] private int poolCapacity = 16;

    [Tooltip("Past this the pool stops keeping returned pickups and destroys them instead.")]
    [SerializeField] private int poolMax = 64;

    private ObjectPool<WeaponPickup> pool;
    private float nextSpawnTime;


    private readonly List<Vector2> groupPoints = new List<Vector2>();

    void Awake()
    {
        pool = new ObjectPool<WeaponPickup>(
            CreatePickup,
            actionOnGet: null,
            OnReleasePickup,
            OnDestroyPickup,
            collectionCheck: true,
            poolCapacity,
            poolMax);
    }

    void Start()
    {
        if (collectablePrefab == null)
        {
            Debug.LogWarning($"{name} has no collectable prefab, so it will not spawn anything. " +
                             "Drop the Collectable prefab on Collectable Spawner.", this);
            enabled = false;
            return;
        }

        nextSpawnTime = Time.time + firstSpawnDelay;
        Prewarm(poolCapacity);
    }

    void Update()
    {
        if (Time.time < nextSpawnTime) return;


        nextSpawnTime = Time.time + Random.Range(intervalRange.x, intervalRange.y);

        TrySpawnGroup();
    }


    private void TrySpawnGroup()
    {
        int room = maxAlive - WeaponPickup.Active.Count;
        if (room <= 0) return;


        if (!TryFindPoint(out Vector2 anchor)) return;

        int wanted = Mathf.Clamp(RollGroupSize(), 1, room);

        groupPoints.Clear();
        groupPoints.Add(anchor);


        for (int i = 1; i < wanted; i++)
        {
            if (TryFindGroupPoint(anchor, out Vector2 point)) groupPoints.Add(point);
        }

        foreach (Vector2 point in groupPoints) Spawn(point);
    }

    private int RollGroupSize()
    {
        int low = Mathf.Max(1, Mathf.Min(groupSize.x, groupSize.y));
        int high = Mathf.Max(low, Mathf.Max(groupSize.x, groupSize.y));

        return Random.Range(low, high + 1);
    }

    private void Spawn(Vector2 point)
    {

        WeaponPickup pickup = pool.Get();
        pickup.transform.position = point;
        pickup.gameObject.SetActive(true);
    }

    private bool TryFindGroupPoint(Vector2 anchor, out Vector2 point)
    {
        for (int i = 0; i < placementAttempts; i++)
        {
            point = anchor + Random.insideUnitCircle * groupRadius;
            if (IsClear(point) && HasRoomInGroup(point)) return true;
        }

        point = default;
        return false;
    }

    private bool HasRoomInGroup(Vector2 point)
    {
        foreach (Vector2 placed in groupPoints)
        {
            if ((placed - point).sqrMagnitude < spacingWithinGroup * spacingWithinGroup) return false;
        }

        return true;
    }

    private bool TryFindPoint(out Vector2 point)
    {
        for (int i = 0; i < placementAttempts; i++)
        {
            point = RandomPointInArea();
            if (IsClear(point)) return true;
        }

        point = default;
        return false;
    }

    private Vector2 RandomPointInArea()
    {
        if (spawnArea == null)
        {
            Vector2 half = fallbackAreaSize * 0.5f;
            return (Vector2)transform.position +
                   new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y));
        }


        Bounds bounds = spawnArea.bounds;
        return new Vector2(Random.Range(bounds.min.x, bounds.max.x), Random.Range(bounds.min.y, bounds.max.y));
    }

    private bool IsClear(Vector2 point)
    {
        if (spawnArea != null && !spawnArea.OverlapPoint(point)) return false;


        foreach (WeaponOrbitController character in WeaponOrbitController.Active)
        {
            if (character == null) continue;

            float sqr = ((Vector2)character.transform.position - point).sqrMagnitude;
            if (sqr < clearanceFromCharacters * clearanceFromCharacters) return false;
        }

        foreach (WeaponPickup pickup in WeaponPickup.Active)
        {
            if (pickup == null) continue;

            float sqr = ((Vector2)pickup.transform.position - point).sqrMagnitude;
            if (sqr < clearanceFromPickups * clearanceFromPickups) return false;
        }

        return true;
    }

    private void Prewarm(int count)
    {
        if (count <= 0) return;

        WeaponPickup[] warmed = new WeaponPickup[count];

        for (int i = 0; i < count; i++) warmed[i] = pool.Get();
        for (int i = 0; i < count; i++) pool.Release(warmed[i]);
    }

    private WeaponPickup CreatePickup()
    {
        WeaponPickup pickup = Instantiate(collectablePrefab);


        pickup.SetPool(pool);
        pickup.gameObject.SetActive(false);

        return pickup;
    }

    private void OnReleasePickup(WeaponPickup pickup)
    {
        pickup.gameObject.SetActive(false);
    }

    private void OnDestroyPickup(WeaponPickup pickup)
    {
        if (pickup != null) Destroy(pickup.gameObject);
    }

    void OnDestroy()
    {

        pool?.Clear();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.5f);

        if (spawnArea != null) Gizmos.DrawWireCube(spawnArea.bounds.center, spawnArea.bounds.size);
        else Gizmos.DrawWireCube(transform.position, fallbackAreaSize);
    }
}
