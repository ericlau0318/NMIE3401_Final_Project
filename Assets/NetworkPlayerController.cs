using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Mathematics;
using UnityEngine.InputSystem;
public class NetworkPlayerController : NetworkBehaviour
{
    private PlayerInputAction pActions;
    [SerializeField] float moveSpeed = 1f;
    [SerializeField] float jumpForce = 5f;
    [SerializeField] Transform groundCheck;
    [SerializeField] float CheckRadius = 0.2f;
    [SerializeField] LayerMask groundLayer = 1 << 6;
    [SerializeField] Transform gunTransform; // 拖你的Gun物件
    [SerializeField] Transform firePoint; // 拖槍口
    [SerializeField] GameObject bulletPrefab; // 拖你的Bullet Prefab
    [SerializeField] float fireRate = 0.2f;
    [SerializeField] Transform graphicsTransform;
    [SerializeField] private SpriteRenderer gunSpriteRenderer;
    
    // 血量系統
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float respawnDelay = 3f;
    
    private NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    
    private NetworkVariable<bool> isDead = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> isGunFlipped = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    
    // 儲存渲染器狀態，用於正確恢復
    private List<SpriteRenderer> cachedRenderers = new List<SpriteRenderer>();
    
    private Vector2 finalMove;
    private Rigidbody2D rb;
    private bool isGrounded;
    private float nextFireTime;
    private Camera mainCam;
    void Awake()
    {
        isGunFlipped.OnValueChanged += OnGunFlipChanged;
        currentHealth.OnValueChanged += OnHealthChanged;
        isDead.OnValueChanged += OnDeadStateChanged;
    }

    private void OnGunFlipChanged(bool previous, bool current)
    {
        if (gunSpriteRenderer != null)
            gunSpriteRenderer.flipY = current;
    }

    private void OnHealthChanged(float previous, float current)
    {
        Debug.Log($"玩家 {OwnerClientId} 血量變化: {previous} -> {current}");
        
        // 這裡可以更新UI或播放受傷動畫
        if (current < previous)
        {
            // 受傷效果
            Debug.Log($"玩家 {OwnerClientId} 受到傷害!");
        }
    }

