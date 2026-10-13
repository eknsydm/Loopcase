using UnityEngine;


[RequireComponent(typeof(CharacterStats))]
public class CollectableDropper : MonoBehaviour
{
    [Tooltip("What falls out. The same pickup that is scattered around the arena, so a drop is collected like any other.")]
    [SerializeField] private WeaponPickup collectablePrefab;

    [Tooltip("Dropped whether or not the character was carrying anything.")]
    [SerializeField] private int baseDropCount = 3;

    [Header("Scatter")]
    [Tooltip("How far the drops land from the body. They are laid out in a ring so a big drop is not one pickup deep.")]
    [SerializeField] private float scatterRadius = 1.2f;

    [Tooltip("Random slack on that distance, so the spill does not read as a formation.")]
    [SerializeField] private float scatterJitter = 0.35f;

    private CharacterStats stats;
    private WeaponOrbitController orbit;

    void Awake()
    {
        stats = GetComponent<CharacterStats>();
        orbit = GetComponent<WeaponOrbitController>();

        stats.Died += Drop;
    }

    void OnDestroy()
    {
        if (stats != null) stats.Died -= Drop;
    }

    private void Drop()
    {

        int carried = orbit != null ? orbit.WeaponCount : 0;
        if (orbit != null) orbit.ClearWeapons();

        int total = baseDropCount + carried;
        if (total <= 0) return;

        if (collectablePrefab == null)
        {
            Debug.LogWarning($"{name} has no collectable prefab, so its {total} drops are lost. " +
                             "Drop the Collectable prefab on Collectable Dropper.", this);
            return;
        }

        float startAngle = Random.Range(0f, 360f);

        for (int i = 0; i < total; i++)
        {
            float rad = (startAngle + 360f / total * i) * Mathf.Deg2Rad;
            float distance = scatterRadius + Random.Range(-scatterJitter, scatterJitter);
            Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * distance;

            Instantiate(collectablePrefab, transform.position + offset, Quaternion.identity);
        }
    }
}
