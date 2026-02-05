using UnityEngine;
using BeneathTheFloor.Interaction;
using BeneathTheFloor.Machines;

namespace BeneathTheFloor.Logistics
{
    public class LogisticsRobotDock : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform dockPoint;
        [SerializeField] private LogisticsRobotController robot;

        public bool CanInteract
        {
            get
            {
                if (!FirstRoomUpgradeStationUI.LogisticsRobotActivated) return true;
                if (LogisticsRobotCarry.IsCarryingLogisticsRobot) return true;
                if (robot == null) return false;
                return robot.CurrentState == LogisticsState.Docked || robot.CurrentState == LogisticsState.Recharging;
            }
        }

        public string GetInteractionText()
        {
            if (!FirstRoomUpgradeStationUI.LogisticsRobotActivated)
                return "LOGISTICS ROBOT NOT ACTIVATED";

            if (LogisticsRobotCarry.IsCarryingLogisticsRobot)
                return "[E] Place Robot on Dock";

            if (robot != null && robot.CurrentState == LogisticsState.Docked)
                return "[E] Deploy Robot";

            if (robot != null && robot.CurrentState == LogisticsState.Recharging)
                return $"Recharging... {Mathf.RoundToInt(robot.BatteryRatio * 100)}%";

            return "";
        }

        public void Interact(GameObject interactor)
        {
            if (!FirstRoomUpgradeStationUI.LogisticsRobotActivated) return;

            if (LogisticsRobotCarry.IsCarryingLogisticsRobot)
            {
                var carry = interactor.GetComponent<LogisticsRobotCarry>();
                if (carry != null)
                {
                    carry.DropRobotAtDock(this);
                    return;
                }
            }

            if (robot != null && robot.CurrentState == LogisticsState.Docked)
            {
                robot.Deploy();
            }
        }

        public void OnHoverEnter() { }
        public void OnHoverExit() { }

        public Transform GetDockPoint() => dockPoint != null ? dockPoint : transform;
    }
}
