using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class WeaponPickup : MonoBehaviour
{

    private static readonly List<WeaponPickup> active = new List<WeaponPickup>();
    public static IReadOnlyList<WeaponPickup> Active => active;


    private IObjectPool<WeaponPickup> pool;
    private bool claimed;

    public void SetPool(IObjectPool<WeaponPickup> owner)
    {
        pool = owner;
    }

    private void OnEnable()
    {
        claimed = false;
        active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {

        if (claimed) return;

        WeaponOrbitController orbitController = other.GetComponent<WeaponOrbitController>();
        if (orbitController == null) return;

        claimed = true;
        orbitController.CollectWeapon();

        if (pool != null) pool.Release(this);
        else Destroy(gameObject);
    }
}
