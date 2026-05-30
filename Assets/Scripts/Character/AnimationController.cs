using UnityEngine;

/// <summary>
/// 状態に応じて wait / walk / look / sit のアニメを再生する。
/// </summary>
[RequireComponent(typeof(Animator))]
[DefaultExecutionOrder(200)]
public class AnimationController : MonoBehaviour
{
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    static readonly int LookHash = Animator.StringToHash("Look");
    static readonly int SitHash = Animator.StringToHash("Sit");
    static readonly int WaitHash = Animator.StringToHash("Wait");
    static readonly int WalkHash = Animator.StringToHash("Walk");

    [SerializeField] float walkAnimSpeed = 1f;

    Animator animator;
    CharacterStateMachine stateMachine;
    CharacterState lastPlayedState = (CharacterState)(-1);
    bool subscribed;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void OnEnable()
    {
        TrySubscribe();
    }

    void Start()
    {
        TrySubscribe();
        if (stateMachine != null)
            ApplyState(stateMachine.CurrentState);
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void LateUpdate()
    {
        TrySubscribe();
        if (stateMachine == null || animator == null)
            return;

        CharacterState state = stateMachine.CurrentState;
        if (state != lastPlayedState)
            ApplyState(state);

        bool isWalking = state == CharacterState.Walking;
        animator.SetBool(IsMovingHash, isWalking);
        animator.SetFloat(SpeedHash, isWalking ? walkAnimSpeed : 0f);
    }

    /// <summary>
    /// Rig 再構成後に StateMachine へ再接続し、現在状態のアニメを反映する。
    /// </summary>
    public void RefreshBinding()
    {
        Unsubscribe();
        stateMachine = GetComponentInParent<CharacterStateMachine>();
        lastPlayedState = (CharacterState)(-1);
        TrySubscribe();
        if (stateMachine != null)
            ApplyState(stateMachine.CurrentState);
    }

    void TrySubscribe()
    {
        if (subscribed)
            return;

        if (stateMachine == null)
            stateMachine = GetComponentInParent<CharacterStateMachine>();

        if (stateMachine == null)
            return;

        stateMachine.OnStateChanged += HandleStateChanged;
        subscribed = true;
    }

    void Unsubscribe()
    {
        if (!subscribed || stateMachine == null)
            return;

        stateMachine.OnStateChanged -= HandleStateChanged;
        subscribed = false;
    }

    void HandleStateChanged(CharacterState previous, CharacterState next)
    {
        ApplyState(next);
    }

    void ApplyState(CharacterState state)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        lastPlayedState = state;

        switch (state)
        {
            case CharacterState.Walking:
                animator.ResetTrigger(LookHash);
                animator.ResetTrigger(SitHash);
                animator.SetBool(IsMovingHash, true);
                animator.SetFloat(SpeedHash, walkAnimSpeed);
                animator.Play(WalkHash, 0, 0f);
                break;

            case CharacterState.Looking:
                animator.SetBool(IsMovingHash, false);
                animator.SetFloat(SpeedHash, 0f);
                animator.ResetTrigger(SitHash);
                animator.SetTrigger(LookHash);
                break;

            case CharacterState.Sitting:
                animator.SetBool(IsMovingHash, false);
                animator.SetFloat(SpeedHash, 0f);
                animator.ResetTrigger(LookHash);
                animator.SetTrigger(SitHash);
                break;

            default:
                animator.ResetTrigger(LookHash);
                animator.ResetTrigger(SitHash);
                animator.SetBool(IsMovingHash, false);
                animator.SetFloat(SpeedHash, 0f);
                animator.Play(WaitHash, 0, 0f);
                break;
        }
    }

    /// <summary>
    /// トリガーベースのアニメーション（LookやSit）が再生完了したかどうか判定
    /// </summary>
    public bool IsAnimationComplete()
    {
        if (animator == null || !animator.isActiveAndEnabled)
            return true;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        
        // アニメーションが再生中でない場合は完了
        if (stateInfo.normalizedTime >= 1.0f)
            return true;

        return false;
    }
}
