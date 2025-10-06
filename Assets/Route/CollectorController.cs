using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using DG.Tweening;

public class CollectorController : MonoBehaviour
{
    [SerializeField] private GameObject pathParent; // 路径点的父物体
    [SerializeField] private bool loopPath = true; // 是否循环回到第一个点
    public float speed = 5f; // 移动速度（单位/秒）
    public float heightOffset = 1f; // 高度偏移
    public float turnSpeed = 90f; // 转向速度（度/单位距离）
    [SerializeField] private int smoothFactor = 10; // 平滑插值密度
    [SerializeField] private float positionLerpSpeed = 10f; // 位置平滑插值速度
    [SerializeField] private float rotationLerpSpeed = 10f; // 旋转平滑插值速度

    private Vector3 targetPosition; // 当前目标位置
    private Quaternion targetRotation; // 当前目标旋转
    private bool isMoving = false; // 是否正在移动
    
    //appended variables
    private Sequence moveSequence;
    
    private bool isDecelerating = false; 
    private Vector3 currentVelocity = Vector3.zero; 
    private float initialSpeed; // Stores the speed at the moment of stopping
    private float decelerationDuration; // How long the final move takes
    private float startTime; // To track the start of the final deceleration
    
    private List<Animator> _animators = new List<Animator>();
    private float initialAnimSpeed = 1.0f; // Assuming the animation starts at normal speed

    private void Start()
    {
        decelerationDuration=Random.Range(5f, 11f);
        
        targetPosition = transform.position;
        targetRotation = transform.rotation;
        if (!pathParent)
        {
            Debug.LogError("PathParent not assigned!");
            return;
        }
        StartMovement();

        _animators = GetComponentsInChildren<Animator>(true).ToList();
        //Debug.Log(_animators.Count);
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

        // 构建完整路径
        List<Vector3> fullPath = new List<Vector3> { transform.position };
        List<Vector3> originalPoints = new List<Vector3> { transform.position };
        originalPoints.AddRange(pathPoints);

        for (int i = 0; i < originalPoints.Count - 1; i++)
        {
            Vector3 p1 = originalPoints[i];
            Vector3 p2 = originalPoints[i + 1];
            Vector3 p3 = (i + 2 < originalPoints.Count) ? originalPoints[i + 2] : originalPoints[1]; // 循环
            Vector3? center = CalculateCircleCenter(p1, p2, p3);
            List<Vector3> subPoints;

            if (center.HasValue)
            {
                subPoints = CalculateArcPoints(p1, p2, center.Value, 5);
            }
            else
            {
                subPoints = CalculateLinearPoints(p1, p2, 5);
            }

            fullPath.AddRange(subPoints);
            fullPath.Add(p2);
        }

        // 循环补尾
        if (loopPath)
        {
            Vector3 lastPoint = originalPoints[originalPoints.Count - 1];
            Vector3 firstPoint = originalPoints[1];
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

        // 射线高度修正
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
                fullPath[k] = new Vector3(pos.x, heightOffset, pos.z);
            }
        }

        // 平滑插值路径
        fullPath = SmoothPath(fullPath, smoothFactor);

        // 设置起始位置
        transform.position = fullPath[0];
        targetPosition = fullPath[0];

        // DOTween 移动控制
        moveSequence = DOTween.Sequence();
        Quaternion currentRot = transform.rotation;
        Vector3 currentPos = fullPath[0];

        for (int i = 1; i < fullPath.Count; i++)
        {
            Vector3 nextPos = fullPath[i];
            Vector3 dirToNext = (nextPos - currentPos).normalized;
            if (dirToNext.magnitude == 0) continue;

            float dist = Vector3.Distance(currentPos, nextPos);
            float moveTime = dist / speed;

            Quaternion targetRot;
            if (i + 1 < fullPath.Count)
            {
                Vector3 nextNextPos = fullPath[i + 1];
                Vector3 dirToNextNext = (nextNextPos - nextPos).normalized;
                targetRot = Quaternion.LookRotation(dirToNextNext);
            }
            else
            {
                if (loopPath)
                {
                    Vector3 firstPathPoint = fullPath[1];
                    Vector3 dirToFirst = (firstPathPoint - nextPos).normalized;
                    targetRot = Quaternion.LookRotation(dirToFirst);
                }
                else
                {
                    targetRot = currentRot;
                }
            }

            float angle = Quaternion.Angle(currentRot, targetRot);
            float maxAngleForDist = turnSpeed * dist;
            float adjustedMoveTime = moveTime;

            if (angle > maxAngleForDist)
            {
                adjustedMoveTime = (angle / turnSpeed) / speed;
            }

            Tween moveTween = transform.DOMove(nextPos, adjustedMoveTime).SetEase(Ease.Linear)
                .OnUpdate(() =>
                {
                    targetPosition = transform.position;
                    targetRotation = transform.rotation;
                });
            Tween rotateTween = transform.DORotateQuaternion(targetRot, adjustedMoveTime).SetEase(Ease.Linear)
                .OnUpdate(() =>
                {
                    targetPosition = transform.position;
                    targetRotation = transform.rotation;
                });
            moveSequence.Append(moveTween);
            moveSequence.Join(rotateTween);

            currentPos = nextPos;
            currentRot = targetRot;
        }

