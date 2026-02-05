using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeneathTheFloor.Interaction
{
    public class CrackedWall : MonoBehaviour, IInteractable
    {
        [Header("Settings")]
        [SerializeField] private string basementSceneName = "BasementScene";
        [SerializeField] private bool startDestroyed = false; // Set true if wall starts already broken
        [SerializeField] private bool loadBasementOnBreak = false; // Disabled: basement is now part of HouseBuilding
        [SerializeField] private GameObject intactWall;
        [SerializeField] private GameObject brokenWall;
        [SerializeField] private GameObject stairsToBasement;
        [SerializeField] private bool disableChildTransitions = true; // Disable child SceneTransition objects to prevent duplicate interactables

        [Header("Audio")]
        [SerializeField] private AudioClip breakSound;
        [SerializeField] private AudioClip interactSound;

        [Header("Highlight")]
        [SerializeField] private Color highlightColor = new Color(1f, 0.8f, 0.3f, 1f);
        [SerializeField] private float highlightIntensity = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        private AudioSource audioSource;
        private bool hasBeenBroken = false;
        private Renderer[] renderers;
        private Color[] originalEmissionColors;
        private bool[] hadEmission;

        public bool CanInteract => !hasBeenBroken;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            // Ensure this object or a child has a collider for raycasting
            EnsureCollider();
        }

        private void EnsureCollider()
        {
            // Check if we have a collider
            Collider col = GetComponent<Collider>();
            if (col == null)
            {
                // Check children
                col = GetComponentInChildren<Collider>();
            }

            if (col == null && debugMode)
            {
                Debug.LogWarning($"[CrackedWall] No collider found on {gameObject.name} or children. Adding BoxCollider.");
                // Add a default collider if none exists
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(2f, 3f, 0.5f);
                box.center = new Vector3(0f, 1.5f, 0f);
            }
        }

        private void Start()
        {
            if (startDestroyed)
            {
                hasBeenBroken = true;
            }

            // Disable any child SceneTransition objects to prevent duplicate interactables
            if (disableChildTransitions)
            {
                DisableChildSceneTransitions();
            }

            // Cache renderers for highlighting
            CacheRenderers();

            UpdateWallState();

            if (debugMode)
            {
                Debug.Log($"[CrackedWall] Initialized. CanInteract: {CanInteract}, LoadOnBreak: {loadBasementOnBreak}");
            }
        }

        private void CacheRenderers()
        {
            // Get all renderers including children (especially IntactWall)
            renderers = GetComponentsInChildren<Renderer>(true);
            originalEmissionColors = new Color[renderers.Length];
            hadEmission = new bool[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                Material mat = renderers[i].material;
                if (mat.HasProperty("_EmissionColor"))
                {
                    originalEmissionColors[i] = mat.GetColor("_EmissionColor");
                    hadEmission[i] = mat.IsKeywordEnabled("_EMISSION");
                }
            }

            if (debugMode)
            {
                Debug.Log($"[CrackedWall] Cached {renderers.Length} renderers for highlighting");
            }
        }

        private void DisableChildSceneTransitions()
        {
            // Find all child SceneTransition components and disable their GameObjects
            SceneTransition[] childTransitions = GetComponentsInChildren<SceneTransition>(true);
            foreach (SceneTransition transition in childTransitions)
            {
                if (transition.gameObject != gameObject)
                {
                    if (debugMode)
                    {
                        Debug.Log($"[CrackedWall] Disabling child SceneTransition on: {transition.gameObject.name}");
                    }
                    transition.gameObject.SetActive(false);
                }
            }
        }

        public string GetInteractionText()
        {
            return hasBeenBroken ? "" : "Press E to break wall";
        }

        public void Interact(GameObject interactor)
        {
            if (debugMode)
            {
                Debug.Log($"[CrackedWall] Interact() called. hasBeenBroken: {hasBeenBroken}");
            }

            if (hasBeenBroken) return;

            BreakWall();
        }

        public void OnHoverEnter()
        {
            if (debugMode)
            {
                Debug.Log($"[CrackedWall] OnHoverEnter() - Applying highlight");
            }
            ApplyHighlight(true);
        }

        public void OnHoverExit()
        {
            if (debugMode)
            {
                Debug.Log($"[CrackedWall] OnHoverExit() - Removing highlight");
            }
            ApplyHighlight(false);
        }

        private void ApplyHighlight(bool highlight)
        {
            if (renderers == null) return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;

                Material mat = renderers[i].material;
                if (mat.HasProperty("_EmissionColor"))
                {
                    if (highlight)
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", highlightColor * highlightIntensity);
                    }
                    else
                    {
                        if (!hadEmission[i])
                        {
                            mat.DisableKeyword("_EMISSION");
                        }
                        mat.SetColor("_EmissionColor", originalEmissionColors[i]);
                    }
                }
            }
        }

        private void BreakWall()
        {
            if (debugMode)
            {
                Debug.Log($"[CrackedWall] Breaking wall!");
            }

            hasBeenBroken = true;

            if (breakSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(breakSound);
            }

            UpdateWallState();

            // Trigger any events or achievements
            GameEvents.OnWallBroken?.Invoke();

            // Save state
            PlayerPrefs.SetInt("WallBroken", 1);
            PlayerPrefs.Save();

            // Optionally load basement immediately
            if (loadBasementOnBreak)
            {
                LoadBasement();
            }
        }

        private void UpdateWallState()
        {
            if (intactWall != null)
            {
                intactWall.SetActive(!hasBeenBroken);
            }

            if (brokenWall != null)
            {
                brokenWall.SetActive(hasBeenBroken);
            }

            if (stairsToBasement != null)
            {
                stairsToBasement.SetActive(hasBeenBroken);
            }
        }

        public void LoadBasement()
        {
            // BasementScene was merged into HouseBuilding — no scene load needed.
            // If the scene doesn't exist in build settings, skip to avoid crash.
            if (basementSceneName == SceneManager.GetActiveScene().name)
                return;

            // Check if scene exists in build settings before loading
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                if (sceneName == basementSceneName)
                {
                    PlayerPrefs.SetString("LastScene", SceneManager.GetActiveScene().name);
                    PlayerPrefs.SetString("SpawnPoint", "BasementEntrance");
                    PlayerPrefs.Save();
                    SceneManager.LoadScene(basementSceneName, LoadSceneMode.Single);
                    return;
                }
            }

            Debug.LogWarning($"[CrackedWall] Scene '{basementSceneName}' not found in build settings — skipping load.");
        }
    }
}
