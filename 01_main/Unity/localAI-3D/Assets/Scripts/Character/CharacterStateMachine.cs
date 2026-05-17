using System;
using UnityEngine;

/// <summary>
/// 現在のキャラクター状態を一元管理する。
/// </summary>
public class CharacterStateMachine : MonoBehaviour
{
    public CharacterState CurrentState { get; private set; } = CharacterState.Idle;

    public event Action<CharacterState, CharacterState> OnStateChanged;

    public void SetState(CharacterState newState)
    {
        if (CurrentState == newState)
            return;

        CharacterState previous = CurrentState;
        CurrentState = newState;
        OnStateChanged?.Invoke(previous, newState);
    }
}
