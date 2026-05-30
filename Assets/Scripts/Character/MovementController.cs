using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NavMeshAgent による移動。NavMesh が使えない場合は平面移動にフォールバックする。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class MovementController : MonoBehaviour
{
    [SerializeField] float arrivalThreshold = 0.35f;
    [SerializeField] float fallbackMoveSpeed = 2f;

    NavMeshAgent agent;
    CharacterStateMachine stateMachine;

    bool useFallback;
    Vector3 fallbackTarget;
    bool hasDestination;
    int destinationSetFrame;

    public float CurrentSpeed
    {
        get
        {
            if (useFallback && hasDestination)
                return agent.speed;  // NavMeshAgentの速度設定を反映

            return agent.velocity.magnitude;
        }
    }

    public bool IsMoving => CurrentSpeed > 0.05f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        stateMachine = GetComponent<CharacterStateMachine>();
        agent.updateRotation = true;
        agent.updateUpAxis = true;
    }

    bool CanMove =>
        stateMachine == null || stateMachine.CurrentState == CharacterState.Walking;

    void Start()
    {
        TryPlaceOnNavMesh();
    }

    void Update()
    {
        if (!CanMove || !useFallback || !hasDestination)
            return;

        Vector3 target = fallbackTarget;
        target.y = transform.position.y;

        Vector3 toTarget = target - transform.position;
        if (toTarget.sqrMagnitude > 0.01f)
        {
            Vector3 step = toTarget.normalized * (agent.speed * Time.deltaTime);  // agent.speedを使用
            if (step.sqrMagnitude > toTarget.sqrMagnitude)
                transform.position = target;
            else
                transform.position += step;

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(toTarget.normalized),
                8f * Time.deltaTime);
        }
    }

    public bool MoveTo(Vector3 target)
    {
        if (!CanMove)
            return false;

        hasDestination = true;
        destinationSetFrame = Time.frameCount;

        if (TryPlaceOnNavMesh() && TrySampleOnNavMesh(target, out Vector3 navTarget))
        {
            useFallback = false;
            agent.isStopped = false;
            agent.SetDestination(navTarget);
            return true;
        }

        useFallback = true;
        fallbackTarget = target;
        fallbackTarget.y = transform.position.y;
        return true;
    }

    public void Stop()
    {
        hasDestination = false;
        useFallback = false;

        if (!agent.isOnNavMesh)
            return;

        agent.isStopped = true;
        agent.ResetPath();
    }

    public bool HasArrived()
    {
        if (!hasDestination)
            return false;

        if (Time.frameCount - destinationSetFrame < 15)
            return false;

        bool arrived = false;

        if (useFallback)
        {
            Vector3 flat = transform.position;
            Vector3 target = fallbackTarget;
            flat.y = target.y;
            arrived = Vector3.Distance(flat, target) <= arrivalThreshold;
        }
        else
        {
            if (!agent.isOnNavMesh || agent.pathPending)
                return false;

            if (!agent.hasPath)
                return false;

            if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
                return false;

            if (float.IsInfinity(agent.remainingDistance))
                return false;

            arrived = agent.remainingDistance <= agent.stoppingDistance + arrivalThreshold;
        }

        if (arrived)
        {
            // ウェイポイント到着時にマイナスZ方向を向く
            transform.rotation = Quaternion.LookRotation(Vector3.back);
            hasDestination = false;
            return true;
        }

        return false;
    }

    bool TryPlaceOnNavMesh()
    {
        if (agent.isOnNavMesh)
            return true;

        if (TrySampleOnNavMesh(transform.position, out Vector3 sampled))
        {
            agent.Warp(sampled);
            return agent.isOnNavMesh;
        }

        return false;
    }

    static bool TrySampleOnNavMesh(Vector3 position, out Vector3 result)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 8f, NavMesh.AllAreas))
        {
            result = hit.position;
            return true;
        }

        result = position;
        return false;
    }
}
