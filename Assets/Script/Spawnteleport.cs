using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawnteleport : MonoBehaviour
{
    [SerializeField] private Transform teleportTo1;
    [SerializeField] private Transform teleportTo2;
    private int num=1;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("TP");
            if (num == 1)
            {
                other.transform.position = teleportTo1.position;
                num = 0;
                Debug.Log("To 1");
            }
            else if (num == 0)
            {
                other.transform.position = teleportTo2.position;
                Debug.Log("To 2");
            }
        }
    }
}
