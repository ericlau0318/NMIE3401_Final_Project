using UnityEngine;
using Unity.Netcode;

/// <summary>
/// 網絡調試輔助工具
/// 將此腳本掛載到任何物件上，按快捷鍵查看調試信息
/// </summary>
public class NetworkDebugHelper : MonoBehaviour
{
    [SerializeField] private bool enableDebugKeys = true;
    [SerializeField] private KeyCode checkRenderersKey = KeyCode.F1;
    [SerializeField] private KeyCode forceEnableRenderersKey = KeyCode.F2;

    private void Update()
    {
        if (!enableDebugKeys) return;

        // F1: 檢查所有玩家的渲染器狀態
        if (Input.GetKeyDown(checkRenderersKey))
        {
            CheckAllPlayerRenderers();
        }

        // F2: 強制啟用所有玩家的渲染器
        if (Input.GetKeyDown(forceEnableRenderersKey))
        {
            ForceEnableAllRenderers();
        }
    }

    private void CheckAllPlayerRenderers()
    {
        Debug.Log("========== 檢查所有玩家渲染器狀態 ==========");
        
        var players = FindObjectsOfType<NetworkPlayerController>();
        foreach (var player in players)
        {
            if (player == null) continue;

            var renderers = player.GetComponentsInChildren<SpriteRenderer>(true);
            int enabledCount = 0;
            int disabledCount = 0;

            foreach (var renderer in renderers)
            {
                if (renderer.enabled) enabledCount++;
                else disabledCount++;
            }

            string ownerStatus = player.IsOwner ? "本地玩家" : "遠端玩家";
            string deadStatus = player.IsDead() ? "死亡" : "存活";
            
            Debug.Log($"玩家 {player.OwnerClientId} ({ownerStatus}, {deadStatus}): " +
                     $"啟用渲染器 {enabledCount}, 禁用渲染器 {disabledCount}");
            
            // 列出所有禁用的渲染器
            if (disabledCount > 0)
            {
                foreach (var renderer in renderers)
                {
                    if (!renderer.enabled)
                    {
                        Debug.LogWarning($"  - 禁用的渲染器: {GetGameObjectPath(renderer.gameObject)}");
                    }
                }
            }
        }
        
        Debug.Log("===============================================");
    }

    private void ForceEnableAllRenderers()
    {
        Debug.Log("========== 強制啟用所有玩家渲染器 ==========");
        
        var players = FindObjectsOfType<NetworkPlayerController>();
        foreach (var player in players)
        {
            if (player == null || player.IsDead()) continue;

            var renderers = player.GetComponentsInChildren<SpriteRenderer>(true);
            int enabledCount = 0;

            foreach (var renderer in renderers)
            {
                if (!renderer.enabled)
                {
                    renderer.enabled = true;
                    enabledCount++;
                    Debug.Log($"啟用渲染器: {GetGameObjectPath(renderer.gameObject)}");
                }
            }

            if (enabledCount > 0)
            {
                Debug.Log($"玩家 {player.OwnerClientId}: 啟用了 {enabledCount} 個渲染器");
            }
        }
        
        Debug.Log("===============================================");
    }

    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        
        return path;
    }

    private void OnGUI()
    {
        if (!enableDebugKeys) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 100));
        GUILayout.Label("=== 網絡調試工具 ===");
        GUILayout.Label($"F1: 檢查渲染器狀態");
        GUILayout.Label($"F2: 強制啟用所有渲染器");
        GUILayout.EndArea();
    }
}

