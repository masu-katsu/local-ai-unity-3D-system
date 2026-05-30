#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// メニューから一発でチャットシーンを自動構築するエディタスクリプト
/// Unity メニュー → LocalAI → Build Chat Scene
/// </summary>
public class SceneBuilder
{
    // ===== カラーパレット =====
    static readonly Color BG_DARK      = new Color(0.10f, 0.10f, 0.14f);     // 背景
    static readonly Color HEADER_BG    = new Color(0.13f, 0.13f, 0.19f);     // ヘッダー
    static readonly Color INPUT_BG     = new Color(0.13f, 0.13f, 0.19f);     // 入力エリア
    static readonly Color INPUT_FIELD  = new Color(0.17f, 0.17f, 0.23f);     // 入力フィールド
    static readonly Color USER_BUBBLE  = new Color(0.22f, 0.45f, 0.85f);     // ユーザー吹き出し
    static readonly Color AI_BUBBLE    = new Color(0.20f, 0.20f, 0.28f);     // AI 吹き出し
    static readonly Color ACCENT       = new Color(0.30f, 0.55f, 0.95f);     // アクセント（ボタン）
    static readonly Color TEXT_PRIMARY = new Color(0.93f, 0.93f, 0.95f);     // メインテキスト
    static readonly Color TEXT_SECONDARY = new Color(0.55f, 0.55f, 0.65f);   // サブテキスト
    static readonly Color CONNECTED    = new Color(0.30f, 0.85f, 0.40f);     // 接続OK
    static readonly Color DISCONNECTED = new Color(0.90f, 0.30f, 0.30f);     // 切断

    [MenuItem("LocalAI/Build Chat Scene")]
    public static void BuildScene()
    {
        // ===== 既存の EventSystem を削除（重複防止）=====
        var existingES = Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>();
        foreach (var es in existingES)
            Object.DestroyImmediate(es.gameObject);

        // ===== GameManager =====
        var gameManager = new GameObject("[GameManager]");
        gameManager.AddComponent<ApiClient>();
        gameManager.AddComponent<ChatManager>();
        gameManager.AddComponent<NetworkMonitor>();

        // ===== Canvas =====
        var canvasObj = new GameObject("Canvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();

        // ===== Background =====
        var bgImage = canvasObj.AddComponent<Image>();
        bgImage.color = BG_DARK;

        // ===== Header (高さ 64) =====
        var header = CreatePanel("Header", canvasObj.transform, HEADER_BG);
        SetAnchors(header, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -64), new Vector2(0, 0));
        header.sizeDelta = new Vector2(0, 64);
        header.pivot = new Vector2(0.5f, 1);

        // ヘッダータイトル
        var titleText = CreateTMP("TitleText", header, "Local AI Chat", 24, TextAlignmentOptions.MidlineLeft);
        titleText.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        titleText.GetComponent<TextMeshProUGUI>().color = TEXT_PRIMARY;
        SetAnchors(titleText, new Vector2(0, 0), new Vector2(0.7f, 1), new Vector2(20, 0), new Vector2(0, 0));

        // 接続インジケーター（丸い点）
        var connectionIndicator = CreateImage("ConnectionIndicator", header, DISCONNECTED);
        var ciRect = connectionIndicator.GetComponent<RectTransform>();
        SetAnchors(ciRect, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-108, -6), new Vector2(-96, 6));

