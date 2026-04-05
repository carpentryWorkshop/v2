using UnityEngine;

/// <summary>
/// Manages the wood piece in the CNC machine.
/// Resets the wood to a fresh state when the machine starts a new cut.
///
/// Attach this to the CNC machine root alongside <see cref="CNCMachine"/>.
/// </summary>
public class WoodSpawner : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The CNC machine to monitor for start events.")]
    [SerializeField] private CNCMachine _machine;

    [Tooltip("The wood piece currently in the machine.")]
    [SerializeField] private WoodPiece _woodPiece;

    [Tooltip("Transform marking the spawn position for the wood.")]
    [SerializeField] private Transform _spawnPoint;

    [Header("Settings")]
    [Tooltip("If true, automatically resets wood when CNC machine starts.")]
    [SerializeField] private bool _resetOnStart = true;

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>The current wood piece in the machine.</summary>
    public WoodPiece CurrentWoodPiece => _woodPiece;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (_machine == null)
            _machine = GetComponent<CNCMachine>();

        if (_machine == null)
            _machine = GetComponentInParent<CNCMachine>();
    }

    private void OnEnable()
    {
        if (_machine != null)
            _machine.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (_machine != null)
            _machine.OnStateChanged -= HandleStateChanged;
    }

    private void Start()
    {
        // Update spawn point if one is assigned
        if (_woodPiece != null && _spawnPoint != null)
        {
            _woodPiece.SetSpawnPoint(
                _spawnPoint.localPosition,
                _spawnPoint.localEulerAngles
            );
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Resets the current wood piece to its spawn position.
    /// Clears any engravings (when gravure system is implemented).
    /// </summary>
    public void ResetWood()
    {
        if (_woodPiece == null)
        {
            Debug.LogWarning("[WoodSpawner] No wood piece assigned to reset.", this);
            return;
        }

        Debug.Log("[WoodSpawner] Resetting wood piece to fresh state.");

        // Update spawn point position if it has moved
        if (_spawnPoint != null)
        {
            _woodPiece.SetSpawnPoint(
                _spawnPoint.localPosition,
                _spawnPoint.localEulerAngles
            );
        }

        _woodPiece.ResetPiece();
    }

    /// <summary>
    /// Assigns a new wood piece to the spawner.
    /// </summary>
    public void SetWoodPiece(WoodPiece piece)
    {
        _woodPiece = piece;

        if (_woodPiece != null && _spawnPoint != null)
        {
            _woodPiece.SetSpawnPoint(
                _spawnPoint.localPosition,
                _spawnPoint.localEulerAngles
            );
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void HandleStateChanged(CNCMachine.CNCState newState)
    {
        // Reset wood when transitioning from Idle to Positioning (start of cut)
        if (_resetOnStart && newState == CNCMachine.CNCState.Positioning)
        {
            ResetWood();
        }
    }
}
