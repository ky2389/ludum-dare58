using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class CollectorController : MonoBehaviour
{
    [SerializeField] private GameObject pathParent; // 路径点的父物体
    [SerializeField] private bool loopPath = true; // 是否循环回到第一个点
    public float speed = 5f; // 移动速度（单位/秒）
    public float heightOffset = 1f; // 高度偏移
    public float turnSpeed = 90f; // 转向速度（度/单位距离）

    private void Start()
    {
        if (!pathParent)
        {
            Debug.LogError("PathParent not assigned!");
            return;
        }
        StartMovement();
    }

    private void StartMovement()
    {
        // 获取路径点
        List<Vector3> pathPoints = new List<Vector3>();
        foreach (Transform child in pathParent.transform)
        {
            pathPoints.Add(child.position);
        }

        if (pathPoints.Count < 3)
        {
            Debug.LogWarning("路径点不足，至少需要3个路径点");
            return;
        }

        // 构建完整的路径，包括子路径点（XZ平面）
        List<Vector3> fullPath = new List<Vector3> { transform.position }; // 从当前位置开始
        List<Vector3> originalPoints = new List<Vector3> { transform.position };
        originalPoints.AddRange(pathPoints);

        // 计算每段的弧形子路径点
        for (int i = 0; i < originalPoints.Count - 1; i++)
        {
            Vector3 p1 = originalPoints[i];
            Vector3 p2 = originalPoints[i + 1];
            Vector3 p3 = (i + 2 < originalPoints.Count) ? originalPoints[i + 2] : originalPoints[1]; // 循环到第一个点
            Vector3? center = CalculateCircleCenter(p1, p2, p3);
            List<Vector3> subPoints = new List<Vector3>();

            if (center.HasValue)
            {
                subPoints = CalculateArcPoints(p1, p2, center.Value, 5);
            }
            else
            {
                Debug.LogWarning($"路径点 {i} 到 {i+1} 无法形成圆弧，使用直线插值");
                subPoints = CalculateLinearPoints(p1, p2, 5);
            }

            fullPath.AddRange(subPoints);
            fullPath.Add(p2);
        }

        // 添加从最后一个点到第一个点的路径（如果需要循环）
        if (loopPath)
        {
            Vector3 lastPoint = originalPoints[originalPoints.Count - 1];
            Vector3 firstPoint = originalPoints[1]; // originalPoints[0] 是当前位置
            Vector3 secondPoint = originalPoints[2];
            Vector3? finalCenter = CalculateCircleCenter(lastPoint, firstPoint, secondPoint);
            if (finalCenter.HasValue)
            {
                fullPath.AddRange(CalculateArcPoints(lastPoint, firstPoint, finalCenter.Value, 5));
            }
            else
            {
                fullPath.AddRange(CalculateLinearPoints(lastPoint, firstPoint, 5));
            }
            fullPath.Add(firstPoint);
        }

        // 为每个点调整Y（射线检测地形）
        for (int k = 0; k < fullPath.Count; k++)
        {
            Vector3 pos = fullPath[k];
            Ray ray = new Ray(new Vector3(pos.x, 1000f, pos.z), Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, LayerMask.GetMask("ground")))
            {
                fullPath[k] = new Vector3(pos.x, hit.point.y + heightOffset, pos.z);
            }
            else
            {
                Debug.LogWarning($"射线未击中地形，点{k}使用默认高度");
                fullPath[k] = new Vector3(pos.x, heightOffset, pos.z);
            }
        }

        // 设置起始位置
        transform.position = fullPath[0];

        // 使用DOTween序列控制移动
        Sequence seq = DOTween.Sequence();
        Quaternion currentRot = transform.rotation;
        Vector3 currentPos = fullPath[0];

        // 移动到所有点
        for (int i = 1; i < fullPath.Count; i++)
        {
            Vector3 nextPos = fullPath[i];
            Vector3 dirToNext = (nextPos - currentPos).normalized;
            if (dirToNext.magnitude == 0) continue;

            // 计算移动时间
            float dist = Vector3.Distance(currentPos, nextPos);
            float moveTime = dist / speed;

            // 计算目标朝向（基于下下个点）
            Quaternion targetRot;
            if (i + 1 < fullPath.Count)
            {
                Vector3 nextNextPos = fullPath[i + 1];
                Vector3 dirToNextNext = (nextNextPos - nextPos).normalized;
                targetRot = Quaternion.LookRotation(dirToNextNext);
            }
            else
            {
                // 最后一个点，朝向第一个路径点（如果循环）或保持当前朝向
                if (loopPath)
                {
                    Vector3 firstPathPoint = fullPath[1]; // fullPath[0] 是起始位置
                    Vector3 dirToFirst = (firstPathPoint - nextPos).normalized;
                    targetRot = Quaternion.LookRotation(dirToFirst);
                }
                else
                {
                    targetRot = currentRot; // 保持当前朝向
                }
            }

            // 计算转向角度
            float angle = Quaternion.Angle(currentRot, targetRot);

            // 基于距离的转向速度（度/单位距离）
            float maxAngleForDist = turnSpeed * dist; // 最大允许转向角度
            float adjustedMoveTime = moveTime;

            if (angle > maxAngleForDist)
            {
                // 如果转向角度超过基于距离的限制，延长移动时间
                adjustedMoveTime = (angle / turnSpeed) / speed;
            }

            // 同时移动和转向（仅在移动时转向）
            Tween moveTween = transform.DOMove(nextPos, adjustedMoveTime).SetEase(Ease.Linear);
            Tween rotateTween = transform.DORotateQuaternion(targetRot, adjustedMoveTime).SetEase(Ease.Linear);
            seq.Append(moveTween);
            seq.Join(rotateTween);

            // 更新当前
            currentPos = nextPos;
            currentRot = targetRot;
        }

        // 设置循环（仅当 loopPath 为 true 时）
        if (loopPath)
        {
            seq.SetLoops(-1); // 无限循环
        }
        seq.Play();
    }

    private Vector3? CalculateCircleCenter(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        // 将 Y 置为 0，仅在 XZ 平面计算
        p1.y = 0;
        p2.y = 0;
        p3.y = 0;

        float x1 = p1.x, z1 = p1.z;
        float x2 = p2.x, z2 = p2.z;
        float x3 = p3.x, z3 = p3.z;

        // 计算两条弦的中点和法线
        float ma = (z2 - z1) / (x2 - x1 + 0.0001f); // 防止除以零
        float mb = (z3 - z2) / (x3 - x2 + 0.0001f);
        if (Mathf.Abs(ma - mb) < 0.0001f) return null; // 三点共线

        float centerX = (ma * mb * (z1 - z3) + mb * (x1 + x2) - ma * (x2 + x3)) / (2 * (mb - ma));
        float centerZ = (-1 / ma) * (centerX - (x1 + x2) / 2) + (z1 + z2) / 2;

        return new Vector3(centerX, 0, centerZ);
    }

    private List<Vector3> CalculateArcPoints(Vector3 start, Vector3 end, Vector3 center, int numPoints)
    {
        List<Vector3> points = new List<Vector3>();

        Vector3 vecS = start - center;
        vecS.y = 0;
        Vector3 vecE = end - center;
        vecE.y = 0;

        float radS = vecS.magnitude;
        float radE = vecE.magnitude;

        float angleS = Mathf.Atan2(vecS.z, vecS.x);
        float angleE = Mathf.Atan2(vecE.z, vecE.x);

        float angleDiff = Mathf.DeltaAngle(angleS * Mathf.Rad2Deg, angleE * Mathf.Rad2Deg) * Mathf.Deg2Rad;

        for (int j = 1; j <= numPoints; j++)
        {
            float t = (float)j / (numPoints + 1);
            float angle = angleS + t * angleDiff;
            float rad = Mathf.Lerp(radS, radE, t);
            Vector3 pos = center + new Vector3(Mathf.Cos(angle) * rad, 0, Mathf.Sin(angle) * rad);
            points.Add(pos);
        }

        return points;
    }

    private List<Vector3> CalculateLinearPoints(Vector3 start, Vector3 end, int numPoints)
    {
        List<Vector3> points = new List<Vector3>();
        for (int j = 1; j <= numPoints; j++)
        {
            float t = (float)j / (numPoints + 1);
            Vector3 pos = Vector3.Lerp(start, end, t);
            points.Add(pos);
        }
        return points;
    }
}