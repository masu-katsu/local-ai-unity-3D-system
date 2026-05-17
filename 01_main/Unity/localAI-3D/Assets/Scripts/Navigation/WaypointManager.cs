using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// シーン内 Waypoint の管理とランダム目的地の提供。
/// </summary>
public class WaypointManager : MonoBehaviour
{
    [SerializeField] bool collectChildWaypointsOnAwake = true;
    [SerializeField] Transform[] waypoints;
    [SerializeField] float minDistanceFromCurrent = 1f;

    int lastWaypointIndex = -1;

    void Awake()
    {
        CollectWaypointsIfNeeded();
    }

    void Start()
    {
        CollectWaypointsIfNeeded();
    }

    public bool HasWaypoints => waypoints != null && waypoints.Length > 0;

    public void CollectWaypointsIfNeeded()
    {
        if (!collectChildWaypointsOnAwake)
            return;

        if (waypoints == null || waypoints.Length == 0)
            CollectChildWaypoints();
    }

    public Vector3 GetRandomDestination(Vector3 currentPosition)
    {
        CollectWaypointsIfNeeded();

        if (!HasWaypoints)
            return currentPosition;

        if (waypoints.Length == 1)
            return waypoints[0].position;

        int index = PickRandomIndex(currentPosition);
        lastWaypointIndex = index;
        return waypoints[index].position;
    }

    void CollectChildWaypoints()
    {
        var found = GetComponentsInChildren<Waypoint>();
        if (found.Length == 0)
            return;

        var list = new List<Transform>(found.Length);
        foreach (Waypoint wp in found)
            list.Add(wp.transform);

        waypoints = list.ToArray();
    }

    int PickRandomIndex(Vector3 currentPosition)
    {
        int attempts = waypoints.Length * 2;
        int bestIndex = Random.Range(0, waypoints.Length);
        float bestScore = -1f;

        for (int i = 0; i < attempts; i++)
        {
            int candidate = Random.Range(0, waypoints.Length);
            if (candidate == lastWaypointIndex && waypoints.Length > 2)
                continue;

            float distance = Vector3.Distance(currentPosition, waypoints[candidate].position);
            if (distance >= minDistanceFromCurrent)
                return candidate;

            if (distance > bestScore)
            {
                bestScore = distance;
                bestIndex = candidate;
            }
        }

        return bestIndex;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (waypoints == null)
            return;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        foreach (Transform wp in waypoints)
        {
            if (wp != null)
                Gizmos.DrawLine(transform.position, wp.position);
        }
    }
#endif
}
