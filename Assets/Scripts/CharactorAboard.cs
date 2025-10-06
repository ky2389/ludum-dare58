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
            // 跳过凸型 MeshCollider（只收集非凸/或其他类型）
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

    /// <summary>
    /// 清理：移除已被销毁的 collector / collider，避免 MissingReferenceException
    /// </summary>
    private void PruneCollectors()
    {
        for (int i = collectors.Count - 1; i >= 0; i--)
        {
            var info = collectors[i];
            if (info == null || !info.obj) // 整个 collector 被删
            {
                collectors.RemoveAt(i);
                continue;
            }

            var cols = info.nonConvexColliders;
            for (int k = cols.Count - 1; k >= 0; k--)
            {
                if (!cols[k]) cols.RemoveAt(k); // Unity 假 null 判定
            }
        }

        // 如果上一次命中的 collector 已经失效或没有可用 collider，则清空状态
        if (lastCollector != null && (!lastCollector.obj || lastCollector.nonConvexColliders.Count == 0))
        {
            lastCollector = null;
            lastHitPoint = null;
        }
    }

    private void LateUpdate()
    {
        // 先清理失效引用
        PruneCollectors();

        // 更新每个 collector 的位移/旋转变化
        foreach (var info in collectors)
        {
            // info 可能在 Prune 后被移除，这里再做一次保护
            if (info == null || !info.obj) continue;

            info.deltaPos = info.obj.transform.position - info.lastPos;
            info.deltaRot = info.obj.transform.rotation * Quaternion.Inverse(info.lastRot);
            info.lastPos = info.obj.transform.position;
            info.lastRot = info.obj.transform.rotation;
        }

        // 向下射线检测
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, raycastLength, groundMask))
        {
            GameObject hitObj = hit.collider ? hit.collider.gameObject : null;
            var collector = FindCollectorByCollider(hitObj);
            if (collector != null && collector.obj) // collector 仍有效
            {
                Vector3 hitPoint = hit.point;

                if (lastHitPoint.HasValue && lastCollector == collector)
                {
                    // ---- 计算水平位移 delta (忽略Y)
                    Vector3 deltaPos = collector.deltaPos;
                    deltaPos.y = 0;

                    // ---- 提取 collector 的水平旋转(Yaw)
                    Vector3 euler = collector.deltaRot.eulerAngles;
                    float yaw = euler.y;
                    Quaternion deltaYawRot = Quaternion.Euler(0f, yaw, 0f);

                    // ---- 计算水平旋转后的点移动
                    Vector3 rotatedPoint = deltaYawRot * (hitPoint - collector.obj.transform.position) + collector.obj.transform.position;
                    Vector3 moveDelta = rotatedPoint - hitPoint + deltaPos;

                    // ---- 应用到角色（仅 XZ）
                    Vector3 velocity = moveDelta / Time.deltaTime;
                    velocity.y = 0;
                    rb.linearVelocity += velocity; // 保持你原有写法

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

    /// <summary>
    /// 先在缓存的非凸 Collider 里找；找不到时向上回溯父链，匹配到带 "collector" 标签的根，再映射回 collectors。
    /// 这样即便 collector 的某些部件被销毁或层级调整，也能稳妥找到归属。
    /// </summary>
    private CollectorInfo FindCollectorByCollider(GameObject obj)
    {
        if (!obj) return null;

        // 1) 优先：在缓存的 nonConvexColliders 里找（并顺手清掉失效 collider）
        foreach (var collector in collectors)
        {
            if (collector == null || !collector.obj) continue;

            var list = collector.nonConvexColliders;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var col = list[i];
                if (!col) { list.RemoveAt(i); continue; } // 清理失效引用

                // 现在 col 安全，才访问 gameObject
                if (obj == col.gameObject)
                    return collector;
            }
        }

        // 2) 兜底：向上回溯父链，遇到带 "collector" 标签的根
        Transform t = obj.transform;
        while (t != null)
        {
            if (t.CompareTag("collector"))
            {
                GameObject root = t.gameObject;
                // 在 collectors 列表中找到对应的 CollectorInfo
                foreach (var c in collectors)
                {
                    if (c != null && c.obj == root)
                        return c;
                }
                return null;
            }
            t = t.parent;
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
