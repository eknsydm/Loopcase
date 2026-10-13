using UnityEngine;


public static class Earshot
{
    private static AudioListener ear;


    public static float VolumeAt(Vector2 position, float range, float fullVolumeFraction)
    {
        if (range <= 0f) return 1f;

        if (ear == null) ear = Object.FindAnyObjectByType<AudioListener>();
        if (ear == null) return 1f;

        float distance = Vector2.Distance(position, ear.transform.position);

        float full = range * Mathf.Clamp01(fullVolumeFraction);
        if (distance <= full) return 1f;
        if (distance >= range) return 0f;

        return 1f - (distance - full) / Mathf.Max(range - full, 0.001f);
    }
}
