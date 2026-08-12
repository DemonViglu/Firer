using System.Collections.Generic;
using DemonViglu.FirePlay.Player;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// One continuous ice surface whose shader mask and MeshCollider holes are driven by
    /// the same pressure grid. Fast traversal leaves cracks; lingering breaks the cells below.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class IcePathCrackField : MonoBehaviour
    {
        [Header("Surface")]
        [SerializeField, Min(2f)] private float _width = 70f;
        [SerializeField, Min(2f)] private float _depth = 100f;
        [SerializeField, Min(0.5f)] private float _collisionCellSize = 1.4f;
        [SerializeField, Range(0.1f, 1f)] private float _ellipseFill = 0.96f;
        [SerializeField] private Material _iceMaterial;

        [Header("Recorded Path Mask")]
        [SerializeField, Range(128, 1024)] private int _maskResolution = 512;
        [SerializeField, Min(0.1f)] private float _brushRadius = 1.15f;
        [SerializeField, Range(0f, 1f)] private float _enterPressure = 0.18f;
        [SerializeField, Min(0.01f)] private float _pressurePerSecond = 1.55f;
        [SerializeField, Range(0f, 1f)] private float _breakPressure = 0.8f;
        [SerializeField, Min(0.1f)] private float _playerHeightTolerance = 2.2f;

        [Header("Optional Warmth Bridge")]
        [SerializeField] private WarmthNode _warmthNode;
        [SerializeField, Min(0.1f)] private float _warmthBrushRadius = 3.5f;
        [SerializeField, Min(0f)] private float _warmthPressurePerSecond = 0.38f;

        private static readonly int CrackMaskId = Shader.PropertyToID("_CrackMask");
        private static readonly int IceWorldRectId = Shader.PropertyToID("_IceWorldRect");
        private static readonly int BrushUvId = Shader.PropertyToID("_BrushUV");
        private static readonly int BrushRadiusUvId = Shader.PropertyToID("_BrushRadiusUV");
        private static readonly int BrushStrengthId = Shader.PropertyToID("_BrushStrength");

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshCollider _meshCollider;
        private Mesh _renderMesh;
        private Mesh _collisionMesh;
        private Material _stampMaterial;
        private MaterialPropertyBlock _propertyBlock;
        private RenderTexture _maskA;
        private RenderTexture _maskB;
        private float[] _cellPressure;
        private bool[] _validCells;
        private bool[] _brokenCells;
        private Vector3[] _vertices;
        private int _columns;
        private int _rows;
        private int _lastPlayerCell = -1;

        public RenderTexture PathMask => _maskA;
        public int BrokenCellCount { get; private set; }

        public void Configure(float width, float depth, float collisionCellSize, int maskResolution, Material iceMaterial, WarmthNode warmthNode = null)
        {
            _width = Mathf.Max(2f, width);
            _depth = Mathf.Max(2f, depth);
            _collisionCellSize = Mathf.Max(0.5f, collisionCellSize);
            _maskResolution = Mathf.Clamp(maskResolution, 128, 1024);
            _iceMaterial = iceMaterial;
            _warmthNode = warmthNode;
            CacheComponents();
            BuildMeshes();
            ApplyRendererMaterial();
        }

        private void Awake()
        {
            CacheComponents();
            BuildMeshes();
            CreatePathMask();
            ApplyRendererMaterial();
        }

        private void Update()
        {
            RecordLocalPlayerPath();
            if (_warmthNode != null && _warmthNode.Warmth > 0.001f)
            {
                ApplyWarmth(transform.position, _warmthNode.Warmth, _warmthBrushRadius, Time.deltaTime);
            }
        }

        /// <summary>
        /// Presentation bridge entry point for a spatial WarmthNode/director. The caller supplies
        /// authoritative read-only warmth; this component only records its local visual/physical result.
        /// </summary>
        public void ApplyWarmth(Vector3 worldPosition, float normalizedWarmth, float radius, float deltaTime)
        {
            radius = Mathf.Max(0.1f, radius);
            var local = transform.InverseTransformPoint(worldPosition);
            if (Mathf.Abs(local.x) > _width * 0.5f + radius || Mathf.Abs(local.z) > _depth * 0.5f + radius)
            {
                return;
            }

            var strength = Mathf.Clamp01(normalizedWarmth) * _warmthPressurePerSecond * Mathf.Max(0f, deltaTime);
            if (strength <= 0f)
            {
                return;
            }

            StampAtWorldPosition(worldPosition, radius, strength);
            AddPressureAround(worldPosition, radius, strength);
        }

        [ContextMenu("Ice Path/Clear Recorded Path")]
        public void ClearRecordedPath()
        {
            if (_cellPressure != null)
            {
                System.Array.Clear(_cellPressure, 0, _cellPressure.Length);
                System.Array.Clear(_brokenCells, 0, _brokenCells.Length);
            }

            BrokenCellCount = 0;
            _lastPlayerCell = -1;
            RebuildCollider();
            ClearMask(_maskA);
            ClearMask(_maskB);
            ApplyMaskProperties();
        }

        private void RecordLocalPlayerPath()
        {
            var player = LocalPlayerContext.Current;
            if (player == null || !player.IsLocalPlayer)
            {
                _lastPlayerCell = -1;
                return;
            }

            var worldPosition = player.transform.position;
            var local = transform.InverseTransformPoint(worldPosition);
            if (Mathf.Abs(local.y) > _playerHeightTolerance || !TryGetCell(local, out var cellIndex))
            {
                _lastPlayerCell = -1;
                return;
            }

            var strength = _pressurePerSecond * Time.deltaTime;
            if (cellIndex != _lastPlayerCell)
            {
                strength += _enterPressure;
                _lastPlayerCell = cellIndex;
            }

            StampAtWorldPosition(worldPosition, _brushRadius, strength);
            // Feed the physical grid with the same circular brush used by the mask.
            // The collider therefore opens under the visible hole instead of only
            // removing the single cell containing the player's pivot.
            AddPressureAround(worldPosition, _brushRadius, strength);
        }

        private void AddPressureAround(Vector3 worldPosition, float radius, float strength)
        {
            var local = transform.InverseTransformPoint(worldPosition);
            var minColumn = Mathf.Clamp(Mathf.FloorToInt((local.x - radius + _width * 0.5f) / (_width / _columns)), 0, _columns - 1);
            var maxColumn = Mathf.Clamp(Mathf.FloorToInt((local.x + radius + _width * 0.5f) / (_width / _columns)), 0, _columns - 1);
            var minRow = Mathf.Clamp(Mathf.FloorToInt((local.z - radius + _depth * 0.5f) / (_depth / _rows)), 0, _rows - 1);
            var maxRow = Mathf.Clamp(Mathf.FloorToInt((local.z + radius + _depth * 0.5f) / (_depth / _rows)), 0, _rows - 1);
            var colliderChanged = false;

            for (var row = minRow; row <= maxRow; row++)
            {
                for (var column = minColumn; column <= maxColumn; column++)
                {
                    var center = GetCellCenter(column, row);
                    var distance = Vector2.Distance(new Vector2(local.x, local.z), new Vector2(center.x, center.z));
                    if (distance <= radius)
                    {
                        colliderChanged |= AddCellPressure(row * _columns + column, strength * (1f - distance / radius));
                    }
                }
            }

            if (colliderChanged)
            {
                RebuildCollider();
            }
        }

        private bool AddCellPressure(int cellIndex, float amount)
        {
            if (cellIndex < 0 || cellIndex >= _cellPressure.Length || !_validCells[cellIndex] || _brokenCells[cellIndex])
            {
                return false;
            }

            _cellPressure[cellIndex] = Mathf.Clamp01(_cellPressure[cellIndex] + Mathf.Max(0f, amount));
            if (_cellPressure[cellIndex] >= _breakPressure)
            {
                _brokenCells[cellIndex] = true;
                BrokenCellCount++;
                return true;
            }

            return false;
        }

        private void StampAtWorldPosition(Vector3 worldPosition, float worldRadius, float strength)
        {
            if (_maskA == null || _maskB == null || _stampMaterial == null)
            {
                return;
            }

            var local = transform.InverseTransformPoint(worldPosition);
            var uv = new Vector2(local.x / _width + 0.5f, local.z / _depth + 0.5f);
            if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f)
            {
                return;
            }

            _stampMaterial.SetVector(BrushUvId, new Vector4(uv.x, uv.y, 0f, 0f));
            _stampMaterial.SetVector(BrushRadiusUvId, new Vector4(worldRadius / _width, worldRadius / _depth, 0f, 0f));
            _stampMaterial.SetFloat(BrushStrengthId, Mathf.Clamp01(strength));
            Graphics.Blit(_maskA, _maskB, _stampMaterial);
            (_maskA, _maskB) = (_maskB, _maskA);
            ApplyMaskProperties();
        }

        private void CacheComponents()
        {
            _meshFilter ??= GetComponent<MeshFilter>();
            _meshRenderer ??= GetComponent<MeshRenderer>();
            _meshCollider ??= GetComponent<MeshCollider>();
        }

        private void BuildMeshes()
        {
            _columns = Mathf.Max(2, Mathf.CeilToInt(_width / _collisionCellSize));
            _rows = Mathf.Max(2, Mathf.CeilToInt(_depth / _collisionCellSize));
            _vertices = new Vector3[(_columns + 1) * (_rows + 1)];
            var uvs = new Vector2[_vertices.Length];
            var cellCount = _columns * _rows;
            _validCells = new bool[cellCount];
            _brokenCells = new bool[cellCount];
            _cellPressure = new float[cellCount];

            for (var row = 0; row <= _rows; row++)
            {
                for (var column = 0; column <= _columns; column++)
                {
                    var uv = new Vector2(column / (float)_columns, row / (float)_rows);
                    var index = row * (_columns + 1) + column;
                    _vertices[index] = new Vector3((uv.x - 0.5f) * _width, 0f, (uv.y - 0.5f) * _depth);
                    uvs[index] = uv;
                }
            }

            var renderTriangles = new List<int>(cellCount * 6);
            for (var row = 0; row < _rows; row++)
            {
                for (var column = 0; column < _columns; column++)
                {
                    var cellIndex = row * _columns + column;
                    var center = GetCellCenter(column, row);
                    var ellipse = center.x * center.x / Mathf.Pow(_width * 0.5f, 2f)
                        + center.z * center.z / Mathf.Pow(_depth * 0.5f, 2f);
                    _validCells[cellIndex] = ellipse <= _ellipseFill;
                    if (_validCells[cellIndex])
                    {
                        AppendCellTriangles(renderTriangles, column, row);
                    }
                }
            }

            DestroyMesh(ref _renderMesh);
            _renderMesh = new Mesh { name = "IcePath_RenderMesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            _renderMesh.vertices = _vertices;
            _renderMesh.uv = uvs;
            _renderMesh.triangles = renderTriangles.ToArray();
            _renderMesh.RecalculateNormals();
            _renderMesh.RecalculateBounds();
            _meshFilter.sharedMesh = _renderMesh;
            RebuildCollider();
        }

        private void RebuildCollider()
        {
            if (_vertices == null || _validCells == null)
            {
                return;
            }

            var triangles = new List<int>(_columns * _rows * 6);
            for (var row = 0; row < _rows; row++)
            {
                for (var column = 0; column < _columns; column++)
                {
                    var cellIndex = row * _columns + column;
                    if (_validCells[cellIndex] && !_brokenCells[cellIndex])
                    {
                        AppendCellTriangles(triangles, column, row);
                    }
                }
            }

            if (_collisionMesh == null)
            {
                _collisionMesh = new Mesh { name = "IcePath_CollisionMesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            }

            _meshCollider.sharedMesh = null;
            _collisionMesh.Clear();
            _collisionMesh.vertices = _vertices;
            _collisionMesh.triangles = triangles.ToArray();
            _collisionMesh.RecalculateNormals();
            _collisionMesh.RecalculateBounds();
            _meshCollider.sharedMesh = _collisionMesh;
        }

        private void AppendCellTriangles(List<int> triangles, int column, int row)
        {
            var stride = _columns + 1;
            var bottomLeft = row * stride + column;
            var bottomRight = bottomLeft + 1;
            var topLeft = bottomLeft + stride;
            var topRight = topLeft + 1;
            triangles.Add(bottomLeft);
            triangles.Add(topLeft);
            triangles.Add(bottomRight);
            triangles.Add(bottomRight);
            triangles.Add(topLeft);
            triangles.Add(topRight);
        }

        private Vector3 GetCellCenter(int column, int row)
        {
            return new Vector3(
                ((column + 0.5f) / _columns - 0.5f) * _width,
                0f,
                ((row + 0.5f) / _rows - 0.5f) * _depth);
        }

        private bool TryGetCell(Vector3 localPosition, out int cellIndex)
        {
            var column = Mathf.FloorToInt((localPosition.x / _width + 0.5f) * _columns);
            var row = Mathf.FloorToInt((localPosition.z / _depth + 0.5f) * _rows);
            if (column < 0 || column >= _columns || row < 0 || row >= _rows)
            {
                cellIndex = -1;
                return false;
            }

            cellIndex = row * _columns + column;
            return _validCells[cellIndex] && !_brokenCells[cellIndex];
        }

        private void CreatePathMask()
        {
            ReleasePathMask();
            var descriptor = new RenderTextureDescriptor(_maskResolution, _maskResolution, RenderTextureFormat.ARGB32, 0)
            {
                msaaSamples = 1,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false
            };
            _maskA = new RenderTexture(descriptor) { name = name + "_IcePathMask_A", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            _maskB = new RenderTexture(descriptor) { name = name + "_IcePathMask_B", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            _maskA.Create();
            _maskB.Create();
            ClearMask(_maskA);
            ClearMask(_maskB);

            var stampShader = Shader.Find("Hidden/DemonViglu/FirePlay/Ice Path Stamp");
            if (stampShader != null)
            {
                _stampMaterial = new Material(stampShader) { name = "IcePathStamp_Runtime" };
            }
            else
            {
                Debug.LogError("[IcePathCrackField] Missing Ice Path Stamp shader.", this);
            }

            ApplyMaskProperties();
        }

        private void ApplyRendererMaterial()
        {
            if (_meshRenderer != null && _iceMaterial != null)
            {
                _meshRenderer.sharedMaterial = _iceMaterial;
                _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            ApplyMaskProperties();
        }

        private void ApplyMaskProperties()
        {
            if (_meshRenderer == null)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();
            _meshRenderer.GetPropertyBlock(_propertyBlock);
            if (_maskA != null)
            {
                _propertyBlock.SetTexture(CrackMaskId, _maskA);
            }

            var min = transform.TransformPoint(new Vector3(-_width * 0.5f, 0f, -_depth * 0.5f));
            _propertyBlock.SetVector(IceWorldRectId, new Vector4(min.x, min.z, _width, _depth));
            _meshRenderer.SetPropertyBlock(_propertyBlock);
        }

        private static void ClearMask(RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = previous;
        }

        private void OnDestroy()
        {
            ReleasePathMask();
            if (_stampMaterial != null)
            {
                Destroy(_stampMaterial);
            }

            DestroyMesh(ref _renderMesh);
            DestroyMesh(ref _collisionMesh);
        }

        private void ReleasePathMask()
        {
            ReleaseRenderTexture(ref _maskA);
            ReleaseRenderTexture(ref _maskB);
        }

        private static void ReleaseRenderTexture(ref RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.Release();
            UnityEngine.Object.Destroy(texture);
            texture = null;
        }

        private static void DestroyMesh(ref Mesh mesh)
        {
            if (mesh == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(mesh);
            mesh = null;
        }

        private void OnValidate()
        {
            _width = Mathf.Max(2f, _width);
            _depth = Mathf.Max(2f, _depth);
            _collisionCellSize = Mathf.Max(0.5f, _collisionCellSize);
            _maskResolution = Mathf.Clamp(_maskResolution, 128, 1024);
            _brushRadius = Mathf.Max(0.1f, _brushRadius);
            _breakPressure = Mathf.Max(_enterPressure, _breakPressure);
        }
    }
}
