using UnityEngine;

namespace BeneathTheFloor.Robot
{
    public class DiggerRobotCarry : MonoBehaviour
    {
        public static bool IsCarryingRobot { get; private set; }

        private Transform anchor;
        private DiggerRobotStateMachine carriedRobot;
        private Vector3 originalScale;
        private Light carryLight;
        private float pickupGrace;

        private void EnsureAnchor()
        {
            if (anchor != null) return;

            Camera cam = Camera.main;
            if (cam == null) cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                GameObject anchorObj = new GameObject("LeftHandAnchor");
                anchorObj.transform.SetParent(cam.transform, false);
                anchorObj.transform.localPosition = new Vector3(-0.3f, -0.25f, 0.4f);
                anchor = anchorObj.transform;
            }
        }

        public void PickUpRobot(DiggerRobotStateMachine robot)
        {
            EnsureAnchor();

            // Reset stale carry state (e.g. carried robot was destroyed or dropped without clearing flag)
            if (IsCarryingRobot && carriedRobot == null)
            {
                IsCarryingRobot = false;
                Debug.Log("[DiggerRobotCarry] Cleared stale carry state");
            }

            if (IsCarryingRobot || anchor == null)
            {
                Debug.LogWarning($"[DiggerRobotCarry] Cannot pick up — anchor={anchor} carrying={IsCarryingRobot}");
                return;
            }

            carriedRobot = robot;
            originalScale = robot.transform.localScale;

            // Disable physics FIRST
            var rb = robot.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Disable ALL colliders
            foreach (var col in robot.GetComponentsInChildren<Collider>(true))
                col.enabled = false;

            // Parent to hand anchor — use false so local transform is clean
            robot.transform.SetParent(anchor, false);
            robot.transform.localPosition = Vector3.zero;
            robot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            robot.transform.localScale = originalScale * 0.3f;

            // Carry light
            if (carryLight == null)
            {
                GameObject lightObj = new GameObject("RobotCarryLight");
                lightObj.transform.SetParent(anchor, false);
                lightObj.transform.localPosition = Vector3.zero;
                carryLight = lightObj.AddComponent<Light>();
                carryLight.type = LightType.Point;
                carryLight.range = 5f;
                carryLight.intensity = 0.6f;
                carryLight.color = new Color(0.6f, 0.75f, 1f);
                carryLight.shadows = LightShadows.None;
            }

            IsCarryingRobot = true;
            pickupGrace = 0.3f; // prevent same-frame E press from dropping
            Debug.Log($"[DiggerRobotCarry] Picked up robot, anchor={anchor.position}, cam={Camera.main?.name}");
        }

        public void DropRobot(Transform dockPoint)
        {
            if (!IsCarryingRobot || carriedRobot == null) return;

            // Unparent but keep world position (player's hand)
            carriedRobot.transform.SetParent(null, true);
            carriedRobot.transform.localScale = originalScale;

            // Re-enable ALL colliders
            foreach (var col in carriedRobot.GetComponentsInChildren<Collider>(true))
                col.enabled = true;

            DestroyCarryLight();

            // Dock will animate the robot smoothly from current pos to dock point
            carriedRobot.Dock();
            carriedRobot = null;
            IsCarryingRobot = false;
        }

        public void DropRobotHere()
        {
            if (!IsCarryingRobot || carriedRobot == null) return;

            Vector3 dropPos = transform.position + transform.forward * 1.5f;
            if (Physics.Raycast(dropPos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f))
                dropPos = hit.point + Vector3.up * 0.2f;

            carriedRobot.transform.SetParent(null, true);
            carriedRobot.transform.localScale = originalScale;
            carriedRobot.transform.position = dropPos;
            carriedRobot.transform.rotation = Quaternion.identity;

            var rb = carriedRobot.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.velocity = Vector3.zero;
            }

            foreach (var col in carriedRobot.GetComponentsInChildren<Collider>(true))
                col.enabled = true;

            DestroyCarryLight();
            carriedRobot = null;
            IsCarryingRobot = false;
        }

        private void DestroyCarryLight()
        {
            if (carryLight != null)
            {
                Destroy(carryLight.gameObject);
                carryLight = null;
            }
        }

        private void Update()
        {
            // Keep robot locked to anchor every frame (safety)
            if (IsCarryingRobot && carriedRobot != null && anchor != null)
            {
                if (carriedRobot.transform.parent != anchor)
                    carriedRobot.transform.SetParent(anchor, false);
                carriedRobot.transform.localPosition = Vector3.zero;
            }

            if (pickupGrace > 0f)
            {
                pickupGrace -= Time.deltaTime;
                return;
            }

            if (IsCarryingRobot && Input.GetKeyDown(KeyCode.E))
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, 3f))
                    {
                        var dock = hit.collider.GetComponent<DiggerRobotDock>();
                        if (dock == null)
                            dock = hit.collider.GetComponentInParent<DiggerRobotDock>();
                        if (dock != null)
                            return;
                    }
                }
                DropRobotHere();
            }
        }
    }
}
