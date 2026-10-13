using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class CharacterNameplate : MonoBehaviour
{
    [Tooltip("Left empty, the nameplate finds the stats on the body it hangs off.")]
    [SerializeField] private CharacterStats stats;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private Image flagImage;

    [Tooltip("Hide the flag rather than leaving a white box when a character has none.")]
    [SerializeField] private bool hideFlagWhenEmpty = true;

    void Awake()
    {
        if (stats == null) stats = GetComponentInParent<CharacterStats>();
    }

    void OnEnable()
    {
        if (stats != null) stats.CharacterChanged += OnCharacterChanged;
    }

    void OnDisable()
    {
        if (stats != null) stats.CharacterChanged -= OnCharacterChanged;
    }


    void Start()
    {
        Refresh();
    }

    private void OnCharacterChanged(CharacterDefinition definition) => Refresh();

    public void Refresh()
    {
        if (stats == null) return;

        if (nameLabel != null) nameLabel.text = stats.DisplayName;
        if (flagImage == null) return;

        flagImage.sprite = stats.Flag;
        if (hideFlagWhenEmpty) flagImage.enabled = stats.Flag != null;
    }
}
