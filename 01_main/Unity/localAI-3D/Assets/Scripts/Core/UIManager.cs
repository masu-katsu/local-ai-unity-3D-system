using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// UIメニューを管理し、3Dシーンのセットアップを制御する。
/// </summary>
public class UIManager : MonoBehaviour
{
    private CanvasGroup canvasGroup;
    private Button build3DButton;

    private AutonomousPlayBootstrap bootstrap;
    private bool isSceneSetup = false;

    private void Awake()
    {
        bootstrap = FindObjectOfType<AutonomousPlayBootstrap>();
    }

    /// <summary>
    /// UIコンポーネントへの参照を設定する。
    /// </summary>
    public void SetReferences(CanvasGroup group, Button button)
    {
        canvasGroup = group;
        build3DButton = button;
    }

    private void Start()
    {
        // 参照が設定されている場合、ボタンのリスナーを追加
        if (build3DButton != null)
            build3DButton.onClick.AddListener(OnBuild3DClicked);
    }

    public void OnBuild3DClicked()
    {
        if (!isSceneSetup && bootstrap != null)
        {
            StartCoroutine(SetupScene());
        }
    }

    private IEnumerator SetupScene()
    {
        isSceneSetup = true;

        // AutonomousPlayBootstrapに既存のメソッドを使用
        bootstrap.ManualSetupWorld();

        yield return new WaitForSeconds(2.0f);

        // UIを半透明に
        if (canvasGroup != null)
            canvasGroup.alpha = 0.3f;
    }

    public void ShowMenu()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    public void HideMenu()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0.3f;
    }
}
