using UnityEngine;
using System.Collections;

public class CollectorRockBreaker : MonoBehaviour
{
    private GameObject vfxObject; // 存储原始子物体的引用
    private GameObject vfxInstance; // 存储复制的子物体实例

    void Start()
    {
        // 获取第一个子物体
        if (transform.childCount > 0)
        {
            vfxObject = transform.GetChild(0).gameObject;
            // 确保原始子物体默认是关闭的
            if (vfxObject != null)
            {
                vfxObject.SetActive(false);
            }
        }
        else
        {
            Debug.LogWarning("CollectorRockBreaker: Requires exactly 1 child GameObject!");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // 检查碰撞物体的名称是否以Explosion_Rock_Trigger开头
        if (other.gameObject.name.StartsWith("Explosion_Rock_Trigger"))
        {
            // 启动协程来管理VFX和销毁
            StartCoroutine(ManageVFXAndDestroy(other.gameObject));
        }
    }

    private IEnumerator ManageVFXAndDestroy(GameObject objectToDestroy)
    {
        // 等待0.5秒后销毁触发物体并创建并激活新子物体
        yield return new WaitForSeconds(0.5f);
        Destroy(objectToDestroy);
        
        if (vfxObject != null)
        {
            // 复制原始子物体并将其作为当前物体的子物体
            vfxInstance = Instantiate(vfxObject, transform);
            vfxInstance.SetActive(true);
        }

        // 等待2秒后（第3秒）关闭复制的子物体
        yield return new WaitForSeconds(2f);
        if (vfxInstance != null)
        {
            vfxInstance.SetActive(false);
        }
    }
}