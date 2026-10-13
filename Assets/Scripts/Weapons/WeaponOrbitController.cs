using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class WeaponOrbitController : MonoBehaviour
{
    [SerializeField] private OrbitingWeapon weaponPrefab;
    [SerializeField] private Transform originPoint;
    [SerializeField] private float orbitRadius = 1.5f;
    [SerializeField] private float baseSpinSpeed = 90f;
    [SerializeField] private float spinSpeedPerWeapon = 30f;

    [Header("Pool")]
    [Tooltip("Blades kept ready up front, so the first pickups of a round do not each cost an Instantiate.")]
    [SerializeField] private int poolCapacity = 8;

    [Tooltip("Past this the pool stops keeping returned blades and destroys them instead.")]
    [SerializeField] private int poolMax = 32;

    private readonly List<OrbitingWeapon> weapons = new List<OrbitingWeapon>();
    private ObjectPool<OrbitingWeapon> pool;


    private static readonly List<WeaponOrbitController> active = new List<WeaponOrbitController>();
    public static IReadOnlyList<WeaponOrbitController> Active => active;


    public event Action WeaponCollected;

    void Awake()
    {
        pool = new ObjectPool<OrbitingWeapon>(
            CreateWeapon,
            OnGetWeapon,
            OnReleaseWeapon,
            OnDestroyWeapon,
            collectionCheck: true,
            poolCapacity,
            poolMax);
    }

    void OnEnable()
    {
        active.Add(this);
    }

    void OnDisable()
    {
        active.Remove(this);
    }

    private Transform Origin => originPoint != null ? originPoint : transform;

    public float SpinPhase { get; private set; }
    public float SpinSpeed { get; private set; }
    public int WeaponCount => weapons.Count;

    void Start()
    {
        CollectWeapon();
    }
    void Update()
    {
        if (weapons.Count == 0) return;

        SpinSpeed = baseSpinSpeed + spinSpeedPerWeapon * (weapons.Count - 1);
        SpinPhase = Mathf.Repeat(SpinPhase - SpinSpeed * Time.deltaTime, 360f);
    }

    public void CollectWeapon()
    {
        if (weaponPrefab == null)
        {
            Debug.LogError("Weapon Orbit Controller has no weapon prefab, so nothing can be picked up.", this);
            return;
        }

        OrbitingWeapon weapon = pool.Get();

        weapon.transform.SetPositionAndRotation(Origin.position, Quaternion.identity);
        weapon.Init(Origin, orbitRadius, this);
        weapons.Add(weapon);

        UpdateOrbits();


        WeaponCollected?.Invoke();
    }

    private OrbitingWeapon CreateWeapon()
    {
        OrbitingWeapon weapon = Instantiate(weaponPrefab);

        weapon.SetPool(pool);
        weapon.gameObject.SetActive(false);

        return weapon;
    }

    private void OnGetWeapon(OrbitingWeapon weapon)
    {
        weapon.gameObject.SetActive(true);
        weapon.ResetForUse();
    }

    private void OnReleaseWeapon(OrbitingWeapon weapon)
    {
        weapon.gameObject.SetActive(false);
    }

    private void OnDestroyWeapon(OrbitingWeapon weapon)
    {
        if (weapon != null) Destroy(weapon.gameObject);
    }

    public void RemoveWeapon(OrbitingWeapon weapon)
    {
        weapons.Remove(weapon);
        UpdateOrbits();
    }


    public void ClearWeapons()
    {

        for (int i = weapons.Count - 1; i >= 0; i--)
        {
            if (weapons[i] != null) weapons[i].Retire();
        }

        weapons.Clear();
    }

    private void UpdateOrbits()
    {
        if (weapons.Count == 0) return;


        float anchor = weapons[0].CurrentOffset;

        for (int i = 0; i < weapons.Count; i++)
        {
            float angleOffset = Mathf.Repeat(anchor + 360f / weapons.Count * i, 360f);
            weapons[i].SetTargetOffset(angleOffset);
        }
    }
}
