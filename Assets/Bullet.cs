using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class Bullet : NetworkBehaviour
{
    [SerializeField] float speed = 20f;
    [SerializeField] float lifetime = 2f;      // 自動銷毜避免記憶體洩漏
    [SerializeField] int damage = 10;

    public ulong OwnerClientId;
    public override void OnNetworkSpawn()
    {
        GetComponent<Rigidbody2D>().velocity = transform.right * speed;

        Invoke(nameof(DespawnBullet), lifetime);
    }

    void DespawnBullet()
    {
        if (IsServer)
            GetComponent<NetworkObject>().Despawn();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        GetComponent<NetworkObject>().Despawn();
    }
}