    private void OnDeadStateChanged(bool previous, bool current)
    {
        if (current)
        {
            Debug.Log($"[{(IsServer ? "Server" : "Client")}] 玩家 {OwnerClientId} 死亡!");
            HandleDeath();
        }
        else
        {
            Debug.Log($"[{(IsServer ? "Server" : "Client")}] 玩家 {OwnerClientId} 準備重生!");
        }
    }
    void Start()
    {
        pActions = new PlayerInputAction();
        pActions.Enable();
        rb = GetComponent<Rigidbody2D>();
        
        // 緩存所有渲染器（包含子物件）
        CacheRenderers();
        
        // 在服務器端初始化血量
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            isDead.Value = false;
        }
    }
    
    /// <summary>
    /// 緩存所有渲染器
    /// </summary>
    private void CacheRenderers()
    {
        cachedRenderers.Clear();
        // 獲取所有子物件的渲染器（包含當前物件）
        var renderers = GetComponentsInChildren<SpriteRenderer>(true); // true = 包含被禁用的
        cachedRenderers.AddRange(renderers);
        Debug.Log($"緩存了 {cachedRenderers.Count} 個渲染器");
    }
    // Update is called once per frame
    void Update()
    {
        if (!IsOwner) return;
        
        // 死亡時禁用所有輸入
        if (isDead.Value) return;
        var move = pActions.Player.Move.ReadValue<Vector2>();
        finalMove = new Vector2(move.x, 0) * moveSpeed;
        rb.velocity = new Vector2(finalMove.x, rb.velocity.y);
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, CheckRadius, groundLayer);
        if (pActions.Player.Jump.IsPressed() && isGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        }

        if (mainCam == null)
        {
            mainCam = Camera.main;
            if (mainCam == null) return;
        }

        Vector3 mouseWorldPos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mouseWorldPos.z = 0f;

        Vector3 direction = (mouseWorldPos - gunTransform.position).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        bool facingLeft = mouseWorldPos.x < transform.position.x;

        // 1. 槍轉向（360° 完全正確）
        gunTransform.rotation = Quaternion.Euler(0f, 0f, angle);

        // 2. 角色視覺翻轉 → 這行會被 Player 根物件的 NetworkTransform 同步 Scale X
        graphicsTransform.localScale = new Vector3(facingLeft ? -1f : 1f, 1f, 1f);

        // 3. 槍上下翻轉 → 只由 Owner 寫入 NetworkVariable
        isGunFlipped.Value = facingLeft;

        // 射擊
        if (Mouse.current.leftButton.isPressed && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            ShootServerRpc();
        }
    }
    [ServerRpc(RequireOwnership = false)]
    private void ShootServerRpc(ServerRpcParams rpcParams = default)
    {
        // 只讓真正擁有者射擊
        GameObject bulletGO = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        bulletGO.GetComponent<NetworkObject>().Spawn(true);
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // 確保在網絡生成時緩存渲染器
        if (cachedRenderers.Count == 0)
        {
            CacheRenderers();
        }
        
        if (IsOwner)
        {
            mainCam = GetComponentInChildren<Camera>();

            if (mainCam != null)
            {
                mainCam.enabled = true;
                var follow = mainCam.GetComponent<CameraFollow>();
                if (follow != null)
                {
                    follow.target = transform;
                }
            }
        }
        else
        {
            // 非擁有者：禁用相機和AudioListener
            mainCam = GetComponentInChildren<Camera>();
            if (mainCam != null)
            {
                mainCam.enabled = false;
                var audioListener = mainCam.GetComponent<AudioListener>();
                if (audioListener != null)
                {
                    audioListener.enabled = false;
                }
            }
        }
    }

    // ==================== 血量與傷害系統 ====================
    
    /// <summary>
    /// 受到傷害（可以從任何客戶端調用，在服務器上執行）
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(float damage, ServerRpcParams rpcParams = default)
    {
        if (isDead.Value) return; // 已經死亡則不再受傷
        
        currentHealth.Value = Mathf.Max(0, currentHealth.Value - damage);
        Debug.Log($"[Server] 玩家 {OwnerClientId} 受到 {damage} 點傷害，剩餘血量: {currentHealth.Value}");
        
        // 檢查是否死亡
        if (currentHealth.Value <= 0)
        {
            Debug.Log($"[Server] 玩家 {OwnerClientId} 死亡，準備重生");
            isDead.Value = true;
            
            // 立即停止物理模擬（服務器端）
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            
            StartCoroutine(RespawnCoroutine());
        }
    }
    
    /// <summary>
    /// 治療（恢復血量）
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void HealServerRpc(float healAmount, ServerRpcParams rpcParams = default)
    {
        if (isDead.Value) return; // 死亡狀態不能治療
        
        currentHealth.Value = Mathf.Min(maxHealth, currentHealth.Value + healAmount);
        Debug.Log($"玩家 {OwnerClientId} 恢復 {healAmount} 點血量，當前血量: {currentHealth.Value}");
    }
    
    /// <summary>
    /// 處理死亡效果（所有客戶端執行）
    /// </summary>
    private void HandleDeath()
    {
        Debug.Log($"[{(IsServer ? "Server" : "Client")}] HandleDeath: 開始處理玩家 {OwnerClientId} 的死亡效果");
        
        // 如果沒有緩存渲染器，立即緩存
        if (cachedRenderers.Count == 0)
        {
            CacheRenderers();
        }
        
        // 禁用碰撞
        var collider = GetComponent<Collider2D>();
        if (collider != null) 
        {
            collider.enabled = false;
            Debug.Log($"[{(IsServer ? "Server" : "Client")}] 禁用碰撞器");
        }
        
        // 禁用所有緩存的渲染器
        int disabledCount = 0;
        foreach (var renderer in cachedRenderers)
        {
            if (renderer != null && renderer.enabled)
            {
                renderer.enabled = false;
                disabledCount++;
            }
        }
        Debug.Log($"[{(IsServer ? "Server" : "Client")}] 禁用 {disabledCount} 個渲染器");
        
        // 停止物理
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
            Debug.Log($"[{(IsServer ? "Server" : "Client")}] 停止物理模擬");
        }
    }
    
    /// <summary>
    /// 重生協程（僅在服務器上執行）
    /// </summary>
    private IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(respawnDelay);
        RespawnPlayer();
    }
    
    /// <summary>
    /// 重生玩家
    /// </summary>
    private void RespawnPlayer()
    {
        if (!IsServer) return;
        
        Debug.Log($"[Server] 開始重生玩家 {OwnerClientId}");
        
        // 重置血量和死亡狀態
        currentHealth.Value = maxHealth;
        
        // 找到重生點
        Vector3 respawnPosition = Vector3.zero;
        
        // 優先使用 RespawnManager
        if (RespawnManager.Instance != null)
        {
            respawnPosition = RespawnManager.Instance.GetRespawnPosition();
            Debug.Log($"[Server] 使用 RespawnManager 獲取重生點: {respawnPosition}");
        }
        else
        {
            // 備用方案：尋找重生點標籤
            GameObject spawnPoint = GameObject.FindGameObjectWithTag("Respawn");
            if (spawnPoint != null)
            {
                respawnPosition = spawnPoint.transform.position;
                Debug.Log($"[Server] 使用標籤 'Respawn' 獲取重生點: {respawnPosition}");
            }
            else
            {
                Debug.LogWarning($"[Server] 沒有找到重生點，使用原點 (0,0,0)");
            }
        }
        
        // 先停止物理模擬，重置速度
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        
        // 設置新位置
        transform.position = respawnPosition;
        Debug.Log($"[Server] 設置玩家 {OwnerClientId} 位置為: {respawnPosition}");
        
        // 重要：在設置位置後才改變死亡狀態，確保位置先同步
        isDead.Value = false;
        
        // 通知所有客戶端恢復渲染和物理
        RespawnClientRpc(respawnPosition);
    }
    
    /// <summary>
    /// 在所有客戶端上恢復玩家渲染和位置
    /// </summary>
    [ClientRpc]
    private void RespawnClientRpc(Vector3 respawnPosition)
    {
        Debug.Log($"[{(IsServer ? "Server" : "Client")}] RespawnClientRpc: 開始重生玩家 {OwnerClientId} 在位置 {respawnPosition}");
        
        // 確保所有客戶端都設置相同的位置
        transform.position = respawnPosition;
        
        // 重新緩存渲染器（確保獲取最新狀態）
        CacheRenderers();
        
        // 恢復碰撞
        var collider = GetComponent<Collider2D>();
        if (collider != null) 
        {
            collider.enabled = true;
            Debug.Log($"[{(IsServer ? "Server" : "Client")}] 啟用碰撞器");
        }
        
        // 恢復所有緩存的渲染器
        int enabledCount = 0;
        foreach (var renderer in cachedRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = true;
                enabledCount++;
            }
        }
        Debug.Log($"[{(IsServer ? "Server" : "Client")}] 啟用 {enabledCount} 個渲染器");
        
        // 恢復物理並重置速度
        if (rb != null)
        {
            rb.simulated = true;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            Debug.Log($"[{(IsServer ? "Server" : "Client")}] 恢復物理模擬");
        }
        
        // 延遲一幀再次確保所有渲染器都啟用（解決同步問題）
        StartCoroutine(EnsureRenderersEnabledCoroutine());
        
        Debug.Log($"[{(IsServer ? "Server" : "Client")}] 玩家 {OwnerClientId} 在位置 {respawnPosition} 重生完成");
    }
    
    /// <summary>
    /// 確保所有渲染器都正確啟用（延遲執行）
    /// </summary>
    private IEnumerator EnsureRenderersEnabledCoroutine()
    {
        // 等待一幀，確保所有網絡同步完成
        yield return null;
        
        // 再次獲取並啟用所有渲染器
        var allRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        int count = 0;
        foreach (var renderer in allRenderers)
        {
            if (renderer != null && !renderer.enabled)
            {
                renderer.enabled = true;
                count++;
            }
        }
        
        if (count > 0)
        {
            Debug.Log($"[{(IsServer ? "Server" : "Client")}] 延遲啟用了額外的 {count} 個渲染器");
        }
    }
    
    /// <summary>
    /// 獲取當前血量（只讀）
    /// </summary>
    public float GetCurrentHealth()
    {
        return currentHealth.Value;
    }
    
    /// <summary>
    /// 獲取最大血量
    /// </summary>
    public float GetMaxHealth()
    {
        return maxHealth;
    }
    
    /// <summary>
    /// 獲取血量百分比
    /// </summary>
    public float GetHealthPercentage()
    {
        return currentHealth.Value / maxHealth;
    }
    
    /// <summary>
    /// 檢查是否死亡
    /// </summary>
    public bool IsDead()
    {
        return isDead.Value;
    }
}