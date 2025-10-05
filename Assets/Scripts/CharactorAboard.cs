using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CharactorAboard : MonoBehaviour
{
    private Rigidbody rb;

    private class CollectorInfo
    {
        public GameObject obj;
        public List<Collider> nonConvexColliders = new();
        public Vector3 lastPos;
        public Quaternion lastRot;
        public Vector3 deltaPos;
        public Quaternion deltaRot;
    }

    private List<CollectorInfo> collectors = new();
    private Vector3? lastHitPoint;
    private CollectorInfo lastCollector;

    [Header("Raycast Settings")]
    public float raycastLength = 2f;
    public LayerMask groundMask = ~0;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        GameObject[] collectorObjects = GameObject.FindGameObjectsWithTag("collector");
        foreach (var obj in collectorObjects)
        {
            var info = new CollectorInfo { obj = obj };
            info.nonConvexColliders.AddRange(GetAllNonConvexColliders(obj.transform));
            info.lastPos = obj.transform.position;
            info.lastRot = obj.transform.rotation;
            collectors.Add(info);
        }
    }

    private IEnumerable<Collider> GetAllNonConvexColliders(Transform root)
    {
        foreach (var col in root.GetComponents<Collider>())
        {
            if (col is MeshCollider mc && mc.convex)
                continue;
            yield return col;
        }

        foreach (Transform child in root)
        {
            foreach (var c in GetAllNonConvexColliders(child))
                yield return c;
        }
    }

    private void LateUpdate()
    {
        // 更新collector的位移/旋转变化
        foreach (var info in collectors)
        {
            info.deltaPos = info.obj.transform.position - info.lastPos;
            info.deltaRot = info.obj.transform.rotation * Quaternion.Inverse(info.lastRot);
            info.lastPos = info.obj.transform.position;
            info.lastRot = info.obj.transform.rotation;
        }

        // 向下射线检测 collector 的 convex collider
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, raycastLength, groundMask))
        {
            GameObject hitObj = hit.collider.gameObject;
            var collector = FindCollectorByCollider(hitObj);
            if (collector != null)
            {
                Vector3 hitPoint = hit.point;

                if (lastHitPoint.HasValue && lastCollector == collector)
                {
                    // ---- 计算水平位移 delta (忽略Y)
                    Vector3 deltaPos = collector.deltaPos;
                    deltaPos.y = 0;

                    // ---- 提取collector的水平旋转(Yaw)
                    Vector3 euler = collector.deltaRot.eulerAngles;
                    float yaw = euler.y;
                    Quaternion deltaYawRot = Quaternion.Euler(0f, yaw, 0f);

                    // ---- 计算水平旋转后的点移动
                    Vector3 rotatedPoint = deltaYawRot * (hitPoint - collector.obj.transform.position) + collector.obj.transform.position;
                    Vector3 moveDelta = rotatedPoint - hitPoint + deltaPos;

                    // ---- 应用到角色
                    Vector3 velocity = moveDelta / Time.deltaTime;
                    velocity.y = 0; // 只影响XZ
                    rb.linearVelocity += velocity;

                    // ---- 应用水平角速度
                    if (Mathf.Abs(yaw) > 0.01f)
                    {
                        Vector3 angularVel = Vector3.up * yaw * Mathf.Deg2Rad / Time.deltaTime;
                        rb.angularVelocity += angularVel;
                    }
                }

                lastHitPoint = hitPoint;
                lastCollector = collector;
                return;
            }
        }

        lastHitPoint = null;
        lastCollector = null;
    }

    private CollectorInfo FindCollectorByCollider(GameObject obj)
    {
        foreach (var collector in collectors)
        {
            foreach (var col in collector.nonConvexColliders)
            {
                if (obj == col.gameObject)
                    return collector;
            }
        }
        return null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * raycastLength);
    }
#endif
}
