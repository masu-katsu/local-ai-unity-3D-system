using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Play 時に床・Waypoint・キャラクターを構築する。
/// </summary>
[DefaultExecutionOrder(-300)]
public class AutonomousPlayBootstrap : MonoBehaviour
{
    const string FloorName = "WalkFloor";
    const string CharacterName = "Character";
    const string WaypointRootName = "WaypointManager";

#if UNITY_EDITOR
    const string FbxPath = "Assets/untitled.fbx";
    const string AnimatorPath = "Assets/Animations/CharacterAnimator.controller";
#endif

    [SerializeField] GameObject characterModelPrefab;
    [SerializeField] RuntimeAnimatorController animatorController;

    void Awake()
    {
#if UNITY_EDITOR
        if (characterModelPrefab == null)
            characterModelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (animatorController == null)
            animatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorPath);
#endif

        if (GetComponent<CharacterBehaviorStarter>() == null)
            gameObject.AddComponent<CharacterBehaviorStarter>();
    }

    /// <summary>
    /// エディタメニューからモデルを設定する。
    /// </summary>
    public void SetCharacterModel(GameObject model, RuntimeAnimatorController animator)
    {
        characterModelPrefab = model;
        animatorController = animator;
    }

    void Start()
    {
        // エディタメニュー「3D model/Build」から手動でセットアップされる
    }

    /// <summary>
    /// UIから呼び出されるメソッド。シーンを手動でセットアップする。
    /// </summary>
    public void ManualSetupWorld()
    {
        StartCoroutine(SetupWorld());
    }

    IEnumerator SetupWorld()
    {
        EnsureFloor();
        yield return null;

        EnsureNavMesh();
        yield return null;

        EnsureWaypoints();
        yield return null;

        EnsureCharacter();
        yield return null;
        yield return null;

        SetupCamera();
        yield return null;

        CharacterBrain brain = FindObjectOfType<CharacterBrain>();
        if (brain != null)
        {
            brain.BeginBehavior();
        }
        else
        {
            Debug.LogError("[自律キャラ] CharacterBrainが見つかりません");
        }

        Debug.Log("[自律キャラ] セットアップ完了。キャラクターが行動を開始します。");
    }

    /// <summary>
    /// エディタメニューから同期的にセットアップを実行する。
    /// </summary>
    public void EditorSetupWorld()
    {
        Debug.Log("[エディタセットアップ] 開始");

        EnsureFloor();
        EnsureNavMesh();
        EnsureWaypoints();
        EnsureCharacter();
        SetupCamera();

        Debug.Log("[エディタセットアップ] 完了（Playモードで自動開始）");
    }

    void EnsureFloor()
    {
        if (GameObject.Find(FloorName) != null)
            return;

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = FloorName;
        
        // X: 0～719, Z: -30～0
        // Plane: default 10x10
        // スケール: X = 719/10 = 71.9, Z = 30/10 = 3
        // 位置: X中心 = 359.5, Z中心 = -15
        floor.transform.localScale = new Vector3(71.9f, 0.1f, 3f);
        floor.transform.position = new Vector3(359.5f, -0.05f, -15f);
    }

    void EnsureNavMesh()
    {
        NavMeshRuntimeBuilder builder = GetComponent<NavMeshRuntimeBuilder>();
        if (builder == null)
            builder = gameObject.AddComponent<NavMeshRuntimeBuilder>();

        GameObject floor = GameObject.Find(FloorName);
        if (floor != null)
            builder.SetFloorRoot(floor.transform);

        builder.BuildNow();
    }

    void EnsureWaypoints()
    {
        GameObject existingRoot = GameObject.Find(WaypointRootName);
        if (existingRoot != null)
        {
            existingRoot.GetComponent<WaypointManager>()?.CollectWaypointsIfNeeded();
            return;
        }

        GameObject root = new GameObject(WaypointRootName);

        // 床のスケール(71.9, 0.1, 3)、位置(359.5, -0.05, -15)に合わせて
        // X: 0～719, Z: -30～0 の範囲に均等に配置
        // ChatUIとの競合を還けるため、Z=-30側のみに配置
        Vector3[] points =
        {
            new(0f, 0f, -30f),          // 左奥
            new(359.5f, 0f, -30f),      // 中奥
            new(719f, 0f, -30f),        // 右奥
            new(0f, 0f, -15f),          // 左中間
            new(359.5f, 0f, -15f),      // 中中間
            new(719f, 0f, -15f)         // 右中間
        };

        for (int i = 0; i < points.Length; i++)
        {
            GameObject wp = new GameObject($"Waypoint_{(char)('A' + i)}");
            wp.transform.SetParent(root.transform, false);
            wp.transform.position = points[i];
            wp.AddComponent<Waypoint>();
        }

        root.AddComponent<WaypointManager>();
        
        Debug.Log($"[EnsureWaypoints] {points.Length}個のウェイポイントを作成しました");
    }

    void EnsureCharacter()
    {
        GameObject existing = GameObject.Find(CharacterName);
        if (existing != null)
        {
            if (HasRequiredComponents(existing))
            {
                Transform modelTransform = existing.transform.Find("Model");
                if (modelTransform != null)
                    CharacterRigSetup.Configure(existing, modelTransform.gameObject, animatorController);
                WireBrain(existing);
                return;
            }

            Destroy(existing);
        }

        if (characterModelPrefab == null)
        {
            Debug.LogError("[自律キャラ] characterModelPrefab が null です。");
            return;
        }

        Debug.Log($"[EnsureCharacter] モデルプリフェブをインスタンシエート開始: {characterModelPrefab.name}");

        GameObject root = new GameObject(CharacterName);
        GameObject model = Instantiate(characterModelPrefab, root.transform);
        
        if (model == null)
        {
            Debug.LogError("[自律キャラ] Instantiate に失敗しました。");
            Destroy(root);
            return;
        }

        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        Debug.Log($"[EnsureCharacter] モデル作成完了。子オブジェクト数: {root.transform.childCount}");

        // エディタモードでのマテリアル設定の修復
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            RepairMaterialsInEditorMode(model);
        }
