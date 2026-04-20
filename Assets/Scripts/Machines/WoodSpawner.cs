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

    [Header("Manual Mode")]
    [Tooltip("If true, wood is re-centered under the cutter when a manual cut starts.")]
    [SerializeField] private bool _spawnUnderCutterInManualMode = true;

    [Tooltip("Tip transform of the meche/cutter. If null, resolved automatically.")]
    [SerializeField] private Transform _manualCutterTip;

    [Tooltip("CNC cutter root transform used for X/Z and Y placement.")]
    [SerializeField] private Transform _cutterReference;

    [Tooltip("CNC base transform used to place wood height on top surface.")]
    [SerializeField] private Transform _cncBase;

    [Tooltip("Keep Y from spawn point (recommended for this scene).")]
    [SerializeField] private bool _keepSpawnPointHeight = true;

    [Tooltip("Used only when Keep Spawn Point Height is off.")]
    [SerializeField] private float _manualSpawnVerticalOffset = -0.6f;

    [Tooltip("Extra height above CNC base surface.")]
    [SerializeField] private float _baseSurfaceOffset = 0.005f;

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>The current wood piece in the machine.</summary>
    public WoodPiece CurrentWoodPiece => _woodPiece;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        ResolveReferences();

        PlaceWoodOnBase();
    }

    private void OnEnable()
    {
        ResolveReferences();

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
        ResolveReferences();

        PlaceWoodOnBase();
    }

    private void Update()
    {
        // Intentionally left empty: wood is always present on the CNC base.
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Resets the current wood piece to its spawn position.
    /// Clears any engravings (when gravure system is implemented).
    /// </summary>
    public void ResetWood()
    {
        ResolveReferences();

        if (_woodPiece == null)
        {
            Debug.LogWarning("[WoodSpawner] No wood piece assigned to reset.", this);
            return;
        }

        Debug.Log("[WoodSpawner] Resetting wood piece to fresh state.");

        // Update spawn point position if it has moved
        Vector3 targetSpawnPosition = _woodPiece.transform.position;
        Vector3 targetSpawnRotation = _woodPiece.transform.eulerAngles;

        if (_spawnPoint != null)
        {
            targetSpawnPosition = _spawnPoint.position;
            targetSpawnRotation = _spawnPoint.eulerAngles;
        }

        if (ShouldSpawnUnderCutter())
        {
            targetSpawnPosition = ComputeManualSpawnPosition(targetSpawnPosition);
        }

        _woodPiece.SetSpawnPoint(targetSpawnPosition, targetSpawnRotation);

        if (!_woodPiece.gameObject.activeSelf)
            _woodPiece.gameObject.SetActive(true);

        _woodPiece.ResetPiece();
        _woodPiece.PlaceInMachine();
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
                _spawnPoint.position,
                _spawnPoint.eulerAngles
            );
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void HandleStateChanged(CNCMachine.CNCState newState)
    {
        _ = newState;
    }

    private bool ShouldSpawnUnderCutter()
    {
        ResolveReferences();

        return _spawnUnderCutterInManualMode
               && _machine != null
               && _machine.CurrentMode == CNCMachine.CNCMode.Manual;
    }

    private Vector3 ComputeManualSpawnPosition(Vector3 fallback)
    {
        ResolveReferences();

        if (_manualCutterTip == null && _cutterReference == null)
            return fallback;

        Vector3 target = fallback;
        Transform xyRef = _cutterReference != null ? _cutterReference : _manualCutterTip;
        target.x = xyRef.position.x;
        target.z = xyRef.position.z;

        if (_keepSpawnPointHeight)
        {
            float baseY = _cncBase != null ? _cncBase.position.y : fallback.y;
            float cutterY = (_cutterReference != null ? _cutterReference.position.y : _manualCutterTip.position.y);

            float minY = Mathf.Min(baseY, cutterY);
            float maxY = Mathf.Max(baseY, cutterY);
            target.y = Mathf.Clamp(Mathf.Lerp(baseY, cutterY, 0.5f) + _baseSurfaceOffset, minY + 0.002f, maxY - 0.002f);
        }
        else
        {
            float cutterY = (_cutterReference != null ? _cutterReference.position.y : _manualCutterTip.position.y);
            target.y = cutterY + _manualSpawnVerticalOffset;
        }

        return target;
    }

    private float ComputeBaseTopY(float fallbackY)
    {
        if (_cncBase == null)
            return fallbackY;

        Renderer baseRenderer = _cncBase.GetComponentInChildren<Renderer>();
        if (baseRenderer == null)
            return _cncBase.position.y + _baseSurfaceOffset;

        float woodHalfHeight = 0.03f;
        if (_woodPiece != null)
        {
            BoxCollider box = _woodPiece.GetComponent<BoxCollider>();
            if (box != null)
                woodHalfHeight = Mathf.Max(0.005f, box.size.z * 0.5f);
        }

        return baseRenderer.bounds.max.y + woodHalfHeight + _baseSurfaceOffset;
    }

    private void ResolveReferences()
    {
        if (_machine == null)
            _machine = GetComponent<CNCMachine>();

        if (_machine == null)
            _machine = GetComponentInParent<CNCMachine>();

        if (_woodPiece == null)
            _woodPiece = FindObjectOfType<WoodPiece>();

        if (_woodPiece == null)
            _woodPiece = FindObjectOfType<WoodPiece>(true);

        if (_manualCutterTip == null)
        {
            Transform machineRoot = _machine != null ? _machine.transform : transform;
            _manualCutterTip = machineRoot.Find("cncCutter/spindleHolder/spindleFinal/meche");
        }

        if (_cncBase == null)
        {
            Transform machineRoot = _machine != null ? _machine.transform : transform;
            _cncBase = machineRoot.Find("cncBase");
        }

        if (_cutterReference == null)
        {
            Transform machineRoot = _machine != null ? _machine.transform : transform;
            _cutterReference = machineRoot.Find("cncCutter");
        }
    }

    private void PlaceWoodOnBase()
    {
        if (_woodPiece == null)
            return;

        Vector3 targetSpawnPosition = _woodPiece.transform.position;
        Vector3 targetSpawnRotation = _woodPiece.transform.eulerAngles;

        if (_spawnPoint != null)
        {
            targetSpawnPosition = _spawnPoint.position;
            targetSpawnRotation = _spawnPoint.eulerAngles;
        }

        // Always place the plank on the CNC base under cutter area.
        targetSpawnPosition = ComputeManualSpawnPosition(targetSpawnPosition);

        // Always keep plank flat (not standing up).
        float yaw = _cncBase != null ? _cncBase.eulerAngles.y : transform.eulerAngles.y;
        targetSpawnRotation = new Vector3(0f, yaw, 0f);

        _woodPiece.SetSpawnPoint(targetSpawnPosition, targetSpawnRotation);
        _woodPiece.ResetPiece();
        _woodPiece.PlaceInMachine();
    }
}
