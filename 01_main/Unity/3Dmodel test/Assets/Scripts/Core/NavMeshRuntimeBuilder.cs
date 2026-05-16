using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// プレイ開始時に床から NavMesh を生成する。
/// </summary>
[DefaultExecutionOrder(-200)]
public class NavMeshRuntimeBuilder : MonoBehaviour
{
    [SerializeField] Transform floorRoot;

    public void SetFloorRoot(Transform root)
    {
        floorRoot = root;
    }

    static NavMeshDataInstance activeInstance;

    public void BuildNow()
    {
        if (floorRoot == null)
        {
            GameObject floor = GameObject.Find("WalkFloor");
            if (floor != null)
                floorRoot = floor.transform;
        }

        BuildFromFloor();
    }

    void OnDestroy()
    {
        if (activeInstance.valid)
        {
            NavMesh.RemoveNavMeshData(activeInstance);
            activeInstance = default;
        }
    }

    public static bool HasNavMeshNear(Vector3 position)
    {
        return NavMesh.SamplePosition(position, out _, 5f, NavMesh.AllAreas);
    }

    void BuildFromFloor()
    {
        var sources = new List<NavMeshBuildSource>();
        var markups = new List<NavMeshBuildMarkup>();
        Bounds bounds = new Bounds(Vector3.zero, new Vector3(30f, 4f, 30f));

        if (floorRoot != null)
        {
            Renderer renderer = floorRoot.GetComponent<Renderer>();
            if (renderer != null)
                bounds = renderer.bounds;

            MeshFilter filter = floorRoot.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Mesh,
                    sourceObject = filter.sharedMesh,
                    transform = filter.transform.localToWorldMatrix,
                    area = 0
                });
            }
        }

        if (sources.Count == 0)
        {
            NavMeshBuilder.CollectSources(
                bounds,
                ~0,
                NavMeshCollectGeometry.RenderMeshes,
                0,
                markups,
                sources);
        }

        if (sources.Count == 0)
        {
            Debug.LogWarning("[NavMesh] 床メッシュが見つかりません。WalkFloor を確認してください。");
            return;
        }

        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);

        NavMeshData data = NavMeshBuilder.BuildNavMeshData(
            settings,
            sources,
            bounds,
            bounds.center,
            Quaternion.identity);

        if (activeInstance.valid)
            NavMesh.RemoveNavMeshData(activeInstance);

        activeInstance = NavMesh.AddNavMeshData(data, bounds.center, Quaternion.identity);

        if (!HasNavMeshNear(bounds.center))
            Debug.LogWarning("[NavMesh] ビルドしましたが SamplePosition に失敗しました。");
    }
}
