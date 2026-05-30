#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using System.Reflection;

/// <summary>
/// Build Scene 後に Inspector の SerializeField を自動接続するスクリプト
/// Unity メニュー → LocalAI → Auto Wire References
/// </summary>
public class AutoWire
{
    [MenuItem("LocalAI/Auto Wire References")]
    public static void Wire()
    {
        // ===== ChatUIManager =====
        var chatUI = Object.FindFirstObjectByType<ChatUIManager>();
        if (chatUI != null)
        {
            var canvas = chatUI.transform;
            SetField(chatUI, "inputField", FindInChildren<TMP_InputField>(canvas, "InputArea/InputField"));
            SetField(chatUI, "sendButton", FindInChildren<Button>(canvas, "InputArea/SendButton"));
            SetField(chatUI, "scrollRect", FindInChildren<ScrollRect>(canvas, "MessageArea/ScrollView"));
            SetField(chatUI, "messageContainer", FindTransform(canvas, "MessageArea/ScrollView/Viewport/Content"));
            SetField(chatUI, "typingIndicator", FindGameObject(canvas, "MessageArea/TypingIndicator"));
            SetField(chatUI, "statusText", FindInChildren<TextMeshProUGUI>(canvas, "MessageArea/StatusText"));
            SetField(chatUI, "connectionIndicator", FindInChildren<Image>(canvas, "Header/ConnectionIndicator"));

            EditorUtility.SetDirty(chatUI);
            Debug.Log("[AutoWire] ✅ ChatUIManager の参照を接続しました");
        }
        else
        {
            Debug.LogWarning("[AutoWire] ChatUIManager が見つかりません。先に Build Chat Scene を実行してください。");
        }

        // ===== SettingsUIManager =====
        var settingsUI = Object.FindFirstObjectByType<SettingsUIManager>();
        if (settingsUI != null)
        {
            var canvas = settingsUI.transform;
            var settingsPanel = FindTransform(canvas, "SettingsPanel");

            if (settingsPanel != null)
            {
                SetField(settingsUI, "settingsPanel", settingsPanel.gameObject);
                SetField(settingsUI, "serverUrlInput", FindInChildren<TMP_InputField>(settingsPanel, "ServerUrlInput"));
                SetField(settingsUI, "apiKeyInput", FindInChildren<TMP_InputField>(settingsPanel, "ApiKeyInput"));
                SetField(settingsUI, "userIdInput", FindInChildren<TMP_InputField>(settingsPanel, "UserIdInput"));
                SetField(settingsUI, "testConnectionButton", FindInChildren<Button>(settingsPanel, "TestConnectionButton"));
                SetField(settingsUI, "saveButton", FindInChildren<Button>(settingsPanel, "SaveButton"));
                SetField(settingsUI, "cancelButton", FindInChildren<Button>(settingsPanel, "CancelButton"));
                SetField(settingsUI, "statusText", FindInChildren<TextMeshProUGUI>(settingsPanel, "SettingsStatusText"));
            }

            // 設定ボタン（Header 内）
            SetField(settingsUI, "openSettingsButton", FindInChildren<Button>(canvas, "Header/SettingsButton"));

            EditorUtility.SetDirty(settingsUI);
            Debug.Log("[AutoWire] ✅ SettingsUIManager の参照を接続しました");
        }

        // ===== MobileInputHelper =====
        var mobileHelper = Object.FindFirstObjectByType<MobileInputHelper>();
        if (mobileHelper != null)
        {
            var canvas = mobileHelper.transform;
            SetField(mobileHelper, "inputArea", FindRectTransform(canvas, "InputArea"));
            SetField(mobileHelper, "scrollRect", FindInChildren<ScrollRect>(canvas, "MessageArea/ScrollView"));
            EditorUtility.SetDirty(mobileHelper);
            Debug.Log("[AutoWire] ✅ MobileInputHelper の参照を接続しました");
        }

        EditorUtility.DisplayDialog("Auto Wire",
            "参照の自動接続が完了しました！\n\n" +
            "Inspector で各コンポーネントを確認してください。",
            "OK");
    }

    // ===== ヘルパー =====

    static void SetField(object target, string fieldName, object value)
    {
        if (value == null) return;
        var field = target.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        if (field != null)
        {
            field.SetValue(target, value);
        }
        else
        {
            Debug.LogWarning($"[AutoWire] フィールド '{fieldName}' が見つかりません");
        }
    }

    static T FindInChildren<T>(Transform root, string path) where T : Component
    {
        var target = root.Find(path);
        return target != null ? target.GetComponent<T>() : null;
    }

    static T FindInChildren<T>(RectTransform root, string path) where T : Component
    {
        return FindInChildren<T>((Transform)root, path);
    }

    static Transform FindTransform(Transform root, string path)
    {
        return root.Find(path);
    }

    static RectTransform FindRectTransform(Transform root, string path)
    {
        var t = root.Find(path);
        return t != null ? t.GetComponent<RectTransform>() : null;
    }

    static GameObject FindGameObject(Transform root, string path)
    {
        var t = root.Find(path);
        return t != null ? t.gameObject : null;
    }
}
#endif
