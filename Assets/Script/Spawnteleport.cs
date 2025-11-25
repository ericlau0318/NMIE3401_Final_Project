using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Simple teleporter that alternates between two locations
public class Spawnteleport : MonoBehaviour
{
    [SerializeField] private Transform teleportTo1; // First teleport destination
    [SerializeField] private Transform teleportTo2; // Second teleport destination
    private int num = 1; // Which location to teleport to next (1 or 0)

    // When player touches the teleporter, send them to the next location
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (num == 1)
            {
                Debug.Log("Player teleported to location 1");
                other.transform.position = teleportTo1.position;
                num = 0;
            }
            else if (num == 0)
            {
                Debug.Log("Player teleported to location 2");
                other.transform.position = teleportTo2.position;
            }
        }
    }
}
