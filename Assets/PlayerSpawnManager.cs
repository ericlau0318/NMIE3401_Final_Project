using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerSpawnManager : MonoBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private Transform spawnPointLeft;
    [SerializeField] private Transform spawnPointRight;

    [Header("Player Prefab")]
    [SerializeField] private GameObject playerPrefab;  // 確保拖入

    private List<Transform> availableSpawns = new List<Transform>();

    void OnEnable()  // 用OnEnable確保每次啟用都註冊
    {
        if (NetworkManager.Singleton == null) return;

        // 註冊Server啟動事件（修正名稱）
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        // 註冊Client連接事件
        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
    }

    void OnDisable()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
    }

    private void OnServerStarted()
    {
        // Server啟動後初始化生成點
        availableSpawns.Clear();
        if (spawnPointLeft != null) availableSpawns.Add(spawnPointLeft);
        if (spawnPointRight != null) availableSpawns.Add(spawnPointRight);

        Debug.Log("PlayerSpawnManager Server啟動，初始化完成！");
    }

    private void HandleClientConnected(ulong clientId)
    {
        Debug.Log("HandleClientConnected 觸發 for Client: " + clientId);  // 加這行debug確認觸發

        if (availableSpawns.Count == 0)
        {
            Debug.LogWarning("生成點已滿！踢出Client: " + clientId);
            NetworkManager.Singleton.DisconnectClient(clientId);
            return;
        }

        Transform spawnPoint = availableSpawns[0];
        availableSpawns.RemoveAt(0);

        if (playerPrefab == null)
        {
            Debug.LogError("缺少Player Prefab！");
            return;
        }

        GameObject playerInstance = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        playerInstance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);

        Debug.Log("玩家生成在 " + spawnPoint.name + " (位置: " + spawnPoint.position + ") for Client: " + clientId);
    }
}