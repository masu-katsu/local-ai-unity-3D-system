using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// チャットのロジックを管理する
/// UI とは分離し、イベントで通知する
/// </summary>
public class ChatManager : MonoBehaviour
{
    public static ChatManager Instance { get; private set; }

    [Header("ユーザー設定")]
    [SerializeField] private string userId = "default_user";

    // チャットメッセージのリスト（表示用）
    private List<ChatMessage> messages = new List<ChatMessage>();

    // 送信中フラグ
    public bool IsSending { get; private set; }

    // Events
    public event Action<ChatMessage> OnMessageAdded;
    public event Action<bool> OnSendingStateChanged;
    public event Action<string> OnError;

    public string UserId
    {
        get => userId;
        set => userId = value;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 保存済み userId を読み込む
        if (PlayerPrefs.HasKey("UserId"))
            userId = PlayerPrefs.GetString("UserId");
    }
    
    private void Start()
    {
        // Initialize
        Debug.Log("[ChatManager] チャットマネージャー起動");
    }

    /// <summary>
    /// Send message to AI
    /// </summary>
    public new void SendMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || IsSending) return;

        // ユーザーメッセージを追加
        var userMsg = new ChatMessage
        {
            role = "user",
            content = text,
            timestamp = DateTime.Now
        };
        messages.Add(userMsg);
        OnMessageAdded?.Invoke(userMsg);

        // 送信状態を変更
        IsSending = true;
        OnSendingStateChanged?.Invoke(true);

        // API に送信
        SendChatRequest(text);
    }
    
    private void SendChatRequest(string message)
    {
        // API に送信
        ApiClient.Instance.SendChat(
            message,
            userId,
            onSuccess: (response) =>
            {
                HandleChatResponse(response);
                IsSending = false;
                OnSendingStateChanged?.Invoke(false);
            },
            onError: (error) =>
            {
                OnError?.Invoke(error);
                IsSending = false;
                OnSendingStateChanged?.Invoke(false);
            }
        );
    }

    private void HandleChatResponse(ChatResponse response)
    {
        // AI の応答を追加
        var aiMsg = new ChatMessage
        {
            role = "assistant",
            content = response.response,
            timestamp = DateTime.Now,
            modelUsed = response.model_used,
            processingTime = response.processing_time,
            contextUsed = response.context_used
        };
        messages.Add(aiMsg);
        OnMessageAdded?.Invoke(aiMsg);
    }

    /// <summary>
    /// ユーザーID を保存する
    /// </summary>
    public void SaveUserId()
    {
        PlayerPrefs.SetString("UserId", userId);
        PlayerPrefs.Save();
    }
}

/// <summary>
/// チャットメッセージのデータ
/// </summary>
[Serializable]
public class ChatMessage
{
    public string role;       // "user" or "assistant"
    public string content;
    public DateTime timestamp;
    public string modelUsed;
    public float processingTime;
    public bool contextUsed;
}
