using UnityEngine;
using UnityEditor;

/// <summary>
/// Unityエディタメニューに3D modelメニューを追加する。
/// </summary>
public class D3DModelMenuEditor
{
    [MenuItem("3D model/Build")]
    public static void Build3DModel()
    {
        Debug.Log("[3D Model Menu] Build を実行します...");

        // シーン内にあるGameObjectを全削除（既存データのリセット）
        ClearScene();

        // BootstrapGameObjectを作成
        GameObject bootstrapObj = new GameObject("AutonomousWorld");
        AutonomousPlayBootstrap bootstrap = bootstrapObj.AddComponent<AutonomousPlayBootstrap>();

        // リソースを手動で割り当て
        string fbxPath = "Assets/untitled.fbx";
        string animatorPath = "Assets/Animations/CharacterAnimator.controller";

        GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        RuntimeAnimatorController animator = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(animatorPath);

        if (modelPrefab != null)
        {
            bootstrap.SetCharacterModel(modelPrefab, animator);
            Debug.Log("[3D Model Menu] モデルを読み込みました: " + fbxPath);
        }
        else
        {
            Debug.LogError("[3D Model Menu] モデルが見つかりません: " + fbxPath);
            return;
        }

        // エディタモードでセットアップを実行
        bootstrap.EditorSetupWorld();

        Debug.Log("[3D Model Menu] セットアップが完了しました。Playボタンを押してください。");
    }

    private static void ClearScene()
    {
        const string FloorName = "WalkFloor";
        const string CharacterName = "Character";
        const string WaypointRootName = "WaypointManager";

        // 既存のオブジェクトを削除
        GameObject floor = GameObject.Find(FloorName);
        if (floor != null)
            Object.DestroyImmediate(floor);

        GameObject character = GameObject.Find(CharacterName);
        if (character != null)
            Object.DestroyImmediate(character);

        GameObject waypoints = GameObject.Find(WaypointRootName);
        if (waypoints != null)
            Object.DestroyImmediate(waypoints);

        // 既存の AutonomousWorld も削除
        GameObject autonomousWorld = GameObject.Find("AutonomousWorld");
        if (autonomousWorld != null)
            Object.DestroyImmediate(autonomousWorld);

        Debug.Log("[3D Model Menu] 既存のシーンオブジェクトをクリアしました。");
    }
}
