using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeneathTheFloor.Interaction
{
    public class SceneTransition : MonoBehaviour, IInteractable
    {
        [Header("Transition Settings")]
        [SerializeField] private string targetSceneName;
        [SerializeField] private string interactionText = "Enter";
        [SerializeField] private bool requiresCondition = false;
        [SerializeField] private string requiredCondition = "";

        [Header("Spawn Point")]
        [SerializeField] private string spawnPointName = "SpawnPoint";
        [SerializeField] private Vector3 spawnOffset = Vector3.zero;

        [Header("Audio")]
        [SerializeField] private AudioClip transitionSound;

        private bool canUse = true;

        public bool CanInteract => canUse && CheckCondition();

        private void Start()
        {
            // Check spawn point on scene load
            CheckForSpawnPoint();
        }

        private void CheckForSpawnPoint()
        {
            // If we just loaded this scene, check if we need to teleport player to spawn point
            string lastScene = PlayerPrefs.GetString("LastScene", "");
            string currentSpawn = PlayerPrefs.GetString("SpawnPoint", "");

            if (!string.IsNullOrEmpty(currentSpawn) && currentSpawn == spawnPointName)
            {
                // Find player and teleport
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    player.transform.position = transform.position + spawnOffset;
                    player.transform.rotation = transform.rotation;
                }

                // Clear spawn point
                PlayerPrefs.DeleteKey("SpawnPoint");
            }
        }

        public string GetInteractionText()
        {
            if (!CheckCondition())
            {
                return "Locked";
            }
            return $"Press E to {interactionText}";
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract) return;

            TransitionToScene();
        }

        public void OnHoverEnter()
        {
            // Could add highlight effect
        }

        public void OnHoverExit()
        {
            // Remove highlight effect
        }

        private bool CheckCondition()
        {
            if (!requiresCondition) return true;

            // Check various conditions
            switch (requiredCondition)
            {
                case "WallBroken":
                    return PlayerPrefs.GetInt("WallBroken", 0) == 1;
                default:
                    return true;
            }
        }

        private void TransitionToScene()
        {
            // Play sound
            if (transitionSound != null)
            {
                AudioSource.PlayClipAtPoint(transitionSound, transform.position);
            }

            // Save current scene info
            PlayerPrefs.SetString("LastScene", SceneManager.GetActiveScene().name);
            PlayerPrefs.SetString("SpawnPoint", spawnPointName);
            PlayerPrefs.Save();

            // Load target scene
            if (!string.IsNullOrEmpty(targetSceneName))
            {
                SceneManager.LoadScene(targetSceneName);
            }
        }

        public void SetTargetScene(string sceneName)
        {
            targetSceneName = sceneName;
        }

        public void SetSpawnPoint(string pointName)
        {
            spawnPointName = pointName;
        }
    }
}
