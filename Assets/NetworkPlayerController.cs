using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Mathematics;

public class NetworkPlayerController : NetworkBehaviour
{

    private PlayerInputAction pActions;
    [SerializeField] float moveSpeed=1f;
    [SerializeField] float jumpForce=5f;
    [SerializeField] Transform groundCheck;
    [SerializeField] float CheckRadius = 0.2f;
    [SerializeField] LayerMask groundLayer=1 << 6;

    private Vector2 finalMove;
    private Rigidbody2D rb;
    private bool isGrounded;
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
        

        if (isGrounded)
        {
            //Debug.Log("touching ground");
        }

    }

}
