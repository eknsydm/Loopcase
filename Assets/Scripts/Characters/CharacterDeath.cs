using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(CharacterStats))]
[DisallowMultipleComponent]
public class CharacterDeath : MonoBehaviour
{
    [Tooltip("Seconds from the killing blow to the body disappearing. Zero skips the animation and the body goes instantly.")]
    [SerializeField] private float duration = 0.45f;

    [Tooltip("Full turns on the way out. The direction is rolled per death, so two bodies dying together do not mirror each other.")]
    [SerializeField] private float spinTurns = 2f;

    [Tooltip("Scale across the animation, 1 down to 0. Eased in, so the body hangs for a beat before it goes.")]
    [SerializeField] private AnimationCurve shrink = new AnimationCurve(
        new Keyframe(0f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, -2f, -2f));

    [Tooltip("How the spin is spread across the animation. Eased in to match the shrink, so the body accelerates as it leaves.")]
    [SerializeField] private AnimationCurve spinEase = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(1f, 1f, 2f, 2f));

    private CharacterStats stats;
    private Rigidbody2D body;

    private readonly List<Behaviour> suspended = new List<Behaviour>();

    private Vector3 restScale;
    private Quaternion restRotation;
    private Vector3 fromScale;
    private float spinDegrees;
    private float elapsed;
    private bool bodyWasSimulated;

    public bool IsPlaying { get; private set; }

    void Awake()
    {
        stats = GetComponent<CharacterStats>();
        body = GetComponent<Rigidbody2D>();

        restScale = transform.localScale;
        restRotation = transform.localRotation;
    }


    void OnEnable()
    {
        stats.Died += Begin;
    }


    void OnDisable()
    {
        stats.Died -= Begin;
        if (IsPlaying) Restore();
    }

    void Update()
    {
        if (!IsPlaying) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        transform.localScale = fromScale * shrink.Evaluate(t);
        transform.localRotation = restRotation * Quaternion.Euler(0f, 0f, spinDegrees * spinEase.Evaluate(t));

        if (t < 1f) return;

        gameObject.SetActive(false);
    }

    private void Begin()
    {
        if (duration <= 0f) return; 


        fromScale = transform.localScale;
        spinDegrees = 360f * spinTurns * (Random.value < 0.5f ? -1f : 1f);
        elapsed = 0f;
        IsPlaying = true;


        Suspend(GetComponent<PlayerMovement>());
        Suspend(GetComponent<EnemyAI>());

  
        Suspend(GetComponent<WeaponOrbitController>());

        Suspend(GetComponent<Animator>());


        foreach (Canvas overlay in GetComponentsInChildren<Canvas>(true)) Suspend(overlay);

        if (body != null)
        {
            bodyWasSimulated = body.simulated;
            body.linearVelocity = Vector2.zero;

            body.simulated = false;
        }
    }

    private void Suspend(Behaviour behaviour)
    {
        if (behaviour == null || !behaviour.enabled) return;

        behaviour.enabled = false;
        suspended.Add(behaviour);
    }

    private void Restore()
    {
        IsPlaying = false;

        for (int i = 0; i < suspended.Count; i++)
        {
            if (suspended[i] != null) suspended[i].enabled = true;
        }

        suspended.Clear();

        if (body != null) body.simulated = bodyWasSimulated;

        transform.localScale = restScale;
        transform.localRotation = restRotation;
    }
}
