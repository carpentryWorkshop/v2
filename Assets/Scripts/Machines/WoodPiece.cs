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

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (_forceThinPlankVisual)
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
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
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
        if (!_enableEngraveVisual)
            return;

        EnsureEngraveRenderer();
        if (_engraveLine == null)
            return;

        Vector3 normal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
        Vector3 projectedPoint = worldPoint - normal * Mathf.Max(depth, 0f) + normal * _engraveSurfaceOffset;
        Vector3 localPoint = transform.InverseTransformPoint(projectedPoint);

        float spacing = Mathf.Max(_engravePointSpacing, brushSize * 0.35f);
        if (_engravePoints.Count > 0)
        {
            float lastDistance = Vector3.Distance(_engravePoints[_engravePoints.Count - 1], localPoint);
            if (lastDistance < spacing)
                return;
        }

        _engravePoints.Add(localPoint);

        float width = Mathf.Max(_engraveLineWidth, brushSize * 0.35f);
        _engraveLine.startWidth = width;
        _engraveLine.endWidth = width;
        _engraveLine.positionCount = _engravePoints.Count;
        _engraveLine.SetPosition(_engravePoints.Count - 1, localPoint);
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
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith("WoodVisual") || child.name == "WoodFallbackVisual")
                Destroy(child.gameObject);
        }

        if (!TryGetComponent(out BoxCollider box))
            box = gameObject.AddComponent<BoxCollider>();

        box.center = Vector3.zero;
        box.size = _thinPlankSize;
        _collider = box;

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "WoodVisualThin";
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = box.center;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = box.size;

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
        if (_engraveLine != null)
            _engraveLine.positionCount = 0;
    }

    private void OnDestroy()
    {
        if (_runtimeEngraveMaterial != null)
            Destroy(_runtimeEngraveMaterial);

        if (_runtimeFallbackMaterial != null)
            Destroy(_runtimeFallbackMaterial);
    }
}
