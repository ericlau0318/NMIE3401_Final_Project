using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float jumpForce = 5f;
    public bool flipOnMouseLeft = true;
    public int maxHealth = 3;
    public int currentHealth;
    public GameObject bullet;
    public Transform ShootPoint;
    public float CD = 0.5f;
    public Transform gunTransform;

    private Vector3 mouseScreenPos;
    private Vector3 mouseWorldPos;
    private SpriteRenderer spriteRenderer;
    private Camera playerCamera;
    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isGrounded;
    private float nextFireTime = 0f;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        playerCamera = GetComponentInChildren<Camera>();
        rb = GetComponent<Rigidbody2D>();
        currentHealth = maxHealth;
    }

    void Update()
    {
        Vector2 velocity = rb.velocity;
        velocity.x = moveInput.x * moveSpeed;
        rb.velocity = velocity;

        mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = Mathf.Abs(playerCamera.transform.position.z);
        mouseWorldPos = playerCamera.ScreenToWorldPoint(mouseScreenPos);

        bool shouldFlip = mouseWorldPos.x < transform.position.x;
        spriteRenderer.flipX = flipOnMouseLeft ? shouldFlip : !shouldFlip;

        RotateGunToMouse(mouseWorldPos);
    }
    
    void RotateGunToMouse(Vector3 mouseWorldPos)
    {
        if (gunTransform == null) return;

        // 計算槍口到鼠標的方向
        Vector2 directionFromGun = (mouseWorldPos - gunTransform.position);

        // 轉換成角度（弧度 → 度數）
        float angle = Mathf.Atan2(directionFromGun.y, directionFromGun.x) * Mathf.Rad2Deg;

        // 旋轉槍（Z軸旋轉，2D）
        gunTransform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
    void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    void OnJump()
    {
        if (isGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            isGrounded = false;
        }
    }

    void OnShoot()
    {
        if (Time.time < nextFireTime) return;

        Vector3 mouseScreenPos = Mouse.current.position.ReadValue();
        mouseScreenPos.z = Mathf.Abs(playerCamera.transform.position.z);
        Vector3 mouseWorldPos = playerCamera.ScreenToWorldPoint(mouseScreenPos);

        Vector2 direction = ((Vector2)mouseWorldPos - (Vector2)ShootPoint.position).normalized;

        GameObject bulletObj = Instantiate(bullet, ShootPoint.position, Quaternion.identity);
        Bullet bulletScript = bulletObj.GetComponent<Bullet>();
        if (bulletScript != null)
            bulletScript.SetVelocity(direction);

        nextFireTime = Time.time + CD;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            // 死亡邏輯
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }
}