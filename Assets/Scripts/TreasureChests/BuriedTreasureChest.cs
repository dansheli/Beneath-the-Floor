using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using BeneathTheFloor.Interaction;
using BeneathTheFloor.Digging;
using BeneathTheFloor.Inventory;
using BeneathTheFloor.Economy;

namespace BeneathTheFloor.TreasureChests
{
    /// <summary>
    /// A treasure chest buried in the terrain.
    /// Becomes interactable when uncovered (no terrain above).
    /// Press E to open and receive rewards.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BuriedTreasureChest : MonoBehaviour, IInteractable
    {
        [Header("Identification")]
        [Tooltip("Unique ID for this chest (used for save/load). Auto-generated if empty.")]
        [SerializeField] private string chestId;

        [Header("Rewards")]
        [Tooltip("Rewards given when this chest is opened")]
        [SerializeField] private TreasureChestReward[] rewards;

        [Header("Reveal Detection")]
        [Tooltip("How often to check if chest is uncovered (seconds)")]
        [SerializeField] private float revealCheckInterval = 0.5f;

        [Tooltip("Initial delay before checking (lets terrain generate first)")]
        [SerializeField] private float initialDelay = 2f;

        [Tooltip("Radius around chest to check for terrain density")]
        [SerializeField] private float exposureCheckRadius = 0.4f;

        [Tooltip("Density threshold - below this means terrain was dug away")]
        [Range(0f, 1f)]
        [SerializeField] private float exposureThreshold = 0.5f;

        [Header("Physics")]
        [Tooltip("Rigidbody to enable (auto-found if null)")]
        [SerializeField] private Rigidbody chestRigidbody;

        [Header("Radar Integration")]
        [Tooltip("Enable radar detection for this chest")]
        [SerializeField] private bool enableRadarDetection = true;

        [Tooltip("Multiplier for radar detection range (higher = detected from further)")]
        [Range(0.1f, 5f)]
        [SerializeField] private float radarRangeMultiplier = 1f;

        [Tooltip("Priority multiplier for radar (higher = shows before nodes at same distance)")]
        [Range(0.1f, 5f)]
        [SerializeField] private float radarPriorityMultiplier = 1f;

        [Header("Visual Effects")]
        [Tooltip("Particle system for disintegration effect (auto-created if null)")]
        [SerializeField] private ParticleSystem disintegrationEffect;

        [Tooltip("Duration of disintegration animation")]
        [SerializeField] private float disintegrationDuration = 1.5f;

        [Tooltip("Color for disintegration particles")]
        [SerializeField] private Color disintegrationColor = new Color(1f, 0.85f, 0.5f);

        [Header("Audio")]
        [Tooltip("Sound when chest is revealed (uncovered)")]
        [SerializeField] private AudioClip revealSound;

        [Tooltip("Sound when chest is opened")]
        [SerializeField] private AudioClip openSound;

        [Tooltip("Volume for chest sounds")]
        [Range(0f, 1f)]
        [SerializeField] private float soundVolume = 1f;

        [Header("Interaction")]
        [Tooltip("Text shown when hovering over revealed chest")]
        [SerializeField] private string interactionText = "Press E to Open";

        [Header("Glow Effect")]
        [Tooltip("Glow color when revealed")]
        [SerializeField] private Color glowColor = new Color(1f, 0.9f, 0.5f);

        [Tooltip("Glow intensity")]
        [Range(0f, 5f)]
        [SerializeField] private float glowIntensity = 2f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        // State
        private bool isRevealed = false;
        private bool isOpened = false;
        private bool isDisintegrating = false;
        private float revealCheckTimer = 0f;
        private float initialDelayTimer = 0f;
        private bool initialDelayPassed = false;
        private AudioSource audioSource;
        private List<MeshRenderer> meshRenderers = new List<MeshRenderer>();
        private Dictionary<MeshRenderer, Material[]> originalMaterials = new Dictionary<MeshRenderer, Material[]>();
        private bool hasLoggedMissingDigSystem = false;

        // Properties
        public string ChestId => chestId;
        public bool IsRevealed => isRevealed;
        public bool IsOpened => isOpened;
        public bool EnableRadarDetection => enableRadarDetection && !isOpened;
        public float RadarRangeMultiplier => radarRangeMultiplier;
        public float RadarPriorityMultiplier => radarPriorityMultiplier;

        // IInteractable
        public bool CanInteract => isRevealed && !isOpened && !isDisintegrating;

        private void Awake()
        {
            // Auto-generate chest ID if empty
            if (string.IsNullOrEmpty(chestId))
            {
                chestId = $"chest_{transform.position.x:F0}_{transform.position.y:F0}_{transform.position.z:F0}_{GetInstanceID()}";
            }

            // Get/add components
            if (chestRigidbody == null)
            {
                chestRigidbody = GetComponent<Rigidbody>();
            }

            // Cache mesh renderers for glow effect
            meshRenderers.AddRange(GetComponentsInChildren<MeshRenderer>());
            foreach (var mr in meshRenderers)
            {
                originalMaterials[mr] = mr.materials;
            }

            // Setup audio source
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound
            audioSource.maxDistance = 20f;
        }

        private void Start()
        {
            // Register with manager
            if (TreasureChestManager.Instance != null)
            {
                TreasureChestManager.Instance.RegisterChest(this);
            }
            else
            {
                StartCoroutine(WaitForManager());
            }

            // Initially disable physics
            if (chestRigidbody != null && !isRevealed)
            {
                chestRigidbody.isKinematic = true;
            }

            // Make sure collider is trigger initially for reveal detection
            var collider = GetComponent<Collider>();
            if (collider != null && !isRevealed)
            {
                // Keep collider active but physics disabled
            }

            if (debugMode)
            {
                Debug.Log($"[BuriedTreasureChest] Started: {chestId} at {transform.position}");
            }
        }

        private IEnumerator WaitForManager()
        {
            float timeout = 5f;
            float elapsed = 0f;

            while (TreasureChestManager.Instance == null && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (TreasureChestManager.Instance != null)
            {
                TreasureChestManager.Instance.RegisterChest(this);
            }
        }

        private void OnDestroy()
        {
            if (TreasureChestManager.Instance != null)
            {
                TreasureChestManager.Instance.UnregisterChest(this);
            }
        }

        private void Update()
        {
            // Skip if already revealed or opened
            if (isRevealed || isOpened)
                return;

            // Wait for initial delay (let terrain generate)
            if (!initialDelayPassed)
            {
                initialDelayTimer += Time.deltaTime;
                if (initialDelayTimer >= initialDelay)
                {
                    initialDelayPassed = true;
                    if (debugMode)
                    {
                        Debug.Log($"[BuriedTreasureChest] {chestId} initial delay passed, starting reveal checks");
                    }
                }
                return;
            }

            // Periodic reveal check
            revealCheckTimer += Time.deltaTime;
            if (revealCheckTimer >= revealCheckInterval)
            {
                revealCheckTimer = 0f;
                CheckIfRevealed();
            }
        }

        #region Reveal Detection

        /// <summary>
        /// Check if the chest is uncovered by checking terrain density at chest position.
        /// When the terrain covering the chest is dug away, reveal it.
        /// </summary>
        private void CheckIfRevealed()
        {
            if (DiggingSystem.Instance == null)
            {
                // No digging system - can't check density (log once only)
                if (debugMode && !hasLoggedMissingDigSystem)
                {
                    hasLoggedMissingDigSystem = true;
                    Debug.LogWarning($"[BuriedTreasureChest] {chestId} No DiggingSystem found!");
                }
                return;
            }

            // Check density at chest center and around it
            float centerDensity = DiggingSystem.Instance.GetDensityAt(transform.position);

            // Also check a few points around the chest
            float totalDensity = centerDensity;
            int sampleCount = 1;

            // Sample points around chest at the exposure radius
            Vector3[] offsets = new Vector3[]
            {
                Vector3.forward * exposureCheckRadius,
                Vector3.back * exposureCheckRadius,
                Vector3.left * exposureCheckRadius,
                Vector3.right * exposureCheckRadius,
                Vector3.up * exposureCheckRadius,
            };

            foreach (var offset in offsets)
            {
                float density = DiggingSystem.Instance.GetDensityAt(transform.position + offset);
                totalDensity += density;
                sampleCount++;
            }

            float avgDensity = totalDensity / sampleCount;

            if (debugMode && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[BuriedTreasureChest] {chestId} density - center: {centerDensity:F2}, avg: {avgDensity:F2}, threshold: {exposureThreshold}");
            }

            // Reveal if average density is below threshold (terrain was dug away)
            if (avgDensity < exposureThreshold)
            {
                if (debugMode)
                {
                    Debug.Log($"[BuriedTreasureChest] {chestId} EXPOSED! Avg density {avgDensity:F2} < {exposureThreshold}");
                }
                OnRevealed();
            }
        }

        /// <summary>
        /// Called when the chest becomes revealed (uncovered).
        /// </summary>
        private void OnRevealed()
        {
            if (isRevealed)
                return;

            isRevealed = true;

            if (debugMode)
            {
                Debug.Log($"[BuriedTreasureChest] Revealed: {chestId}");
            }

            // Keep chest kinematic — never enable physics to avoid falling through terrain
            if (chestRigidbody != null)
            {
                chestRigidbody.isKinematic = true;
            }

            // Play reveal sound
            if (revealSound != null)
            {
                audioSource.PlayOneShot(revealSound, soundVolume);
            }

            // Notify manager
            if (TreasureChestManager.Instance != null)
            {
                TreasureChestManager.Instance.NotifyChestRevealed(this);
            }

            // Auto-open: give rewards and disintegrate immediately
            OpenChest();
        }

        #endregion

        #region Interaction (IInteractable)

        public string GetInteractionText()
        {
            return interactionText;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract)
                return;

            OpenChest();
        }

        public void OnHoverEnter()
        {
            // Could add highlight effect here
        }

        public void OnHoverExit()
        {
            // Could remove highlight effect here
        }

        #endregion

        #region Open Chest

        /// <summary>
        /// Open the chest and give rewards.
        /// </summary>
        public void OpenChest()
        {
            if (isOpened || isDisintegrating)
                return;

            isOpened = true;
            isDisintegrating = true;

            if (debugMode)
            {
                Debug.Log($"[BuriedTreasureChest] Opening: {chestId}");
            }

            // Play open sound
            if (openSound != null)
            {
                audioSource.PlayOneShot(openSound, soundVolume);
            }

            // Give rewards
            GiveRewards();

            // Notify manager
            if (TreasureChestManager.Instance != null)
            {
                TreasureChestManager.Instance.NotifyChestOpened(this, rewards);
            }

            // Start disintegration
            StartCoroutine(DisintegrationSequence());
        }

        /// <summary>
        /// Give all rewards to the player.
        /// </summary>
        private void GiveRewards()
        {
            if (rewards == null || rewards.Length == 0)
            {
                if (debugMode)
                {
                    Debug.LogWarning($"[BuriedTreasureChest] No rewards configured for: {chestId}");
                }
                return;
            }

            foreach (var reward in rewards)
            {
                if (reward == null)
                    continue;

                GiveReward(reward);
            }
        }

        /// <summary>
        /// Give a single reward to the player.
        /// </summary>
        private void GiveReward(TreasureChestReward reward)
        {
            switch (reward.rewardType)
            {
                case RewardType.Item:
                case RewardType.Tool:
                    GiveItemReward(reward);
                    break;

                case RewardType.Currency:
                    GiveCurrencyReward(reward);
                    break;

                case RewardType.EnergyDrink:
                    GiveEnergyDrinkReward(reward);
                    break;

                case RewardType.Note:
                    GiveNoteReward(reward);
                    break;

                case RewardType.Achievement:
                    GiveAchievementReward(reward);
                    break;

                case RewardType.Bundle:
                    // Bundle type would have its own nested rewards
                    break;
            }

            // Give energy drink bonus if enabled (works with any reward type)
            if (reward.includeEnergyDrink && reward.energyDrinkCount > 0)
            {
                GiveEnergyDrinkReward(reward);
            }

            // Play reward-specific sound
            if (reward.rewardSound != null)
            {
                audioSource.PlayOneShot(reward.rewardSound, reward.rewardVolume);
            }

            // Show reward popup
            ShowRewardPopup(reward);
        }

        private void GiveItemReward(TreasureChestReward reward)
        {
            if (reward.itemReward == null)
            {
                Debug.LogWarning($"[BuriedTreasureChest] Item reward has no item set: {reward.rewardId}");
                return;
            }

            if (InventorySystem.Instance != null)
            {
                int remaining;
                bool added = InventorySystem.Instance.TryAddItem(reward.itemReward, reward.itemQuantity, out remaining);

                if (remaining > 0)
                {
                    // Inventory full - spawn remaining as world drop
                    SpawnWorldDrop(reward.itemReward, remaining, transform.position + Vector3.up * 0.5f);
                }

                if (debugMode)
                {
                    Debug.Log($"[BuriedTreasureChest] Gave item: {reward.itemQuantity}x {reward.itemReward.itemName}");
                }
            }
        }

        private void GiveCurrencyReward(TreasureChestReward reward)
        {
            if (CurrencyManager.Instance != null && reward.currencyAmount > 0)
            {
                CurrencyManager.Instance.Add(reward.currencyAmount);

                if (debugMode)
                {
                    Debug.Log($"[BuriedTreasureChest] Gave currency: ${reward.currencyAmount}");
                }
            }
        }

        private void GiveEnergyDrinkReward(TreasureChestReward reward)
        {
            if (Energy.EnergyManager.Instance != null && reward.energyDrinkCount > 0)
            {
                Energy.EnergyManager.Instance.AddDrinks(reward.energyDrinkCount);

                if (debugMode)
                {
                    Debug.Log($"[BuriedTreasureChest] Gave energy drinks: {reward.energyDrinkCount}");
                }
            }
        }

        private void GiveNoteReward(TreasureChestReward reward)
        {
            // Notes could be shown in a popup or added to a journal
            // For now, just show in reward popup
            if (debugMode)
            {
                Debug.Log($"[BuriedTreasureChest] Gave note: {reward.noteTitle}");
            }
        }

        private void GiveAchievementReward(TreasureChestReward reward)
        {
            // Achievement system integration would go here
            if (debugMode)
            {
                Debug.Log($"[BuriedTreasureChest] Unlocked achievement: {reward.achievementId}");
            }
        }

        private void SpawnWorldDrop(BeneathTheFloor.Crafting.ItemSO item, int amount, Vector3 position)
        {
            // Use existing world drop system if available
            if (World.WorldDropManager.Instance != null)
            {
                World.WorldDropManager.Instance.SpawnItemAt(item, amount, position);
            }
        }

        private void ShowRewardPopup(TreasureChestReward reward)
        {
            // Use TreasureChestRewardUI if available
            if (TreasureChestRewardUI.Instance != null)
            {
                TreasureChestRewardUI.Instance.ShowReward(reward);
            }
        }

        #endregion

        #region Visual Effects

        /// <summary>
        /// Apply glow effect to chest meshes.
        /// </summary>
        private void ApplyGlowEffect()
        {
            foreach (var mr in meshRenderers)
            {
                if (mr == null)
                    continue;

                // Create glowing materials
                var materials = mr.materials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null)
                    {
                        materials[i].EnableKeyword("_EMISSION");
                        materials[i].SetColor("_EmissionColor", glowColor * glowIntensity);
                    }
                }
                mr.materials = materials;
            }
        }

        /// <summary>
        /// Disintegration animation sequence.
        /// </summary>
        private IEnumerator DisintegrationSequence()
        {
            // Schedule guaranteed destruction - this ensures chest disappears even if coroutine fails
            Destroy(gameObject, disintegrationDuration + 0.5f);

            // Create disintegration particles if not assigned
            try
            {
                if (disintegrationEffect == null)
                {
                    CreateDisintegrationEffect();
                }

                // Play particles
                if (disintegrationEffect != null)
                {
                    disintegrationEffect.Play();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BuriedTreasureChest] Failed to create particle effect: {e.Message}");
            }

            // Fade out meshes
            float elapsed = 0f;
            while (elapsed < disintegrationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / disintegrationDuration;

                // Scale down
                transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);

                // Fade materials (if possible)
                try
                {
                    foreach (var mr in meshRenderers)
                    {
                        if (mr == null)
                            continue;

                        foreach (var mat in mr.materials)
                        {
                            if (mat != null && mat.HasProperty("_Color"))
                            {
                                Color c = mat.color;
                                c.a = 1f - t;
                                mat.color = c;
                            }
                        }
                    }
                }
                catch { }

                yield return null;
            }

            // Destroy immediately if we reached the end normally
            if (this != null && gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Create a simple disintegration particle effect.
        /// </summary>
        private void CreateDisintegrationEffect()
        {
            GameObject particleObj = new GameObject("DisintegrationParticles");
            particleObj.transform.SetParent(transform);
            particleObj.transform.localPosition = Vector3.zero;

            disintegrationEffect = particleObj.AddComponent<ParticleSystem>();

            // Stop the system before configuring (Unity auto-plays on AddComponent)
            disintegrationEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = disintegrationEffect.main;
            main.playOnAwake = false;
            main.duration = disintegrationDuration;
            main.startLifetime = 1f;
            main.startSpeed = 2f;
            main.startSize = 0.1f;
            main.startColor = disintegrationColor;
            main.maxParticles = 100;
            main.loop = false;

            var emission = disintegrationEffect.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 50)
            });

            var shape = disintegrationEffect.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = Vector3.one * 0.5f;

            // Velocity over lifetime - all axes must be same mode
            var velocity = disintegrationEffect.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-1f, 1f);
            velocity.y = new ParticleSystem.MinMaxCurve(1f, 3f);
            velocity.z = new ParticleSystem.MinMaxCurve(-1f, 1f);

            // Size over lifetime - shrink particles
            var sizeOverLifetime = disintegrationEffect.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, 0f);

            // Add renderer
            var renderer = disintegrationEffect.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            if (shader != null)
            {
                renderer.material = new Material(shader);
                renderer.material.color = disintegrationColor;
            }
        }

        #endregion

        #region Save/Load & Reset

        /// <summary>
        /// Called by manager when loading save data.
        /// </summary>
        public void SetOpenedFromSave()
        {
            isOpened = true;
            isRevealed = true;

            // Hide the chest
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Reset chest state (for new game).
        /// </summary>
        public void ResetChest()
        {
            isOpened = false;
            isRevealed = false;
            isDisintegrating = false;

            // Reset scale
            transform.localScale = Vector3.one;

            // Reset materials
            foreach (var kvp in originalMaterials)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.materials = kvp.Value;
                }
            }

            // Disable physics
            if (chestRigidbody != null)
            {
                chestRigidbody.isKinematic = true;
            }

            gameObject.SetActive(true);
        }

        /// <summary>
        /// Force reveal the chest (debug/editor).
        /// </summary>
        public void ForceReveal()
        {
            OnRevealed();
        }

        /// <summary>
        /// Force open the chest (debug/editor).
        /// </summary>
        [ContextMenu("Debug: Force Open")]
        public void ForceOpen()
        {
            if (!isRevealed)
                OnRevealed();

            OpenChest();
        }

        #endregion

        #region Editor Gizmos

        private void OnDrawGizmos()
        {
            // Draw chest position
            Gizmos.color = isOpened ? Color.gray : (isRevealed ? Color.green : Color.yellow);
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);

            // Draw chest state
            Gizmos.color = isOpened ? Color.gray : (isRevealed ? Color.green : Color.yellow);
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.4f);

            // Draw exposure check radius
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, exposureCheckRadius);
        }

        private void OnDrawGizmosSelected()
        {
            // Draw exposure check sphere
            Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, exposureCheckRadius);

            // Draw sample points
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(transform.position, 0.1f); // Center

            Vector3[] offsets = new Vector3[]
            {
                Vector3.forward * exposureCheckRadius,
                Vector3.back * exposureCheckRadius,
                Vector3.left * exposureCheckRadius,
                Vector3.right * exposureCheckRadius,
                Vector3.up * exposureCheckRadius,
            };

            foreach (var offset in offsets)
            {
                Gizmos.DrawSphere(transform.position + offset, 0.08f);
                Gizmos.DrawLine(transform.position, transform.position + offset);
            }

            // Draw chest bounds
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);

            // Label info
            #if UNITY_EDITOR
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.8f,
                $"Radius: {exposureCheckRadius}m\nThreshold: {exposureThreshold}");
            #endif
        }

        #endregion
    }
}
