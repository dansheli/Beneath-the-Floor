using UnityEngine;
using System.Collections.Generic;

namespace BeneathTheFloor.Robot
{
    public class DiggerRobotBreadcrumbs : MonoBehaviour
    {
        private readonly List<Vector3> trail = new List<Vector3>();
        private float spacing = 1.5f;

        public void Init(float breadcrumbSpacing)
        {
            spacing = breadcrumbSpacing;
        }

        public void DropCrumb(Vector3 position)
        {
            if (trail.Count == 0 || Vector3.Distance(trail[trail.Count - 1], position) >= spacing)
            {
                trail.Add(position);
            }
        }

        public Vector3? PopNextReturnPoint()
        {
            if (trail.Count == 0) return null;
            int last = trail.Count - 1;
            Vector3 point = trail[last];
            trail.RemoveAt(last);
            return point;
        }

        public bool HasCrumbs => trail.Count > 0;

        public void Clear()
        {
            trail.Clear();
        }
    }
}
