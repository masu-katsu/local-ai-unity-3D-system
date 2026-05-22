using System;
using System.Text;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class ApiClient : MonoBehaviour
{
    public static ApiClient Instance { get; private set; }

    [Header("Connection Settings")]
    [Tooltip("FastAPI server URL")]
    [SerializeField] private string serverUrl = "http://localhost:8000";

    [Tooltip("API Key")]
    [SerializeField] private string apiKey = "";

    [Header("Timeout Settings")]
    [SerializeField] private int timeoutSeconds = 120;

    public string ServerUrl
    {
        get => serverUrl;
        set => serverUrl = value.TrimEnd('/');
    }

    public string ApiKey
    {
        get => apiKey;
        set => apiKey = value;
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
        LoadSettings();
    }

    public void SendChat(string message, string userId, Action<ChatResponse> onSuccess, Action<string> onError)
    {
        StartCoroutine(SendChatCoroutine(message, userId, onSuccess, onError));
    }

    private IEnumerator SendChatCoroutine(string message, string userId, Action<ChatResponse> onSuccess, Action<string> onError)
    {
        var requestBody = new ChatRequest 
        { 
            message = message, 
            user_id = userId
        };
        string json = JsonUtility.ToJson(requestBody);

        using (var request = new UnityWebRequest($"{serverUrl}/api/chat", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            request.SetRequestHeader("X-API-Key", apiKey);
            request.timeout = timeoutSeconds;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string jsonText = request.downloadHandler.text;
                    Debug.Log("[ApiClient] Response: " + jsonText);
                    var response = JsonUtility.FromJson<ChatResponse>(jsonText);
                    
                    if (response == null)
                    {
                        onError?.Invoke("Parse error: null response");
                        yield break;
                    }
                    
                    onSuccess?.Invoke(response);
                }
                catch (Exception e)
                {
                    Debug.LogError("[ApiClient] Error: " + e);
                    onError?.Invoke("Parse error: " + e.Message);
                }
            }
            else
            {
                onError?.Invoke(GetErrorMessage(request));
            }
        }
    }

    public void CheckHealth(Action<HealthResponse> onSuccess, Action<string> onError)
    {
        StartCoroutine(CheckHealthCoroutine(onSuccess, onError));
    }

    private IEnumerator CheckHealthCoroutine(Action<HealthResponse> onSuccess, Action<string> onError)
    {
        using (var request = UnityWebRequest.Get($"{serverUrl}/api/health"))
        {
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<HealthResponse>(request.downloadHandler.text);
                    onSuccess?.Invoke(response);
                }
                catch (Exception e)
                {
                    onError?.Invoke("Parse error: " + e.Message);
                }
            }
            else
            {
                onError?.Invoke(GetErrorMessage(request));
            }
        }
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetString("ServerUrl", serverUrl);
        PlayerPrefs.SetString("ApiKey", apiKey);
        PlayerPrefs.Save();
        Debug.Log("[ApiClient] Settings saved");
    }

    private void LoadSettings()
    {
        if (PlayerPrefs.HasKey("ServerUrl"))
            serverUrl = PlayerPrefs.GetString("ServerUrl");
        if (PlayerPrefs.HasKey("ApiKey"))
            apiKey = PlayerPrefs.GetString("ApiKey");
    }

    private string GetErrorMessage(UnityWebRequest request)
    {
        switch (request.result)
        {
            case UnityWebRequest.Result.ConnectionError:
                return string.IsNullOrEmpty(request.error)
                    ? "Connection error（サーバーに接続できません。バックエンド起動と URL を確認してください）"
                    : $"Connection error: {request.error}";
            case UnityWebRequest.Result.ProtocolError:
                if (request.responseCode == 403)
                    return "API Key error";
                return "Server error: " + request.responseCode;
            case UnityWebRequest.Result.DataProcessingError:
                return "Data error";
            default:
                return "Error: " + request.error;
        }
    }
}
