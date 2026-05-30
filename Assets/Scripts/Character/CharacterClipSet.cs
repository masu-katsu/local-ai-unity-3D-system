using UnityEngine;

/// <summary>
/// FBX から取り出したアニメーションクリップ参照。
/// </summary>
[CreateAssetMenu(fileName = "CharacterClipSet", menuName = "自律キャラ/Character Clip Set")]
public class CharacterClipSet : ScriptableObject
{
    public AnimationClip wait;
    public AnimationClip walk;
    public AnimationClip look;
    public AnimationClip sit;
}
