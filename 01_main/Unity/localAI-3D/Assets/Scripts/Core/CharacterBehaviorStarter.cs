using System.Collections;
using UnityEngine;

/// <summary>
/// ワールド構築後に Brain の行動を開始する。
/// </summary>
[DefaultExecutionOrder(100)]
public class CharacterBehaviorStarter : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.3f);

        CharacterBrain brain = FindObjectOfType<CharacterBrain>();
        if (brain != null)
            brain.BeginBehavior();
    }
}
