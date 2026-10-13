using Unity.Cinemachine;
using UnityEngine;
public class KillCam : MonoBehaviour
{
    [Tooltip("Left empty, the Cinemachine camera on this object.")]
    [SerializeField] private CinemachineCamera cam;

    [Tooltip("Hand over again when the new body is killed in its turn, so the view is never left on " +
             "someone who has stopped moving. Off, it follows the player's killer and no further.")]
    [SerializeField] private bool followEveryKiller = true;

    private CharacterStats watched;

    void Awake()
    {
        if (cam == null) cam = GetComponent<CinemachineCamera>();
    }

    void Start()
    {
        if (cam == null)
        {
            Debug.LogWarning("Kill Cam has no Cinemachine camera to hand over, so a death will leave the " +
                             "view where it was. Put this on the CmCam object, or fill in Cam.", this);
            enabled = false;
            return;
        }

        Watch(cam.Follow);
    }

    void OnDestroy() => Watch(null);

    private void Watch(Transform body)
    {
        if (watched != null) watched.Died -= OnWatchedDied;

        watched = body != null ? body.GetComponent<CharacterStats>() : null;

        if (watched != null) watched.Died += OnWatchedDied;
    }

    private void OnWatchedDied()
    {
        CharacterStats killer = watched != null ? watched.Killer : null;

        if (killer == null) return;

        cam.Follow = killer.transform;

        Watch(followEveryKiller ? killer.transform : null);
    }
}
