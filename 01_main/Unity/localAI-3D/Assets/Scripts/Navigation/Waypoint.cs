using UnityEngine;

/// <summary>
/// シーン内の移動目的地マーカー。WaypointManager が収集して利用する。
/// </summary>
public class Waypoint : MonoBehaviour
{
    public Vector3 Position => transform.position;

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.35f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.5f);
    }
#endif
}
