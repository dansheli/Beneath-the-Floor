using UnityEngine;

namespace BeneathTheFloor.Player
{
    /// <summary>
    /// Attach this to any GameObject to automatically set up a complete player prefab
    /// </summary>
    public class PlayerSetup : MonoBehaviour
    {
        [Header("Auto Setup")]
        [SerializeField] private bool setupOnStart = true;

        private void Start()
        {
            if (setupOnStart)
            {
                SetupPlayer();
            }
        }

        [ContextMenu("Setup Player")]
        public void SetupPlayer()
        {
            // Ensure we have required components
            EnsureCharacterController();
            EnsureFirstPersonController();
            EnsureInteractionSystem();
            EnsureCamera();
            EnsureGroundCheck();
            EnsureHeldToolController();
            EnsureMagnetPullAbility();

            Debug.Log("Player setup complete!");
        }

        private void EnsureCharacterController()
        {
            CharacterController cc = GetComponent<CharacterController>();
            if (cc == null)
            {
                cc = gameObject.AddComponent<CharacterController>();
            }
            cc.height = 2f;
            cc.radius = 0.5f;
            cc.center = new Vector3(0, 1f, 0);
        }

        private void EnsureFirstPersonController()
        {
            FirstPersonController fpc = GetComponent<FirstPersonController>();
            if (fpc == null)
            {
                gameObject.AddComponent<FirstPersonController>();
            }
        }

        private void EnsureInteractionSystem()
        {
            Interaction.InteractionSystem interaction = GetComponent<Interaction.InteractionSystem>();
            if (interaction == null)
            {
                gameObject.AddComponent<Interaction.InteractionSystem>();
            }
        }

        private void EnsureCamera()
        {
            Transform cameraHolder = transform.Find("CameraHolder");
            if (cameraHolder == null)
            {
                GameObject holder = new GameObject("CameraHolder");
                holder.transform.parent = transform;
                holder.transform.localPosition = new Vector3(0, 1.0f, 0);
                cameraHolder = holder.transform;
            }

            Transform mainCamera = cameraHolder.Find("Main Camera");
            if (mainCamera == null)
            {
                GameObject cam = new GameObject("Main Camera");
                cam.tag = "MainCamera";
                cam.transform.parent = cameraHolder;
                cam.transform.localPosition = Vector3.zero;
                cam.transform.localRotation = Quaternion.identity;

                Camera camera = cam.AddComponent<Camera>();
                camera.fieldOfView = 70;
                camera.nearClipPlane = 0.01f;

                cam.AddComponent<AudioListener>();
            }

            // Ensure camera has wall avoidance to prevent clipping through terrain
            Camera mainCam = cameraHolder.GetComponentInChildren<Camera>();
            if (mainCam != null)
            {
                var existing = mainCam.GetComponent("CameraWallAvoidance");
                if (existing == null)
                {
                    // Try to add CameraWallAvoidance by type name (avoids hard reference)
                    var type = System.Type.GetType("BeneathTheFloor.Player.CameraWallAvoidance, Assembly-CSharp");
                    if (type == null)
                        type = System.Type.GetType("BeneathTheFloor.Player.CameraWallAvoidance, BeneathTheFloor");
                    if (type != null)
                        mainCam.gameObject.AddComponent(type);
                }
            }
        }

        private void EnsureGroundCheck()
        {
            Transform groundCheck = transform.Find("GroundCheck");
            if (groundCheck == null)
            {
                GameObject gc = new GameObject("GroundCheck");
                gc.transform.parent = transform;
                gc.transform.localPosition = Vector3.zero;
            }
        }

        private void EnsureHeldToolController()
        {
            Tools.HeldToolController htc = GetComponent<Tools.HeldToolController>();
            if (htc == null)
            {
                htc = gameObject.AddComponent<Tools.HeldToolController>();
                Debug.Log("[PlayerSetup] Added HeldToolController to Player");
            }
        }

        private void EnsureMagnetPullAbility()
        {
            MagnetPullAbility magnetPull = GetComponent<MagnetPullAbility>();
            if (magnetPull == null)
            {
                magnetPull = gameObject.AddComponent<MagnetPullAbility>();
                Debug.Log("[PlayerSetup] Added MagnetPullAbility to Player");
            }
        }
    }
}
