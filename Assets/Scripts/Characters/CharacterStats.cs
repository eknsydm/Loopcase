using System;
using UnityEngine;


public class CharacterStats : MonoBehaviour
{
    [Tooltip("The CharacterDefinition asset this body is built from. Swapping it swaps the character.")]
    [SerializeField] private CharacterDefinition character;

    [Tooltip("Seconds a landed hit buys before the next one can register. Lives here rather than on the " +
             "definition because it is a rule of the fight, not a trait of a country: two blades that " +
             "meet a body at once should land once whoever is carrying them.")]
    [SerializeField] private float invulnerabilityDuration = 0.1f;

    private int currentHealth;
    private float invulnerableUntil;
    private PlayerMovement movement;
    private CharacterDeath deathAnimation;


    private string nickname;

    private const int FallbackMaxHealth = 3;

    public CharacterDefinition Character => character;


    public string DisplayName => !string.IsNullOrEmpty(nickname)
        ? nickname
        : (character != null ? character.DisplayName : name);
    public Sprite Flag => character != null ? character.Flag : null;
    public string Nickname => nickname;

    public CharacterStats Killer { get; private set; }
    public int MaxHealth => character != null ? character.MaxHealth : FallbackMaxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsAlive => currentHealth > 0;

    public float InvulnerabilityDuration
    {
        get => invulnerabilityDuration;
        set => invulnerabilityDuration = Mathf.Max(0f, value);
    }

    public event Action<int, int> HealthChanged;
    public event Action Died;


    public event Action<Vector2> Hit;
    public event Action<CharacterDefinition> CharacterChanged;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        deathAnimation = GetComponent<CharacterDeath>();
        ApplyCharacter();
    }

    public void SetCharacter(CharacterDefinition definition)
    {
        character = definition;
        ApplyCharacter();
    }


    public void SetNickname(string handle)
    {
        nickname = handle;

        CharacterChanged?.Invoke(character);
    }

    public void ClearNickname() => SetNickname(null);

    private void ApplyCharacter()
    {
        if (character == null)
        {
            Debug.LogWarning($"{name} has no CharacterDefinition, so it falls back to {FallbackMaxHealth} " +
                             "health. Make one with Create > Loopcase > Character and drop it on Character Stats.", this);
        }

        currentHealth = MaxHealth;

        CharacterChanged?.Invoke(character);
        HealthChanged?.Invoke(currentHealth, MaxHealth);
    }

    public void TakeDamage(int amount)
    {
        TakeDamage(amount, transform.position, 0f, null);
    }

    public void TakeDamage(int amount, Vector2 sourcePosition, float knockbackForce)
    {
        TakeDamage(amount, sourcePosition, knockbackForce, null);
    }


    public void TakeDamage(int amount, Vector2 sourcePosition, float knockbackForce, CharacterStats attacker)
    {
        if (!IsAlive || Time.time < invulnerableUntil) return;

        currentHealth = Mathf.Max(currentHealth - amount, 0);
        invulnerableUntil = Time.time + InvulnerabilityDuration;

        HealthChanged?.Invoke(currentHealth, MaxHealth);
        Hit?.Invoke(sourcePosition);

        if (knockbackForce > 0f && movement != null)
        {
            movement.ApplyKnockback((Vector2)transform.position - sourcePosition, knockbackForce);
        }

        if (!IsAlive)
        {
            Killer = attacker;
            Die();
        }
    }

    private void Die()
    {
        Died?.Invoke();


        if (deathAnimation != null && deathAnimation.IsPlaying) return;

        gameObject.SetActive(false);
    }
}
