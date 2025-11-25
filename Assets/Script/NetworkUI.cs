using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class NetworkUI : MonoBehaviour
{
    public GameObject MainMeun;
    public GameObject Maincamera;
    public GameObject HowToPlayPanel;
    [SerializeField] private AudioSource shootSound; 
    
    // Start as server only (for dedicated servers)
    public void StartServerOnClick()
    {
        NetworkManager.Singleton.StartServer();
        Debug.Log("Server is starting");
    }

    // Start as host (server + client combined - player 1)
    public void StartHostOnClick()
    {
        shootSound.Play();
        Maincamera.SetActive(false);
        NetworkManager.Singleton.StartHost();
        Debug.Log("Host is starting, ready for player 2 to join");
        MainMeun.SetActive(false);
        
    }

    // Start as client (player 2 - connects to the host)
    public void StartClientOnClick()
    {
        shootSound.Play();
        Maincamera.SetActive(false);
        NetworkManager.Singleton.StartClient();
        MainMeun.SetActive(false);
        Debug.Log("Client is starting and trying to connect");
    }

    // Toggle the "How to Play" panel on/off
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

    // Close the "How to Play" panel
    public void CloseHTP()
    {
        HowToPlayPanel.SetActive(false);
    }
}
