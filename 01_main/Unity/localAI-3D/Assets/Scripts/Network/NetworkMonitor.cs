using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// ネットワーク接続を定期的に監視し、状態変化を通知する
/// WiFi 切断時に自動でユーザーに知らせる
/// </summary>
public class NetworkMonitor : MonoBehaviour
{
    public static NetworkMonitor Instance { get; private set; }

    [Header("監視設定")]
    [Tooltip("ヘルスチェックの間隔（秒）")]
    [SerializeField] private float checkInterval = 30f;

    [Tooltip("接続断の際のリトライ間隔（秒）")]
    [SerializeField] private float retryInterval = 5f;

    /// <summary>接続状態が変化したときに発火</summary>
    public event Action<bool> OnConnectionChanged;

    /// <summary>現在の接続状態</summary>
    public bool IsConnected { get; private set; } = false;

    private Coroutine monitorCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        monitorCoroutine = StartCoroutine(MonitorLoop());
    }

    private void OnDestroy()
    {
        if (monitorCoroutine != null)
            StopCoroutine(monitorCoroutine);
    }

    private IEnumerator MonitorLoop()
    {
        while (true)
        {
            // まず端末のネットワーク状態を確認
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                SetConnectionState(false);
                yield return new WaitForSeconds(retryInterval);
                continue;
            }

            // FastAPI にヘルスチェック
            bool checkDone = false;
            bool checkResult = false;

            ApiClient.Instance?.CheckHealth(
                onSuccess: (_) =>
                {
                    checkResult = true;
                    checkDone = true;
                },
                onError: (_) =>
                {
                    checkResult = false;
                    checkDone = true;
                }
            );

            // 結果を待つ（最大10秒）
            float waited = 0f;
            while (!checkDone && waited < 10f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            SetConnectionState(checkDone && checkResult);

            float interval = IsConnected ? checkInterval : retryInterval;
            yield return new WaitForSeconds(interval);
        }
    }

    private void SetConnectionState(bool connected)
    {
        if (IsConnected != connected)
        {
            IsConnected = connected;
            Debug.Log($"[NetworkMonitor] 接続状態: {(connected ? "✓ 接続中" : "✗ 切断")}");
            OnConnectionChanged?.Invoke(connected);
        }
    }

    /// <summary>手動で即座にチェック</summary>
    public void CheckNow()
    {
        if (monitorCoroutine != null)
            StopCoroutine(monitorCoroutine);
        monitorCoroutine = StartCoroutine(MonitorLoop());
    }
}
