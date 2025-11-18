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
        Destroy(this.gameObject, lifetime);
        GetComponent<SpriteRenderer>().forceRenderingOff = false;
        // 強制所有客戶端都能看到這顆子彈
        var sprite = GetComponent<SpriteRenderer>();
        if (sprite != null)
            sprite.enabled = true;
    }

    // Server 端自動銷毀

    // 碰撞檢測（用 Trigger 比較好，不會被擋住）
    private void OnTriggerEnter2D(Collider2D other)
    {

        Destroy(this.gameObject);
    }

}
