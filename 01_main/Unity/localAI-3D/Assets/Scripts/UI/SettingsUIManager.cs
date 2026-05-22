using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 設定画面の UI を管理する
/// サーバー URL、API キー、ユーザー ID の設定
/// </summary>
public class SettingsUIManager : MonoBehaviour
{
    [Header("設定パネル")]
    [SerializeField] private GameObject settingsPanel;

    [Header("入力フィールド")]
    [SerializeField] private TMP_InputField serverUrlInput;
    [SerializeField] private TMP_InputField apiKeyInput;
    [SerializeField] private TMP_InputField userIdInput;

    [Header("ボタン")]
    [SerializeField] private Button openSettingsButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button testConnectionButton;

    [Header("ステータス")]
    [SerializeField] private TextMeshProUGUI settingsStatusText;

    private void Awake()
    {
        WireMissingReferencesFromHierarchy();
    }

    private void WireMissingReferencesFromHierarchy()
    {
        Transform root = transform;
        if (settingsPanel == null)
        {
            var t = root.Find("SettingsPanel");
            if (t != null) settingsPanel = t.gameObject;
        }

        Transform panel = settingsPanel != null ? settingsPanel.transform : null;

        if (openSettingsButton == null)
        {
            var t = root.Find("Header/SettingsButton");
            if (t != null) openSettingsButton = t.GetComponent<Button>();
        }

        if (panel == null) return;

        if (serverUrlInput == null)
        {
            var t = panel.Find("ServerUrlInput");
            if (t != null) serverUrlInput = t.GetComponent<TMP_InputField>();
        }
        if (apiKeyInput == null)
        {
            var t = panel.Find("ApiKeyInput");
            if (t != null) apiKeyInput = t.GetComponent<TMP_InputField>();
        }
        if (userIdInput == null)
        {
            var t = panel.Find("UserIdInput");
            if (t != null) userIdInput = t.GetComponent<TMP_InputField>();
        }
        if (testConnectionButton == null)
        {
            var t = panel.Find("TestConnectionButton");
            if (t != null) testConnectionButton = t.GetComponent<Button>();
        }
        if (saveButton == null)
        {
            var t = panel.Find("SaveButton");
            if (t != null) saveButton = t.GetComponent<Button>();
        }
        if (cancelButton == null)
        {
            var t = panel.Find("CancelButton");
            if (t != null) cancelButton = t.GetComponent<Button>();
        }
        if (settingsStatusText == null)
        {
            var t = panel.Find("SettingsStatusText");
            if (t != null) settingsStatusText = t.GetComponent<TextMeshProUGUI>();
        }
    }

    private void Start()
    {
        if (settingsPanel == null || openSettingsButton == null || saveButton == null || cancelButton == null ||
            testConnectionButton == null || serverUrlInput == null || apiKeyInput == null || userIdInput == null)
        {
            Debug.LogError("[SettingsUI] 必須 UI 参照が欠けています。LocalAI → Auto Wire References を実行するか、Build Chat Scene でシーンを再生成してください。");
            return;
        }

        openSettingsButton.onClick.AddListener(OpenSettings);
        saveButton.onClick.AddListener(SaveSettings);
        cancelButton.onClick.AddListener(CloseSettings);
        testConnectionButton.onClick.AddListener(TestConnection);

        settingsPanel.SetActive(false);
    }

    // ========== パネル開閉 ==========

    public void OpenSettings()
    {
        // 現在の設定値を表示
        serverUrlInput.text = ApiClient.Instance.ServerUrl;
        apiKeyInput.text = ApiClient.Instance.ApiKey;
        userIdInput.text = ChatManager.Instance.UserId;

        settingsPanel.SetActive(true);
        if (settingsStatusText != null)
            settingsStatusText.text = "";
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
    }

    // ========== 保存 ==========

    private void SaveSettings()
    {
        // 値を反映
        ApiClient.Instance.ServerUrl = serverUrlInput.text.Trim();
        ApiClient.Instance.ApiKey = apiKeyInput.text.Trim();
        ChatManager.Instance.UserId = userIdInput.text.Trim();

        // 永続化
        ApiClient.Instance.SaveSettings();
        ChatManager.Instance.SaveUserId();

        if (settingsStatusText != null)
        {
            settingsStatusText.color = Color.green;
            settingsStatusText.text = "設定を保存しました ✓";
        }

        // パネルを閉じる
        Invoke(nameof(CloseSettings), 1f);
    }

    // ========== 接続テスト ==========

    private void TestConnection()
    {
        // テスト用に一時的に URL を適用
        string testUrl = serverUrlInput.text.Trim();
        string testKey = apiKeyInput.text.Trim();
        string originalUrl = ApiClient.Instance.ServerUrl;
        string originalKey = ApiClient.Instance.ApiKey;

        ApiClient.Instance.ServerUrl = testUrl;
        ApiClient.Instance.ApiKey = testKey;

        if (settingsStatusText != null)
        {
            settingsStatusText.color = Color.white;
            settingsStatusText.text = "接続テスト中...";
        }

        ApiClient.Instance.CheckHealth(
            onSuccess: (health) =>
            {
                if (settingsStatusText != null)
                {
                    settingsStatusText.color = Color.green;
                    settingsStatusText.text = $"接続成功 ✓ | Phi3: {health.phi3} | Qwen: {health.qwen}";
                }
            },
            onError: (error) =>
            {
                if (settingsStatusText != null)
                {
                    settingsStatusText.color = Color.red;
                    settingsStatusText.text = $"接続失敗: {error}";
                }
                // 元に戻す
                ApiClient.Instance.ServerUrl = originalUrl;
                ApiClient.Instance.ApiKey = originalKey;
            }
        );
    }
}
