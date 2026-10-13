using UnityEngine;


[RequireComponent(typeof(CharacterStats))]
public class RandomIdentity : MonoBehaviour
{
    [Tooltip("The pool drawn from. Left empty, the body keeps the country and name it was built with.")]
    [SerializeField] private CharacterRoster roster;

    [Tooltip("Off, nothing happens until something calls Assign -- for a select screen that picks the " +
             "player's country itself.")]
    [SerializeField] private bool assignOnAwake = true;

    private CharacterStats stats;
    private string nickname;

    public string Nickname => nickname;


    void Awake()
    {
        stats = GetComponent<CharacterStats>();
        if (assignOnAwake) Assign();
    }

    [ContextMenu("Assign")]
    public void Assign()
    {
        if (roster == null) return;
        Release();

        if (!roster.TryClaim(out CharacterDefinition character, out string drawn)) return;

        nickname = drawn;
        if (stats == null) stats = GetComponent<CharacterStats>();


        stats.SetCharacter(character);
        stats.SetNickname(drawn);
    }

    void OnDestroy() => Release();

    private void Release()
    {
        if (nickname == null) return;

        if (roster != null) roster.Release(nickname);
        nickname = null;
    }
}
