using UnityEngine;

/// <summary>
/// Represents a wood workpiece that can be placed in the CNC machine.
/// Handles positioning and reset functionality.
/// 
/// Note: Gravure (engraving) system is not implemented - this is a placeholder
/// for future texture modification features.
///
/// Attach this to the wood plank GameObject in the scene.
/// </summary>
public class WoodPiece : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The MeshRenderer for this wood piece.")]
    [SerializeField] private MeshRenderer _renderer;

    [Header("Reset Settings")]
    [Tooltip("Position to reset to when ResetPiece() is called.")]
    [SerializeField] private Vector3 _spawnPosition;

    [Tooltip("Rotation to reset to when ResetPiece() is called.")]
    [SerializeField] private Vector3 _spawnRotation;

    [Tooltip("If true, uses current position/rotation as spawn point on Awake.")]
    [SerializeField] private bool _useCurrentAsSpawn = true;

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>The MeshRenderer component.</summary>
    public MeshRenderer Renderer => _renderer;

    /// <summary>Whether this wood piece is currently in the CNC machine.</summary>
    public bool IsInMachine { get; private set; }

    // ── Private state ─────────────────────────────────────────────────────────

    private Rigidbody _rigidbody;
    private Material _originalMaterial;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (_renderer == null)
            _renderer = GetComponent<MeshRenderer>();

        _rigidbody = GetComponent<Rigidbody>();

        if (_renderer != null)
            _originalMaterial = _renderer.sharedMaterial;

        if (_useCurrentAsSpawn)
        {
            _spawnPosition = transform.localPosition;
            _spawnRotation = transform.localEulerAngles;
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
        transform.localPosition = _spawnPosition;
        transform.localEulerAngles = _spawnRotation;

        // Reset physics if present
        if (_rigidbody != null)
        {
            _rigidbody.velocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        // Reset material (for future gravure system)
        if (_renderer != null && _originalMaterial != null)
        {
            _renderer.sharedMaterial = _originalMaterial;
        }

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

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
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
        }
    }

    /// <summary>
    /// Gets the world-space bounds of the wood piece.
    /// </summary>
    public Bounds GetBounds()
    {
        if (_renderer != null)
            return _renderer.bounds;

        return new Bounds(transform.position, Vector3.one);
    }

    // ── Future: Gravure System Hooks ──────────────────────────────────────────

    /// <summary>
    /// Placeholder for future gravure system.
    /// Would convert a world position to UV coordinates on the wood surface.
    /// </summary>
    public Vector2 WorldToUV(Vector3 worldPosition)
    {
        // TODO: Implement when gravure system is added
        // This would use the mesh's UV mapping to convert world position to texture UV
        Bounds bounds = GetBounds();
        Vector3 local = worldPosition - bounds.min;
        return new Vector2(
            local.x / bounds.size.x,
            local.z / bounds.size.z
        );
    }

    /// <summary>
    /// Placeholder for future gravure system.
    /// Would apply an engraving mark at the given UV position.
    /// </summary>
    public void ApplyEngraveMark(Vector2 uv, float depth, float brushSize)
    {
        // TODO: Implement when gravure system is added
        // This would modify a RenderTexture mask that affects the material
        Debug.Log($"[WoodPiece] Engrave at UV({uv.x:F2}, {uv.y:F2}) depth={depth:F3}");
    }
}
