using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public sealed class EndRunResultsUI : MonoBehaviour
{
    private GameObject root;
    private Text titleText;
    private Text statsText;
    private Button restartButton;

    public void ShowResults(int completedLevels, int descentMeters)
    {
        EnsureUI();
        if (titleText != null) titleText.text = "Вы погибли";
        if (statsText != null) statsText.text = $"Пройдено уровней: {completedLevels}\nГлубина спуска: {descentMeters} м";
        if (root != null) root.SetActive(true);
    }

    private void EnsureUI()
    {
        if (root != null) return;

        GameObject canvasObject = new("EndRunResults Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panel = new("EndRun Panel");
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.25f, 0.25f);
        panelRect.anchorMax = new Vector2(0.75f, 0.75f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.85f);

        GameObject titleObj = new("Title");
        titleObj.transform.SetParent(panel.transform, false);
        Text title = titleObj.AddComponent<Text>();
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title.fontSize = 28;
        title.alignment = TextAnchor.UpperCenter;
        title.color = Color.white;
        RectTransform tRect = title.GetComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0.05f, 0.62f);
        tRect.anchorMax = new Vector2(0.95f, 0.95f);

        GameObject statsObj = new("Stats");
        statsObj.transform.SetParent(panel.transform, false);
        Text stats = statsObj.AddComponent<Text>();
        stats.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        stats.fontSize = 18;
        stats.alignment = TextAnchor.MiddleCenter;
        stats.color = Color.white;
        RectTransform sRect = stats.GetComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0.05f, 0.28f);
        sRect.anchorMax = new Vector2(0.95f, 0.62f);

        GameObject restartObj = new("Restart Button");
        restartObj.transform.SetParent(panel.transform, false);
        Button btn = restartObj.AddComponent<Button>();
        Image btnImg = restartObj.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.6f, 1f, 1f);
        RectTransform bRect = restartObj.GetComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0.32f, 0.05f);
        bRect.anchorMax = new Vector2(0.68f, 0.22f);
        bRect.offsetMin = Vector2.zero;
        bRect.offsetMax = Vector2.zero;

        GameObject btnTextObj = new("Restart Text");
        btnTextObj.transform.SetParent(restartObj.transform, false);
        Text btnText = btnTextObj.AddComponent<Text>();
        btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btnText.text = "Restart (R)";
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.color = Color.white;
        RectTransform btRect = btnText.GetComponent<RectTransform>();
        btRect.anchorMin = Vector2.zero;
        btRect.anchorMax = Vector2.one;

        btn.onClick.AddListener(() => RestartRun());

        root = panel;
        titleText = title;
        statsText = stats;
        restartButton = btn;
        root.SetActive(false);
    }

    private void RestartRun()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void Update()
    {
        if (root != null && root.activeSelf && Input.GetKeyDown(KeyCode.R))
        {
            RestartRun();
        }
    }

    public static EndRunResultsUI EnsureInScene()
    {
        EndRunResultsUI existing = FindAnyObjectByType<EndRunResultsUI>();
        if (existing != null) return existing;
        GameObject go = new GameObject("EndRunResultsUI");
        return go.AddComponent<EndRunResultsUI>();
    }
}
