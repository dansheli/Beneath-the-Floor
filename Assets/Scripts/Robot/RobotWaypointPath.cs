using UnityEngine;

namespace BeneathTheFloor.Robot
{
    /// <summary>
    /// Designer-placed waypoint path for the digger robot.
    /// Child transforms define waypoints ordered by sibling index.
    /// Place this on a parent GameObject and add empty child objects as waypoints.
    /// </summary>
    public class RobotWaypointPath : MonoBehaviour
    {
        public int WaypointCount => transform.childCount;

        public Vector3 GetWaypoint(int index)
        {
            if (index < 0 || index >= transform.childCount)
            {
                Debug.LogWarning($"[RobotWaypointPath] Index {index} out of range (count={transform.childCount})");
                return transform.position;
            }
            return transform.GetChild(index).position;
        }

        private void OnDrawGizmos()
        {
            int count = transform.childCount;
            if (count == 0) return;

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = transform.GetChild(i).position;

                // Last waypoint = yellow, others = green
                bool isLast = i == count - 1;
                Gizmos.color = isLast ? Color.yellow : Color.green;
                Gizmos.DrawSphere(pos, 0.25f);

#if UNITY_EDITOR
                UnityEditor.Handles.Label(pos + Vector3.up * 0.4f, $"WP {i}");
#endif

                // Line to next waypoint
                if (i < count - 1)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(pos, transform.GetChild(i + 1).position);
                }
            }
        }
    }
}
