using UnityEngine;

#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
#endif

/// <summary>
/// スキンメッシュの骨格に Animator を正しく割り当てる。
/// </summary>
public static class CharacterRigSetup
{
    const string FbxPath = "Assets/untitled.fbx";

    public static void Configure(GameObject characterRoot, GameObject modelInstance, RuntimeAnimatorController controller)
    {
        RemoveMisplacedComponents(characterRoot);

        Animator animator = FindOrCreateModelAnimator(modelInstance);
        if (animator == null)
        {
            Debug.LogError("[CharacterRigSetup] Animator を配置できませんでした。");
            return;
        }

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.enabled = true;

        EnsureAvatar(animator);
        RemoveLegacyAnimation(modelInstance);

        AnimationController animationController = animator.GetComponent<AnimationController>();
        if (animationController == null)
            animationController = animator.gameObject.AddComponent<AnimationController>();

        animationController.RefreshBinding();
    }

    public static bool IsConfigured(GameObject characterRoot)
    {
        if (characterRoot == null)
            return false;

        Animator animator = characterRoot.GetComponentInChildren<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        if (animator.avatar == null || !animator.avatar.isValid)
            return false;

        return characterRoot.GetComponentInChildren<AnimationController>() != null
            && characterRoot.GetComponent<CharacterBrain>() != null
            && characterRoot.GetComponent<MovementController>() != null;
    }

    static void RemoveMisplacedComponents(GameObject characterRoot)
    {
        DestroyComponent(characterRoot.GetComponent<Animator>());
        DestroyComponent(characterRoot.GetComponent<AnimationController>());
    }

    static void DestroyComponent(Component component)
    {
        if (component == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Object.DestroyImmediate(component);
        else
#endif
            Object.Destroy(component);
    }

    static void RemoveLegacyAnimation(GameObject modelInstance)
    {
        Animation legacy = modelInstance.GetComponent<Animation>();
        if (legacy != null)
            DestroyComponent(legacy);
    }

    static Animator FindOrCreateModelAnimator(GameObject modelInstance)
    {
        Animator animator = modelInstance.GetComponent<Animator>();
        if (animator == null)
            animator = modelInstance.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = modelInstance.AddComponent<Animator>();

        return animator;
    }

    static void EnsureAvatar(Animator animator)
    {
        if (animator.avatar != null && animator.avatar.isValid)
            return;

#if UNITY_EDITOR
        ReimportFbx();
        Avatar avatar = FindAvatarInFbx();
        if (avatar != null)
            animator.avatar = avatar;
#endif
    }

#if UNITY_EDITOR
    public static void ReimportFbx()
    {
        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null)
            return;

        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.optimizeGameObjects = false;
        importer.SaveAndReimport();
        AssetDatabase.Refresh();
    }

    public static Avatar FindAvatarInFbx()
    {
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<Avatar>()
            .FirstOrDefault(a => a.isValid);

        if (avatar != null)
            return avatar;

        GameObject fbxRoot = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (fbxRoot == null)
            return null;

        Animator prefabAnimator = fbxRoot.GetComponentInChildren<Animator>(true);
        return prefabAnimator != null && prefabAnimator.avatar != null && prefabAnimator.avatar.isValid
            ? prefabAnimator.avatar
            : null;
    }
#endif
}
