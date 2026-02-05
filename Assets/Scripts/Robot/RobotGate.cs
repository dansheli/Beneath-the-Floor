using UnityEngine;

namespace BeneathTheFloor.Robot
{
    /// <summary>
    /// One-way gate at the dig area entrance.
    /// Starts as a trigger — robot passes through freely.
    /// Once the robot enters, the collider becomes solid so the robot can't leave.
    /// Only interacts with the Robot layer (11) — player never sees it.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class RobotGate : MonoBehaviour
    {
        private BoxCollider boxCollider;
        private enum GateState { Trigger, Solid, Off }
        private GateState state = GateState.Trigger;

        private const int RobotLayer = 11;

        private void Awake()
        {
            boxCollider = GetComponent<BoxCollider>();
            ResetGate();
        }

        /// <summary>Reset gate to trigger mode (robot can pass through). Called on deploy.</summary>
        public void ResetGate()
        {
            boxCollider.enabled = true;
            boxCollider.isTrigger = true;
            state = GateState.Trigger;
        }

        /// <summary>Disable gate entirely (robot picked up). Called on pick up.</summary>
        public void DeactivateGate()
        {
            boxCollider.enabled = false;
            state = GateState.Off;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer == RobotLayer)
            {
                // Robot passed through — solidify the gate behind it
                boxCollider.isTrigger = false;
                state = GateState.Solid;
                Debug.Log("[RobotGate] Robot passed through — gate is now solid");
            }
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;

            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            switch (state)
            {
                case GateState.Trigger:
                    Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
                    break;
                case GateState.Solid:
                    Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
                    break;
                case GateState.Off:
                    Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.2f);
                    break;
            }

            Gizmos.DrawCube(box.center, box.size);
            Gizmos.DrawWireCube(box.center, box.size);

            Gizmos.matrix = old;
        }
    }
}
