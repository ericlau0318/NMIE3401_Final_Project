using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

/// <summary>
/// 重生點管理器
/// 將此腳本掛載到場景中的空物件上
/// </summary>
public class RespawnManager : NetworkBehaviour
{
    public static RespawnManager Instance { get; private set; }
    
    [SerializeField] private Transform[] respawnPoints; // 重生點列表
    [SerializeField] private bool randomRespawn = true; // 是否隨機選擇重生點
    
    private void Awake()
    {
        // 單例模式
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        // 如果沒有設置重生點，自動查找帶有 "Respawn" 標籤的物件
        if (respawnPoints == null || respawnPoints.Length == 0)
        {
            GameObject[] respawnObjects = GameObject.FindGameObjectsWithTag("Respawn");
            respawnPoints = new Transform[respawnObjects.Length];
            
            for (int i = 0; i < respawnObjects.Length; i++)
            {
                respawnPoints[i] = respawnObjects[i].transform;
            }
            
            Debug.Log($"自動找到 {respawnPoints.Length} 個重生點");
        }
    }
    
    /// <summary>
    /// 獲取重生位置
    /// </summary>
    public Vector3 GetRespawnPosition()
    {
        if (respawnPoints == null || respawnPoints.Length == 0)
        {
            Debug.LogWarning("沒有設置重生點，使用原點 (0,0,0)");
            return Vector3.zero;
        }
        
        if (randomRespawn)
        {
            // 隨機選擇一個重生點
            int randomIndex = Random.Range(0, respawnPoints.Length);
            return respawnPoints[randomIndex].position;
        }
        else
        {
            // 返回第一個重生點
            return respawnPoints[0].position;
        }
    }
    
    /// <summary>
    /// 獲取特定索引的重生點
    /// </summary>
    public Vector3 GetRespawnPosition(int index)
    {
        if (respawnPoints == null || respawnPoints.Length == 0)
        {
            Debug.LogWarning("沒有設置重生點");
            return Vector3.zero;
        }
        
        if (index < 0 || index >= respawnPoints.Length)
        {
            Debug.LogWarning($"重生點索引 {index} 超出範圍，使用第一個重生點");
            return respawnPoints[0].position;
        }
        
        return respawnPoints[index].position;
    }
    
    /// <summary>
    /// 獲取最近的重生點
    /// </summary>
    public Vector3 GetNearestRespawnPosition(Vector3 position)
    {
        if (respawnPoints == null || respawnPoints.Length == 0)
        {
            return Vector3.zero;
        }
        
        Transform nearest = respawnPoints[0];
        float minDistance = Vector3.Distance(position, nearest.position);
        
        for (int i = 1; i < respawnPoints.Length; i++)
        {
            float distance = Vector3.Distance(position, respawnPoints[i].position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = respawnPoints[i];
            }
        }
        
        return nearest.position;
    }
}