#endif

        root.AddComponent<CharacterStateMachine>();

        NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
        // 50倍スケール対応のNavMeshAgent設定
        agent.speed = 50f;           // 50倍スケール対応
        agent.angularSpeed = 360f;
        agent.acceleration = 60f;     // 50倍スケール対応
        agent.stoppingDistance = 15f;  // 50倍スケール対応
        agent.radius = 15f;            // 50倍スケール対応
        agent.height = 90f;            // 50倍スケール対応
        agent.autoBraking = true;

        root.AddComponent<MovementController>();
        root.AddComponent<CharacterBrain>();

        FitCharacter(root, model);
        CharacterRigSetup.Configure(root, model, animatorController);

        WireBrain(root);
    }

#if UNITY_EDITOR
    static void RepairMaterialsInEditorMode(GameObject model)
    {
        // FBXアセットのマテリアルを取得
        string fbxPath = "Assets/untitled.fbx";
        Object[] fbxAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        
        Material[] originalMaterials = System.Array.FindAll(fbxAssets, obj => obj is Material).Cast<Material>().ToArray();
        
        if (originalMaterials.Length == 0)
        {
            Debug.LogWarning("[RepairMaterialsInEditorMode] FBXから直接マテリアルが見つかりません。");
            return;
        }

        Debug.Log($"[RepairMaterialsInEditorMode] FBXアセットから {originalMaterials.Length} 個のマテリアルを取得");

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length && i < originalMaterials.Length; i++)
        {
            Material mat = originalMaterials[i];
            renderers[i].sharedMaterial = mat;
            
            // ナチュラルな見た目に調整
            mat.SetFloat("_Metallic", 0.3f);  // メタリック値を下げる（テカテカを減らす）
            mat.SetFloat("_Glossiness", 0.4f);  // グロッシネス値を下げる
            
            // テクスチャを割り当て直す
            Texture2D roughnessTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/texture_roughness.png");
            if (roughnessTexture != null)
                mat.SetTexture("_MetallicGlossMap", roughnessTexture);
            
            Texture2D normalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/texture_normal.png");
            if (normalTexture != null)
                mat.SetTexture("_BumpMap", normalTexture);
            
            Debug.Log($"[RepairMaterialsInEditorMode] レンダラー{i}に {mat.name} を割り当て（メタリック=0.3, グロッシネス=0.4）");
        }
    }
#endif

    static bool HasRequiredComponents(GameObject character)
    {
        return CharacterRigSetup.IsConfigured(character)
            && character.GetComponent<NavMeshAgent>() != null
            && character.GetComponent<MovementController>() != null
            && character.GetComponent<CharacterBrain>() != null
            && character.GetComponent<CharacterStateMachine>() != null;
    }

    void WireBrain(GameObject character)
    {
        CharacterBrain brain = character.GetComponent<CharacterBrain>();
        WaypointManager manager = FindObjectOfType<WaypointManager>();
        if (brain != null && manager != null)
            brain.SetWaypointManager(manager);
    }

    static void FitCharacter(GameObject root, GameObject model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning("[FitCharacter] レンダラーが見つかりません");
            root.transform.position = Vector3.zero;
            return;
        }

        Debug.Log($"[FitCharacter] レンダラー数: {renderers.Length}");

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Debug.Log($"[FitCharacter] バウンズ: center={bounds.center}, size={bounds.size}, min={bounds.min}, max={bounds.max}");

        float scale = 1f;
        if (bounds.size.y < 0.5f || bounds.size.y > 3f)
            scale = 1.6f / Mathf.Max(bounds.size.y, 0.01f);

        Debug.Log($"[FitCharacter] スケール前: {model.transform.localScale}, 新しいスケール: {Vector3.one * scale}");
        model.transform.localScale = Vector3.one * scale;

        // モデルを50倍の大きさにする
        model.transform.localScale *= 50f;

        renderers = model.GetComponentsInChildren<Renderer>();
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float y = Mathf.Max(0.05f, -bounds.min.y + 0.05f);
        Debug.Log($"[FitCharacter] 新しい位置: y={y}, バウンズ: min={bounds.min}, max={bounds.max}");
        root.transform.position = new Vector3(0f, y, 0f);
    }

    static void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        // 50倍スケールのキャラクターに対応したカメラ配置
        cam.transform.position = new Vector3(0f, 250f, -400f);
        cam.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
    }
}
