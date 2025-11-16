using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Mathematics;

public class NetworkPlayerController : NetworkBehaviour
{

    private PlayerInputAction pActions;
    [SerializeField]
    float moveSpeed;
    private Vector2 finalMove;
    void Start()
    {
        pActions = new PlayerInputAction();
        pActions.Enable();
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsOwner) return;

        var move = pActions.Player.Move.ReadValue<Vector2>();
        finalMove = new Vector3(move.x, 0,0) * Time.deltaTime * moveSpeed;
        Moveme(finalMove);
    }

    public void Moveme(Vector2 move)
    {
        transform.Translate(move, Space.World);
        if(move != Vector2.zero)
        {
            //transform.rotation = Quaternion.LookRotation(new Vector3(move.x, 0f, 0f));
        }
    }
}
