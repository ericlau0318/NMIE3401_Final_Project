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
    [SerializeField] Transform gunTransform;
    [SerializeField] Transform firePoint;
    [SerializeField] GameObject bulletPrefab;
    [SerializeField] float fireRate = 0.2f;
    [SerializeField] Transform graphicsTransform;
    [SerializeField] private SpriteRenderer gunSpriteRenderer;
    
    // 角色圖像（Host和Client使用不同的Sprite）
    [SerializeField] private Sprite hostPlayerSprite;
    [SerializeField] private Sprite clientPlayerSprite;
    
    [SerializeField] private float maxHealth = 3f;
    [SerializeField] private float respawnDelay = 3f;
    
    [SerializeField] private GameObject youWinText;
    [SerializeField] private GameObject youLoseText;
    
    // 愛心圖片引用
    [SerializeField] private GameObject healthImage1; // Health
    [SerializeField] private GameObject healthImage2; // Health(1)
    [SerializeField] private GameObject healthImage3; // Health(2)
    
    private NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        3f,
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
    
    // 玩家類型：true表示Host，false表示Client
    private NetworkVariable<bool> isHostPlayer = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    
    private List<SpriteRenderer> cachedRenderers = new List<SpriteRenderer>();
    private SpriteRenderer graphicsSpriteRenderer; // Graphics子物件的SpriteRenderer
    
    private Vector2 finalMove;
    private Rigidbody2D rb;
    private bool isGrounded;
    private float nextFireTime;
    private Camera mainCam;
    private float lastUIUpdateTime = 0f;
    private const float UIUpdateInterval = 0.1f; // 每0.1秒檢查一次UI
    
    // 無敵時間相關
    private NetworkVariable<float> invincibilityEndTime = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private const float invincibilityDuration = 0.4f; // 無敵時間0.4秒
    private Coroutine invincibilityFlashCoroutine; // 無敵閃爍協程
    
    void Awake()
    {
        isGunFlipped.OnValueChanged += OnGunFlipChanged;
        isDead.OnValueChanged += OnDeathStatusChanged;
        currentHealth.OnValueChanged += OnHealthChanged;
        isHostPlayer.OnValueChanged += OnPlayerTypeChanged;
        
        // 查找Graphics子物件的SpriteRenderer
        if (graphicsTransform != null)
        {
            graphicsSpriteRenderer = graphicsTransform.GetComponent<SpriteRenderer>();
            if (graphicsSpriteRenderer == null)
            {
                // 如果Graphics本身沒有SpriteRenderer，嘗試查找子物件
                graphicsSpriteRenderer = graphicsTransform.GetComponentInChildren<SpriteRenderer>();
            }
        }
    }

    private void OnGunFlipChanged(bool previous, bool current)
    {
        if (gunSpriteRenderer != null)
            gunSpriteRenderer.flipY = current;
    }
    
    private void OnDeathStatusChanged(bool previous, bool current)
    {
        // 當任何玩家的死亡狀態改變時，更新所有本地玩家的UI
        // 需要在所有客戶端上執行，所以不檢查 IsOwner
        UpdateAllPlayersWinLoseUI();
    }
    
    private void OnHealthChanged(float previous, float current)
    {
        // 當生命值改變時，更新愛心顯示（所有客戶端都需要更新）
        UpdateHealthUI();
    }
    
    private void OnPlayerTypeChanged(bool previous, bool current)
    {
        // 當玩家類型改變時，更新角色圖像（所有客戶端都需要更新）
        SetPlayerSprite();
    }
    
    private void UpdateAllPlayersWinLoseUI()
    {
        // 找到所有玩家
        NetworkPlayerController[] allPlayers = FindObjectsOfType<NetworkPlayerController>();
        
        // 找到本地玩家和對方玩家
        NetworkPlayerController localPlayer = null;
        NetworkPlayerController otherPlayer = null;
        
        foreach (var player in allPlayers)
        {
            if (player == null) continue;
            
            if (player.IsOwner)
            {
                localPlayer = player;
            }
            else
            {
                otherPlayer = player;
            }
        }
        
        // 只更新本地玩家擁有的物件的UI
        if (localPlayer != null)
        {
            // 如果本地玩家死亡，顯示 "You Lose"
            if (localPlayer.isDead.Value)
            {
                if (localPlayer.youLoseText != null)
                {
                    localPlayer.youLoseText.SetActive(true);
                }
                if (localPlayer.youWinText != null)
                {
                    localPlayer.youWinText.SetActive(false);
                }
            }
            // 如果對方玩家死亡，顯示 "You Win"
            else if (otherPlayer != null && otherPlayer.isDead.Value)
            {
                if (localPlayer.youWinText != null)
                {
                    localPlayer.youWinText.SetActive(true);
                }
                if (localPlayer.youLoseText != null)
                {
                    localPlayer.youLoseText.SetActive(false);
                }
            }
            // 如果沒有人死亡，隱藏所有文字
            else
            {
                if (localPlayer.youWinText != null)
                {
                    localPlayer.youWinText.SetActive(false);
                }
                if (localPlayer.youLoseText != null)
                {
                    localPlayer.youLoseText.SetActive(false);
                }
            }
        }
    }
    
    private void HideWinLoseUI()
    {
        if (youWinText != null)
        {
            youWinText.SetActive(false);
        }
        if (youLoseText != null)
        {
            youLoseText.SetActive(false);
        }
    }

    void Start()
    {
        pActions = new PlayerInputAction();
        pActions.Enable();
        rb = GetComponent<Rigidbody2D>();

        
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            isDead.Value = false;
            invincibilityEndTime.Value = 0f;
            
            // 設置玩家類型：OwnerClientId == 0 表示Host
            isHostPlayer.Value = (OwnerClientId == 0);
        }
        
        // 初始化愛心UI（所有客戶端都需要）
        InitializeHealthUI();
    }
    


    // Update is called once per frame
    void Update()
    {
        // 定期更新UI（確保所有客戶端都能更新）
        if (IsOwner && Time.time - lastUIUpdateTime > UIUpdateInterval)
        {
            UpdateAllPlayersWinLoseUI();
            lastUIUpdateTime = Time.time;
        }
        
        if (!IsOwner) return;
        
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

         gunTransform.rotation = Quaternion.Euler(0f, 0f, angle);

        graphicsTransform.localScale = new Vector3(facingLeft ? -1f : 1f, 1f, 1f);

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
        GameObject bulletGO = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        bulletGO.GetComponent<NetworkObject>().Spawn(true);
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // 初始化Graphics的SpriteRenderer（所有客戶端都需要）
        if (graphicsSpriteRenderer == null && graphicsTransform != null)
        {
            graphicsSpriteRenderer = graphicsTransform.GetComponent<SpriteRenderer>();
            if (graphicsSpriteRenderer == null)
            {
                graphicsSpriteRenderer = graphicsTransform.GetComponentInChildren<SpriteRenderer>();
            }
        }
        
        // 根據玩家身份設置不同的角色圖像
        // 使用協程延遲一下，確保NetworkVariable已經初始化
        StartCoroutine(SetPlayerSpriteDelayed());
        
        // 在服務器端，將玩家傳送到對應的SpawnPoint
        // 使用協程延遲，確保isHostPlayer.Value已經設置
        if (IsServer)
        {
            StartCoroutine(TeleportToSpawnPointDelayed());
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
            
            // 初始化UI文字物件（如果沒有在Inspector中設置，則自動查找）
            if (youWinText == null || youLoseText == null)
            {
                Transform canvas = transform.Find("Canvas");
                if (canvas != null)
                {
                    if (youWinText == null)
                    {
                        Transform winTxt = canvas.Find("YouWinTxt");
                        if (winTxt != null)
                        {
                            youWinText = winTxt.gameObject;
                        }
                    }
                    if (youLoseText == null)
                    {
                        Transform loseTxt = canvas.Find("YouLoseTxt");
                        if (loseTxt != null)
                        {
                            youLoseText = loseTxt.gameObject;
                        }
                    }
                }
            }
            
            // 確保初始狀態是隱藏的
            HideWinLoseUI();
        }
        else
        {
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
        
        // 初始化愛心UI（所有客戶端都需要）
        InitializeHealthUI();
        UpdateHealthUI();
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(float damage, ServerRpcParams rpcParams = default)
    {
        if (isDead.Value) return;
        
        // 檢查是否在無敵時間內
        if (Time.time < invincibilityEndTime.Value)
        {
            return; // 無敵時間內，不受到傷害
        }
        
        float previousHealth = currentHealth.Value;
        // 每次傷害減少1點生命值（一個愛心）
        currentHealth.Value = Mathf.Max(0, currentHealth.Value - 1f);
        
        // 如果受到傷害，啟動無敵時間並通知所有客戶端顯示閃爍效果
        if (currentHealth.Value < previousHealth)
        {
            // 設置無敵時間結束時間
            invincibilityEndTime.Value = Time.time + invincibilityDuration;
            
            // 通知所有客戶端開始無敵閃爍效果
            StartInvincibilityFlashClientRpc();
        }
        
        if (currentHealth.Value <= 0)
        {
            isDead.Value = true;
            // 停止無敵閃爍（如果還在運行）
            StopInvincibilityFlashClientRpc();

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            
            // 通知GameManager更新分數並切換場景
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerDeath(OwnerClientId);
            }
            
            // 不再重生，改為切換場景（由GameManager處理）
        }
    }
    
    [ClientRpc]
    private void StartInvincibilityFlashClientRpc()
    {
        // 在所有客戶端執行無敵閃爍效果
        // 如果已經有閃爍協程在運行，先停止它
        if (invincibilityFlashCoroutine != null)
        {
            StopCoroutine(invincibilityFlashCoroutine);
        }
        invincibilityFlashCoroutine = StartCoroutine(InvincibilityFlashCoroutine());
    }
    
    [ClientRpc]
    private void StopInvincibilityFlashClientRpc()
    {
        // 停止無敵閃爍效果並恢復原始顏色
        if (invincibilityFlashCoroutine != null)
        {
            StopCoroutine(invincibilityFlashCoroutine);
            invincibilityFlashCoroutine = null;
        }
        
        // 恢復原始顏色
        // 如果還沒有找到Graphics的SpriteRenderer，嘗試查找
        if (graphicsSpriteRenderer == null)
        {
            if (graphicsTransform != null)
            {
                graphicsSpriteRenderer = graphicsTransform.GetComponent<SpriteRenderer>();
                if (graphicsSpriteRenderer == null)
                {
                    graphicsSpriteRenderer = graphicsTransform.GetComponentInChildren<SpriteRenderer>();
                }
            }
        }
        
        if (graphicsSpriteRenderer != null)
        {
            Color currentColor = graphicsSpriteRenderer.color;
            graphicsSpriteRenderer.color = new Color(currentColor.r, currentColor.g, currentColor.b, 1f);
        }
    }
    
    private IEnumerator InvincibilityFlashCoroutine()
    {
        // 如果還沒有找到Graphics的SpriteRenderer，嘗試查找
        if (graphicsSpriteRenderer == null)
        {
            if (graphicsTransform != null)
            {
                graphicsSpriteRenderer = graphicsTransform.GetComponent<SpriteRenderer>();
                if (graphicsSpriteRenderer == null)
                {
                    graphicsSpriteRenderer = graphicsTransform.GetComponentInChildren<SpriteRenderer>();
                }
            }
        }
        
        if (graphicsSpriteRenderer == null)
        {
            Debug.LogWarning("無法找到Graphics子物件的SpriteRenderer");
            yield break;
        }
        
        // 保存原始顏色
        Color originalColor = graphicsSpriteRenderer.color;
        float startTime = Time.time;
        float endTime = startTime + invincibilityDuration;
        
        // 在無敵時間內循環閃爍
        while (Time.time < endTime)
        {
            float remainingTime = endTime - Time.time;
            float cycleTime = Time.time - startTime;
            
            // 每個循環是0.2秒（0.1秒從255到0，0.1秒從0到255）
            float cycleProgress = (cycleTime % 0.2f) / 0.2f;
            
            float alpha;
            if (cycleProgress < 0.5f)
            {
                // 前0.1秒：從255（1.0）減到0
                alpha = Mathf.Lerp(1f, 0f, cycleProgress * 2f);
            }
            else
            {
                // 後0.1秒：從0加到255（1.0）
                alpha = Mathf.Lerp(0f, 1f, (cycleProgress - 0.5f) * 2f);
            }
            
            // 更新Alpha值，保持RGB不變
            graphicsSpriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            
            yield return null; // 每幀更新
        }
        
        // 恢復原始顏色和Alpha
        graphicsSpriteRenderer.color = originalColor;
        invincibilityFlashCoroutine = null;
    }
    
    // 延遲設置角色圖像，確保NetworkVariable已經初始化
    private IEnumerator SetPlayerSpriteDelayed()
    {
        // 等待一幀，確保NetworkVariable已經同步
        yield return null;
        SetPlayerSprite();
    }
    
    // 延遲傳送到SpawnPoint，確保isHostPlayer.Value已經設置
    private IEnumerator TeleportToSpawnPointDelayed()
    {
        // 等待幾幀，確保isHostPlayer.Value已經在Start()中設置
        yield return new WaitForSeconds(0.1f);
        
        // 獲取spawn位置並傳送
        Vector3 spawnPosition = GetSpawnPosition();
        transform.position = spawnPosition;
        
        // 通知客戶端同步位置
        TeleportToSpawnPointClientRpc(spawnPosition);
    }
    
    [ClientRpc]
    private void TeleportToSpawnPointClientRpc(Vector3 spawnPosition)
    {
        // 在客戶端同步位置（如果不是服務器端）
        if (!IsServer)
        {
            transform.position = spawnPosition;
        }
    }
    
    // 根據玩家身份設置不同的角色圖像
    private void SetPlayerSprite()
    {
        if (graphicsSpriteRenderer == null) return;
        
        // 使用NetworkVariable來判斷是否是Host（所有客戶端都能看到正確的值）
        bool isHost = isHostPlayer.Value;
        
        Sprite spriteToUse = isHost ? hostPlayerSprite : clientPlayerSprite;
        
        if (spriteToUse != null)
        {
            graphicsSpriteRenderer.sprite = spriteToUse;
        }
        else
        {
            Debug.LogWarning($"未設置{(isHost ? "Host" : "Client")}玩家的Sprite圖像");
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void HealServerRpc(float healAmount, ServerRpcParams rpcParams = default)
    {
        if (isDead.Value) return;
        
        currentHealth.Value = Mathf.Min(maxHealth, currentHealth.Value + healAmount);
    }
    

    private void HandleDeath()
    {        
        var collider = GetComponent<Collider2D>();
        if (collider != null) 
        {
            collider.enabled = false;
        }
        
        int disabledCount = 0;
        foreach (var renderer in cachedRenderers)
        {
            if (renderer != null && renderer.enabled)
            {
                renderer.enabled = false;
                disabledCount++;
            }
        }
        
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }
    }
    
    private IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(respawnDelay);
        RespawnPlayer();
    }
    
    private void RespawnPlayer()
    {
        if (!IsServer) return;
        
        isDead.Value = false;
        currentHealth.Value = maxHealth;
        invincibilityEndTime.Value = 0f;
        
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        
        // 復活時也會觸發 OnDeathStatusChanged，所以UI會自動更新
    }
    

    [ClientRpc]
    private void RespawnClientRpc(Vector3 respawnPosition)
    {        
        transform.position = respawnPosition;
        
        var collider = GetComponent<Collider2D>();
        if (collider != null) 
        {
            collider.enabled = true;
        }
        int enabledCount = 0;
        foreach (var renderer in cachedRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = true;
                enabledCount++;
            }
        }
        
        if (rb != null)
        {
            rb.simulated = true;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }
    
    // 公共方法：重置玩家狀態（用於場景切換後）
    public void ResetPlayerState()
    {
        if (IsServer)
        {
            // 服務器端重置狀態
            ResetPlayerStateServerRpc();
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void ResetPlayerStateServerRpc()
    {
        // 重置生命值和死亡狀態
        currentHealth.Value = maxHealth;
        isDead.Value = false;
        invincibilityEndTime.Value = 0f;
        
        // 重置物理狀態
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = true;
        }
        
        // 啟用碰撞器
        var collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = true;
        }
        
        // 獲取spawn位置（根據玩家ClientId設置不同的spawn點）
        Vector3 spawnPosition = GetSpawnPosition();
        
        // 重置位置和旋轉
        transform.position = spawnPosition;
        transform.rotation = Quaternion.identity;
        
        // 重置槍支和圖形的旋轉
        if (gunTransform != null)
        {
            gunTransform.localRotation = Quaternion.identity;
        }
        if (graphicsTransform != null)
        {
            graphicsTransform.localScale = new Vector3(1f, 1f, 1f);
        }
        // 注意：isGunFlipped 的寫權限是 Owner，不能在服務器端直接設置
        // 會在 ClientRpc 中讓 Owner 自己設置
        
        // 通知客戶端重置視覺狀態
        ResetPlayerStateClientRpc(spawnPosition);
    }
    
    // 獲取spawn位置（根據玩家類型設置不同位置：Host傳送到SpawnPoint1，Client傳送到SpawnPoint2）
    private Vector3 GetSpawnPosition()
    {
        // 根據isHostPlayer來決定spawn位置
        bool isHost = isHostPlayer.Value;
        
        // 查找場景中的SpawnPoint物件
        GameObject spawnPoint1 = GameObject.Find("SpawnPoint1");
        GameObject spawnPoint2 = GameObject.Find("SpawnPoint2");
        
        // Host Player傳送到SpawnPoint1
        if (isHost && spawnPoint1 != null)
        {
            return spawnPoint1.transform.position;
        }
        // Client Player傳送到SpawnPoint2
        else if (!isHost && spawnPoint2 != null)
        {
            return spawnPoint2.transform.position;
        }
        
        // 如果沒有找到對應的spawn點，使用默認位置
        // Host在左邊(-5, 0)，Client在右邊(5, 0)
        float spawnX = isHost ? -5f : 5f;
        return new Vector3(spawnX, 0f, transform.position.z);
    }
    
    [ClientRpc]
    private void ResetPlayerStateClientRpc(Vector3 spawnPosition)
    {
        // 重置位置（只在客戶端同步位置）
        if (!IsServer)
        {
            transform.position = spawnPosition;
            transform.rotation = Quaternion.identity;
        }
        
        // 重置視覺狀態
        var collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = true;
        }
        
        // 啟用所有渲染器
        foreach (var renderer in cachedRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }
        
        // 重置物理狀態
        if (rb != null)
        {
            rb.simulated = true;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        
        // 重置槍支和圖形的旋轉
        if (gunTransform != null)
        {
            gunTransform.localRotation = Quaternion.identity;
        }
        if (graphicsTransform != null)
        {
            graphicsTransform.localScale = new Vector3(1f, 1f, 1f);
        }
        
        // 重置 isGunFlipped（只有 Owner 才能設置）
        if (IsOwner)
        {
            isGunFlipped.Value = false;
        }
        
        // 隱藏Win/Lose UI
        HideWinLoseUI();
        
        // 停止無敵閃爍（如果還在運行）
        if (invincibilityFlashCoroutine != null)
        {
            StopCoroutine(invincibilityFlashCoroutine);
            invincibilityFlashCoroutine = null;
        }
        
        // 恢復原始顏色
        if (graphicsSpriteRenderer != null)
        {
            Color currentColor = graphicsSpriteRenderer.color;
            graphicsSpriteRenderer.color = new Color(currentColor.r, currentColor.g, currentColor.b, 1f);
        }
    }
    
    // 初始化愛心UI引用
    private void InitializeHealthUI()
    {
        // 如果沒有在Inspector中設置，則自動查找
        if (healthImage1 == null || healthImage2 == null || healthImage3 == null)
        {
            Transform canvas = transform.Find("Canvas");
            if (canvas != null)
            {
                if (healthImage1 == null)
                {
                    Transform health1 = canvas.Find("Health");
                    if (health1 != null)
                    {
                        healthImage1 = health1.gameObject;
                    }
                }
                if (healthImage2 == null)
                {
                    Transform health2 = canvas.Find("Health(1)");
                    if (health2 != null)
                    {
                        healthImage2 = health2.gameObject;
                    }
                }
                if (healthImage3 == null)
                {
                    Transform health3 = canvas.Find("Health(2)");
                    if (health3 != null)
                    {
                        healthImage3 = health3.gameObject;
                    }
                }
            }
        }
    }
    
    // 根據當前生命值更新愛心顯示
    private void UpdateHealthUI()
    {
        int health = Mathf.RoundToInt(currentHealth.Value);
        
        // 生命值3時，顯示3張愛心
        if (healthImage1 != null) healthImage1.SetActive(health >= 1);
        if (healthImage2 != null) healthImage2.SetActive(health >= 2);
        if (healthImage3 != null) healthImage3.SetActive(health >= 3);
    }
   
}