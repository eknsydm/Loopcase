using UnityEngine;


[RequireComponent(typeof(WeaponPickup))]
public class CollectableMagnet : MonoBehaviour
{
    private enum State
    {
        Waiting,
        Recoiling,
        Chasing
    }

    [Tooltip("How close a collector has to be before the pickup reacts to it.")]
    [SerializeField] private float attractRadius = 5f;

    [Tooltip("How far it shies away before being pulled in.")]
    [SerializeField] private float recoilDistance = 0.5f;
    [SerializeField] private float recoilDuration = 0.12f;

    [SerializeField] private float chaseStartSpeed = 2f;
    [SerializeField] private float chaseAcceleration = 45f;
    [SerializeField] private float chaseMaxSpeed = 25f;

    private State state = State.Waiting;
    private Transform collector;
    private Vector3 recoilFrom;
    private Vector3 recoilTo;
    private float recoilElapsed;
    private float speed;

    private bool HasCollector => collector != null && collector.gameObject.activeInHierarchy;


    void OnEnable()
    {
        state = State.Waiting;
        collector = null;
        speed = 0f;
    }

    void Update()
    {
        switch (state)
        {
            case State.Waiting:
                Wait();
                break;

            case State.Recoiling:
                Recoil();
                break;

            default:
                Chase();
                break;
        }
    }

    private void Wait()
    {
        Transform nearest = FindNearestCollector();
        if (nearest == null) return;

        collector = nearest;
        recoilFrom = transform.position;

        Vector3 away = transform.position - nearest.position;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : (Vector3)Random.insideUnitCircle.normalized;
        recoilTo = recoilFrom + away * recoilDistance;

        recoilElapsed = 0f;
        state = State.Recoiling;
    }

    private void Recoil()
    {

        if (!HasCollector)
        {
            state = State.Waiting;
            collector = null;
            return;
        }

        recoilElapsed += Time.deltaTime;
        float t = recoilDuration > 0f ? Mathf.Clamp01(recoilElapsed / recoilDuration) : 1f;


        transform.position = Vector3.Lerp(recoilFrom, recoilTo, 1f - (1f - t) * (1f - t));

        if (t < 1f) return;

        speed = chaseStartSpeed;
        state = State.Chasing;
    }

    private void Chase()
    {
        if (!HasCollector)
        {
            state = State.Waiting;
            collector = null;
            return;
        }

        speed = Mathf.Min(speed + chaseAcceleration * Time.deltaTime, chaseMaxSpeed);
        transform.position = Vector3.MoveTowards(transform.position, collector.position, speed * Time.deltaTime);
    }


    private Transform FindNearestCollector()
    {
        Transform nearest = null;
        float nearestSqr = attractRadius * attractRadius;
        Vector2 position = transform.position;

        foreach (WeaponOrbitController candidate in WeaponOrbitController.Active)
        {
            if (candidate == null) continue;

            float sqr = ((Vector2)candidate.transform.position - position).sqrMagnitude;
            if (sqr >= nearestSqr) continue;

            nearestSqr = sqr;
            nearest = candidate.transform;
        }

        return nearest;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, attractRadius);
    }
}
