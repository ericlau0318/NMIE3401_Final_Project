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

    private NetworkVariable<bool> isGunFlipped = new NetworkVariable<bool>(false);
    private Vector2 finalMove;
    private Rigidbody2D rb;
    private bool isGrounded;
    private float nextFireTime;
    private Camera mainCam;
    void Start()
    {
        pActions = new PlayerInputAction();
        pActions.Enable();
        rb = GetComponent<Rigidbody2D>();
    }
    // Update is called once per frame
    void Update()
    {
        if (!IsOwner) return;
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
        // 槍永遠正確轉向（不加180！因為我們不用父物件翻轉了）
        gunTransform.rotation = Quaternion.Euler(0f, 0f, angle);

        // 判斷面向


        // 角色視覺翻轉（Graphics 子物件）
        graphicsTransform.localScale = new Vector3(facingLeft ? -1f : 1f, 1f, 1f);

        if (IsOwner)
        {
            isGunFlipped.Value = facingLeft;  // Owner 決定翻轉
        }

        gunSpriteRenderer.flipY = isGunFlipped.Value;

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
    }
}