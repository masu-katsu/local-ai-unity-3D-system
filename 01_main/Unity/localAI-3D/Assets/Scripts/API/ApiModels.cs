using System;

/// <summary>
/// FastAPI との通信に使用するデータモデル
/// </summary>

// ========== リクエスト ==========

[Serializable]
public class ChatRequest
{
    public string message;
    public string user_id;
    public bool web_search_confirmed = false;
    public string web_search_action = null;  // "stop" で停止
}

// ========== レスポンス ==========

[Serializable]
public class ChatResponse
{
    public string response = "";
    public string model_used = "";
    public float processing_time = 0f;
    public bool context_used = false;
    public bool web_search_used = false;
    public bool requires_confirmation = false;
    public string pending_web_search = "";
    public bool search_in_progress = false;
}

[Serializable]
public class HealthResponse
{
    public string status;
    public string phi3;
    public string qwen;
    public string chromadb;
}