        if (loopPath)
        {
            moveSequence.SetLoops(-1);
        }
        moveSequence.OnStart(() => isMoving = true);
        moveSequence.OnComplete(() => isMoving = false);
        moveSequence.Play();
    }

    // private void FixedUpdate()
    // {
    //     if (isMoving)
    //     {
    //         // 物理步长平滑插值
    //         transform.position = Vector3.Lerp(transform.position, targetPosition, positionLerpSpeed * Time.fixedDeltaTime);
    //         transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationLerpSpeed * Time.fixedDeltaTime);
    //     }
    // }
    //
    // private void Update()
    // {
    //     if (isMoving)
    //     {
    //         // 每帧平滑插值
    //         transform.position = Vector3.Lerp(transform.position, targetPosition, positionLerpSpeed * Time.deltaTime);
    //         transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationLerpSpeed * Time.deltaTime);
    //     }
    // }
    //
    // private void LateUpdate()
    // {
    //     if (isMoving)
    //     {
    //         // 渲染前平滑插值
    //         transform.position = Vector3.Lerp(transform.position, targetPosition, positionLerpSpeed * Time.deltaTime);
    //         transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationLerpSpeed * Time.deltaTime);
    //     }
    // }
    
    private void FixedUpdate()
    {
        if (isDecelerating)
        {
            // 1. Calculate interpolation factor (t)
            float elapsedTime = Time.time - startTime;
            float t = Mathf.Clamp01(elapsedTime / decelerationDuration); // t goes from 0 to 1
            float easedT = t * t; // Simple Ease-Out Quad formula

            // 2. Interpolate the speed from initialSpeed to 0
            float currentSpeed = Mathf.Lerp(initialSpeed, 0f, t);
            float currentAnimSpeed = Mathf.Lerp(initialAnimSpeed, 0f, easedT);

            // 3. Apply the new velocity
            Vector3 newVelocity = currentVelocity.normalized * currentSpeed;
            
            foreach (Animator animator in _animators)
            {
                animator.speed = currentAnimSpeed;
            }

            // 4. Apply movement
            transform.position += newVelocity * Time.fixedDeltaTime;

            // 5. Check for stop condition
            if (t >= 1f)
            {
                currentVelocity = Vector3.zero;
                isDecelerating = false;
                // Ensure no rotation is applied by the old system
                targetRotation = transform.rotation;

                foreach (Animator animator in _animators)
                {
                    animator.speed = 0f; // Ensure it's exactly 0 at the end
                }
              
            }
        
            // Ensure no rotation is applied during the straight-line coast
            transform.rotation = targetRotation;
        }
        // DELETE/COMMENT OUT the old redundant Lerp code here
    }

    // ====== 辅助函数 ======

    private Vector3? CalculateCircleCenter(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        p1.y = 0;
        p2.y = 0;
        p3.y = 0;

        float x1 = p1.x, z1 = p1.z;
        float x2 = p2.x, z2 = p2.z;
        float x3 = p3.x, z3 = p3.z;

        float ma = (z2 - z1) / (x2 - x1 + 0.0001f);
        float mb = (z3 - z2) / (x3 - x2 + 0.0001f);
        if (Mathf.Abs(ma - mb) < 0.0001f) return null;

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

    private List<Vector3> SmoothPath(List<Vector3> originalPoints, int smoothFactor)
    {
        if (originalPoints.Count < 4) return originalPoints;

        List<Vector3> smoothedPoints = new List<Vector3>();
        for (int i = 0; i < originalPoints.Count - 1; i++)
        {
            Vector3 p0 = i == 0 ? originalPoints[i] : originalPoints[i - 1];
            Vector3 p1 = originalPoints[i];
            Vector3 p2 = originalPoints[i + 1];
            Vector3 p3 = (i + 2 < originalPoints.Count) ? originalPoints[i + 2] : originalPoints[i + 1];

            for (int j = 0; j < smoothFactor; j++)
            {
                float t = j / (float)smoothFactor;
                Vector3 point = CatmullRom(p0, p1, p2, p3, t);
                smoothedPoints.Add(point);
            }
        }

        smoothedPoints.Add(originalPoints[originalPoints.Count - 1]);
        return smoothedPoints;
    }

    private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }



    #region publically accessible functions
    
    public void StopMovement()
    {
        if (moveSequence != null && moveSequence.IsActive())
        {
            moveSequence.Kill(true); // true means complete any currently running step instantly
            isMoving = false; // Manually set the state
            
            // OPTIONAL: Reset the targets so the redundant Lerp calls stop trying to move
            targetPosition = transform.position;
            targetRotation = transform.rotation;
        }
    }

    public void StartGradualStop()
    {
        if (isMoving)
        {
            // Stop any existing stop attempt
            StopCoroutine(nameof(TimedDecelerationCoroutine));
            StartCoroutine(TimedDecelerationCoroutine());
        }
    }

    private IEnumerator TimedDecelerationCoroutine()
    {
        
        float randomDelay = Random.Range(0.5f, 1.5f);

        // 2. Wait for the random time
        yield return new WaitForSeconds(randomDelay);
        
        // 3. Start the Deceleration
        if (moveSequence != null && moveSequence.IsActive())
        {
            // 1. CAPTURE VELOCITY AND SPEED
            Vector3 direction = transform.forward; 
            currentVelocity = direction * speed; 
            initialSpeed = speed; // Store the vehicle's current speed

            // 2. KILL THE PATH SEQUENCE
            moveSequence.Kill(false);
        
            // 3. SET NEW STATE
            isMoving = false;
            isDecelerating = true;
            startTime = Time.time;
        }
    }
    

    #endregion
}