        // 設定ボタン
        var settingsBtn = CreateButton("SettingsButton", header, "設定", 15);
        var sbRect = settingsBtn.GetComponent<RectTransform>();
        SetAnchors(sbRect, new Vector2(1, 0.15f), new Vector2(1, 0.85f), new Vector2(-80, 0), new Vector2(-12, 0));
        settingsBtn.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.35f);

        // ===== Message Area =====
        var messageArea = CreatePanel("MessageArea", canvasObj.transform, Color.clear);
        SetAnchors(messageArea, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 68), new Vector2(0, -64));

        // ScrollView
        var scrollViewObj = new GameObject("ScrollView");
        scrollViewObj.transform.SetParent(messageArea, false);
        var scrollRect = scrollViewObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.scrollSensitivity = 30f;
        var svRect = scrollViewObj.GetComponent<RectTransform>();
        svRect.anchorMin = Vector2.zero;
        svRect.anchorMax = Vector2.one;
        svRect.offsetMin = Vector2.zero;
        svRect.offsetMax = Vector2.zero;

        // Viewport — Mask+透明Image はステンシルが空になり子が全クリップされるため RectMask2D を使う
        var viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollViewObj.transform, false);
        var viewport = viewportObj.AddComponent<RectTransform>();
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        viewportObj.AddComponent<RectMask2D>();

        // Content
        var content = new GameObject("Content");
        content.transform.SetParent(viewport, false);
        var contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.offsetMin = new Vector2(0, 0);
        contentRect.offsetMax = new Vector2(0, 0);

        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 12;
        vlg.childControlWidth = true;
        // false: 各行（吹き出し）の LayoutElement 高さをそのまま使い、重なりを防ぐ
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(12, 12, 12, 12);

        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport;
        scrollRect.content = contentRect;

        // ===== Status Text =====
        var statusText = CreateTMP("StatusText", messageArea, "", 13, TextAlignmentOptions.Bottom);
        statusText.GetComponent<TextMeshProUGUI>().color = TEXT_SECONDARY;
        SetAnchors(statusText, new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 2), new Vector2(-12, 20));

        // ===== Typing Indicator =====
        var typingObj = new GameObject("TypingIndicator");
        typingObj.transform.SetParent(messageArea, false);
        var typingRect = typingObj.AddComponent<RectTransform>();
        SetAnchors(typingRect, new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(16, 22), new Vector2(0, 46));
        var typingText = typingObj.AddComponent<TextMeshProUGUI>();
        typingText.text = "AI が考え中...";
        typingText.fontSize = 14;
        typingText.fontStyle = FontStyles.Italic;
        typingText.color = TEXT_SECONDARY;
        typingObj.SetActive(false);

        // ===== Input Area (高さ 68) =====
        var inputArea = CreatePanel("InputArea", canvasObj.transform, INPUT_BG);
        SetAnchors(inputArea, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 68));
        inputArea.pivot = new Vector2(0.5f, 0);
        inputArea.sizeDelta = new Vector2(0, 68);

        // InputField
        var inputFieldObj = new GameObject("InputField");
        inputFieldObj.transform.SetParent(inputArea, false);
        var inputFieldRect = inputFieldObj.AddComponent<RectTransform>();
        SetAnchors(inputFieldRect, new Vector2(0, 0), new Vector2(1, 1), new Vector2(12, 10), new Vector2(-76, -10));
        var inputBg = inputFieldObj.AddComponent<Image>();
        inputBg.color = INPUT_FIELD;

        // InputField Text
        var inputTextObj = new GameObject("Text");
        inputTextObj.transform.SetParent(inputFieldObj.transform, false);
        var inputTextRect = inputTextObj.AddComponent<RectTransform>();
        inputTextRect.anchorMin = Vector2.zero;
        inputTextRect.anchorMax = Vector2.one;
        inputTextRect.offsetMin = new Vector2(14, 4);
        inputTextRect.offsetMax = new Vector2(-14, -4);
        var inputTMP = inputTextObj.AddComponent<TextMeshProUGUI>();
        inputTMP.fontSize = 17;
        inputTMP.color = TEXT_PRIMARY;

        // Placeholder
        var placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(inputFieldObj.transform, false);
        var phRect = placeholderObj.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = new Vector2(14, 4);
        phRect.offsetMax = new Vector2(-14, -4);
        var phTMP = placeholderObj.AddComponent<TextMeshProUGUI>();
        phTMP.text = "メッセージを入力...";
        phTMP.fontSize = 17;
        phTMP.fontStyle = FontStyles.Italic;
        phTMP.color = TEXT_SECONDARY;

        var inputField = inputFieldObj.AddComponent<TMP_InputField>();
        inputField.textComponent = inputTMP;
        inputField.placeholder = phTMP;
        inputField.textViewport = inputTextRect;

        // Send Button
        var sendBtn = CreateButton("SendButton", inputArea, "送信", 16);
        var sendRect = sendBtn.GetComponent<RectTransform>();
        SetAnchors(sendRect, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-68, 10), new Vector2(-12, -10));
        sendBtn.GetComponent<Image>().color = ACCENT;
        sendBtn.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        // ===== Settings Panel =====
        var settingsPanel = CreatePanel("SettingsPanel", canvasObj.transform, new Color(0.11f, 0.11f, 0.16f, 0.98f));
        settingsPanel.anchorMin = Vector2.zero;
        settingsPanel.anchorMax = Vector2.one;
        settingsPanel.offsetMin = Vector2.zero;
        settingsPanel.offsetMax = Vector2.zero;

        float yPos = -80;
        var settingsTitle = CreateTMP("SettingsTitle", settingsPanel, "接続設定", 26, TextAlignmentOptions.TopLeft);
        settingsTitle.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        settingsTitle.GetComponent<TextMeshProUGUI>().color = TEXT_PRIMARY;
        SetAnchors(settingsTitle, new Vector2(0.08f, 1), new Vector2(0.92f, 1), new Vector2(0, yPos - 40), new Vector2(0, yPos));
        yPos -= 60;

        // Server URL
        var urlLabel = CreateTMP("UrlLabel", settingsPanel, "サーバー URL", 15, TextAlignmentOptions.BottomLeft);
        urlLabel.GetComponent<TextMeshProUGUI>().color = TEXT_SECONDARY;
        SetAnchors(urlLabel, new Vector2(0.08f, 1), new Vector2(0.92f, 1), new Vector2(0, yPos - 26), new Vector2(0, yPos));
        yPos -= 30;
        var urlInput = CreateInputField("ServerUrlInput", settingsPanel, "http://192.168.x.x:8000");
        SetAnchors(urlInput, new Vector2(0.08f, 1), new Vector2(0.92f, 1), new Vector2(0, yPos - 48), new Vector2(0, yPos));
        yPos -= 64;

        // API Key
        var keyLabel = CreateTMP("KeyLabel", settingsPanel, "API キー", 15, TextAlignmentOptions.BottomLeft);
        keyLabel.GetComponent<TextMeshProUGUI>().color = TEXT_SECONDARY;
        SetAnchors(keyLabel, new Vector2(0.08f, 1), new Vector2(0.92f, 1), new Vector2(0, yPos - 26), new Vector2(0, yPos));
        yPos -= 30;
        var keyInput = CreateInputField("ApiKeyInput", settingsPanel, "APIキーを入力...");
        SetAnchors(keyInput, new Vector2(0.08f, 1), new Vector2(0.92f, 1), new Vector2(0, yPos - 48), new Vector2(0, yPos));
        yPos -= 64;

        // User ID
        var uidLabel = CreateTMP("UidLabel", settingsPanel, "ユーザー ID", 15, TextAlignmentOptions.BottomLeft);
        uidLabel.GetComponent<TextMeshProUGUI>().color = TEXT_SECONDARY;
        SetAnchors(uidLabel, new Vector2(0.08f, 1), new Vector2(0.92f, 1), new Vector2(0, yPos - 26), new Vector2(0, yPos));
        yPos -= 30;
        var uidInput = CreateInputField("UserIdInput", settingsPanel, "user01");
        SetAnchors(uidInput, new Vector2(0.08f, 1), new Vector2(0.92f, 1), new Vector2(0, yPos - 48), new Vector2(0, yPos));
        yPos -= 74;

        // Buttons
        var testBtn = CreateButton("TestConnectionButton", settingsPanel, "接続テスト", 17);
        SetAnchors(testBtn.GetComponent<RectTransform>(), new Vector2(0.08f, 1), new Vector2(0.92f, 1),
            new Vector2(0, yPos - 50), new Vector2(0, yPos));
        testBtn.GetComponent<Image>().color = ACCENT;
        testBtn.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        yPos -= 62;

        var saveBtn = CreateButton("SaveButton", settingsPanel, "保存", 17);
        SetAnchors(saveBtn.GetComponent<RectTransform>(), new Vector2(0.08f, 1), new Vector2(0.46f, 1),
            new Vector2(0, yPos - 50), new Vector2(0, yPos));
        saveBtn.GetComponent<Image>().color = new Color(0.25f, 0.75f, 0.35f);
        saveBtn.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        var cancelBtn = CreateButton("CancelButton", settingsPanel, "キャンセル", 17);
        SetAnchors(cancelBtn.GetComponent<RectTransform>(), new Vector2(0.54f, 1), new Vector2(0.92f, 1),
            new Vector2(0, yPos - 50), new Vector2(0, yPos));
        cancelBtn.GetComponent<Image>().color = new Color(0.40f, 0.40f, 0.48f);
        cancelBtn.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        yPos -= 62;

        var settingsStatus = CreateTMP("SettingsStatusText", settingsPanel, "", 14, TextAlignmentOptions.TopLeft);
        SetAnchors(settingsStatus, new Vector2(0.08f, 1), new Vector2(0.92f, 1),
            new Vector2(0, yPos - 30), new Vector2(0, yPos));

        settingsPanel.gameObject.SetActive(false);

        // ===== ChatUIManager をアタッチ =====
        canvasObj.AddComponent<ChatUIManager>();

        // ===== SettingsUIManager をアタッチ =====
        canvasObj.AddComponent<SettingsUIManager>();

        // ===== MobileInputHelper をアタッチ =====
        canvasObj.AddComponent<MobileInputHelper>();

        // ===== EventSystem =====
        var eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // ===== Prefabs フォルダ作成 =====
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        // ===== User Message Prefab =====
        var userPrefab = CreateMessagePrefab("UserMessageBubble", USER_BUBBLE, TextAlignmentOptions.TopLeft, true);
        PrefabUtility.SaveAsPrefabAsset(userPrefab, "Assets/Prefabs/UserMessageBubble.prefab");
        Object.DestroyImmediate(userPrefab);

        // ===== AI Message Prefab =====
        var aiPrefab = CreateMessagePrefab("AIMessageBubble", AI_BUBBLE, TextAlignmentOptions.TopLeft, false);
        PrefabUtility.SaveAsPrefabAsset(aiPrefab, "Assets/Prefabs/AIMessageBubble.prefab");
        Object.DestroyImmediate(aiPrefab);

        // ===== 日本語フォント適用 =====
        var jpFont = FindJapaneseFont();
        if (jpFont != null)
        {
            ApplyFontToAll(canvasObj, jpFont);
            ApplyFontToPrefabs(jpFont);
            Debug.Log("[SceneBuilder] 日本語フォントを全テキストに適用しました");
        }
        else
        {
            Debug.LogWarning("[SceneBuilder] 日本語フォントが見つかりません！");
            Debug.LogWarning("  → Assets/Fonts/ に Noto Sans JP の Static フォントを配置し、");
            Debug.LogWarning("  → Font Asset Creator でフォントアセットを作成してください");
        }

        Debug.Log("============================================");
        Debug.Log("[SceneBuilder] シーン構築完了！");
        Debug.Log("次のステップ: LocalAI → Auto Wire References");
        Debug.Log("============================================");

        Selection.activeGameObject = canvasObj;
        EditorUtility.DisplayDialog("Local AI Chat",
            "シーンを構築しました！\n\n" +
            "次のステップ:\n" +
            "1. LocalAI → Auto Wire References を実行\n" +
            "2. LocalAI → Apply Japanese Font を実行\n" +
            "3. Play で動作確認",
            "OK");
    }

    [MenuItem("LocalAI/Apply Japanese Font")]
    public static void ApplyJapaneseFontMenu()
    {
        var jpFont = FindJapaneseFont();
        if (jpFont == null)
        {
            EditorUtility.DisplayDialog("日本語フォント",
                "日本語フォントが見つかりません。\n\n" +
                "重要: Static フォントを使ってください！\n" +
                "（Variable フォントは TextMesh Pro に対応していません）\n\n" +
                "手順:\n" +
                "1. Noto Sans JP の Static フォルダから Regular を取得\n" +
                "2. Assets/Fonts/ に配置\n" +
                "3. Window → TextMeshPro → Font Asset Creator\n" +
                "4. Character Set → Unicode Range (Hex)\n" +
                "5. 範囲: 0020-007E,3000-30FF,4E00-9FBF,FF00-FF9F\n" +
                "6. Atlas: 4096 x 4096\n" +
                "7. Generate → Save\n" +
                "8. もう一度このメニューを実行",
                "OK");
            return;
        }

        // シーン内の全テキストに適用
        var allTexts = Object.FindObjectsOfType<TextMeshProUGUI>(true);
        foreach (var t in allTexts) t.font = jpFont;
        Debug.Log($"[SceneBuilder] シーン内 {allTexts.Length} 個のテキストに日本語フォントを適用しました");

        // プレハブにも適用
        ApplyFontToPrefabs(jpFont);

        EditorUtility.DisplayDialog("日本語フォント",
            $"日本語フォントを {allTexts.Length} 個のテキストとプレハブに適用しました！",
            "OK");
    }

    // ===== プレハブにフォント適用 =====
    static void ApplyFontToPrefabs(TMP_FontAsset jpFont)
    {
        string[] prefabPaths = { "Assets/Prefabs/UserMessageBubble.prefab", "Assets/Prefabs/AIMessageBubble.prefab" };
        foreach (var path in prefabPaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                foreach (var t in prefab.GetComponentsInChildren<TextMeshProUGUI>(true))
                    t.font = jpFont;
                PrefabUtility.SavePrefabAsset(prefab);
            }
        }
    }

    // ===== 日本語フォント検索 =====
    static TMP_FontAsset FindJapaneseFont()
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string lower = path.ToLower();
            if (lower.Contains("noto") || lower.Contains("japanese") || lower.Contains("jp")
                || lower.Contains("gothic") || lower.Contains("meiryo") || lower.Contains("hiragino")
                || lower.Contains("mincho") || lower.Contains("migmix") || lower.Contains("mplus")
                || lower.Contains("rounded") || lower.Contains("ud"))
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font != null)
                {
                    Debug.Log($"[SceneBuilder] 日本語フォント発見: {path}");
                    return font;
                }
            }
        }
        return null;
    }

    static void ApplyFontToAll(GameObject root, TMP_FontAsset font)
    {
        if (font == null) return;
        var texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in texts) t.font = font;
        Debug.Log($"[SceneBuilder] {texts.Length} 個のテキストにフォントを適用しました");
    }

    // ===== メッセージプレハブ作成 =====
    static GameObject CreateMessagePrefab(string name, Color bgColor, TextAlignmentOptions align, bool isUser)
    {
        var obj = new GameObject(name);
        var rect = obj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 80);

        // HorizontalLayoutGroup で左右マージン制御
        var hlg = obj.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = false;
        // ユーザーは右寄せ、AIは左寄せ
        if (isUser)
            hlg.padding = new RectOffset(100, 16, 4, 4);
        else
            hlg.padding = new RectOffset(16, 100, 4, 4);

        var rootCsf = obj.AddComponent<ContentSizeFitter>();
        rootCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // バブル本体
        var bubbleObj = new GameObject("Bubble");
        bubbleObj.transform.SetParent(obj.transform, false);
        var bubbleRect = bubbleObj.AddComponent<RectTransform>();
        var bubbleBg = bubbleObj.AddComponent<Image>();
        bubbleBg.color = bgColor;

        var bubbleVlg = bubbleObj.AddComponent<VerticalLayoutGroup>();
        bubbleVlg.padding = new RectOffset(16, 16, 10, 10);
        bubbleVlg.spacing = 6;
        bubbleVlg.childControlWidth = true;
        bubbleVlg.childControlHeight = true;
        bubbleVlg.childForceExpandWidth = true;
        bubbleVlg.childForceExpandHeight = false;

        var bubbleCsf = bubbleObj.AddComponent<ContentSizeFitter>();
        bubbleCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // MessageText
        var msgObj = new GameObject("MessageText");
        msgObj.transform.SetParent(bubbleObj.transform, false);
        var msgRect = msgObj.AddComponent<RectTransform>();
        var msgTMP = msgObj.AddComponent<TextMeshProUGUI>();
        msgTMP.text = "";
        msgTMP.fontSize = 18;
        msgTMP.color = TEXT_PRIMARY;
        msgTMP.alignment = align;
        msgTMP.enableWordWrapping = true;
        msgTMP.overflowMode = TextOverflowModes.Overflow;
        // LayoutElement で最小サイズ確保
        var msgLE = msgObj.AddComponent<LayoutElement>();
        msgLE.minHeight = 24;

        // TimeText
        var timeObj = new GameObject("TimeText");
        timeObj.transform.SetParent(bubbleObj.transform, false);
        var timeRect = timeObj.AddComponent<RectTransform>();
        var timeTMP = timeObj.AddComponent<TextMeshProUGUI>();
        timeTMP.text = "";
        timeTMP.fontSize = 12;
        timeTMP.color = TEXT_SECONDARY;
        timeTMP.alignment = isUser ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.BottomLeft;

        // MetaText (AI only)
        TextMeshProUGUI metaTMP = null;
        if (!isUser)
        {
            var metaObj = new GameObject("MetaText");
            metaObj.transform.SetParent(bubbleObj.transform, false);
            var metaRect = metaObj.AddComponent<RectTransform>();
            metaTMP = metaObj.AddComponent<TextMeshProUGUI>();
            metaTMP.text = "";
            metaTMP.fontSize = 12;
            metaTMP.color = TEXT_SECONDARY;
            metaTMP.alignment = TextAlignmentOptions.BottomLeft;
        }

        // MessageBubble コンポーネント — フィールドを自動接続
        var bubble = obj.AddComponent<MessageBubble>();
        var bubbleType = typeof(MessageBubble);
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        bubbleType.GetField("messageText", flags)?.SetValue(bubble, msgTMP);
        bubbleType.GetField("timeText", flags)?.SetValue(bubble, timeTMP);
        bubbleType.GetField("bubbleBackground", flags)?.SetValue(bubble, bubbleBg);

        if (!isUser && metaTMP != null)
        {
            bubbleType.GetField("metaText", flags)?.SetValue(bubble, metaTMP);
        }

        return obj;
    }

    // ===== ヘルパーメソッド =====

    static RectTransform CreatePanel(string name, Transform parent, Color color)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        var img = obj.AddComponent<Image>();
        img.color = color;
        return rect;
    }

    static RectTransform CreateTMP(string name, Transform parent, string text, float fontSize, TextAlignmentOptions align)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = TEXT_PRIMARY;
        return rect;
    }

    static RectTransform CreateTMP(string name, RectTransform parent, string text, float fontSize, TextAlignmentOptions align)
    {
        return CreateTMP(name, (Transform)parent, text, fontSize, align);
    }

    static GameObject CreateImage(string name, Transform parent, Color color)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        var img = obj.AddComponent<Image>();
        img.color = color;
        return obj;
    }

    static GameObject CreateImage(string name, RectTransform parent, Color color)
    {
        return CreateImage(name, (Transform)parent, color);
    }

    static GameObject CreateButton(string name, Transform parent, string label, float fontSize)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        var img = obj.AddComponent<Image>();
        img.color = new Color(0.3f, 0.3f, 0.4f);
        obj.AddComponent<Button>();

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(obj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return obj;
    }

    static GameObject CreateButton(string name, RectTransform parent, string label, float fontSize)
    {
        return CreateButton(name, (Transform)parent, label, fontSize);
    }

    static RectTransform CreateInputField(string name, RectTransform parent, string placeholder)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        var bg = obj.AddComponent<Image>();
        bg.color = INPUT_FIELD;

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(obj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14, 4);
        textRect.offsetMax = new Vector2(-14, -4);
        var textTMP = textObj.AddComponent<TextMeshProUGUI>();
        textTMP.fontSize = 16;
        textTMP.color = TEXT_PRIMARY;

        var phObj = new GameObject("Placeholder");
        phObj.transform.SetParent(obj.transform, false);
        var phRect = phObj.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = new Vector2(14, 4);
        phRect.offsetMax = new Vector2(-14, -4);
        var phTMP = phObj.AddComponent<TextMeshProUGUI>();
        phTMP.text = placeholder;
        phTMP.fontSize = 16;
        phTMP.fontStyle = FontStyles.Italic;
        phTMP.color = TEXT_SECONDARY;

        var input = obj.AddComponent<TMP_InputField>();
        input.textComponent = textTMP;
        input.placeholder = phTMP;
        input.textViewport = textRect;

        return rect;
    }

    static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    static void SetAnchors(Component comp, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        SetAnchors(comp.GetComponent<RectTransform>(), anchorMin, anchorMax, offsetMin, offsetMax);
    }
}
#endif
