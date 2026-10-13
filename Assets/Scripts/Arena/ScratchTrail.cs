using UnityEngine;

// Carves the arena card along the path this transform sweeps. A blade cuts along
// its whole length, so the stroke is drawn across the blade rather than at its
// pivot, and repeated between last frame's pose and this one whenever the tip
// outruns the brush.
//
// Runs after the orbit has placed the blade for the frame, so the cut lands where
// the sprite is drawn rather than a frame behind it.
[DefaultExecutionOrder(100)]
public class ScratchTrail : MonoBehaviour
{
    [Tooltip("Ends of the cutting edge along the object's local X axis. Leave both at zero to trail from the pivot.")]
    [SerializeField] private float edgeInner;
    [SerializeField] private float edgeOuter;

    [Tooltip("Shifts the whole cut off the pivot, in the object's own space. For a blade drawn with its " +
             "edge up one side of the sprite rather than through the middle: y lifts the cut across the " +
             "blade, x slides it along. Turns with the blade, so a stroke stays on the edge it was lined " +
             "up with however the weapon is swinging.")]
    [SerializeField] private Vector2 offset;

    [SerializeField] private float brushScale = 1f;
    [SerializeField, Range(0f, 1f)] private float pressure = 1f;

    [Tooltip("Movement beyond this in one frame is treated as a teleport rather than a sweep.")]
    [SerializeField] private float teleportDistance = 5f;
    [SerializeField] private int maxSweepSteps = 16;

    private Vector3 previousInner;
    private Vector3 previousOuter;
    private bool hasPrevious;

    void LateUpdate()
    {
        ArenaScratchCard card = ArenaScratchCard.Instance;
        if (card == null || !card.Ready)
        {
            // Whatever ground was covered while there was no card to cut is not a
            // stroke this one owes, so the next frame starts a fresh trail.
            hasPrevious = false;
            return;
        }

        Vector3 inner = EdgePoint(edgeInner);
        Vector3 outer = EdgePoint(edgeOuter);

        float travel = hasPrevious
            ? Mathf.Max(Vector3.Distance(previousInner, inner), Vector3.Distance(previousOuter, outer))
            : 0f;

        // A spawn, a respawn, or a blade snapping into a new formation is a jump
        // rather than a sweep; dragging the brush across one would slash the card.
        if (!hasPrevious || travel > teleportDistance)
        {
            card.ScratchLine(inner, outer, brushScale, pressure);
        }
        else
        {
            Sweep(card, inner, outer, travel);
        }

        previousInner = inner;
        previousOuter = outer;
        hasPrevious = true;
    }

    // Where one end of the cutting edge is in the world this frame. The offset is
    // applied before the transform rather than after, which is what keeps it fixed
    // to the blade instead of to the arena.
    private Vector3 EdgePoint(float alongEdge)
    {
        return transform.TransformPoint(new Vector3(alongEdge + offset.x, offset.y, 0f));
    }

    private void Sweep(ArenaScratchCard card, Vector3 inner, Vector3 outer, float travel)
    {
        // One stroke per half brush width of travel: any fewer leaves gaps between
        // frames, any more just redraws pixels that are already clear.
        float stride = card.GetBrushWorldSize(brushScale) * 0.5f;
        int steps = stride > 0f ? Mathf.CeilToInt(travel / stride) : 1;
        steps = Mathf.Clamp(steps, 1, maxSweepSteps);

        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            card.ScratchLine(Vector3.Lerp(previousInner, inner, t), Vector3.Lerp(previousOuter, outer, t),
                brushScale, pressure);
        }
    }

    // The line that actually gets cut, drawn over the sprite it is meant to follow.
    // An offset is dialled in by eye against the blade's art, and numbers in the
    // inspector are a poor way to find out you are cutting a centimetre off it.
    void OnDrawGizmosSelected()
    {
        Vector3 inner = EdgePoint(edgeInner);
        Vector3 outer = EdgePoint(edgeOuter);

        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
        Gizmos.DrawLine(inner, outer);
        Gizmos.DrawWireSphere(inner, 0.04f);
        Gizmos.DrawWireSphere(outer, 0.04f);
    }
}
