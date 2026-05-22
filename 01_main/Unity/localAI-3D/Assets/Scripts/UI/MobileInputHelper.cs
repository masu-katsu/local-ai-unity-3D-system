using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// スマホのソフトウェアキーボード表示時に入力エリアを押し上げる
/// Canvas の最下部に配置された InputArea にアタッチする
/// </summary>
public class MobileInputHelper : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private RectTransform inputArea;
    [SerializeField] private ScrollRect scrollRect;

    [Header("設定")]
    [Tooltip("キーボードが開いたときに追加する下マージン")]
    #pragma warning disable CS0414
    [SerializeField] private float extraPadding = 10f;

    private float originalBottomOffset;
    private bool keyboardVisible = false;
    #pragma warning restore CS0414

    private void Start()
    {
        if (inputArea != null)
            originalBottomOffset = inputArea.offsetMin.y;
    }

    private void Update()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (TouchScreenKeyboard.visible)
        {
            if (!keyboardVisible)
            {
                keyboardVisible = true;
                AdjustForKeyboard();
            }
        }
        else
        {
            if (keyboardVisible)
            {
                keyboardVisible = false;
                ResetLayout();
            }
        }
#endif
    }

    private void AdjustForKeyboard()
    {
#if UNITY_ANDROID || UNITY_IOS
        float keyboardHeight = GetKeyboardHeight();
        if (inputArea != null)
        {
            inputArea.offsetMin = new Vector2(
                inputArea.offsetMin.x,
                keyboardHeight + extraPadding
            );
        }

        // スクロールを最下部に
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
#endif
    }

    private void ResetLayout()
    {
        if (inputArea != null)
        {
            inputArea.offsetMin = new Vector2(
                inputArea.offsetMin.x,
                originalBottomOffset
            );
        }
    }

    private float GetKeyboardHeight()
    {
#if UNITY_ANDROID
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            var window = activity.Call<AndroidJavaObject>("getWindow");
            var decorView = window.Call<AndroidJavaObject>("getDecorView");
            var rect = new AndroidJavaObject("android.graphics.Rect");
            decorView.Call("getWindowVisibleDisplayFrame", rect);
            int screenHeight = Screen.height;
            int visibleHeight = rect.Call<int>("height");
            return (screenHeight - visibleHeight) / (float)Screen.height * ((RectTransform)inputArea.parent).rect.height;
        }
#elif UNITY_IOS
        return TouchScreenKeyboard.area.height / Screen.height * ((RectTransform)inputArea.parent).rect.height;
#else
        return 0f;
#endif
    }
}
