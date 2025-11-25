using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [SerializeField] private Text scoreText; // Reference to the Score text UI in the scene
    [SerializeField] private string nextSceneName = "Level2"; // Name of the next level to load
    [SerializeField] private float sceneTransitionDelay = 2f; // How long to wait before switching scenes (in seconds)
    
    private bool isTransitioning = false; // Are we currently switching scenes?
    private bool rewardWinTextShown = false; // Did we already show the winner text on the reward screen?
    
    // Player 1 and Player 2 scores (using ClientId to tell them apart)
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
            // Keep GameManager alive when loading new scenes so we don't lose the score
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // If there's already a GameManager from the previous scene, update its next level name
            if (Instance != this && !string.IsNullOrEmpty(nextSceneName))
            {
                Instance.nextSceneName = nextSceneName;
                Debug.Log("Next scene is now set to " + nextSceneName);
            }
            Destroy(gameObject);
            return;
        }
        
        // Listen for when the score changes
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
        
        // If we didn't set the score text in Inspector, try to find it automatically
        if (scoreText == null)
        {
            GameObject scoreObj = GameObject.Find("Score");
            if (scoreObj != null)
            {
                scoreText = scoreObj.GetComponent<Text>();
            }
        }
        
        // Show the starting score
        UpdateScoreDisplay();
        
        // Reset the scene transition flag (when we respawn)
        isTransitioning = false;
        
        // Listen for scene loading events so we can reset players and update the next level name
        // Note: All clients need to listen to scene events, not just the server
        if (NetworkManager != null && NetworkManager.SceneManager != null)
        {
            NetworkManager.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;
            NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;
        }
    }
    
    public override void OnNetworkDespawn()
    {
        // Stop listening to scene events when this object is removed
        if (NetworkManager != null && NetworkManager.SceneManager != null)
        {
            NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
            NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
        }
        
        base.OnNetworkDespawn();
    }
    
    private void OnSceneEvent(Unity.Netcode.SceneEvent sceneEvent)
    {
        // When a scene starts loading, grab the next level name from the new scene's GameManager
        // We need to do this before the new GameManager gets destroyed
        if (sceneEvent.SceneEventType == Unity.Netcode.SceneEventType.Load)
        {
            StartCoroutine(UpdateNextSceneNameOnLoad(sceneEvent.SceneName));
        }
    }
    
    private IEnumerator UpdateNextSceneNameOnLoad(string sceneName)
    {
        // Wait one frame to make sure the new scene's GameManager has been created
        yield return null;
        
        // Look for GameManagers in the new scene
        GameManager[] allGameManagers = FindObjectsOfType<GameManager>();
        foreach (var gm in allGameManagers)
        {
            if (gm != null && gm != this)
            {
                // Grab the next level name from the new scene's GameManager (before it gets destroyed)
                string newSceneName = gm.nextSceneName;
                if (!string.IsNullOrEmpty(newSceneName))
                {
                    nextSceneName = newSceneName;
                    Debug.Log("Scene " + sceneName + " is loading, next level will be " + nextSceneName);
                    break;
                }
            }
        }
    }
    
    private void OnSceneLoadCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, System.Collections.Generic.List<ulong> clientsCompleted, System.Collections.Generic.List<ulong> clientsTimedOut)
    {
        Debug.Log("Scene " + sceneName + " loaded successfully on client " + NetworkManager.LocalClientId);
        
        // Reset the scene transition flag (important! GameManager uses DontDestroyOnLoad so it won't respawn)
        isTransitioning = false;
        Debug.Log("Ready for new scene transitions now");
        
        // If we're on the reward screen, show who won
        if (sceneName == "reward")
        {
            rewardWinTextShown = false; // Reset the flag so we can show the winner text
            StartCoroutine(UpdateRewardSceneWinText());
        }
        else
        {
            // After loading a level, reset all players (server only)
            if (IsServer)
            {
                // Wait a bit to make sure all players have spawned
                StartCoroutine(DelayedResetPlayersState());
            }
        }
        
        // Update the Score text reference (new scene might have a new Score object) - all clients need this
        StartCoroutine(UpdateScoreTextAfterSceneLoad());
        
        // Update the next level name (grab it from the new scene's GameManager if it exists) - all clients need this
        StartCoroutine(UpdateNextSceneNameAfterSceneLoad());
    }
    
    private IEnumerator UpdateRewardSceneWinText()
    {
        GameObject p1WinObj = null;
        GameObject p2WinObj = null;
        
        // Wait for the scene to fully load, try multiple times (GameObject.Find can't find inactive objects)
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForSeconds(0.1f);
            
            // Method 1: Try GameObject.Find (only finds active objects)
            p1WinObj = GameObject.Find("P1Wintxt");
            p2WinObj = GameObject.Find("P2Wintxt");
            
            // Method 2: If Find didn't work, search all children of Canvas (including inactive ones)
            if (p1WinObj == null || p2WinObj == null)
            {
                Canvas canvas = FindObjectOfType<Canvas>();
                if (canvas != null)
                {
                    // Search all children under Canvas (including inactive ones)
                    Transform[] allChildren = canvas.GetComponentsInChildren<Transform>(true); // true = include inactive objects
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
            
            // Method 3: If still not found, use Resources.FindObjectsOfTypeAll (finds all objects including inactive and out-of-scene)
            if (p1WinObj == null || p2WinObj == null)
            {
                GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (GameObject obj in allObjects)
                {
                    // Only check objects in the current scene
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
                Debug.Log("Found the winner text objects on attempt " + (i+1));
                break;
            }
        }
        
        if (p1WinObj == null || p2WinObj == null)
        {
            Debug.LogError("Could not find winner text objects");
            yield break;
        }
        
        // Get the current scores
        int p1Score = player1Score.Value;
        int p2Score = player2Score.Value;
        
        Debug.Log("Final scores are P1 " + p1Score + " and P2 " + p2Score);
        
        // Show the winner based on the scores
        if (p1Score > p2Score)
        {
            // Player 1 wins!
            p1WinObj.SetActive(true);
            p2WinObj.SetActive(false);
            rewardWinTextShown = true;
            Debug.Log("Player 1 wins with " + p1Score + " points");
        }
        else if (p2Score > p1Score)
        {
            // Player 2 wins!
            p1WinObj.SetActive(false);
            p2WinObj.SetActive(true);
            rewardWinTextShown = true;
            Debug.Log("Player 2 wins with " + p2Score + " points");
        }
        else
        {
            // It's a tie!
            p1WinObj.SetActive(false);
            p2WinObj.SetActive(false);
            rewardWinTextShown = true;
            Debug.Log("It's a tie at " + p1Score + " points each");
        }
    }
    
    private IEnumerator UpdateNextSceneNameAfterSceneLoad()
    {
        // Wait for the scene to fully load and make sure all GameManagers are initialized
        // Try multiple times to find the new scene's GameManager (scene loading takes time)
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForSeconds(0.1f);
            
            // Look for GameManagers in the new scene (might be multiple, but only one stays)
            // Note: The new scene's GameManager gets destroyed in Awake, but we can grab nextSceneName before that
            GameManager[] allGameManagers = FindObjectsOfType<GameManager>();
            foreach (var gm in allGameManagers)
            {
                if (gm != null && gm != this)
                {
                    // Grab the next level name from the new scene's GameManager (before it gets destroyed)
                    string newSceneName = gm.nextSceneName;
                    if (!string.IsNullOrEmpty(newSceneName))
                    {
                        nextSceneName = newSceneName;
                        Debug.Log("Next level updated to " + nextSceneName);
                        yield break; // Success! Exit the loop
                    }
                }
            }
        }
        
        // If we still couldn't find it, show a warning
        Debug.LogWarning("Could not find next level name, keeping current one as " + nextSceneName);
    }
    
    private IEnumerator DelayedResetPlayersState()
    {
        // Wait 0.5 seconds to make sure all players have spawned
        yield return new WaitForSeconds(0.5f);
        
        ResetAllPlayersStateClientRpc();
    }
    
    private IEnumerator UpdateScoreTextAfterSceneLoad()
    {
        // Wait for scene to fully load, try multiple times to find Score object (scene loading takes time)
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForSeconds(0.1f);
            
            // Try to find the Score text object again
            GameObject scoreObj = GameObject.Find("Score");
            if (scoreObj != null)
            {
                scoreText = scoreObj.GetComponent<Text>();
                if (scoreText != null)
                {
                    UpdateScoreDisplay();
                    Debug.Log("Score display found and updated");
                    break;
                }
            }
        }
        
        // If we still couldn't find it, show a warning
        if (scoreText == null)
        {
            Debug.LogWarning("Could not find score text in this scene");
        }
    }
    
    [ClientRpc]
    private void ResetAllPlayersStateClientRpc()
    {
        // Find all players and reset their states
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
        // If we didn't set the score text in Inspector, try to find it automatically
        if (scoreText == null)
        {
            GameObject scoreObj = GameObject.Find("Score");
            if (scoreObj != null)
            {
                scoreText = scoreObj.GetComponent<Text>();
            }
        }
        
        // Show the starting score
        if (scoreText != null)
        {
            UpdateScoreDisplay();
        }
        
        // Check if we're on the reward screen (works in both Play Mode and Build)
        // This is especially important for Play Mode since scene loading events might fire at different times
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentSceneName == "reward" && !rewardWinTextShown)
        {
            Debug.Log("We are on reward scene, showing who won");
            rewardWinTextShown = false; // Reset the flag
            StartCoroutine(UpdateRewardSceneWinText());
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void AddScoreServerRpc(ulong deadPlayerClientId)
    {
        // Find all players in the game
        NetworkPlayerController[] allPlayers = FindObjectsOfType<NetworkPlayerController>();
        
        if (allPlayers.Length < 2) return;
        
        // Figure out who died and who's still alive
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
        
        // If we found both players, give the winner a point
        if (deadPlayer != null && otherPlayer != null)
        {
            // Get both players' ClientIds and sort them
            List<ulong> clientIds = new List<ulong>();
            foreach (var player in allPlayers)
            {
                if (player != null)
                {
                    clientIds.Add(player.OwnerClientId);
                }
            }
            clientIds.Sort();
            
            // Smaller ClientId is Player 1, larger is Player 2
            ulong p1ClientId = clientIds[0];
            ulong p2ClientId = clientIds.Count > 1 ? clientIds[1] : p1ClientId;
            
            // Add a point to the winner
            if (otherPlayer.OwnerClientId == p1ClientId)
            {
                player1Score.Value++;
                Debug.Log("Player 1 scored a point, now at " + player1Score.Value);
            }
            else if (otherPlayer.OwnerClientId == p2ClientId)
            {
                player2Score.Value++;
                Debug.Log("Player 2 scored a point, now at " + player2Score.Value);
            }
        }
    }
    
    // Public method: Called when a player dies
    public void OnPlayerDeath(ulong deadPlayerClientId)
    {
        Debug.Log("Player " + deadPlayerClientId + " just died");
        
        if (!IsServer)
        {
            Debug.LogWarning("This can only be called on the server");
            return;
        }
        
        if (isTransitioning)
        {
            Debug.LogWarning("Already switching scenes, ignoring this death");
            return;
        }
        
        // Before switching scenes, figure out what the next scene should be based on current scene
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        Debug.Log("Current scene is " + currentSceneName);
        
        // If nextSceneName is empty or the same as current scene, set it based on the scene name
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
                nextSceneName = "Level3"; // Or set to another scene if you want
            }
            Debug.Log("Next scene will be " + nextSceneName);
        }
        
        // Try one more time to grab nextSceneName from any GameManager in the scene
        GameManager[] allGameManagers = FindObjectsOfType<GameManager>();
        foreach (var gm in allGameManagers)
        {
            if (gm != null && gm != this && !string.IsNullOrEmpty(gm.nextSceneName))
            {
                nextSceneName = gm.nextSceneName;
                Debug.Log("Found next scene info, will go to " + nextSceneName);
                break;
            }
        }
        
        Debug.Log("Getting ready to load " + nextSceneName);
        
        // Update the score
        AddScoreServerRpc(deadPlayerClientId);
        
        // Start the scene transition
        StartCoroutine(TransitionToNextScene());
    }
    
    private IEnumerator TransitionToNextScene()
    {
        // Prevent switching multiple times
        isTransitioning = true;
        
        // Wait a bit (can show win/lose messages during this time)
        yield return new WaitForSeconds(sceneTransitionDelay);
        
        // Make sure we have a valid scene to load
        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogError("Next scene name is empty, cannot switch scenes");
            isTransitioning = false;
            yield break;
        }
        
        Debug.Log("Starting to load " + nextSceneName + " now");
        
        // Load the next scene
        if (IsServer && NetworkManager != null && NetworkManager.SceneManager != null)
        {
            // Use NetworkManager's SceneManager to load the scene (syncs to all clients)
            NetworkManager.SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
            Debug.Log("Scene load request sent for " + nextSceneName);
        }
        else if (IsServer)
        {
            // If NetworkSceneManager isn't available, use regular SceneManager (not recommended, but backup option)
            Debug.LogWarning("NetworkSceneManager not available, using regular scene manager");
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogError("Only the server can switch scenes");
            isTransitioning = false;
        }
    }
}

