using System;
using UnityEngine;

public class Text_FaceCamera : MonoBehaviour
{
    void LateUpdate()
    {
        Vector3 camPos = Camera.main.transform.position;
        camPos.y = transform.position.y; // lock Y axis
        transform.LookAt(camPos);
        transform.Rotate(0, 180f, 0);
    }

}
