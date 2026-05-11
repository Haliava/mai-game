using UnityEngine;
using UnityEngine.UI;

public sealed class LevelProgressionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject root;
    [SerializeField] private Text levelsText;
    [SerializeField] private Text depthText;

    [Header("Compact Layout")]
    [SerializeField] private bool forceCompactLayout = true;
    [SerializeField] private Vector2 panelAnchorMin = new Vector2(0.02f, 0.735f);
    [SerializeField] private Vector2 panelAnchorMax = new Vector2(0.30f, 0.875f);
    [SerializeField] private float paddingX = 14f;
    [SerializeField] private float topPadding = 11.5f;
    [SerializeField] private float lineHeight = 22f;
    [SerializeField] private float lineGap = 2f;
    [SerializeField] private int levelsFontSize = 14;
    [SerializeField] private int depthFontSize = 14;

    private void Awake()
    {
        ApplyCompactLayout();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            ApplyCompactLayout();
        }
    }
#endif

    public void UpdateProgress(int completedLevels, float depthMeters)
    {
        Debug.Log($"LevelProgressionUI: UpdateProgress levels={completedLevels}, meters={Mathf.FloorToInt(depthMeters)}");

        if (levelsText != null)
        {
            levelsText.text = $"Этаж: {completedLevels + 1}";
        }

        if (depthText != null)
        {
            depthText.text = $"Глубина: {Mathf.FloorToInt(depthMeters)} м";
        }

        ApplyCompactLayout();
    }

    private void ApplyCompactLayout()
    {
        if (!forceCompactLayout)
        {
            return;
        }

        if (root != null)
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            if (rootRect != null)
            {
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
                rootRect.anchorMin = panelAnchorMin;
                rootRect.anchorMax = panelAnchorMax;
            }
        }

        ConfigureText(levelsText, 0, levelsFontSize);
        ConfigureText(depthText, 1, depthFontSize);
    }

    private void ConfigureText(Text text, int lineIndex, int fontSize)
    {
        if (text == null)
        {
            return;
        }

        text.fontSize = fontSize;
        text.alignment = TextAnchor.UpperLeft;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rect = text.rectTransform;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);

        float y = -(topPadding + lineIndex * (lineHeight + lineGap));

        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-paddingX * 2f, lineHeight);
        rect.offsetMin = new Vector2(paddingX, rect.offsetMin.y);
        rect.offsetMax = new Vector2(-paddingX, rect.offsetMax.y);
    }

    public static LevelProgressionUI EnsureInScene()
    {
        LevelProgressionUI existing = FindAnyObjectByType<LevelProgressionUI>();

        if (existing != null)
        {
            Debug.Log("LevelProgressionUI: found existing UI in scene");
            existing.ApplyCompactLayout();
            return existing;
        }

        GameObject canvasObject = new GameObject("LevelProgression Canvas");

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("LevelProgression Panel");
        panel.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.02f, 0.89f);
        panelRect.anchorMax = new Vector2(0.30f, 0.985f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.45f);

        GameObject levelsObj = new GameObject("Levels Text");
        levelsObj.transform.SetParent(panel.transform, false);

        Text levels = levelsObj.AddComponent<Text>();
        levels.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        levels.text = "Этаж: 1";

        GameObject depthObj = new GameObject("Depth Text");
        depthObj.transform.SetParent(panel.transform, false);

        Text depth = depthObj.AddComponent<Text>();
        depth.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        depth.text = "Глубина: 0 м";

        LevelProgressionUI ui = canvasObject.AddComponent<LevelProgressionUI>();
        ui.root = panel;
        ui.levelsText = levels;
        ui.depthText = depth;

        ui.ApplyCompactLayout();

        Debug.Log("LevelProgressionUI: created UI via EnsureInScene");

        return ui;
    }
}