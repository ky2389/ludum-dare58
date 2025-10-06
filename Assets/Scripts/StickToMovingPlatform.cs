using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class StickToMovingPlatform : MonoBehaviour
{
    [Header("Which colliders count as platform")]
    public LayerMask platformMask;          // 选中你的 MovingPlatform 层
    public float groundRay = 0.4f;          // 探测地面射线长度
    public bool rotateWithPlatform = true;  // 平台旋转时是否跟着转

    private CharacterController cc;
    private Transform platform;             // 当前踩着的平台（取其根）
    private Vector3 lastPlatPos;
    private Quaternion lastPlatRot;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    void LateUpdate()
    {
        // 只在贴地时考虑平台
        if (IsGroundedOnPlatform(out Transform plat))
        {
            if (platform != plat)
            {
                platform = plat;
                lastPlatPos = platform.position;
                lastPlatRot = platform.rotation;
            }

            // 1) 平台平移增量
            Vector3 posDelta = platform.position - lastPlatPos;
            if (posDelta.sqrMagnitude > 0f)
                cc.Move(posDelta);

            // 2) 平台旋转增量（可关）
            if (rotateWithPlatform)
            {
                Quaternion rotDelta = platform.rotation * Quaternion.Inverse(lastPlatRot);

                // 围绕平台旋转带来的位移补偿
                Vector3 pivot = platform.position;
                Vector3 before = transform.position - pivot;
                Vector3 after = rotDelta * before;
                Vector3 rotMove = (pivot + after) - (pivot + before);
                if (rotMove.sqrMagnitude > 0f)
                    cc.Move(rotMove);
            }

            lastPlatPos = platform.position;
            lastPlatRot = platform.rotation;
        }
        else
        {
            platform = null;
        }
    }

    // 用向下 Ray 检测脚下是否踩在平台层上
    bool IsGroundedOnPlatform(out Transform platRoot)
    {
        platRoot = null;
        if (!cc || !cc.isGrounded) return false;

        Vector3 origin = transform.position + Vector3.up * 0.1f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundRay, platformMask, QueryTriggerInteraction.Ignore))
        {
            // 命中哪一层算平台就行；取根节点更稳
            platRoot = hit.collider.transform.root;
            return true;
        }
        return false;
    }
}
