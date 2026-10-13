using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Roster", menuName = "Loopcase/Character Roster")]
public class CharacterRoster : ScriptableObject
{
    [Tooltip("The countries a body can turn up as. Each carries its own pool of first names.")]
    [SerializeField] private CharacterDefinition[] characters;

    [Header("Nicknames")]
    [Tooltip("The number stuck on the end of a nick. 1 to 999 gives ahmet7 as readily as ahmet123.")]
    [SerializeField] private Vector2Int numberRange = new Vector2Int(1, 999);

    [Tooltip("Nicks read as typed-in handles rather than as names, which is the difference between " +
             "ahmet123 and Ahmet123.")]
    [SerializeField] private bool lowercase = true;

    [Tooltip("Rolls tried before a nick is forced unique by counting up. Only ever reached once the " +
             "pool is genuinely crowded.")]
    [SerializeField] private int rollAttempts = 24;
    private readonly Dictionary<string, CharacterDefinition> claimed =
        new Dictionary<string, CharacterDefinition>();

    public int Count => characters != null ? characters.Length : 0;
    public int ClaimedCount => claimed.Count;

    public bool TryClaim(out CharacterDefinition character, out string nickname)
    {
        character = null;
        nickname = null;

        if (characters == null || characters.Length == 0)
        {
            Debug.LogWarning($"{name} has no characters on it, so nobody can be drawn from it. Drop the " +
                             "country assets onto its Characters list.", this);
            return false;
        }

        character = PickCountry();
        if (character == null)
        {
            Debug.LogWarning($"{name} has nothing but empty slots in its Characters list.", this);
            return false;
        }

        nickname = BuildNickname(character);
        if (nickname == null) return false;

        claimed.Add(nickname, character);
        return true;
    }
    private CharacterDefinition PickCountry()
    {
        CharacterDefinition pick = null;
        int fewest = int.MaxValue;
        int tied = 0;

        foreach (CharacterDefinition candidate in characters)
        {
            if (candidate == null) continue;

            int uses = UseCount(candidate);
            if (uses < fewest)
            {
                fewest = uses;
                tied = 1;
                pick = candidate;
            }
            else if (uses == fewest && Random.Range(0, ++tied) == 0)
            {
                pick = candidate;
            }
        }

        return pick;
    }

    private int UseCount(CharacterDefinition character)
    {
        int count = 0;
        foreach (CharacterDefinition claimant in claimed.Values)
        {
            if (claimant == character) count++;
        }
        return count;
    }


    private string BuildNickname(CharacterDefinition character)
    {
        IReadOnlyList<string> pool = character.Nicknames;
        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning($"{character.name} has no nicknames on it, so a body from there cannot be " +
                             "named. Add some first names to its Nicknames list.", this);
            return null;
        }

        int low = Mathf.Min(numberRange.x, numberRange.y);
        int high = Mathf.Max(numberRange.x, numberRange.y);

        for (int attempt = 0; attempt < Mathf.Max(1, rollAttempts); attempt++)
        {
            string nick = Compose(pool[Random.Range(0, pool.Count)], Random.Range(low, high + 1));
            if (!claimed.ContainsKey(nick)) return nick;
        }

        string basis = Compose(pool[Random.Range(0, pool.Count)], Random.Range(low, high + 1));
        int suffix = 2;
        while (claimed.ContainsKey(basis + suffix)) suffix++;
        return basis + suffix;
    }

    private string Compose(string firstName, int number)
    {
        string basis = firstName == null ? string.Empty : firstName.Trim();
        if (lowercase) basis = basis.ToLowerInvariant();

        return basis + number;
    }

    public void Release(string nickname)
    {
        if (!string.IsNullOrEmpty(nickname)) claimed.Remove(nickname);
    }

    [ContextMenu("Release All")]
    public void ReleaseAll() => claimed.Clear();
}
