using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using TMPro;

public class NetworkplayerIdentifier : NetworkBehaviour
{
    [SerializeField] TextMeshProUGUI playerID;

    NetworkVariable<ulong> playerIdNetworkVal = new NetworkVariable<ulong>();

    bool isplayerIdSet = false;
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
