using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

// Bullet that flies forward and damages players on hit
public class Bullet : NetworkBehaviour
{
    [SerializeField] private float speed = 20f; // How fast the bullet moves
    [SerializeField] private float lifetime = 10f; // How long before the bullet disappears
    [SerializeField] private int damage = 20; // How much damage it deals

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        GetComponent<Rigidbody2D>().velocity = transform.right * speed;
        StartCoroutine(DespawnAfterLifetime());
    }

    // Wait for the bullet's lifetime to expire, then remove it from the game
    private IEnumerator DespawnAfterLifetime()
    {
        yield return new WaitForSeconds(lifetime);
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
        {            
            Debug.Log("Bullet has been flying for " + lifetime + " seconds, removing it now");
            NetworkObject.Despawn();

        }
    }

    // Check if bullet hit something
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        // If we hit a player, deal damage to them
        var player = other.GetComponent<NetworkPlayerController>();
        if (player != null)
        {
            Debug.Log("Hit a player and dealing " + damage + " damage");
            player.TakeDamageServerRpc(damage);
        }

        // Remove the bullet after hitting something
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            Debug.Log("Bullet hit " + other.gameObject.name + " and disappearing now");
            NetworkObject.Despawn();
        }
    }

}
