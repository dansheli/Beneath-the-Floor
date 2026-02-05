using UnityEngine;
using System.Collections.Generic;

namespace BeneathTheFloor.Digging
{
    /// <summary>
    /// A chunk of terrain that has separated from the main mass and is falling.
    /// Created by extracting triangles from the terrain mesh.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class FallingTerrainChunk : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private bool isFalling = true;
        [SerializeField] private bool isStatic = false;

        [Header("Physics Settings")]
        [SerializeField] private float settleDuration = 0.5f;
        [SerializeField] private float settleVelocityThreshold = 0.1f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // Components
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshCollider _meshCollider;
        private Rigidbody _rigidbody;

        // Mesh (owned by this object)
        private Mesh _mesh;

        // Falling state
        private float _settleTimer = 0f;
        private bool _hasLanded = false;

        // Layer for terrain
        private static int _terrainLayer = -1;

        #region Unity Lifecycle

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshCollider = GetComponent<MeshCollider>();

            // Cache terrain layer
            if (_terrainLayer < 0)
            {
                _terrainLayer = LayerMask.NameToLayer("Terrain");
                if (_terrainLayer < 0) _terrainLayer = 0;
            }
            gameObject.layer = _terrainLayer;
        }

        private void Update()
        {
            if (isFalling && _rigidbody != null)
            {
                CheckSettled();
            }
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize with an extracted mesh from the terrain.
        /// </summary>
        public void InitializeWithMesh(Mesh extractedMesh, Material material)
        {
            // Take ownership of the mesh
            _mesh = extractedMesh;
            _meshFilter.mesh = _mesh;
            _meshRenderer.material = material;

            // Calculate bounds
            _mesh.RecalculateBounds();

            // Set up physics - use convex for Rigidbody
            _meshCollider.convex = true;
            _meshCollider.sharedMesh = _mesh;

            // Add rigidbody for falling
            _rigidbody = gameObject.AddComponent<Rigidbody>();
            _rigidbody.mass = Mathf.Clamp(_mesh.vertexCount * 0.1f, 1f, 50f);
            _rigidbody.drag = 0.1f;
            _rigidbody.angularDrag = 0.5f;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            isFalling = true;
            isStatic = false;

            if (enableDebugLogs)
                Debug.Log($"[FallingTerrainChunk] Initialized with {_mesh.vertexCount} vertices, {_mesh.triangles.Length / 3} triangles");
        }

        #endregion

        #region Falling Physics

        private void CheckSettled()
        {
            if (_rigidbody == null || !isFalling) return;

            if (_rigidbody.velocity.magnitude < settleVelocityThreshold &&
                _rigidbody.angularVelocity.magnitude < settleVelocityThreshold)
            {
                _settleTimer += Time.deltaTime;

                if (_settleTimer >= settleDuration)
                {
                    OnLanded();
                }
            }
            else
            {
                _settleTimer = 0f;
            }
        }

        private void OnLanded()
        {
            if (_hasLanded) return;
            _hasLanded = true;
            isFalling = false;

            if (enableDebugLogs)
                Debug.Log($"[FallingTerrainChunk] Landed at {transform.position}");

            // Make kinematic for stability
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
            }

            // Try to make collider non-convex for better collision (if mesh is valid)
            if (_mesh.triangles.Length <= 765) // Unity limit for non-convex
            {
                _meshCollider.convex = false;
            }
        }

        /// <summary>
        /// Convert to a fully static object.
        /// </summary>
        public void ConvertToStatic()
        {
            if (isStatic) return;
            isStatic = true;
            isFalling = false;

            if (_rigidbody != null)
            {
                Destroy(_rigidbody);
                _rigidbody = null;
            }

            _meshCollider.convex = false;

            if (enableDebugLogs)
                Debug.Log("[FallingTerrainChunk] Converted to static.");
        }

        #endregion

        #region Public Properties

        public bool IsFalling => isFalling;
        public bool IsStatic => isStatic;

        #endregion

        #region Static Factory

        /// <summary>
        /// Create a falling terrain chunk from an extracted mesh.
        /// </summary>
        public static FallingTerrainChunk Create(Mesh extractedMesh, Material material,
            Vector3 position, Transform parent = null)
        {
            GameObject go = new GameObject("FallingTerrainChunk");
            go.transform.position = position;

            if (parent != null)
            {
                go.transform.SetParent(parent);
            }

            // Add required components
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.AddComponent<MeshCollider>();

            // Add and initialize chunk script
            var chunk = go.AddComponent<FallingTerrainChunk>();
            chunk.InitializeWithMesh(extractedMesh, material);

            return chunk;
        }

        #endregion
    }
}
