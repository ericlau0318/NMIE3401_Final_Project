using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Makes the camera follow the player smoothly
public class CameraFollow : MonoBehaviour
{
    public Transform target; // The player to follow
    public Vector3 offset = new Vector3(0, 0, -10); // How far behind/above to stay

    // LateUpdate runs after all other updates, so camera movement is smooth
    private void LateUpdate()
    {
        if(target != null)
        {
            // Keep following the target with the offset
            transform.position = target.position + offset;
        }
    }
}
