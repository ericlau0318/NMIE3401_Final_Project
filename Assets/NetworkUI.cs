using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class NetworkUI : MonoBehaviour
{
    public GameObject MainMeun;
    
    public void StartServerOnClick()
    {
        NetworkManager.Singleton.StartServer();
        Debug.Log("1");
    }

    public void StartHostOnClick()
    {
        NetworkManager.Singleton.StartHost();
        Debug.Log("2");
        MainMeun.SetActive(false);
    }

    public void StartClientOnClick()
    {
        NetworkManager.Singleton.StartClient();
        MainMeun.SetActive(false);
        Debug.Log("3");
    }
}
