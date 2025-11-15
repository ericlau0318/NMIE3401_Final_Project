using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class NetworkUI : MonoBehaviour
{
    public void StartServer()
    {
        NetworkManager.Singleton.StartServer();
        Debug.Log("1");
    }

    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        Debug.Log("2");
    }

    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();
        Debug.Log("3");

    }
}
