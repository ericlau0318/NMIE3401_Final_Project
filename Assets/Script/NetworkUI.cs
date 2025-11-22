using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class NetworkUI : MonoBehaviour
{
    public GameObject MainMeun;
    public GameObject Maincamera;
    public GameObject HowToPlayPanel;
    public void StartServerOnClick()
    {
        NetworkManager.Singleton.StartServer();
        Debug.Log("1");
    }

    public void StartHostOnClick()
    {
        Maincamera.SetActive(false);
        NetworkManager.Singleton.StartHost();
        Debug.Log("2");
        MainMeun.SetActive(false);
        
    }

    public void StartClientOnClick()
    {
        Maincamera.SetActive(false);
        NetworkManager.Singleton.StartClient();
        MainMeun.SetActive(false);
        Debug.Log("3");
    }

    public void OpenHTP()
    {
        if (HowToPlayPanel.activeSelf==false)
        {
            HowToPlayPanel.SetActive(true);
        } 
        else
        {
            HowToPlayPanel.SetActive(false);

        }
    }

    public void CloseHTP()
    {
        HowToPlayPanel.SetActive(false);
    }
}
