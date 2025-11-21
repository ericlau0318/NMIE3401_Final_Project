using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 玩家血量UI顯示
/// 將此腳本掛載到Canvas下的UI物件上
/// </summary>
public class PlayerHealthUI : MonoBehaviour
{
    [SerializeField] private NetworkPlayerController playerController;
    [SerializeField] private Image healthBarFill; // 血條填充圖片
    [SerializeField] private TextMeshProUGUI healthText; // 血量文字（可選）
    [SerializeField] private bool autoFindPlayer = true; // 自動尋找本地玩家

    private void Start()
    {
        if (autoFindPlayer && playerController == null)
        {
            // 等待玩家生成
            Invoke(nameof(FindLocalPlayer), 0.5f);
        }
    }

    private void FindLocalPlayer()
    {
        // 尋找本地擁有的玩家
        var players = FindObjectsOfType<NetworkPlayerController>();
        foreach (var player in players)
        {
            if (player.IsOwner)
            {
                playerController = player;
                Debug.Log("找到本地玩家");
                break;
            }
        }

        // 如果沒找到，繼續嘗試
        if (playerController == null)
        {
            Invoke(nameof(FindLocalPlayer), 0.5f);
        }
    }

    private void Update()
    {
        if (playerController == null) return;

        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        float healthPercentage = playerController.GetHealthPercentage();
        float currentHealth = playerController.GetCurrentHealth();
        float maxHealth = playerController.GetMaxHealth();

        // 更新血條填充
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = healthPercentage;
            
            // 根據血量改變顏色
            if (healthPercentage > 0.5f)
                healthBarFill.color = Color.green;
            else if (healthPercentage > 0.25f)
                healthBarFill.color = Color.yellow;
            else
                healthBarFill.color = Color.red;
        }

        // 更新血量文字
        if (healthText != null)
        {
            healthText.text = $"{currentHealth:F0} / {maxHealth:F0}";
        }
    }
}

