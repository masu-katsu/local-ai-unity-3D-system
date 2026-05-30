using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 起動時に3DモデルメニューUIを動的に生成する。
/// </summary>
[DefaultExecutionOrder(-200)]
public class UISetupBootstrap : MonoBehaviour
{
    private bool uiCreated = false;

    private void Awake()
    {
        // 既存のUICanvasがあれば処理をスキップ
        if (FindObjectOfType<Canvas>() != null || uiCreated)
            return;

        CreateMenuUI();
        uiCreated = true;
    }

    private void CreateMenuUI()
    {
        // Canvas を作成
        GameObject canvasObj = new GameObject("UICanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GraphicRaycaster raycaster = canvasObj.AddComponent<GraphicRaycaster>();

        // CanvasGroup を追加（透明度制御用）
        CanvasGroup canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;

        // パネルを作成（背景）
        GameObject panelObj = new GameObject("Panel");
        panelObj.transform.SetParent(canvasObj.transform, false);

        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // 3D model メニューテキスト
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(panelObj.transform, false);

        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "3D model";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 40;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;

        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchoredPosition = new Vector2(0f, 150f);
        titleRect.sizeDelta = new Vector2(400f, 100f);

        // build ボタン
        GameObject buildButtonObj = new GameObject("BuildButton");
        buildButtonObj.transform.SetParent(panelObj.transform, false);

        Image buttonImage = buildButtonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.5f, 0.8f, 1f);

        Button buildButton = buildButtonObj.AddComponent<Button>();
        ColorBlock colorBlock = buildButton.colors;
        colorBlock.normalColor = new Color(0.2f, 0.5f, 0.8f, 1f);
        colorBlock.highlightedColor = new Color(0.3f, 0.6f, 0.9f, 1f);
        colorBlock.pressedColor = new Color(0.1f, 0.4f, 0.7f, 1f);
        buildButton.colors = colorBlock;

        RectTransform buttonRect = buildButtonObj.GetComponent<RectTransform>();
        buttonRect.anchoredPosition = new Vector2(0f, -80f);
        buttonRect.sizeDelta = new Vector2(300f, 80f);

        // ボタンテキスト
        GameObject buttonTextObj = new GameObject("Text");
        buttonTextObj.transform.SetParent(buildButtonObj.transform, false);

        Text buttonText = buttonTextObj.AddComponent<Text>();
        buttonText.text = "build";
        buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        buttonText.fontSize = 32;
        buttonText.fontStyle = FontStyle.Bold;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.color = Color.white;

        RectTransform buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;

        // UIManager を追加
        GameObject uiManagerObj = new GameObject("UIManager");
        uiManagerObj.transform.SetParent(canvasObj.transform, false);

        UIManager uiManager = uiManagerObj.AddComponent<UIManager>();
        uiManager.SetReferences(canvasGroup, buildButton);

        // ボタンクリック時の処理
        buildButton.onClick.AddListener(() => uiManager.OnBuild3DClicked());

        Debug.Log("[UIセットアップ] 3D model メニューUIを生成しました。");
    }
}
