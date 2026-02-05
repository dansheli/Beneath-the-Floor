using UnityEngine;
using System.Collections;

namespace BeneathTheFloor.Robot
{
    public class DiggerRobotVisual : MonoBehaviour
    {
        [SerializeField] private Transform drillPart;
        [SerializeField] private ParticleSystem dustParticles;
        [SerializeField] private Renderer ledRenderer;

        [Header("Procedural Animation Parts")]
        [SerializeField] private Transform ballRotation;
        [SerializeField] private Transform upperArmL;
        [SerializeField] private Transform upperArmR;

        [Header("Settings")]
        [SerializeField] private float drillSpeed = 720f;

        private DiggerRobotStateMachine stateMachine;
        private DiggerRobotConfig config;
        private MaterialPropertyBlock ledBlock;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Rigidbody rb;
        private Coroutine digCycleCoroutine;

        /// <summary>True while the dig animation coroutine is running.</summary>
        public bool IsDigCycleActive { get; private set; }

        // Cached idle arm rotations
        private Quaternion armLIdleRot;
        private Quaternion armRIdleRot;
        private bool idleRotsCached;

        private void Awake()
        {
            stateMachine = GetComponent<DiggerRobotStateMachine>();
            rb = GetComponent<Rigidbody>();
            ledBlock = new MaterialPropertyBlock();

            // Auto-find parts by name if not assigned
            if (ballRotation == null) ballRotation = FindChildByName("ball_rotation");
            if (upperArmL == null) upperArmL = FindChildByName("upperarm_l");
            if (upperArmR == null) upperArmR = FindChildByName("upperarm_r");
        }

        private void Start()
        {
            // Try to grab config from state machine via serialized field
            var smSO = stateMachine;
            config = smSO != null ? smSO.Config : null;

            // Cache idle arm rotations
            if (upperArmL != null && !idleRotsCached)
            {
                armLIdleRot = upperArmL.localRotation;
                armRIdleRot = upperArmR != null ? upperArmR.localRotation : Quaternion.identity;
                idleRotsCached = true;
            }
        }

        private Transform FindChildByName(string exactName)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == exactName)
                    return child;
            }
            return null;
        }

        private void Update()
        {
            if (stateMachine == null) return;

            var state = stateMachine.CurrentState;

            // Drill rotation
            if (drillPart != null)
            {
                bool spinning = state == DiggerRobotStateMachine.State.Digging;
                if (spinning)
                    drillPart.Rotate(Vector3.forward, drillSpeed * Time.deltaTime, Space.Self);
            }

            // Dust particles
            if (dustParticles != null)
            {
                if (state == DiggerRobotStateMachine.State.Digging && !dustParticles.isPlaying)
                    dustParticles.Play();
                else if (state != DiggerRobotStateMachine.State.Digging && dustParticles.isPlaying)
                    dustParticles.Stop();
            }

            // LED color
            if (ledRenderer != null)
            {
                Color ledColor;
                switch (state)
                {
                    case DiggerRobotStateMachine.State.Digging: ledColor = Color.green; break;
                    case DiggerRobotStateMachine.State.Returning: ledColor = Color.yellow; break;
                    case DiggerRobotStateMachine.State.Shutdown: ledColor = Color.red; break;
                    case DiggerRobotStateMachine.State.Recharging: ledColor = Color.cyan; break;
                    case DiggerRobotStateMachine.State.Carried: ledColor = new Color(1f, 0.5f, 0f); break;
                    default: ledColor = Color.white; break;
                }
                ledBlock.SetColor(EmissionColorId, ledColor * 2f);
                ledRenderer.SetPropertyBlock(ledBlock);
            }

            // Ball rotation — proportional to velocity
            if (ballRotation != null && rb != null)
            {
                float rollFactor = config != null ? config.ballRollFactor : 180f;
                float speed = new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude;
                float degPerFrame = speed * rollFactor * Time.deltaTime;
                ballRotation.Rotate(Vector3.right, degPerFrame, Space.Self);
            }

            // Arms idle relaxation (when not in dig cycle)
            if (!IsDigCycleActive && idleRotsCached)
            {
                float openAngle = config != null ? config.armOpenAngle : 15f;
                if (upperArmL != null)
                {
                    Quaternion target = armLIdleRot * Quaternion.Euler(0f, openAngle, 0f);
                    upperArmL.localRotation = Quaternion.Slerp(upperArmL.localRotation, target, 5f * Time.deltaTime);
                }
                if (upperArmR != null)
                {
                    Quaternion target = armRIdleRot * Quaternion.Euler(0f, -openAngle, 0f);
                    upperArmR.localRotation = Quaternion.Slerp(upperArmR.localRotation, target, 5f * Time.deltaTime);
                }
            }
        }

        /// <summary>
        /// Start the dig animation cycle: body tilt + arm bites + dust.
        /// </summary>
        public Coroutine PlayDigCycle()
        {
            if (digCycleCoroutine != null)
                StopCoroutine(digCycleCoroutine);
            digCycleCoroutine = StartCoroutine(DigCycleRoutine());
            return digCycleCoroutine;
        }

        private IEnumerator DigCycleRoutine()
        {
            IsDigCycleActive = true;

            float duration = config != null ? config.digCycleDuration : 1.2f;
            float tiltAngle = config != null ? config.bodyTiltAngle : 10f;
            float openAngle = config != null ? config.armOpenAngle : 15f;
            float closeAngle = config != null ? config.armCloseAngle : -20f;
            float biteDuration = duration / 3f;

            // Tilt body forward
            Quaternion bodyStart = transform.localRotation;
            Quaternion bodyTilted = bodyStart * Quaternion.Euler(tiltAngle, 0f, 0f);
            yield return LerpRotation(transform, bodyStart, bodyTilted, biteDuration * 0.5f, Space.Self);

            // 3 bite cycles
            for (int bite = 0; bite < 3; bite++)
            {
                // Close arms
                yield return LerpArms(openAngle, closeAngle, biteDuration * 0.4f);
                // Open arms
                yield return LerpArms(closeAngle, openAngle, biteDuration * 0.6f);
            }

            // Return body tilt
            yield return LerpRotation(transform, transform.localRotation, bodyStart, biteDuration * 0.5f, Space.Self);

            IsDigCycleActive = false;
            digCycleCoroutine = null;
        }

        private IEnumerator LerpArms(float fromAngle, float toAngle, float duration)
        {
            if (!idleRotsCached) yield break;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(duration, 0.01f);
                float angle = Mathf.Lerp(fromAngle, toAngle, t);
                if (upperArmL != null)
                    upperArmL.localRotation = armLIdleRot * Quaternion.Euler(0f, angle, 0f);
                if (upperArmR != null)
                    upperArmR.localRotation = armRIdleRot * Quaternion.Euler(0f, -angle, 0f);
                yield return null;
            }
        }

        private IEnumerator LerpRotation(Transform target, Quaternion from, Quaternion to, float duration, Space space)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(duration, 0.01f);
                if (space == Space.Self)
                    target.localRotation = Quaternion.Slerp(from, to, t);
                else
                    target.rotation = Quaternion.Slerp(from, to, t);
                yield return null;
            }
        }
    }
}
