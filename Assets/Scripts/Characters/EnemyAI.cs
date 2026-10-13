using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(WeaponOrbitController))]
public class EnemyAI : MonoBehaviour
{
    private enum Intent
    {
        SeekWeapon,
        Retreat,
        Chase,
        Wander
    }

    [Header("Ranges")]
    [SerializeField] private float chaseRange = 9f;
    [SerializeField] private float dangerRange = 10f;
    [SerializeField] private float grabRange = 7f;
    [SerializeField] private float snatchRange = 4f;

    [Header("Nerve")]
    [SerializeField] private int outgunnedBy = 1;
    [SerializeField] private Vector2 speedFactorRange = new Vector2(0.72f, 0.94f);

    [Header("Wander")]
    [SerializeField] private float wanderRadius = 10f;
    [SerializeField] private float wanderArrivalDistance = 1f;
    [SerializeField] private Vector2 wanderPauseRange = new Vector2(0.4f, 1.6f);

    [SerializeField] private float decisionInterval = 0.2f;

    private const float SlowestPlayerFraction = 0.85f;

    private PlayerMovement movement;
    private WeaponOrbitController orbit;
    private Transform targetPickup;

    private WeaponOrbitController threat;
    private WeaponOrbitController prey;

    private float threatRange;
    private Vector2 spawnPosition;
    private Vector2 wanderTarget;
    private Intent intent;
    private float wanderPauseUntil;
    private float nextDecisionTime;

    public Vector2 SpeedFactorRange
    {
        get => speedFactorRange;
        set => speedFactorRange = value;
    }

