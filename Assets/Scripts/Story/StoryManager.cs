using UnityEngine;
using System.Collections.Generic;

namespace BeneathTheFloor.Story
{
    public class StoryManager : MonoBehaviour
    {
        [Header("Story Items Database")]
        [SerializeField] private List<StoryItem> allStoryItems = new List<StoryItem>();

        [Header("Story Progress")]
        [SerializeField] private int currentChapter = 1;
        [SerializeField] private List<string> discoveredItemIds = new List<string>();

        public static StoryManager Instance { get; private set; }

        public int CurrentChapter => currentChapter;
        public IReadOnlyList<string> DiscoveredItems => discoveredItemIds;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;

                // DontDestroyOnLoad only works on root GameObjects
                if (transform.parent != null)
                {
                    transform.SetParent(null);
                }
                DontDestroyOnLoad(gameObject);

                InitializeDefaultStoryItems();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Subscribe to events
            GameEvents.OnStoryItemFound += OnStoryItemFound;
            GameEvents.OnDepthReached += OnDepthReached;
        }

        private void OnDestroy()
        {
            GameEvents.OnStoryItemFound -= OnStoryItemFound;
            GameEvents.OnDepthReached -= OnDepthReached;
        }

        private void InitializeDefaultStoryItems()
        {
            if (allStoryItems.Count == 0)
            {
                // Chapter 1 items (0-10m)
                allStoryItems.Add(new StoryItem
                {
                    itemId = "journal_page_1",
                    title = "Torn Journal Page",
                    description = "...I've finally found it. After years of research, the readings are unmistakable. Something lies beneath this house. Something old. Tomorrow, I begin digging...",
                    depthFound = 2
                });

                allStoryItems.Add(new StoryItem
                {
                    itemId = "old_photograph",
                    title = "Faded Photograph",
                    description = "A photograph showing a man standing proudly next to strange machinery. On the back is written: 'The beginning of everything - 1952'",
                    depthFound = 5
                });

                allStoryItems.Add(new StoryItem
                {
                    itemId = "journal_page_2",
                    title = "Journal Entry #12",
                    description = "The soil here is different. My instruments detect energy patterns I've never seen before. The deeper I go, the stronger they become. My colleagues think I'm mad, but I know what I've found...",
                    depthFound = 8
                });

                // Chapter 2 items (10-25m)
                allStoryItems.Add(new StoryItem
                {
                    itemId = "strange_device",
                    title = "Mysterious Device",
                    description = "A small metallic device of unknown origin. It hums faintly when held, and strange symbols glow along its surface. It's clearly not of human make.",
                    depthFound = 12
                });

                allStoryItems.Add(new StoryItem
                {
                    itemId = "journal_page_3",
                    title = "Journal Entry #47",
                    description = "I've made contact. They speak through the machines now. They've been waiting for so long... waiting for someone to dig deep enough. They call themselves the Keepers.",
                    depthFound = 18
                });

                allStoryItems.Add(new StoryItem
                {
                    itemId = "ancient_tablet",
                    title = "Stone Tablet",
                    description = "An ancient stone tablet covered in symbols that seem to shift when you're not looking directly at them. One phrase is somehow understandable: 'THE DOOR MUST NOT OPEN'",
                    depthFound = 23
                });

                // Chapter 3 items (25-40m)
                allStoryItems.Add(new StoryItem
                {
                    itemId = "keepers_message",
                    title = "The Keepers' Warning",
                    description = "A crystalline structure that projects a holographic message: 'We were the guardians. We sealed what lies below. You have been chosen to decide: wake it, or let it sleep forever.'",
                    depthFound = 30
                });

                allStoryItems.Add(new StoryItem
                {
                    itemId = "journal_page_4",
                    title = "Final Journal Entry",
                    description = "I understand now what I must do. The world isn't ready for what's down there. I've sealed the deepest passages and destroyed my notes. If you're reading this... please, stop digging. Some things should stay buried.",
                    depthFound = 35
                });

                // Final items (40-50m)
                allStoryItems.Add(new StoryItem
                {
                    itemId = "final_artifact",
                    title = "The Key",
                    description = "A pulsing artifact that seems to be alive. It resonates with something far below. This is what the inventor found. This is what he tried to hide. The choice is now yours.",
                    depthFound = 45
                });
            }
        }

        private void OnStoryItemFound(StoryItem item)
        {
            if (!discoveredItemIds.Contains(item.itemId))
            {
                discoveredItemIds.Add(item.itemId);
                CheckChapterProgress();

                // Could trigger special events or dialogue
                Debug.Log($"Story Progress: Discovered '{item.title}'");
            }
        }

        private void OnDepthReached(int depth)
        {
            // Check if any story items should be spawned at this depth
            foreach (var item in allStoryItems)
            {
                if (item.depthFound == depth && !discoveredItemIds.Contains(item.itemId))
                {
                    // Could spawn the item in the world
                    Debug.Log($"Story item available at depth {depth}: {item.title}");
                }
            }
        }

        private void CheckChapterProgress()
        {
            int itemsFound = discoveredItemIds.Count;

            if (itemsFound >= 9)
            {
                currentChapter = 4; // Finale
            }
            else if (itemsFound >= 6)
            {
                currentChapter = 3;
            }
            else if (itemsFound >= 3)
            {
                currentChapter = 2;
            }
            else
            {
                currentChapter = 1;
            }
        }

        public StoryItem GetStoryItem(string itemId)
        {
            return allStoryItems.Find(x => x.itemId == itemId);
        }

        public List<StoryItem> GetDiscoveredStoryItems()
        {
            List<StoryItem> discovered = new List<StoryItem>();
            foreach (string id in discoveredItemIds)
            {
                StoryItem item = GetStoryItem(id);
                if (item != null)
                {
                    discovered.Add(item);
                }
            }
            return discovered;
        }

        public List<StoryItem> GetItemsForDepthRange(int minDepth, int maxDepth)
        {
            return allStoryItems.FindAll(x => x.depthFound >= minDepth && x.depthFound <= maxDepth);
        }

        public float GetStoryProgress()
        {
            return (float)discoveredItemIds.Count / allStoryItems.Count;
        }

        public void ResetProgress()
        {
            discoveredItemIds.Clear();
            currentChapter = 1;
        }

        /// <summary>
        /// Set story progress from save data.
        /// </summary>
        public void SetProgress(int chapter, List<string> discoveredIds)
        {
            currentChapter = chapter;
            discoveredItemIds.Clear();
            if (discoveredIds != null)
            {
                discoveredItemIds.AddRange(discoveredIds);
            }
            Debug.Log($"[StoryManager] Loaded progress: Chapter {currentChapter}, {discoveredItemIds.Count} items discovered");
        }

        /// <summary>
        /// Get all discovered item IDs for saving.
        /// </summary>
        public List<string> GetDiscoveredItemIds()
        {
            return new List<string>(discoveredItemIds);
        }
    }
}
