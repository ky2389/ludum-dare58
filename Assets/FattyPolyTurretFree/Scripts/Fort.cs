    using UnityEngine;
using System.Collections;

public class Fort : MonoBehaviour
{
    [Header("旋转限制")] 
    [SerializeField] private float maxPitch = 30f; // 最大上仰角度
    [SerializeField] private float minPitch = -45f; // 最大下俯角度
    [SerializeField] private float maxYaw = 45f; // 最大右偏角度
    [SerializeField] private float minYaw = -45f; // 最大左偏角度

    [Header("射击设置")] 
    [SerializeField] private GameObject bulletSpawnPoint; 
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private GameObject fireVFX;
    [SerializeField] private float bulletSpeed = 20f;
    [SerializeField] private float recoilDistance = 1f;
    [SerializeField] private float recoilDuration = 0.3f;
    [SerializeField] private float attackInterval = 5f;
    [SerializeField] private float rotationSpeed = 15f;

    [Header("检测")] 
    [SerializeField] private float detectionRange = 500f;
    [SerializeField] private LayerMask detectionLayerMask = -1; // What layers to detect (exclude shield layers)

    private Vector3 initialPosition;
    private Quaternion referenceRotation; // 初始参考方向，基于 bulletSpawnPoint
    private bool isRecoiling;
    private float lastFireTime;
    private Transform player;
    private Collider playerCollider;

    private float CalculateFOVAngle()
    {
        float maxFOV = 120f;
        float minFOV = 30f;
        float referenceRange = 500f;
        float fov = Mathf.Lerp(maxFOV, minFOV, detectionRange / referenceRange);
        return Mathf.Clamp(fov, minFOV, maxFOV);
    }

    void Start()
    {
        initialPosition = transform.localPosition;

        // 计算初始参考方向（从 Fort 到 bulletSpawnPoint）
        if (bulletSpawnPoint)
        {
            Vector3 referenceDirection = (bulletSpawnPoint.transform.position - transform.position).normalized;
            referenceRotation = Quaternion.LookRotation(referenceDirection);
        }
        else
        {
            referenceRotation = transform.localRotation; // 回退到当前旋转
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            playerCollider = playerObject.GetComponent<Collider>();
        }
    }

    void Update()
    {
        if (player != null && playerCollider != null)
        {
            // 检查玩家是否在检测范围内（用于决定是否旋转和开炮）
            Vector3 playerCenter = playerCollider.bounds.center;
            float distance = Vector3.Distance(playerCenter, transform.position);
            if (distance <= detectionRange)
            {
                // 计算玩家相对于炮台的方向
                Vector3 directionToPlayer = (playerCenter - transform.position).normalized;
                Vector3 localDirection = transform.parent ? 
                    transform.parent.InverseTransformDirection(directionToPlayer) : directionToPlayer;

                // 计算目标旋转
                Quaternion targetRotation = Quaternion.LookRotation(localDirection);
                Quaternion limitedRotation = ClampRotation(targetRotation);

                // 平滑旋转
                transform.localRotation = Quaternion.RotateTowards(
                    transform.localRotation, 
                    limitedRotation, 
                    rotationSpeed * Time.deltaTime
                );

                // 如果玩家可见且可以开火，则射击
                if (IsPlayerVisible() && Time.time >= lastFireTime + attackInterval)
                {
                    Fire();
                }
            }
        }
    }

    private bool IsPlayerVisible()
    {
        // 使用 bulletSpawnPoint 的当前朝向发射射线
        Vector3 rayOrigin = bulletSpawnPoint ? bulletSpawnPoint.transform.position : transform.position;
        Vector3 rayDirection = bulletSpawnPoint ? bulletSpawnPoint.transform.forward : transform.forward;
        
        // 发射射线，检测是否击中玩家 (使用LayerMask忽略护盾)
        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, detectionRange, detectionLayerMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == playerCollider)
            {
                return true;
            }
        }
        return false;
    }

    private Quaternion ClampRotation(Quaternion targetLocalRotation)
    {
        // 计算目标旋转相对于初始参考方向的偏差
        Quaternion deltaRotation = Quaternion.Inverse(referenceRotation) * targetLocalRotation;
        Vector3 euler = deltaRotation.eulerAngles;

        // 标准化欧拉角到 [-180, 180]
        euler.x = NormalizeAngle(euler.x);
        euler.y = NormalizeAngle(euler.y);

        // 限制俯仰（pitch）和偏航（yaw）角度
        euler.x = Mathf.Clamp(euler.x, minPitch, maxPitch);
        euler.y = Mathf.Clamp(euler.y, minYaw, maxYaw);
        euler.z = 0f; // 禁止滚转

        // 应用限制后的旋转，基于初始参考方向
        return referenceRotation * Quaternion.Euler(euler);
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    public void Fire()
    {
        if (isRecoiling || !bulletPrefab || !bulletSpawnPoint) return;

        lastFireTime = Time.time;

        GameObject bullet = Instantiate(bulletPrefab, bulletSpawnPoint.transform.position, bulletSpawnPoint.transform.rotation);

        if (fireVFX)
            Instantiate(fireVFX, bulletSpawnPoint.transform.position, bulletSpawnPoint.transform.rotation);

        StartCoroutine(ApplyRecoil());
        StartCoroutine(MoveBullet(bullet));
    }

    private IEnumerator ApplyRecoil()
    {
        isRecoiling = true;

        Vector3 recoilDir = bulletSpawnPoint ? -bulletSpawnPoint.transform.forward : -transform.forward;
        Vector3 recoilTarget = initialPosition + recoilDir * recoilDistance;

        float elapsed = 0f;

        while (elapsed < recoilDuration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (recoilDuration * 0.5f);
            transform.localPosition = Vector3.Lerp(initialPosition, recoilTarget, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < recoilDuration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (recoilDuration * 0.5f);
            transform.localPosition = Vector3.Lerp(recoilTarget, initialPosition, t);
            yield return null;
        }

        transform.localPosition = initialPosition;
        isRecoiling = false;
    }

    private IEnumerator MoveBullet(GameObject bullet)
    {
        while (bullet != null)
        {
            bullet.transform.position += bullet.transform.forward * bulletSpeed * Time.deltaTime;
            yield return null;
        }
    }
}