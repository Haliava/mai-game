using UnityEngine;
using UnityEngine.UI;

public sealed class LevelProgressionUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Text levelsText;
    [SerializeField] private Text depthText;

    private void Awake()
    {
        // noop
    }

    public void UpdateProgress(int completedLevels, float depthMeters)
    {
        if (levelsText != null) levelsText.text = $"Уровни пройдены: {completedLevels}";
        if (depthText != null) depthText.text = $"Глубина: {Mathf.FloorToInt(depthMeters)} м";
    }

    public static LevelProgressionUI EnsureInScene()
    {
        LevelProgressionUI existing = FindAnyObjectByType<LevelProgressionUI>();
        if (existing != null) return existing;

        GameObject canvasObject = new("LevelProgression Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panel = new("LevelProgression Panel");
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.02f, 0.86f);
        panelRect.anchorMax = new Vector2(0.28f, 0.995f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.45f);

        GameObject levelsObj = new("Levels Text");
        levelsObj.transform.SetParent(panel.transform, false);
        Text levels = levelsObj.AddComponent<Text>();
        levels.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        levels.fontSize = 16;
        levels.alignment = TextAnchor.UpperLeft;
        levels.color = Color.white;
        RectTransform lRect = levels.GetComponent<RectTransform>();
        lRect.anchorMin = new Vector2(0.02f, 0.55f);
        lRect.anchorMax = new Vector2(0.98f, 0.95f);
        lRect.offsetMin = Vector2.zero;
        lRect.offsetMax = Vector2.zero;

        GameObject depthObj = new("Depth Text");
        depthObj.transform.SetParent(panel.transform, false);
        Text depth = depthObj.AddComponent<Text>();
        depth.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        depth.fontSize = 14;
        depth.alignment = TextAnchor.LowerLeft;
        depth.color = Color.white;
        RectTransform dRect = depth.GetComponent<RectTransform>();
        dRect.anchorMin = new Vector2(0.02f, 0.02f);
        dRect.anchorMax = new Vector2(0.98f, 0.55f);
        dRect.offsetMin = Vector2.zero;
        dRect.offsetMax = Vector2.zero;

        LevelProgressionUI ui = canvasObject.AddComponent<LevelProgressionUI>();
        ui.root = panel;
        ui.levelsText = levels;
        ui.depthText = depth;
        ui.UpdateProgress(0, 0f);
        return ui;
    }
}
