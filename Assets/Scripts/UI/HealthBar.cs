using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{

    public CharacterStats target;
    public Slider healthSilder;
    public Slider easeHealthSlider;

    [Tooltip("At or below this many hit points the fill turns to lowHealthColour.")]
    [SerializeField] private int lowHealthThreshold = 1;
    [SerializeField] private Color lowHealthColour = new Color(0.85f, 0.15f, 0.15f, 1f);

    [Tooltip("At or below this many hit points, but above lowHealthThreshold, the fill turns to midHealthColour.")]
    [SerializeField] private int midHealthThreshold = 2;
    [SerializeField] private Color midHealthColour = new Color(0.95f, 0.55f, 0.1f, 1f);


    [SerializeField] private Image fillImage;

    private float lerpSpeed = 0.1f;


    private Color fullHealthColour = Color.white;


    void Awake()
    {
        if (fillImage == null && healthSilder != null && healthSilder.fillRect != null)
        {
            fillImage = healthSilder.fillRect.GetComponent<Image>();
        }

        if (fillImage != null) fullHealthColour = fillImage.color;
    }

    void OnEnable()
    {
        if (target != null) target.HealthChanged += UpdateBar;
    }

    void OnDisable()
    {
        if (target != null) target.HealthChanged -= UpdateBar;
    }


    void Start()
    {
        if (target == null) return;

        UpdateBar(target.CurrentHealth, target.MaxHealth);
        easeHealthSlider.value = target.CurrentHealth;
    }

    void Update()
    {
        if (healthSilder.value != easeHealthSlider.value)
        {
            easeHealthSlider.value = Mathf.Lerp(easeHealthSlider.value, healthSilder.value, lerpSpeed);
        }
        
    }

    public void UpdateBar(int current, int max)
    {
        healthSilder.maxValue = max;
        healthSilder.value = current;
        easeHealthSlider.maxValue = max;

        if (fillImage == null) return;


        if (current <= lowHealthThreshold) fillImage.color = lowHealthColour;
        else if (current <= midHealthThreshold) fillImage.color = midHealthColour;
        else fillImage.color = fullHealthColour;
    }
}
