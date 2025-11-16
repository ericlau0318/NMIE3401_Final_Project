using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class NetworkUI : MonoBehaviour
{
    public void StartServerOnClick()
    {
        NetworkManager.Singleton.StartServer();
    }

    public void StartHostOnClick()
    {
        NetworkManager.Singleton.StartHost();
    }

    public void StartClientOnClick()
    {
        NetworkManager.Singleton.StartClient();
    }
}
