using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "NewCharacter", menuName = "Loopcase/Character")]
public class CharacterDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string displayName = "Unnamed";
    [Tooltip("Country flag drawn on the nameplate. Sliced sprite, not a raw texture.")]
    [SerializeField] private Sprite flag;

    [Tooltip("First names a body from here plays under. The roster picks one and sticks a number on " +
             "the end of it, so this list is people rather than handles: ahmet, not ahmet123.")]
    [SerializeField] private string[] nicknames;

    [Header("Vitals")]
    [SerializeField] private int maxHealth = 3;


    public string DisplayName => displayName;
    public Sprite Flag => flag;

    public IReadOnlyList<string> Nicknames => nicknames;
    public int MaxHealth => maxHealth;
}
