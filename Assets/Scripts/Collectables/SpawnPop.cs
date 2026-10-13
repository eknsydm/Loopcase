using UnityEngine;


[DisallowMultipleComponent]
public class SpawnPop : MonoBehaviour
{
    [Tooltip("Seconds to reach full size. Zero skips the effect.")]
    [SerializeField] private float duration = 0.25f;

    [Tooltip("Size over that window, as a fraction of the object's authored scale.")]
    [SerializeField] private AnimationCurve curve = new AnimationCurve(
        new Keyframe(0f, 0f, 2f, 2f),
        new Keyframe(1f, 1f, 0f, 0f));

    private Vector3 baseScale;
    private Collider2D hitbox;
    private float elapsed;
    private bool grown;

    void Awake()
    {

        baseScale = transform.localScale;
        hitbox = GetComponent<Collider2D>();
    }

    void OnEnable()
    {
        elapsed = 0f;
        grown = false;
        transform.localScale = Vector3.zero;


        if (hitbox != null) hitbox.enabled = false;
    }

    void Update()
    {
        if (grown) return;

        elapsed += Time.deltaTime;
        float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;

        if (t < 1f)
        {
            transform.localScale = baseScale * curve.Evaluate(t);
            return;
        }


        grown = true;
        transform.localScale = baseScale;
        if (hitbox != null) hitbox.enabled = true;
    }
}
