using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a wood workpiece that can be placed in the CNC machine.
/// Handles positioning, reset functionality, and basic gravure visuals.
///
/// Attach this to the wood plank GameObject in the scene.
/// </summary>
public class WoodPiece : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("Renderer for this wood piece. If null, found automatically in children.")]
    [SerializeField] private Renderer _renderer;

    [Tooltip("Collider used for contact and bounds. If null, found automatically in children.")]
    [SerializeField] private Collider _collider;

    [Tooltip("If true, creates a simple fallback cube renderer when no wood mesh exists.")]
    [SerializeField] private bool _createFallbackVisualIfMissing = true;

    [Tooltip("If true, force a thin plank visual and remove old placeholder visuals.")]
    [SerializeField] private bool _forceThinPlankVisual = true;

    [Tooltip("Thin plank local size used for visual and box collider.")]
    [SerializeField] private Vector3 _thinPlankSize = new Vector3(1.0f, 0.06f, 0.22f);

    [Header("Reset Settings")]
    [Tooltip("World position to reset to when ResetPiece() is called.")]
    [SerializeField] private Vector3 _spawnPosition;

    [Tooltip("World rotation to reset to when ResetPiece() is called.")]
    [SerializeField] private Vector3 _spawnRotation;

    [Tooltip("If true, uses current position/rotation as spawn point on Awake.")]
    [SerializeField] private bool _useCurrentAsSpawn = true;

    [Header("Gravure Visual")]
    [Tooltip("Enable simple line-based gravure visuals when the cutter touches wood.")]
    [SerializeField] private bool _enableEngraveVisual = true;

    [Tooltip("Base visual width of the gravure line.")]
    [SerializeField] [Range(0.0005f, 0.02f)] private float _engraveLineWidth = 0.0035f;

    [Tooltip("Minimum distance between two consecutive gravure points.")]
    [SerializeField] [Range(0.0005f, 0.05f)] private float _engravePointSpacing = 0.003f;

    [Tooltip("Small offset above the surface to avoid z-fighting.")]
    [SerializeField] [Range(0f, 0.01f)] private float _engraveSurfaceOffset = 0.0008f;

    [Tooltip("Default gravure color.")]
    [SerializeField] private Color _engraveColor = new Color(0.16f, 0.09f, 0.04f, 0.95f);

    [Tooltip("Optional material for the gravure line. If null, a runtime unlit material is created.")]
    [SerializeField] private Material _engraveMaterial;

    [Header("Drill Mark Visual")]
    [Tooltip("If true, stamps dark drill marks at contact points.")]
    [SerializeField] private bool _enableDrillMarks = true;

    [Tooltip("Base size of one drill mark in meters.")]
    [SerializeField] [Range(0.003f, 0.08f)] private float _drillMarkSize = 0.022f;

    [Tooltip("Maximum number of active drill mark decals.")]
    [SerializeField] [Range(32, 2048)] private int _maxDrillMarks = 400;

    [Tooltip("Base color for engraved cavities.")]
    [SerializeField] private Color _drillMarkColor = new Color(0.04f, 0.02f, 0.01f, 0.88f);

    [Tooltip("Extra offset from surface to avoid z-fighting.")]
    [SerializeField] [Range(0.0001f, 0.01f)] private float _drillMarkSurfaceOffset = 0.0009f;

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>The Renderer component.</summary>
    public Renderer Renderer => _renderer;

    /// <summary>Whether this wood piece is currently in the CNC machine.</summary>
    public bool IsInMachine { get; private set; }

    /// <summary>True if this wood piece has a renderer and can be seen.</summary>
    public bool HasVisibleGeometry => _renderer != null;

    // ── Private state ─────────────────────────────────────────────────────────

    private Rigidbody _rigidbody;
    private Material _originalMaterial;
    private LineRenderer _engraveLine;
    private readonly List<Vector3> _engravePoints = new List<Vector3>();
    private Material _runtimeEngraveMaterial;
    private Material _runtimeFallbackMaterial;
    private Material _runtimeDrillMarkMaterial;
    private Texture2D _runtimeDrillMarkTexture;
    private Transform _drillMarkRoot;
    private readonly Queue<GameObject> _drillMarkQueue = new Queue<GameObject>();
    private Vector3 _lastDrillMarkWorldPoint;
    private bool _hasLastDrillMark;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        EnsureThinPlankVisual();

        if (_collider == null)
            _collider = GetComponentInChildren<Collider>();

        if (_renderer == null)
            _renderer = FindBestRenderer();

        if (_renderer == null && _createFallbackVisualIfMissing)
            CreateFallbackVisual();

        if (_renderer == null)
            _renderer = FindBestRenderer();

        _rigidbody = GetComponent<Rigidbody>();

        if (_renderer != null)
            _originalMaterial = _renderer.sharedMaterial;

        if (_useCurrentAsSpawn)
        {
            _spawnPosition = transform.position;
            _spawnRotation = transform.eulerAngles;
        }
    }

    private void OnEnable()
    {
        EnsureThinPlankVisual();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Resets the wood piece to its spawn position and clears any modifications.
    /// Called by <see cref="WoodSpawner"/> when the CNC machine starts.
    /// </summary>
    public void ResetPiece()
    {
        // Reset position and rotation
        transform.position = _spawnPosition;
        transform.eulerAngles = _spawnRotation;

        // Reset physics if present
        if (_rigidbody != null)
        {
            if (!_rigidbody.isKinematic)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
        }

        // Reset material (for future gravure system)
        if (_renderer != null && _originalMaterial != null)
        {
            _renderer.sharedMaterial = _originalMaterial;
        }

        ClearEngraveVisual();

        IsInMachine = true;

        Debug.Log($"[WoodPiece] {name} reset to spawn position.");
    }

    /// <summary>
    /// Sets the spawn position for this wood piece.
    /// </summary>
    public void SetSpawnPoint(Vector3 position, Vector3 rotation)
    {
        _spawnPosition = position;
        _spawnRotation = rotation;
    }

    /// <summary>
    /// Marks the wood piece as placed in the machine.
    /// </summary>
    public void PlaceInMachine()
    {
        IsInMachine = true;

        gameObject.SetActive(true);

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
        }
    }

    /// <summary>
    /// Marks the wood piece as removed from the machine.
    /// </summary>
    public void RemoveFromMachine()
    {
        IsInMachine = false;

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = false;
        }

        gameObject.SetActive(true);
    }

    /// <summary>
    /// Gets the world-space bounds of the wood piece.
    /// </summary>
    public Bounds GetBounds()
    {
        if (_renderer != null)
            return _renderer.bounds;

        if (_collider != null)
            return _collider.bounds;

        return new Bounds(transform.position, Vector3.one);
    }

    /// <summary>
    /// Converts world position to approximate UV based on world-space bounds.
    /// </summary>
    public Vector2 WorldToUV(Vector3 worldPosition)
    {
        Bounds bounds = GetBounds();
        float width = Mathf.Max(bounds.size.x, 0.0001f);
        float depth = Mathf.Max(bounds.size.z, 0.0001f);
        Vector3 local = worldPosition - bounds.min;
        return new Vector2(
            Mathf.Clamp01(local.x / width),
            Mathf.Clamp01(local.z / depth)
        );
    }

    /// <summary>
    /// Applies an engraving mark at the given UV position.
    /// </summary>
    public void ApplyEngraveMark(Vector2 uv, float depth, float brushSize)
    {
        Bounds bounds = GetBounds();
        float x = Mathf.Lerp(bounds.min.x, bounds.max.x, Mathf.Clamp01(uv.x));
        float z = Mathf.Lerp(bounds.min.z, bounds.max.z, Mathf.Clamp01(uv.y));
        float y = bounds.max.y;

        Vector3 worldPoint = new Vector3(x, y, z);
        ApplyEngraveAtWorldPoint(worldPoint, Vector3.up, depth, brushSize);
    }

    /// <summary>
    /// Applies an engraving mark at an explicit world-space contact point.
    /// </summary>
    public void ApplyEngraveAtWorldPoint(Vector3 worldPoint, Vector3 surfaceNormal, float depth, float brushSize)
    {
        if (!_enableEngraveVisual && !_enableDrillMarks)
            return;

        Vector3 normal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
        float pushedDepth = Mathf.Max(depth, 0f);

        // Keep visual marks on/just above the surface so they stay visible.
        // Depth still influences mark size/intensity, but not by sinking decals into the mesh.
        Vector3 projectedPoint = worldPoint + normal * _engraveSurfaceOffset;

        float spacing = Mathf.Max(_engravePointSpacing, brushSize * 0.35f);
        if (_hasLastDrillMark)
        {
            float lastDistance = Vector3.Distance(_lastDrillMarkWorldPoint, projectedPoint);
            if (lastDistance < spacing)
                return;
        }

        _lastDrillMarkWorldPoint = projectedPoint;
        _hasLastDrillMark = true;

        if (_enableEngraveVisual)
        {
            EnsureEngraveRenderer();
            if (_engraveLine != null)
            {
                Vector3 localPoint = transform.InverseTransformPoint(projectedPoint);
                _engravePoints.Add(localPoint);

                float width = Mathf.Max(_engraveLineWidth, brushSize * 0.35f);
                _engraveLine.startWidth = width;
                _engraveLine.endWidth = width;
                _engraveLine.positionCount = _engravePoints.Count;
                _engraveLine.SetPosition(_engravePoints.Count - 1, localPoint);
            }
        }

        if (_enableDrillMarks)
        {
            float markSize = Mathf.Max(_drillMarkSize, brushSize * 3.2f);
            markSize += Mathf.Clamp01(pushedDepth * 40f) * _drillMarkSize;
            StampDrillMark(projectedPoint, normal, markSize);
        }
    }

    private void EnsureEngraveRenderer()
    {
        if (!_enableEngraveVisual || _engraveLine != null)
            return;

        GameObject engraveRoot = new GameObject("EngraveLine");
        engraveRoot.transform.SetParent(transform, false);
        _engraveLine = engraveRoot.AddComponent<LineRenderer>();

        _engraveLine.useWorldSpace = false;
        _engraveLine.alignment = LineAlignment.TransformZ;
        _engraveLine.textureMode = LineTextureMode.Stretch;
        _engraveLine.numCapVertices = 2;
        _engraveLine.numCornerVertices = 2;
        _engraveLine.positionCount = 0;
        _engraveLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _engraveLine.receiveShadows = false;
        _engraveLine.startColor = _engraveColor;
        _engraveLine.endColor = _engraveColor;
        _engraveLine.startWidth = _engraveLineWidth;
        _engraveLine.endWidth = _engraveLineWidth;

        if (_engraveMaterial != null)
        {
            _engraveLine.sharedMaterial = _engraveMaterial;
        }
        else
        {
            Shader shader = Shader.Find("HDRP/Unlit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                _runtimeEngraveMaterial = new Material(shader);
                _runtimeEngraveMaterial.color = _engraveColor;
                _engraveLine.sharedMaterial = _runtimeEngraveMaterial;
            }
        }
    }

    private void EnsureDrillMarkResources()
    {
        if (_runtimeDrillMarkMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader != null)
            {
                _runtimeDrillMarkMaterial = new Material(shader);
                _runtimeDrillMarkMaterial.name = "WoodDrillMarkMaterial";
                _runtimeDrillMarkMaterial.color = _drillMarkColor;
            }
        }

        if (_runtimeDrillMarkTexture == null)
        {
            _runtimeDrillMarkTexture = BuildDrillMarkTexture(128);
            if (_runtimeDrillMarkMaterial != null)
            {
                if (_runtimeDrillMarkMaterial.HasProperty("_MainTex"))
                    _runtimeDrillMarkMaterial.SetTexture("_MainTex", _runtimeDrillMarkTexture);
                if (_runtimeDrillMarkMaterial.HasProperty("_BaseMap"))
                    _runtimeDrillMarkMaterial.SetTexture("_BaseMap", _runtimeDrillMarkTexture);
            }
        }

        if (_drillMarkRoot == null)
        {
            GameObject root = new GameObject("DrillMarks");
            root.transform.SetParent(transform, false);
            _drillMarkRoot = root.transform;
        }
    }

    private Texture2D BuildDrillMarkTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float inv = 1f / (size - 1);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x * inv * 2f - 1f;
                float v = y * inv * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);

                float baseAlpha = Mathf.Clamp01(1f - Mathf.SmoothStep(0.5f, 1f, r));
                float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.68f) * 12f) * 0.45f;
                float noise = Mathf.PerlinNoise(u * 12.7f + 17.3f, v * 11.9f + 5.2f) * 0.22f;
                float alpha = Mathf.Clamp01(baseAlpha * 0.75f + ring + noise * baseAlpha);

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply(false, false);
        return tex;
    }

    private void StampDrillMark(Vector3 worldPoint, Vector3 surfaceNormal, float size)
    {
        EnsureDrillMarkResources();
        if (_drillMarkRoot == null)
            return;

        GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Quad);
        mark.name = "DrillMark";
        mark.transform.SetParent(_drillMarkRoot, true);

        Collider markCol = mark.GetComponent<Collider>();
        if (markCol != null)
            Destroy(markCol);

        Vector3 normal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
        mark.transform.position = worldPoint + normal * _drillMarkSurfaceOffset;
        Quaternion align = Quaternion.LookRotation(normal, Vector3.up);
        Quaternion spin = Quaternion.AngleAxis(Random.Range(0f, 360f), normal);
        mark.transform.rotation = spin * align;
        mark.transform.localScale = new Vector3(size, size, 1f);

        MeshRenderer mr = mark.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            if (_runtimeDrillMarkMaterial != null)
                mr.sharedMaterial = _runtimeDrillMarkMaterial;

            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            mr.GetPropertyBlock(mpb);

            Color tint = _drillMarkColor;
            float alphaJitter = Random.Range(0.82f, 1f);
            tint.a *= alphaJitter;
            mpb.SetColor("_Color", tint);
            mpb.SetColor("_BaseColor", tint);
            mr.SetPropertyBlock(mpb);

            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        _drillMarkQueue.Enqueue(mark);
        while (_drillMarkQueue.Count > _maxDrillMarks)
        {
            GameObject oldest = _drillMarkQueue.Dequeue();
            if (oldest != null)
                Destroy(oldest);
        }
    }

    private Renderer FindBestRenderer()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer current = renderers[i];
            if (current == null)
                continue;

            if (current is ParticleSystemRenderer)
                continue;

            return current;
        }

        return null;
    }

    private void CreateFallbackVisual()
    {
        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fallback.name = "WoodVisualThin";
        fallback.transform.SetParent(transform, false);

        BoxCollider box = _collider as BoxCollider;
        if (box != null)
        {
            box.center = Vector3.zero;
            box.size = _thinPlankSize;
            fallback.transform.localPosition = box.center;
            fallback.transform.localScale = box.size;
        }
        else
        {
            fallback.transform.localPosition = Vector3.zero;
            fallback.transform.localScale = _thinPlankSize;
        }

        Collider fallbackCollider = fallback.GetComponent<Collider>();
        if (fallbackCollider != null)
            Destroy(fallbackCollider);

        Renderer fallbackRenderer = fallback.GetComponent<Renderer>();
        if (fallbackRenderer != null)
        {
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Diffuse");

            if (shader != null)
            {
                _runtimeFallbackMaterial = new Material(shader);
                _runtimeFallbackMaterial.name = "WoodFallbackMaterial";
                _runtimeFallbackMaterial.color = new Color(0.58f, 0.39f, 0.20f, 1f);

                fallbackRenderer.sharedMaterial = _runtimeFallbackMaterial;
            }
        }

        Debug.LogWarning($"[WoodPiece] {name} has no renderer. Created fallback visual cube.", this);
    }

    private void EnsureThinPlankVisual()
    {
        if (!_forceThinPlankVisual)
            return;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name == "EngraveLine")
                continue;

            if (child.name != "WoodVisualThin" && child.GetComponent<Renderer>() != null)
                child.gameObject.SetActive(false);
        }

        if (!TryGetComponent(out BoxCollider box))
            box = gameObject.AddComponent<BoxCollider>();

        box.center = Vector3.zero;
        box.size = _thinPlankSize;
        _collider = box;

        Transform visualTransform = transform.Find("WoodVisualThin");
        GameObject visual;
        bool createdNow = false;
        if (visualTransform == null)
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "WoodVisualThin";
            visual.transform.SetParent(transform, false);
            createdNow = true;
        }
        else
        {
            visual = visualTransform.gameObject;
            if (visual.GetComponent<MeshFilter>() == null)
                visual.AddComponent<MeshFilter>();
            if (visual.GetComponent<MeshRenderer>() == null)
                visual.AddComponent<MeshRenderer>();
            visual.SetActive(true);
        }

        if (createdNow)
        {
            visual.transform.localPosition = box.center;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = box.size;
        }

        Collider childCollider = visual.GetComponent<Collider>();
        if (childCollider != null)
            Destroy(childCollider);

        Renderer visualRenderer = visual.GetComponent<Renderer>();
        if (visualRenderer != null)
            _renderer = visualRenderer;
    }

    private void ClearEngraveVisual()
    {
        _engravePoints.Clear();
        _hasLastDrillMark = false;
        if (_engraveLine != null)
            _engraveLine.positionCount = 0;

        while (_drillMarkQueue.Count > 0)
        {
            GameObject mark = _drillMarkQueue.Dequeue();
            if (mark != null)
                Destroy(mark);
        }
    }

    private void OnDestroy()
    {
        if (_runtimeEngraveMaterial != null)
            Destroy(_runtimeEngraveMaterial);

        if (_runtimeFallbackMaterial != null)
            Destroy(_runtimeFallbackMaterial);

        if (_runtimeDrillMarkMaterial != null)
            Destroy(_runtimeDrillMarkMaterial);

        if (_runtimeDrillMarkTexture != null)
            Destroy(_runtimeDrillMarkTexture);
    }
}
