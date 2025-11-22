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
    
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float respawnDelay = 3f;
    
    [SerializeField] private GameObject youWinText;
    [SerializeField] private GameObject youLoseText;
    
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
    
    private List<SpriteRenderer> cachedRenderers = new List<SpriteRenderer>();
    
    private Vector2 finalMove;
    private Rigidbody2D rb;
    private bool isGrounded;
    private float nextFireTime;
    private Camera mainCam;
    private float lastUIUpdateTime = 0f;
    private const float UIUpdateInterval = 0.1f; // 每0.1秒檢查一次UI
    void Awake()
    {
        isGunFlipped.OnValueChanged += OnGunFlipChanged;
        isDead.OnValueChanged += OnDeathStatusChanged;
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
        }
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
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(float damage, ServerRpcParams rpcParams = default)
    {
        if (isDead.Value) return;
        
        currentHealth.Value = Mathf.Max(0, currentHealth.Value - damage);
        
        if (currentHealth.Value <= 0)
        {
            isDead.Value = true;
            

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
    
    // 獲取spawn位置（根據玩家索引設置不同位置）
    private Vector3 GetSpawnPosition()
    {
        // 找到所有玩家並排序以確定spawn位置
        NetworkPlayerController[] allPlayers = FindObjectsOfType<NetworkPlayerController>();
        List<ulong> clientIds = new List<ulong>();
        
        foreach (var player in allPlayers)
        {
            if (player != null)
            {
                clientIds.Add(player.OwnerClientId);
            }
        }
        clientIds.Sort();
        
        // 確定這個玩家是玩家1還是玩家2
        int playerIndex = clientIds.IndexOf(OwnerClientId);
        if (playerIndex < 0) playerIndex = 0; // 如果找不到，默認為玩家1
        
        // 默認spawn位置：玩家1在左邊(-5, 0)，玩家2在右邊(5, 0)
        // 如果場景中有SpawnPoint物件，使用它們的位置
        GameObject spawnPoint1 = GameObject.Find("SpawnPoint1");
        GameObject spawnPoint2 = GameObject.Find("SpawnPoint2");
        
        if (playerIndex == 0 && spawnPoint1 != null)
        {
            return spawnPoint1.transform.position;
        }
        else if (playerIndex == 1 && spawnPoint2 != null)
        {
            return spawnPoint2.transform.position;
        }
        
        // 如果沒有找到spawn點，使用默認位置
        float spawnX = playerIndex == 0 ? -5f : 5f;
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
    }
   
}