using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using TMPro;

// Shows the player ID (P1 or P2) above each player
public class NetworkplayerIdentifier : NetworkBehaviour
{
    [SerializeField] TextMeshProUGUI playerID; // Text that shows "P1" or "P2"

    NetworkVariable<ulong> playerIdNetworkVal = new NetworkVariable<ulong>(); // Synced player ID across network

    bool isplayerIdSet = false; // Have we set the text yet?
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            playerIdNetworkVal.Value = OwnerClientId + 1;
        }
        base.OnNetworkSpawn();
    }

    // Update is called once per frame
    void Update()
    {
        if (!isplayerIdSet)
        {
            SetPlayerIdText();
            isplayerIdSet = true;
        }
    }

    public void SetPlayerIdText()
    {
        playerID.text = "P" + playerIdNetworkVal.Value;
    }
}
