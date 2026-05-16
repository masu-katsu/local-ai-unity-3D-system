#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// 自律移動デモシーンをワンクリックで構築する。
/// </summary>
public static class AutonomousCharacterSetup
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const string FbxPath = "Assets/untitled.fbx";
    const string AnimatorPath = "Assets/Animations/CharacterAnimator.controller";
    const string BootstrapName = "AutonomousWorld";

    [MenuItem("Tools/自律キャラ/シーンをセットアップ")]
    public static void SetupSceneMenu()
    {
        SetupScene();
        EditorUtility.DisplayDialog("セットアップ完了", "SampleScene の構築が完了しました。\nPlay でキャラクターが歩き回ります。", "OK");
    }

    [InitializeOnLoadMethod]
    static void AutoSetupOnEditorLoad()
    {
        EditorApplication.delayCall += TryAutoSetup;
    }

    static void TryAutoSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (!File.Exists(ScenePath) || !File.Exists(FbxPath))
            return;

        if (File.Exists(AnimatorPath) && GameObject.Find(BootstrapName) != null)
            return;

        SetupScene();
        Debug.Log("[自律キャラ] 初回セットアップを実行しました。Play で動作を確認できます。");
    }

    public static void SetupScene()
    {
        EnsureFolders();
        CharacterRigSetup.ReimportFbx();
        RuntimeAnimatorController controller = CreateAnimatorController();
        OpenAndBuildScene(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
            AssetDatabase.CreateFolder("Assets", "Animations");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
    }

    static RuntimeAnimatorController CreateAnimatorController()
    {
        AnimationClip wait = LoadClip("wait");
        AnimationClip walk = LoadClip("walk");
        AnimationClip look = LoadClip("look");
        AnimationClip sit = LoadClip("sit");

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorPath);
        else
            controller.parameters = new AnimatorControllerParameter[0];

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Look", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Sit", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine root = controller.layers[0].stateMachine;
        root.states = new ChildAnimatorState[0];
        root.anyStateTransitions = new AnimatorStateTransition[0];

        AnimatorState waitState = root.AddState("Wait", new Vector3(300, 0, 0));
        AnimatorState walkState = root.AddState("Walk", new Vector3(300, 120, 0));
        AnimatorState lookState = root.AddState("Look", new Vector3(540, 0, 0));
        AnimatorState sitState = root.AddState("Sit", new Vector3(540, 120, 0));

        waitState.motion = wait;
        walkState.motion = walk;
        lookState.motion = look;
        sitState.motion = sit;
        root.defaultState = waitState;

        AddTransition(waitState, walkState, AnimatorConditionMode.If, 1f, "IsMoving");
        AddTransition(walkState, waitState, AnimatorConditionMode.IfNot, 0f, "IsMoving");

        AddTriggerTransition(root, lookState, "Look");
        AddTriggerTransition(root, sitState, "Sit");

        AddExitTransition(lookState, waitState, look != null ? look.length : 2.5f);
        AddExitTransition(sitState, waitState, sit != null ? sit.length : 3.5f);

        EditorUtility.SetDirty(controller);
        return controller;
    }

    static AnimationClip LoadClip(string keyword)
    {
        return AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => c.name.ToLowerInvariant().Contains(keyword));
    }

    static void AddTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold, string param)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.15f;
        t.AddCondition(mode, threshold, param);
    }

    static void AddTriggerTransition(AnimatorStateMachine root, AnimatorState to, string trigger)
    {
        AnimatorStateTransition t = root.AddAnyStateTransition(to);
        t.hasExitTime = false;
        t.duration = 0.1f;
        t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
    }

    static void AddExitTransition(AnimatorState from, AnimatorState to, float exitTime)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = Mathf.Clamp01(exitTime > 0f ? 0.92f : 0.9f);
        t.duration = 0.2f;
    }

    static void OpenAndBuildScene(RuntimeAnimatorController controller)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject floor = FindOrCreate("WalkFloor");
        SetupFloor(floor);

        GameObject bootstrap = FindOrCreate(BootstrapName);
        NavMeshRuntimeBuilder navBuilder = GetOrAdd<NavMeshRuntimeBuilder>(bootstrap);
        SerializedObject navSerialized = new SerializedObject(navBuilder);
        navSerialized.FindProperty("floorRoot").objectReferenceValue = floor.transform;
        navSerialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject waypointRoot = FindOrCreate("WaypointManager");
        WaypointManager waypointManager = GetOrAdd<WaypointManager>(waypointRoot);
        ClearChildren(waypointRoot.transform);

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
            var wp = new GameObject($"Waypoint_{(char)('A' + i)}");
            wp.transform.SetParent(waypointRoot.transform, false);
            wp.transform.position = points[i];
            GetOrAdd<Waypoint>(wp);
        }

        waypointManager.CollectWaypointsIfNeeded();

        SerializedObject wpSerialized = new SerializedObject(waypointManager);
        wpSerialized.FindProperty("collectChildWaypointsOnAwake").boolValue = true;
        wpSerialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject characterRoot = FindOrCreate("Character");
        ClearChildren(characterRoot.transform);

        GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, characterRoot.transform);
        modelInstance.name = "Model";
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;

        GetOrAdd<CharacterStateMachine>(characterRoot);
        GetOrAdd<MovementController>(characterRoot);

        NavMeshAgent agent = GetOrAdd<NavMeshAgent>(characterRoot);
        agent.speed = 1.6f;
        agent.angularSpeed = 180f;
        agent.acceleration = 8f;
        agent.stoppingDistance = 0.35f;
        agent.radius = 0.35f;
        agent.height = 1.7f;
        agent.baseOffset = 0f;

        FitCharacterTransform(characterRoot, modelInstance);
        CharacterRigSetup.Configure(characterRoot, modelInstance, controller);

        CharacterBrain brain = GetOrAdd<CharacterBrain>(characterRoot);
        SerializedObject brainSerialized = new SerializedObject(brain);
        brainSerialized.FindProperty("waypointManager").objectReferenceValue = waypointManager;
        brainSerialized.FindProperty("idleBeforeMoveMin").floatValue = 1.2f;
        brainSerialized.FindProperty("idleBeforeMoveMax").floatValue = 2.5f;
        brainSerialized.ApplyModifiedPropertiesWithoutUndo();

        SetupCamera();
        BakeNavMesh(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void SetupFloor(GameObject floor)
    {
        floor.transform.position = Vector3.zero;
        floor.transform.rotation = Quaternion.identity;
        floor.transform.localScale = new Vector3(2.5f, 1f, 2.5f);

        MeshFilter filter = GetOrAdd<MeshFilter>(floor);
        if (filter.sharedMesh == null)
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Plane.fbx");

        MeshRenderer renderer = GetOrAdd<MeshRenderer>(floor);
        if (renderer.sharedMaterial == null)
            renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");

        MeshCollider collider = GetOrAdd<MeshCollider>(floor);
        collider.sharedMesh = filter.sharedMesh;

        GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.NavigationStatic);
    }

    static void FitCharacterTransform(GameObject root, GameObject model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            root.transform.position = new Vector3(0f, 0f, 0f);
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float scale = 1f;
        if (bounds.size.y < 0.5f || bounds.size.y > 5f)
            scale = 1.7f / Mathf.Max(bounds.size.y, 0.01f);

        model.transform.localScale = Vector3.one * scale;
        renderers = model.GetComponentsInChildren<Renderer>();
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        root.transform.position = new Vector3(0f, -bounds.min.y + 0.02f, 0f);
    }

    static void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        cam.transform.position = new Vector3(0f, 7f, -12f);
        cam.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
    }

    static void BakeNavMesh(Scene scene)
    {
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
        EditorSceneManager.MarkSceneDirty(scene);
    }

    static GameObject FindOrCreate(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go == null)
            go = new GameObject(name);
        return go;
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component == null)
            component = go.AddComponent<T>();
        return component;
    }

    static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }
}
#endif
