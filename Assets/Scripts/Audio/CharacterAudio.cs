using UnityEngine;


[RequireComponent(typeof(CharacterStats))]
public class CharacterAudio : MonoBehaviour
{
    [Tooltip("Played when a blow lands on this body. Leave Play On Awake off -- this decides when it " +
             "sounds, and a source set to play on awake fires once at spawn as well.")]
    [SerializeField] private AudioSource hitSound;

    [Tooltip("Played when this body collects a weapon.")]
    [SerializeField] private AudioSource pickUpSound;

    [Header("Earshot")]
    [Tooltip("How far this body can be from the ear and still be heard at all. The ear rides the " +
             "camera, which is on the player until it hands over to whoever killed them. 0 switches " +
             "the whole thing off and every body is heard from anywhere on the map.")]
    [SerializeField] private float hearingRange = 18f;

    [Tooltip("Where the fade starts, as a fraction of that range. Inside it a sound is at full volume; " +
             "from there it falls away to nothing at the edge.")]
    [Range(0f, 1f)]
    [SerializeField] private float fullVolumeFraction = 0.35f;


    private CharacterStats stats;
    private WeaponOrbitController orbit;

    void Awake()
    {
        stats = GetComponent<CharacterStats>();
        orbit = GetComponent<WeaponOrbitController>();
    }

    void OnEnable()
    {
        if (stats != null) stats.Hit += OnHit;
        if (orbit != null) orbit.WeaponCollected += OnWeaponCollected;
    }

    void OnDisable()
    {
        if (stats != null) stats.Hit -= OnHit;
        if (orbit != null) orbit.WeaponCollected -= OnWeaponCollected;
    }

    private void OnHit(Vector2 from) => Play(hitSound);

    private void OnWeaponCollected() => Play(pickUpSound);

    private void Play(AudioSource source)
    {
        if (source == null) return;

        float volume = Earshot.VolumeAt(transform.position, hearingRange, fullVolumeFraction);
        if (volume <= 0f) return;

        if (source.clip != null) source.PlayOneShot(source.clip, volume);
        else source.Play();
    }

}
