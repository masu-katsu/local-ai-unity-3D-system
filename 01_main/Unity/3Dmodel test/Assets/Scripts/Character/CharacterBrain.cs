using UnityEngine;

/// <summary>
/// 行動決定のみを担当。walk / wait / look / sit を乱数で切り替える。
/// </summary>
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(CharacterStateMachine))]
public class CharacterBrain : MonoBehaviour
{
    enum BehaviorKind
    {
        Wait,
        Walk,
        Look,
        Sit
    }

    [System.Serializable]
    struct BehaviorWeights
    {
        [Range(0f, 10f)] public float wait;
        [Range(0f, 10f)] public float walk;
        [Range(0f, 10f)] public float look;
        [Range(0f, 10f)] public float sit;

        public static BehaviorWeights DefaultAfterWait => new() { wait = 1.5f, walk = 3.5f, look = 2.5f, sit = 2f };
        public static BehaviorWeights DefaultAfterWalk => new() { wait = 3f, walk = 1.5f, look = 3.5f, sit = 2f };
        public static BehaviorWeights DefaultAfterLook => new() { wait = 2.5f, walk = 2.5f, look = 1f, sit = 2.5f };
        public static BehaviorWeights DefaultAfterSit => new() { wait = 3f, walk = 3.5f, look = 1.5f, sit = 1f };
    }

    [Header("待機 (wait)")]
    [SerializeField] float waitDurationMin = 0.8f;
    [SerializeField] float waitDurationMax = 3.5f;

    [Header("見渡し (look)")]
    [SerializeField] float lookDurationMin = 1.5f;
    [SerializeField] float lookDurationMax = 4.5f;

    [Header("座る (sit)")]
    [SerializeField] float sitDurationMin = 2f;
    [SerializeField] float sitDurationMax = 5.5f;

    [Header("次の行動の重み（大きいほど選ばれやすい）")]
    [SerializeField] BehaviorWeights weightsAfterWait = BehaviorWeights.DefaultAfterWait;
    [SerializeField] BehaviorWeights weightsAfterWalk = BehaviorWeights.DefaultAfterWalk;
    [SerializeField] BehaviorWeights weightsAfterLook = BehaviorWeights.DefaultAfterLook;
    [SerializeField] BehaviorWeights weightsAfterSit = BehaviorWeights.DefaultAfterSit;

    [Header("自然さ")]
    [SerializeField, Range(0f, 1f)] float repeatPenalty = 0.85f;
    [SerializeField, Range(0f, 0.5f)] float extraPauseChance = 0.12f;
    [SerializeField] float extraPauseMin = 0.3f;
    [SerializeField] float extraPauseMax = 1.2f;

    [Header("参照")]
    [SerializeField] WaypointManager waypointManager;

    MovementController movement;
    CharacterStateMachine stateMachine;

    BehaviorKind lastBehavior = BehaviorKind.Wait;
    float actionTimer;
    bool started;

    void Awake()
    {
        movement = GetComponent<MovementController>();
        stateMachine = GetComponent<CharacterStateMachine>();

        if (waypointManager == null)
            waypointManager = FindObjectOfType<WaypointManager>();
    }

    void OnEnable()
    {
        stateMachine.OnStateChanged += HandleStateChanged;
    }

    void OnDisable()
    {
        stateMachine.OnStateChanged -= HandleStateChanged;
    }

    void Update()
    {
        if (!started)
            return;

        if (actionTimer > 0f)
            actionTimer -= Time.deltaTime;

        switch (stateMachine.CurrentState)
        {
            case CharacterState.Idle:
                if (actionTimer <= 0f)
                    PickAndStartNextBehavior(lastBehavior);
                break;

            case CharacterState.Walking:
                if (movement.HasArrived())
                    PickAndStartNextBehavior(BehaviorKind.Walk);
                break;

            case CharacterState.Looking:
                if (actionTimer <= 0f)
                    PickAndStartNextBehavior(BehaviorKind.Look);
                break;

            case CharacterState.Sitting:
                if (actionTimer <= 0f)
                    PickAndStartNextBehavior(BehaviorKind.Sit);
                break;
        }
    }

