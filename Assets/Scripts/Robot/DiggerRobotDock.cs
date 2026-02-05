using UnityEngine;
using BeneathTheFloor.Interaction;
using BeneathTheFloor.Machines;

namespace BeneathTheFloor.Robot
{
    public class DiggerRobotDock : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform dockPoint;
        [SerializeField] private DiggerRobotStateMachine robot;

        public bool CanInteract => true;

        public string GetInteractionText()
        {
            if (!FirstRoomUpgradeStationUI.RobotActivated)
                return "ROBOT NOT ACTIVATED";

            if (DiggerRobotCarry.IsCarryingRobot)
                return "[E] Place Robot on Dock";

            if (robot != null && robot.CurrentState == DiggerRobotStateMachine.State.Docked)
                return "[E] Deploy Robot";

            if (robot != null && robot.CurrentState == DiggerRobotStateMachine.State.Recharging)
                return $"Recharging... {Mathf.RoundToInt(robot.BatteryRatio * 100)}%";

            return "";
        }

        public void Interact(GameObject interactor)
        {
            if (!FirstRoomUpgradeStationUI.RobotActivated) return;

            // Player placing carried robot onto dock
            if (DiggerRobotCarry.IsCarryingRobot)
            {
                var carry = interactor.GetComponentInChildren<DiggerRobotCarry>();
                if (carry != null)
                {
                    carry.DropRobot(dockPoint != null ? dockPoint : transform);
                }
                return;
            }

            // Deploy docked robot
            if (robot != null && robot.CurrentState == DiggerRobotStateMachine.State.Docked)
            {
                robot.Deploy();
            }
        }

        public void OnHoverEnter() { }
        public void OnHoverExit() { }

        public Transform GetDockPoint() => dockPoint != null ? dockPoint : transform;
    }
}
