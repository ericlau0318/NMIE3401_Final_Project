using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [SerializeField] private Text scoreText; // 場景中的Score文字物件
    [SerializeField] private string nextSceneName = "Level2"; // 下一個場景名稱
    [SerializeField] private float sceneTransitionDelay = 2f; // 場景切換延遲時間（秒）
    
    private bool isTransitioning = false; // 是否正在切換場景
    private bool rewardWinTextShown = false; // 是否已經顯示過reward場景的勝利文字
    
    // 玩家1和玩家2的分數（使用ClientId來區分）
    private NetworkVariable<int> player1Score = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    
    private NetworkVariable<int> player2Score = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // 確保GameManager在場景切換時不被銷毀，以保持分數
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // 如果Instance已經存在（從上一個場景保持的），更新它的nextSceneName
            if (Instance != this && !string.IsNullOrEmpty(nextSceneName))
            {
                Instance.nextSceneName = nextSceneName;
                Debug.Log($"更新Instance的nextSceneName為: {nextSceneName}");
            }
            Destroy(gameObject);
            return;
        }
        
        // 訂閱分數變化事件
        player1Score.OnValueChanged += OnScoreChanged;
        player2Score.OnValueChanged += OnScoreChanged;
    }
    
    private void OnDestroy()
    {
        player1Score.OnValueChanged -= OnScoreChanged;
        player2Score.OnValueChanged -= OnScoreChanged;
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // 如果沒有在Inspector中設置，自動查找Score文字物件
        if (scoreText == null)
        {
            GameObject scoreObj = GameObject.Find("Score");
            if (scoreObj != null)
            {
                scoreText = scoreObj.GetComponent<Text>();
            }
        }
        
        // 初始化顯示
        UpdateScoreDisplay();
        
        // 重置場景切換標記（當重新生成時）
        isTransitioning = false;
        
        // 訂閱場景加載事件，以便在場景切換後重置玩家狀態和更新nextSceneName
        // 注意：場景事件應該在所有客戶端訂閱，而不只是服務器端
        if (NetworkManager != null && NetworkManager.SceneManager != null)
        {
            NetworkManager.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;
            NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;
        }
    }
    
    public override void OnNetworkDespawn()
    {
        // 取消訂閱場景事件
        if (NetworkManager != null && NetworkManager.SceneManager != null)
        {
            NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
            NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
        }
        
        base.OnNetworkDespawn();
    }
    
    private void OnSceneEvent(Unity.Netcode.SceneEvent sceneEvent)
    {
        // 當場景開始加載時，立即查找新場景中的GameManager並讀取nextSceneName
        // 這樣可以在新場景的GameManager被銷毀之前讀取它的nextSceneName
        if (sceneEvent.SceneEventType == Unity.Netcode.SceneEventType.Load)
        {
            StartCoroutine(UpdateNextSceneNameOnLoad(sceneEvent.SceneName));
        }
    }
    
    private IEnumerator UpdateNextSceneNameOnLoad(string sceneName)
    {
        // 等待一幀，確保新場景的GameManager已經被創建
        yield return null;
        
        // 查找新場景中的GameManager
        GameManager[] allGameManagers = FindObjectsOfType<GameManager>();
        foreach (var gm in allGameManagers)
        {
            if (gm != null && gm != this)
            {
                // 從新場景的GameManager中讀取nextSceneName（即使它會被銷毀）
                string newSceneName = gm.nextSceneName;
                if (!string.IsNullOrEmpty(newSceneName))
                {
                    nextSceneName = newSceneName;
                    Debug.Log($"場景 {sceneName} 開始加載，更新nextSceneName為: {nextSceneName}");
                    break;
                }
            }
        }
    }
    
    private void OnSceneLoadCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, System.Collections.Generic.List<ulong> clientsCompleted, System.Collections.Generic.List<ulong> clientsTimedOut)
    {
        Debug.Log($"場景加載完成: {sceneName} (客戶端: {NetworkManager.LocalClientId}, 是否服務器: {IsServer})");
        
        // 重置場景切換標記（重要！因為GameManager使用DontDestroyOnLoad，不會重新spawn）
        isTransitioning = false;
        Debug.Log($"重置isTransitioning為false，允許新的場景切換 (客戶端: {NetworkManager.LocalClientId})");
        
        // 如果是reward場景，顯示勝利文字
        if (sceneName == "reward")
        {
            rewardWinTextShown = false; // 重置標誌，允許新場景顯示勝利文字
            StartCoroutine(UpdateRewardSceneWinText());
        }
        else
        {
            // 場景加載完成後，重置所有玩家的狀態（只在服務器端執行）
            if (IsServer)
            {
                // 等待一小段時間確保所有玩家都已生成
                StartCoroutine(DelayedResetPlayersState());
            }
        }
        
        // 更新Score文字物件引用（新場景可能有新的Score物件）- 所有客戶端都需要執行
        StartCoroutine(UpdateScoreTextAfterSceneLoad());
        
        // 更新nextSceneName（從新場景中的GameManager讀取，如果存在）- 所有客戶端都需要執行
        StartCoroutine(UpdateNextSceneNameAfterSceneLoad());
    }
    
    private IEnumerator UpdateRewardSceneWinText()
    {
        GameObject p1WinObj = null;
        GameObject p2WinObj = null;
        
        // 等待場景完全加載，多次嘗試查找（因為GameObject.Find找不到inactive的物件）
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForSeconds(0.1f);
            
            // 方法1: 嘗試用GameObject.Find（只能找到active的物件）
            p1WinObj = GameObject.Find("P1Wintxt");
            p2WinObj = GameObject.Find("P2Wintxt");
            
            // 方法2: 如果Find找不到，從Canvas查找所有子物件（包括inactive的）
            if (p1WinObj == null || p2WinObj == null)
            {
                Canvas canvas = FindObjectOfType<Canvas>();
                if (canvas != null)
                {
                    // 遞歸查找Canvas下的所有子物件（包括inactive的）
                    Transform[] allChildren = canvas.GetComponentsInChildren<Transform>(true); // true = 包括inactive的
                    foreach (Transform child in allChildren)
                    {
                        if (child.name == "P1Wintxt" && p1WinObj == null)
                        {
                            p1WinObj = child.gameObject;
                        }
                        else if (child.name == "P2Wintxt" && p2WinObj == null)
                        {
                            p2WinObj = child.gameObject;
                        }
                    }
                }
            }
            
            // 方法3: 如果還是找不到，使用Resources.FindObjectsOfTypeAll（包括所有物件，包括inactive和場景外的）
            if (p1WinObj == null || p2WinObj == null)
            {
                GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (GameObject obj in allObjects)
                {
                    // 只檢查當前場景中的物件
                    if (obj != null && obj.scene.isLoaded && obj.scene.name == "reward")
                    {
                        if (obj.name == "P1Wintxt" && p1WinObj == null)
                        {
                            p1WinObj = obj;
                        }
                        else if (obj.name == "P2Wintxt" && p2WinObj == null)
                        {
                            p2WinObj = obj;
                        }
                    }
                }
            }
            
            if (p1WinObj != null && p2WinObj != null)
            {
                Debug.Log($"成功找到勝利文字物件: P1Wintxt={p1WinObj != null}, P2Wintxt={p2WinObj != null} (嘗試 {i+1}/10)");
                break;
            }
        }
        
        if (p1WinObj == null || p2WinObj == null)
        {
            Debug.LogError($"無法找到勝利文字物件: P1Wintxt={p1WinObj != null}, P2Wintxt={p2WinObj != null}");
            yield break;
        }
        
        // 獲取分數
        int p1Score = player1Score.Value;
        int p2Score = player2Score.Value;
        
        Debug.Log($"Reward場景: P1分數={p1Score}, P2分數={p2Score}");
        
        // 根據比分顯示勝利文字
        if (p1Score > p2Score)
        {
            // P1勝利
            p1WinObj.SetActive(true);
            p2WinObj.SetActive(false);
            rewardWinTextShown = true; // 標記為已顯示
            Debug.Log($"✓ 顯示P1勝利 (P1:{p1Score} > P2:{p2Score})");
        }
        else if (p2Score > p1Score)
        {
            // P2勝利
            p1WinObj.SetActive(false);
            p2WinObj.SetActive(true);
            rewardWinTextShown = true; // 標記為已顯示
            Debug.Log($"✓ 顯示P2勝利 (P2:{p2Score} > P1:{p1Score})");
        }
        else
        {
            // 平局（可以選擇顯示兩個或都不顯示）
            p1WinObj.SetActive(false);
            p2WinObj.SetActive(false);
            rewardWinTextShown = true; // 標記為已處理
            Debug.Log($"平局 (P1:{p1Score} = P2:{p2Score})");
        }
    }
    
    private IEnumerator UpdateNextSceneNameAfterSceneLoad()
    {
        // 等待場景完全加載，確保所有GameManager都已初始化
        // 多次嘗試查找新場景中的GameManager（因為場景加載可能需要時間）
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForSeconds(0.1f);
            
            // 查找新場景中的GameManager（可能有多個，但只有一個會被保留）
            // 注意：新場景的GameManager會在Awake時被銷毀，但我們可以在它被銷毀前讀取nextSceneName
            GameManager[] allGameManagers = FindObjectsOfType<GameManager>();
            foreach (var gm in allGameManagers)
            {
                if (gm != null && gm != this)
                {
                    // 從新場景的GameManager中讀取nextSceneName（即使它會被銷毀）
                    string newSceneName = gm.nextSceneName;
                    if (!string.IsNullOrEmpty(newSceneName))
                    {
                        nextSceneName = newSceneName;
                        Debug.Log($"場景切換完成，更新nextSceneName為: {nextSceneName} (客戶端: {NetworkManager.LocalClientId})");
                        yield break; // 成功找到後退出
                    }
                }
            }
        }
        
        // 如果還是找不到，輸出警告
        Debug.LogWarning($"場景加載完成後無法從新場景的GameManager讀取nextSceneName，當前nextSceneName為: {nextSceneName} (客戶端: {NetworkManager.LocalClientId})");
    }
    
    private IEnumerator DelayedResetPlayersState()
    {
        // 等待0.5秒確保所有玩家都已生成
        yield return new WaitForSeconds(0.5f);
        
        ResetAllPlayersStateClientRpc();
    }
    
    private IEnumerator UpdateScoreTextAfterSceneLoad()
    {
        // 等待場景完全加載，多次嘗試查找Score物件（因為場景加載可能需要時間）
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForSeconds(0.1f);
            
            // 重新查找Score文字物件
            GameObject scoreObj = GameObject.Find("Score");
            if (scoreObj != null)
            {
                scoreText = scoreObj.GetComponent<Text>();
                if (scoreText != null)
                {
                    UpdateScoreDisplay();
                    Debug.Log($"場景加載完成，成功更新Score文字物件引用 (客戶端: {NetworkManager.LocalClientId})");
                    break;
                }
            }
        }
        
        // 如果還是找不到，輸出警告
        if (scoreText == null)
        {
            Debug.LogWarning($"場景加載完成後無法找到Score文字物件 (客戶端: {NetworkManager.LocalClientId})");
        }
    }
    
    [ClientRpc]
    private void ResetAllPlayersStateClientRpc()
    {
        // 找到所有玩家並重置狀態
        NetworkPlayerController[] allPlayers = FindObjectsOfType<NetworkPlayerController>();
        foreach (var player in allPlayers)
        {
            if (player != null)
            {
                player.ResetPlayerState();
            }
        }
    }
    
    private void OnScoreChanged(int previous, int current)
    {
        UpdateScoreDisplay();
    }
    
    private void UpdateScoreDisplay()
    {
        if (scoreText != null)
        {
            int p1Score = player1Score.Value;
            int p2Score = player2Score.Value;
            
            scoreText.text = $"{p1Score} : {p2Score}";
        }
    }
    
    private void Start()
    {
        // 如果沒有在Inspector中設置，自動查找Score文字物件
        if (scoreText == null)
        {
            GameObject scoreObj = GameObject.Find("Score");
            if (scoreObj != null)
            {
                scoreText = scoreObj.GetComponent<Text>();
            }
        }
        
        // 初始化顯示
        if (scoreText != null)
        {
            UpdateScoreDisplay();
        }
        
        // 檢查當前場景是否為reward場景（確保在Play Mode和Build中都能正確處理）
        // 這對於Play Mode特別重要，因為場景加載事件的觸發時機可能不同
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentSceneName == "reward" && !rewardWinTextShown)
        {
            Debug.Log($"Start()中檢測到reward場景，開始更新勝利文字 (客戶端: {NetworkManager?.LocalClientId}, 是否服務器: {IsServer}, IsSpawned: {IsSpawned})");
            rewardWinTextShown = false; // 重置標誌
            StartCoroutine(UpdateRewardSceneWinText());
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void AddScoreServerRpc(ulong deadPlayerClientId)
    {
        // 找到所有玩家
        NetworkPlayerController[] allPlayers = FindObjectsOfType<NetworkPlayerController>();
        
        if (allPlayers.Length < 2) return;
        
        // 找到死亡的玩家和對方玩家
        NetworkPlayerController deadPlayer = null;
        NetworkPlayerController otherPlayer = null;
        
        foreach (var player in allPlayers)
        {
            if (player == null) continue;
            
            if (player.OwnerClientId == deadPlayerClientId)
            {
                deadPlayer = player;
            }
            else
            {
                otherPlayer = player;
            }
        }
        
        // 如果找到兩個玩家，增加對方玩家的分數
        if (deadPlayer != null && otherPlayer != null)
        {
            // 獲取兩個玩家的ClientId並排序
            List<ulong> clientIds = new List<ulong>();
            foreach (var player in allPlayers)
            {
                if (player != null)
                {
                    clientIds.Add(player.OwnerClientId);
                }
            }
            clientIds.Sort();
            
            // 較小的ClientId是玩家1，較大的是玩家2
            ulong p1ClientId = clientIds[0];
            ulong p2ClientId = clientIds.Count > 1 ? clientIds[1] : p1ClientId;
            
            // 增加對方玩家的分數
            if (otherPlayer.OwnerClientId == p1ClientId)
            {
                player1Score.Value++;
            }
            else if (otherPlayer.OwnerClientId == p2ClientId)
            {
                player2Score.Value++;
            }
        }
    }
    
    // 公共方法：當玩家死亡時調用
    public void OnPlayerDeath(ulong deadPlayerClientId)
    {
        Debug.Log($"[OnPlayerDeath] 玩家 {deadPlayerClientId} 死亡，是否服務器: {IsServer}, isTransitioning: {isTransitioning}");
        
        if (!IsServer)
        {
            Debug.LogWarning("OnPlayerDeath只能在服務器端調用");
            return;
        }
        
        if (isTransitioning)
        {
            Debug.LogWarning($"場景正在切換中(isTransitioning={isTransitioning})，忽略死亡事件");
            return;
        }
        
        // 在切換場景前，根據當前場景名稱確定下一個場景（使用與Level1相同的方法）
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        Debug.Log($"當前場景: {currentSceneName}, 當前nextSceneName: {nextSceneName}");
        
        // 如果nextSceneName為空或與當前場景相同，根據場景名稱設定下一個場景
        if (string.IsNullOrEmpty(nextSceneName) || nextSceneName == currentSceneName)
        {
            if (currentSceneName == "Level1")
            {
                nextSceneName = "Level2";
            }
            else if (currentSceneName == "Level2")
            {
                nextSceneName = "Level3";
            }
            else if (currentSceneName == "Level3")
            {
                nextSceneName = "Level3"; // 或者設定為其他場景
            }
            Debug.Log($"根據當前場景 {currentSceneName} 設定nextSceneName為: {nextSceneName}");
        }
        
        // 再次嘗試從場景中的GameManager讀取nextSceneName（如果有的話）
        GameManager[] allGameManagers = FindObjectsOfType<GameManager>();
        foreach (var gm in allGameManagers)
        {
            if (gm != null && gm != this && !string.IsNullOrEmpty(gm.nextSceneName))
            {
                nextSceneName = gm.nextSceneName;
                Debug.Log($"從場景中的GameManager更新nextSceneName為: {nextSceneName}");
                break;
            }
        }
        
        Debug.Log($"玩家 {deadPlayerClientId} 死亡，準備切換到場景: {nextSceneName}");
        
        // 更新分數
        AddScoreServerRpc(deadPlayerClientId);
        
        // 開始場景切換
        StartCoroutine(TransitionToNextScene());
    }
    
    private IEnumerator TransitionToNextScene()
    {
        // 防止重複切換
        isTransitioning = true;
        
        // 等待一段時間（可以顯示勝利/失敗訊息）
        yield return new WaitForSeconds(sceneTransitionDelay);
        
        // 檢查nextSceneName是否有效
        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogError("nextSceneName為空，無法切換場景！");
            isTransitioning = false;
            yield break;
        }
        
        Debug.Log($"開始切換場景到: {nextSceneName}");
        
        // 切換場景
        if (IsServer && NetworkManager != null && NetworkManager.SceneManager != null)
        {
            // 使用NetworkManager的SceneManager來加載場景（會同步到所有客戶端）
            NetworkManager.SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
            Debug.Log($"已請求加載場景: {nextSceneName}");
        }
        else if (IsServer)
        {
            // 如果NetworkSceneManager不可用，使用普通SceneManager（不推薦，但作為備用）
            Debug.LogWarning("NetworkSceneManager不可用，使用普通SceneManager切換場景");
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogError("不是服務器，無法切換場景");
            isTransitioning = false;
        }
    }
}