    public static EnemyAI TakeOver(GameObject body)
    {
        if (body == null) return null;

        PlayerInput input = body.GetComponent<PlayerInput>();
        if (input != null) input.enabled = false;

        EnemyAI existing = body.GetComponent<EnemyAI>();
        return existing != null ? existing : body.AddComponent<EnemyAI>();
    }

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        orbit = GetComponent<WeaponOrbitController>();
        spawnPosition = transform.position;
        wanderTarget = spawnPosition;
        threatRange = dangerRange;
    }

    void Start()
    {
        RollSpeed();

        if (!movement.enabled)
        {
            Debug.LogWarning(
                "Player Movement is disabled on this enemy, so it cannot move. " +
                "Enable it and leave Player Input off instead.",
                this
            );
        }
    }

    void Update()
    {
        if (Time.time >= nextDecisionTime)
        {
            nextDecisionTime = Time.time + decisionInterval;
            ChooseIntent();
        }

        movement.SetMoveInput(Steer());
    }

    private void ChooseIntent()
    {
        SurveyOpponents(
            out float threatDistance,
            out float preyDistance
        );

        if (orbit.WeaponCount == 0)
        {
            targetPickup =
                FindNearestPickup(float.MaxValue);

            threatRange = dangerRange;

            if (threatDistance <= dangerRange)
                intent = Intent.Retreat;
            else if (targetPickup != null)
                intent = Intent.SeekWeapon;
            else
                intent = Intent.Wander;

            return;
        }

        Transform underfoot =
            FindNearestPickup(snatchRange);

        if (underfoot != null)
        {
            targetPickup = underfoot;
            intent = Intent.SeekWeapon;
            return;
        }

        if (threatDistance <= chaseRange)
        {
            targetPickup =
                FindNearestPickup(float.MaxValue);

            threatRange = chaseRange;
            intent = Intent.Retreat;
            return;
        }

        if (preyDistance <= chaseRange)
        {
            intent = Intent.Chase;
            return;
        }

        targetPickup =
            FindNearestPickup(grabRange);

        intent =
            targetPickup != null
                ? Intent.SeekWeapon
                : Intent.Wander;
    }

    private void SurveyOpponents(
        out float threatDistance,
        out float preyDistance
    )
    {
        threat = null;
        prey = null;
        threatDistance = float.MaxValue;
        preyDistance = float.MaxValue;

        int mine = orbit.WeaponCount;
        Vector2 position = transform.position;

        foreach (
            WeaponOrbitController other
            in WeaponOrbitController.Active
        )
        {
            if (
                other == null ||
                other == orbit
            )
            {
                continue;
            }

            bool dangerous =
                mine == 0
                    ? other.WeaponCount > 0
                    : other.WeaponCount - mine >= outgunnedBy;

            float distance =
                Vector2.Distance(
                    position,
                    other.transform.position
                );

            if (dangerous)
            {
                if (distance >= threatDistance)
                    continue;

                threatDistance = distance;
                threat = other;
            }
            else if (
                mine > 0 &&
                distance < preyDistance
            )
            {
                preyDistance = distance;
                prey = other;
            }
        }
    }

    private void RollSpeed()
    {
        Transform player = FindPlayer();

        if (
            player == null ||
            !player.TryGetComponent(
                out PlayerMovement theirs
            )
        )
        {
            return;
        }

        float factor =
            Mathf.Clamp(
                Random.Range(
                    speedFactorRange.x,
                    speedFactorRange.y
                ),
                0.1f,
                SlowestPlayerFraction
            );

        movement.MoveSpeed =
            theirs.MoveSpeed * factor;
    }

    private Vector2 Steer()
    {
        switch (intent)
        {
            case Intent.Chase:
                return prey != null
                    ? Direction(prey.transform.position)
                    : Vector2.zero;

            case Intent.SeekWeapon:
                return targetPickup != null
                    ? Direction(targetPickup.position)
                    : Vector2.zero;

            case Intent.Retreat:
                return RetreatDirection();

            default:
                return WanderDirection();
        }
    }

    private Vector2 RetreatDirection()
    {
        if (threat == null)
            return Vector2.zero;

        Vector2 away =
            (Vector2)transform.position -
            (Vector2)threat.transform.position;

        float distance =
            away.magnitude;

        away =
            distance > 0.001f
                ? away / distance
                : Random.insideUnitCircle.normalized;

        if (targetPickup == null)
            return away;

        float urgency =
            Mathf.Clamp01(
                1f -
                distance /
                Mathf.Max(
                    threatRange,
                    0.001f
                )
            );

        Vector2 blended =
            Vector2.Lerp(
                Direction(targetPickup.position),
                away,
                urgency
            );

        return blended.sqrMagnitude > 0.01f
            ? blended.normalized
            : away;
    }

    private Vector2 WanderDirection()
    {
        if (Time.time < wanderPauseUntil)
            return Vector2.zero;

        Vector2 offset =
            wanderTarget -
            (Vector2)transform.position;

        if (
            offset.magnitude >
            wanderArrivalDistance
        )
        {
            return offset.normalized;
        }

        wanderPauseUntil =
            Time.time +
            Random.Range(
                wanderPauseRange.x,
                wanderPauseRange.y
            );

        wanderTarget =
            spawnPosition +
            Random.insideUnitCircle *
            wanderRadius;

        return Vector2.zero;
    }

    private Transform FindNearestPickup(
        float maxDistance
    )
    {
        Transform nearest = null;

        float nearestSqr =
            maxDistance * maxDistance;

        Vector2 position =
            transform.position;

        foreach (
            WeaponPickup pickup
            in WeaponPickup.Active
        )
        {
            if (pickup == null)
                continue;

            float sqr =
                (
                    (Vector2)pickup.transform.position -
                    position
                ).sqrMagnitude;

            if (sqr >= nearestSqr)
                continue;

            nearestSqr = sqr;
            nearest = pickup.transform;
        }

        return nearest;
    }

    private Vector2 Direction(
        Vector3 destination
    )
    {
        Vector2 offset =
            (Vector2)destination -
            (Vector2)transform.position;

        return offset.sqrMagnitude > 0.0001f
            ? offset.normalized
            : Vector2.zero;
    }

    private Transform FindPlayer()
    {
        foreach (
            PlayerMovement candidate
            in FindObjectsByType<PlayerMovement>()
        )
        {
            if (
                candidate.GetComponent<EnemyAI>() ==
                null
            )
            {
                return candidate.transform;
            }
        }

        Debug.LogWarning(
            "No player found, so this enemy keeps the speed its body was built with. " +
            "It still fights whoever else is in the arena.",
            this
        );

        return null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color =
            new Color(
                1f,
                0.4f,
                0.3f,
                0.5f
            );

        Gizmos.DrawWireSphere(
            transform.position,
            chaseRange
        );

        Gizmos.color =
            new Color(
                1f,
                0.9f,
                0.2f,
                0.5f
            );

        Gizmos.DrawWireSphere(
            transform.position,
            dangerRange
        );

        Gizmos.color =
            new Color(
                0.3f,
                0.8f,
                1f,
                0.5f
            );

        Gizmos.DrawWireSphere(
            transform.position,
            grabRange
        );

        Gizmos.color =
            new Color(
                0.6f,
                1f,
                0.4f,
                0.7f
            );

        Gizmos.DrawWireSphere(
            transform.position,
            snatchRange
        );

        Gizmos.color =
            new Color(
                0.5f,
                1f,
                0.5f,
                0.4f
            );

        Gizmos.DrawWireSphere(
            Application.isPlaying
                ? spawnPosition
                : (Vector2)transform.position,
            wanderRadius
        );
    }
}
