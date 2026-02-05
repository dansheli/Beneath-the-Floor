using UnityEngine;
using System.Collections;

namespace BeneathTheFloor.Logistics
{
    public class LogisticsArmAnimator : MonoBehaviour
    {
        [SerializeField] private LogisticsRobotConfig config;

        private Transform leftArm;
        private Transform rightArm;
        private Quaternion leftIdleRot;
        private Quaternion rightIdleRot;
        private bool cached;

        public bool IsAnimating { get; private set; }

        public void Init(LogisticsRobotConfig cfg)
        {
            config = cfg;
            FindArms();
        }

        private void FindArms()
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                string n = child.name;
                if (n == "MB3_Collector_L-Arm" || n.Contains("L-Arm") || n.Contains("L_Arm"))
                {
                    if (leftArm == null) leftArm = child;
                }
                if (n == "MB3_Collector_R-Arm" || n.Contains("R-Arm") || n.Contains("R_Arm"))
                {
                    if (rightArm == null) rightArm = child;
                }
            }

            if (leftArm != null)
            {
                leftIdleRot = leftArm.localRotation;
                cached = true;
            }
            if (rightArm != null)
            {
                rightIdleRot = rightArm.localRotation;
                cached = true;
            }
        }

        public Coroutine PlayCollectAnimation()
        {
            if (!cached || IsAnimating) return null;
            return StartCoroutine(CollectRoutine());
        }

        private IEnumerator CollectRoutine()
        {
            IsAnimating = true;
            float closeAngle = config != null ? config.armCloseAngle : 20f;
            float duration = config != null ? config.armAnimDuration : 0.15f;

            // Close arms
            yield return LerpArms(0f, closeAngle, duration);
            yield return new WaitForSeconds(0.1f);
            // Open arms
            yield return LerpArms(closeAngle, 0f, duration);

            IsAnimating = false;
        }

        private IEnumerator LerpArms(float fromAngle, float toAngle, float duration)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(duration, 0.01f);
                float angle = Mathf.Lerp(fromAngle, toAngle, t);
                if (leftArm != null)
                    leftArm.localRotation = leftIdleRot * Quaternion.Euler(0f, angle, 0f);
                if (rightArm != null)
                    rightArm.localRotation = rightIdleRot * Quaternion.Euler(0f, -angle, 0f);
                yield return null;
            }
        }
    }
}
