using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class Bullet : NetworkBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifetime = 10f;
    [SerializeField] private int damage = 20;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        GetComponent<Rigidbody2D>().velocity = transform.right * speed;
        StartCoroutine(DespawnAfterLifetime());
    }

    private IEnumerator DespawnAfterLifetime()
    {
        yield return new WaitForSeconds(lifetime);
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
        {            
            Debug.Log("self Destory");
            NetworkObject.Despawn();

        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        var player = other.GetComponent<NetworkPlayerController>();
        if (player != null)
        {
            player.TakeDamageServerRpc(damage);
        }

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            Debug.Log("hit");
            NetworkObject.Despawn();
        }
    }

}
