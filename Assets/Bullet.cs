using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class Bullet : NetworkBehaviour
{
    [Header("子彈參數")]
    [SerializeField] private float speed = 20f;         // 子彈飛行速度
    [SerializeField] private float lifetime = 3f;       // 存活時間（秒），避免子彈永遠存在
    [SerializeField] private int damage = 20;           // 傷害值（之後可以接到血量系統）

    // 誰射的（用來避免打到自己，或做擊殺統計）
    public ulong OwnerClientId { get; set; }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        GetComponent<Rigidbody2D>().velocity = transform.right * speed;
        Invoke(nameof(DespawnBullet), lifetime);
        GetComponent<SpriteRenderer>().forceRenderingOff = false;
        // 強制所有客戶端都能看到這顆子彈
        var sprite = GetComponent<SpriteRenderer>();
        if (sprite != null)
            sprite.enabled = true;
    }

    // Server 端自動銷毀
    private void DespawnBullet()
    {
        if (IsServer && NetworkObject != null)
        {
            NetworkObject.Despawn();
        }
    }

    // 碰撞檢測（用 Trigger 比較好，不會被擋住）
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;  // 只有 Server 判斷傷害

        // 避免打到射擊者自己（可選）
        var player = other.GetComponent<NetworkPlayerController>();
        if (player != null && player.OwnerClientId == OwnerClientId)
            return;

        // 這裡之後可以接血量系統
        // var health = other.GetComponent<Health>();
        // if (health != null) health.TakeDamage(damage, OwnerClientId);

        Debug.Log($"子彈擊中 {other.name}，造成 {damage} 傷害");

        // 打到東西就消失
        DespawnBullet();
    }

}
