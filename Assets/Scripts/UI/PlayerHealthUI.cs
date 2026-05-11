using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerHealthUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Text healthText;
    [SerializeField] private Image healthBar;
    [SerializeField] private Image damageFlash;
    [SerializeField] private Slider healthSlider;
    [Header("Damage Flash")]
    [SerializeField, Min(0f)] private float minDamageToFlash = 2f;

    private PlayerHealth observed;

    private void Awake()
    {
        if (damageFlash != null) damageFlash.color = new Color(1f, 0f, 0f, 0f);
    }

    private void Update()
    {
        if (damageFlash != null && damageFlash.color.a > 0f)
        {
            Color c = damageFlash.color;
            c.a = Mathf.MoveTowards(c.a, 0f, Time.deltaTime * 2f);
            damageFlash.color = c;
        }
    }

    public void RegisterHealth(PlayerHealth health)
    {
        if (observed != null) observed.OnHealthChanged -= OnHealthChanged;
        observed = health;
        if (observed != null)
        {
            observed.OnHealthChanged += OnHealthChanged;
            OnHealthChanged(observed.CurrentHealth);
            observed.OnDamaged += OnDamaged;
        }
    }

    private void OnHealthChanged(float current)
    {
        if (healthText != null)
        {
            healthText.text = $"Health: {Mathf.CeilToInt(current)}";
        }
        if (healthBar != null && observed != null)
        {
            healthBar.fillAmount = Mathf.Clamp01(observed.CurrentHealth / observed.MaxHealth);
        }
    }

    private void OnDamaged(float amount, DamageType type, GameObject source)
    {
        if (damageFlash == null) return;

        if (amount < minDamageToFlash) return;

        damageFlash.color = new Color(1f, 0f, 0f, 0.6f);
    }

    public static PlayerHealthUI EnsureInScene()
    {
        PlayerHealthUI existing = FindAnyObjectByType<PlayerHealthUI>();
        if (existing != null) return existing;

        GameObject canvasObject = new("PlayerHealth Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panel = new("PlayerHealth Panel");
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.02f, 0.92f);
        panelRect.anchorMax = new Vector2(0.35f, 0.995f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image barBg = panel.AddComponent<Image>();
        barBg.color = new Color(0f, 0f, 0f, 0.5f);

        GameObject healthBarObj = new("Health Bar");
        healthBarObj.transform.SetParent(panel.transform, false);
        Image hb = healthBarObj.AddComponent<Image>();
        hb.color = Color.red;
        hb.type = Image.Type.Filled;
        hb.fillMethod = Image.FillMethod.Horizontal;
        hb.fillAmount = 1f;
        RectTransform hbRect = healthBarObj.GetComponent<RectTransform>();
        hbRect.anchorMin = new Vector2(0.02f, 0.3f);
        hbRect.anchorMax = new Vector2(0.98f, 0.7f);
        hbRect.offsetMin = Vector2.zero;
        hbRect.offsetMax = Vector2.zero;

        GameObject textObj = new("Health Text");
        textObj.transform.SetParent(panel.transform, false);
        Text t = textObj.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.fontSize = 16;
        RectTransform tRect = t.GetComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0f, 0f);
        tRect.anchorMax = new Vector2(1f, 1f);
        tRect.offsetMin = Vector2.zero;
        tRect.offsetMax = Vector2.zero;

        GameObject flash = new("Damage Flash");
        flash.transform.SetParent(canvasObject.transform, false);
        Image flashImage = flash.AddComponent<Image>();
        flashImage.color = new Color(1f, 0f, 0f, 0f);
        RectTransform flashRect = flash.GetComponent<RectTransform>();
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.offsetMin = Vector2.zero;
        flashRect.offsetMax = Vector2.zero;

        PlayerHealthUI ui = canvasObject.AddComponent<PlayerHealthUI>();
        ui.root = panel;
        ui.healthText = t;
        
        ui.healthBar = hb;
        ui.damageFlash = flashImage;
        return ui;
    }
}
