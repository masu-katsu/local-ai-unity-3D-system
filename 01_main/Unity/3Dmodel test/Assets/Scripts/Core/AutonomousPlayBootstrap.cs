using System.Collections;
using UnityEngine;
using UnityEngine.AI;

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

    void Start()
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
        EnsureCharacter();
        SetupCamera();

        CharacterBrain brain = FindObjectOfType<CharacterBrain>();
        if (brain != null)
            brain.BeginBehavior();

        Debug.Log("[自律キャラ] セットアップ完了。キャラクターが行動を開始します。");
    }

    void EnsureFloor()
    {
        if (GameObject.Find(FloorName) != null)
            return;

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = FloorName;
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(2.5f, 1f, 2.5f);
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

        Vector3[] points =
        {
            new(-6f, 0f, -6f),
            new(6f, 0f, -6f),
            new(6f, 0f, 6f),
            new(-6f, 0f, 6f),
            new(0f, 0f, -7f),
            new(0f, 0f, 7f)
        };

        for (int i = 0; i < points.Length; i++)
        {
            GameObject wp = new GameObject($"Waypoint_{(char)('A' + i)}");
            wp.transform.SetParent(root.transform, false);
            wp.transform.position = points[i];
            wp.AddComponent<Waypoint>();
        }

        root.AddComponent<WaypointManager>();
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
            Debug.LogError("[自律キャラ] untitled.fbx が見つかりません。");
            return;
        }

        GameObject root = new GameObject(CharacterName);
        GameObject model = Instantiate(characterModelPrefab, root.transform);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        root.AddComponent<CharacterStateMachine>();

        NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
        agent.speed = 2f;
        agent.angularSpeed = 360f;
        agent.acceleration = 12f;
        agent.stoppingDistance = 0.3f;
        agent.radius = 0.3f;
        agent.height = 1.8f;
        agent.autoBraking = true;

        root.AddComponent<MovementController>();
        root.AddComponent<CharacterBrain>();

        FitCharacter(root, model);
        CharacterRigSetup.Configure(root, model, animatorController);

        WireBrain(root);
    }

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
            root.transform.position = Vector3.zero;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float scale = 1f;
        if (bounds.size.y < 0.5f || bounds.size.y > 3f)
            scale = 1.6f / Mathf.Max(bounds.size.y, 0.01f);

        model.transform.localScale = Vector3.one * scale;

        renderers = model.GetComponentsInChildren<Renderer>();
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float y = Mathf.Max(0.05f, -bounds.min.y + 0.05f);
        root.transform.position = new Vector3(0f, y, 0f);
    }

    static void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        cam.transform.position = new Vector3(0f, 8f, -14f);
        cam.transform.rotation = Quaternion.Euler(22f, 0f, 0f);
    }
}
