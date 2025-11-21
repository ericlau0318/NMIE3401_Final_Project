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
        // 確保子彈的渲染器始終啟用（所有客戶端執行）
        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.enabled = true;
        }
        
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

        // 檢查是否擊中玩家
        var player = other.GetComponent<NetworkPlayerController>();
        if (player != null)
        {
            // 對玩家造成傷害
            player.TakeDamageServerRpc(damage);
            Debug.Log($"子彈擊中玩家，造成 {damage} 點傷害");
        }

        // 銷毀子彈
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            Debug.Log("hit");
            NetworkObject.Despawn();
        }
    }

}
