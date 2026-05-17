#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// メニューから FBX の Avatar を再生成する。
/// </summary>
public static class FbxAvatarFixer
{
    [MenuItem("Tools/自律キャラ/FBXのAvatarを再生成")]
    public static void ReimportAndVerify()
    {
        CharacterRigSetup.ReimportFbx();
        Avatar avatar = CharacterRigSetup.FindAvatarInFbx();
        if (avatar != null)
            EditorUtility.DisplayDialog("完了", $"Avatar を確認しました:\n{avatar.name}", "OK");
        else
            EditorUtility.DisplayDialog("エラー", "Avatar がまだ生成されていません。\nFBX を選択して Rig タブを確認してください。", "OK");
    }

    [InitializeOnLoadMethod]
    static void AutoFixOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (!System.IO.File.Exists("Assets/untitled.fbx"))
                return;
            if (CharacterRigSetup.FindAvatarInFbx() != null)
                return;
            CharacterRigSetup.ReimportFbx();
        };
    }
}
#endif