    public void BeginBehavior()
    {
        if (started)
            return;

        started = true;
        StartWait();
    }

    void PickAndStartNextBehavior(BehaviorKind completed)
    {
        if (extraPauseChance > 0f && Random.value < extraPauseChance)
        {
            lastBehavior = completed;
            StartWait();
            actionTimer = Random.Range(extraPauseMin, extraPauseMax);
            return;
        }

        BehaviorKind next = PickRandomBehavior(completed);
        lastBehavior = next;

        switch (next)
        {
            case BehaviorKind.Wait:
                StartWait();
                break;
            case BehaviorKind.Walk:
                if (!TryStartWalk())
                    StartBehaviorFallback(completed);
                break;
            case BehaviorKind.Look:
                StartLook();
                break;
            case BehaviorKind.Sit:
                StartSit();
                break;
        }
    }

    void StartBehaviorFallback(BehaviorKind completed)
    {
        BehaviorKind fallback = PickRandomBehavior(completed, excludeWalk: true);
        lastBehavior = fallback;

        switch (fallback)
        {
            case BehaviorKind.Look:
                StartLook();
                break;
            case BehaviorKind.Sit:
                StartSit();
                break;
            default:
                StartWait();
                break;
        }
    }

    BehaviorKind PickRandomBehavior(BehaviorKind completed, bool excludeWalk = false)
    {
        BehaviorWeights weights = completed switch
        {
            BehaviorKind.Wait => weightsAfterWait,
            BehaviorKind.Walk => weightsAfterWalk,
            BehaviorKind.Look => weightsAfterLook,
            BehaviorKind.Sit => weightsAfterSit,
            _ => weightsAfterWait
        };

        float wWait = weights.wait;
        float wWalk = excludeWalk || !CanWalk() ? 0f : weights.walk;
        float wLook = weights.look;
        float wSit = weights.sit;

        if (completed == BehaviorKind.Wait)
            wWait *= 1f - repeatPenalty;
        else if (completed == BehaviorKind.Walk)
            wWalk *= 1f - repeatPenalty;
        else if (completed == BehaviorKind.Look)
            wLook *= 1f - repeatPenalty;
        else if (completed == BehaviorKind.Sit)
            wSit *= 1f - repeatPenalty;

        float total = wWait + wWalk + wLook + wSit;
        if (total <= 0f)
            return BehaviorKind.Wait;

        float roll = Random.Range(0f, total);

        if (roll < wWait)
            return BehaviorKind.Wait;
        roll -= wWait;

        if (roll < wWalk)
            return BehaviorKind.Walk;
        roll -= wWalk;

        if (roll < wLook)
            return BehaviorKind.Look;

        return BehaviorKind.Sit;
    }

    bool CanWalk()
    {
        if (waypointManager == null)
            waypointManager = FindObjectOfType<WaypointManager>();

        waypointManager?.CollectWaypointsIfNeeded();
        return waypointManager != null && waypointManager.HasWaypoints;
    }

    void StartWait()
    {
        stateMachine.SetState(CharacterState.Idle);
        actionTimer = Random.Range(waitDurationMin, waitDurationMax);
    }

    void StartLook()
    {
        stateMachine.SetState(CharacterState.Looking);
        actionTimer = Random.Range(lookDurationMin, lookDurationMax);
    }

    void StartSit()
    {
        stateMachine.SetState(CharacterState.Sitting);
        actionTimer = Random.Range(sitDurationMin, sitDurationMax);
    }

    bool TryStartWalk()
    {
        if (!CanWalk())
            return false;

        Vector3 destination = waypointManager.GetRandomDestination(transform.position);
        stateMachine.SetState(CharacterState.Walking);
        movement.MoveTo(destination);
        return true;
    }

    void HandleStateChanged(CharacterState previous, CharacterState next)
    {
        if (next == CharacterState.Walking)
            return;

        movement.Stop();
    }

    public void SetWaypointManager(WaypointManager manager)
    {
        waypointManager = manager;
    }
}
