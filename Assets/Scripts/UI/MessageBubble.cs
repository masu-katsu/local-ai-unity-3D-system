using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 個々のメッセージバブルの表示を担当する
/// UserMessage / AIMessage プレハブにアタッチする
/// </summary>
public class MessageBubble : MonoBehaviour
{
    [Header("表示要素")]
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI metaText;  // モデル名・処理時間（AI のみ）

    [Header("背景")]
    [SerializeField] private Image bubbleBackground;

    public void SetMessage(ChatMessage message)
    {
        // メッセージ本文
        if (messageText != null)
            messageText.text = message.content;

        // タイムスタンプ
        if (timeText != null)
            timeText.text = message.timestamp.ToString("HH:mm");

        // メタ情報（AI メッセージのみ）
        if (metaText != null && message.role == "assistant")
        {
            string meta = $"{message.modelUsed} | {message.processingTime:F1}s";
            if (message.contextUsed)
                meta += " | 履歴参照";
            metaText.text = meta;
        }
        else if (metaText != null)
        {
            metaText.gameObject.SetActive(false);
        }
    }
}
