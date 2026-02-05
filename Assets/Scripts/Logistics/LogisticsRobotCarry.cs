using UnityEngine;

namespace BeneathTheFloor.Logistics
{
    public class LogisticsRobotCarry : MonoBehaviour
    {
        public static bool IsCarryingLogisticsRobot { get; private set; }

        private Transform anchor;
        private LogisticsRobotController carriedRobot;
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
                GameObject anchorObj = new GameObject("LogisticsCarryAnchor");
                anchorObj.transform.SetParent(cam.transform, false);
                anchorObj.transform.localPosition = new Vector3(-0.3f, -0.25f, 0.4f);
                anchor = anchorObj.transform;
            }
        }

        public void PickUpRobot(LogisticsRobotController robot)
        {
            EnsureAnchor();

            if (IsCarryingLogisticsRobot && carriedRobot == null)
                IsCarryingLogisticsRobot = false;

            if (IsCarryingLogisticsRobot || anchor == null)
            {
                Debug.LogWarning($"[LogisticsRobotCarry] Cannot pick up — anchor={anchor} carrying={IsCarryingLogisticsRobot}");
                return;
            }

            carriedRobot = robot;
            originalScale = robot.transform.localScale;

            // Disable physics
            var rb = robot.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Disable all colliders
            foreach (var col in robot.GetComponentsInChildren<Collider>(true))
                col.enabled = false;

            // Parent to hand anchor — worldPositionStays=false for clean local transform
            robot.transform.SetParent(anchor, false);
            robot.transform.localPosition = Vector3.zero;
            robot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            robot.transform.localScale = originalScale * 0.3f;

            robot.PickUp();

            // Carry light
            if (carryLight == null)
            {
                GameObject lightObj = new GameObject("LogisticsCarryLight");
                lightObj.transform.SetParent(anchor, false);
                lightObj.transform.localPosition = Vector3.zero;
                carryLight = lightObj.AddComponent<Light>();
                carryLight.type = LightType.Point;
                carryLight.range = 5f;
                carryLight.intensity = 0.6f;
                carryLight.color = new Color(0.5f, 0.9f, 0.6f);
                carryLight.shadows = LightShadows.None;
            }

            IsCarryingLogisticsRobot = true;
            pickupGrace = 0.3f;
            Debug.Log($"[LogisticsRobotCarry] Picked up logistics robot, anchor={anchor.position}, scale={robot.transform.localScale}, cam={Camera.main?.name}");
        }

        public void DropRobotAtDock(LogisticsRobotDock dock)
        {
            if (!IsCarryingLogisticsRobot || carriedRobot == null) return;

            // Unparent — keep world position from hand
            carriedRobot.transform.SetParent(null, true);
            carriedRobot.transform.localScale = originalScale;

            // Place at player's current position so the animation slides from player to dock
            Transform dp = dock.GetDockPoint();
            carriedRobot.transform.position = transform.position;
            carriedRobot.transform.rotation = dp.rotation;

            // Re-enable colliders
            foreach (var col in carriedRobot.GetComponentsInChildren<Collider>(true))
                col.enabled = true;

            DestroyCarryLight();

            // Dock() will AnimateTo the dock point from the current position
            carriedRobot.Dock();
            carriedRobot = null;
            IsCarryingLogisticsRobot = false;
        }

        public void DropRobotHere()
        {
            if (!IsCarryingLogisticsRobot || carriedRobot == null) return;

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
                rb.isKinematic = true;
                rb.velocity = Vector3.zero;
            }

            foreach (var col in carriedRobot.GetComponentsInChildren<Collider>(true))
                col.enabled = true;

            DestroyCarryLight();
            carriedRobot = null;
            IsCarryingLogisticsRobot = false;
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
            // Keep robot locked to anchor every frame
            if (IsCarryingLogisticsRobot && carriedRobot != null && anchor != null)
            {
                if (carriedRobot.transform.parent != anchor)
                    carriedRobot.transform.SetParent(anchor, false);
                carriedRobot.transform.localPosition = Vector3.zero;
                carriedRobot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                carriedRobot.transform.localScale = originalScale * 0.3f;
            }

            if (pickupGrace > 0f)
            {
                pickupGrace -= Time.deltaTime;
                return;
            }

            if (IsCarryingLogisticsRobot && Input.GetKeyDown(KeyCode.E))
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, 3f))
                    {
                        var dock = hit.collider.GetComponent<LogisticsRobotDock>();
                        if (dock == null)
                            dock = hit.collider.GetComponentInParent<LogisticsRobotDock>();
                        if (dock != null)
                            return; // Dock IInteractable will handle it
                    }
                }
                DropRobotHere();
            }
        }
    }
}
