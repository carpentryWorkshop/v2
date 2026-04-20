using UnityEngine;

/// <summary>
/// Manages a persistent wood piece in the CNC machine.
/// The wood is always present and kept on top of the CNC base.
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
    [Tooltip("If true, wood XZ follows the cutter reference. If false, manual XZ from WoodVisualThin is preserved.")]
    [SerializeField] private bool _placeUnderCutterXZ = true;

    [Tooltip("Tip transform of the meche/cutter. If null, resolved automatically.")]
    [SerializeField] private Transform _manualCutterTip;

    [Tooltip("CNC cutter root transform used for X/Z and Y placement.")]
    [SerializeField] private Transform _cutterReference;

    [Tooltip("CNC base transform used to place wood height on top surface.")]
    [SerializeField] private Transform _cncBase;

    [Tooltip("Extra height above the CNC base top surface.")]
    [SerializeField] private float _baseSurfaceOffset = 0.003f;

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>The current wood piece in the machine.</summary>
    public WoodPiece CurrentWoodPiece => _woodPiece;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        ResolveReferences();

        ForcePlaceWoodOnBase();
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

        ForcePlaceWoodOnBase();
    }

    private void Update()
    {
        // Keep persistent behavior robust even if external scripts touched the plank.
        if (_woodPiece == null)
            return;

        if (!_woodPiece.gameObject.activeSelf)
            _woodPiece.gameObject.SetActive(true);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Resets the current wood piece to its spawn position.
    /// Clears any engravings (when gravure system is implemented).
    /// </summary>
    public void ResetWood()
    {
        ForcePlaceWoodOnBase();
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

    private void ForcePlaceWoodOnBase()
    {
        ResolveReferences();

        if (_woodPiece == null)
            return;

        float woodHalfY = 0.03f;
        BoxCollider woodBox = _woodPiece.GetComponent<BoxCollider>();
        if (woodBox != null)
            woodHalfY = Mathf.Max(0.01f, woodBox.size.y * 0.5f);

        Vector3 targetPosition = _woodPiece.transform.position;
        if (_cncBase != null)
        {
            Renderer baseRenderer = _cncBase.GetComponentInChildren<Renderer>();
            float baseTopY = baseRenderer != null ? baseRenderer.bounds.max.y : _cncBase.position.y;
            targetPosition.y = baseTopY + woodHalfY + _baseSurfaceOffset;

            if (_placeUnderCutterXZ && (_cutterReference != null || _manualCutterTip != null))
            {
                Transform refXZ = _cutterReference != null ? _cutterReference : _manualCutterTip;
                targetPosition.x = refXZ.position.x;
                targetPosition.z = refXZ.position.z;
            }
            // else: keep current XZ (designer-authored placement)
        }

        float yaw = _cncBase != null ? _cncBase.eulerAngles.y : _woodPiece.transform.eulerAngles.y;
        Vector3 targetRotation = new Vector3(0f, yaw, 0f);

        _woodPiece.SetSpawnPoint(targetPosition, targetRotation);
        _woodPiece.ResetPiece();
        _woodPiece.PlaceInMachine();
    }
}
