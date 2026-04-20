using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CNC-specific control panel. Extends <see cref="ControlPanel"/> with:
///   - Start / Stop buttons that call <see cref="CNCMachine.StartCut"/> / <see cref="CNCMachine.StopCut"/>
///   - Mode switch button to toggle between Manual and Auto modes
///   - Speed adjustment (increase / decrease cutting speed on the cutter)
///   - State and mode indicator text that mirrors the machine's current state
///
/// Setup in the scene:
///   1. Place this on the ControlPanel.fbx GameObject (or a child).
///   2. Assign the <see cref="CNCMachine"/> and <see cref="CNCCutter"/> references.
///   3. Wire the Start/Stop/Mode/SpeedUp/SpeedDown physical buttons to the
///      corresponding public methods via their Interactable/OnClick events,
///      or via the inherited _onStartPressed / _onStopPressed UnityEvents.
/// </summary>
public class CNCControlPanel : ControlPanel
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("CNC Machine References")]
    [Tooltip("The CNCMachine state machine this panel controls.")]
    [SerializeField] private CNCMachine _machine;

    [Tooltip("The CNCCutter whose speed this panel can adjust.")]
    [SerializeField] private CNCCutter _cutter;

    [Tooltip("Optional WoodSpawner to force wood spawn/hide from panel actions.")]
    [SerializeField] private WoodSpawner _woodSpawner;

    [Header("Speed Settings")]
    [Tooltip("Amount added/subtracted from cutter speed per button press.")]
    [SerializeField] [Range(0.01f, 0.2f)] private float _speedStep = 0.05f;

    [Tooltip("Minimum allowable cutting speed (m/s).")]
    [SerializeField] [Range(0.01f, 0.5f)] private float _minSpeed = 0.05f;

    [Tooltip("Maximum allowable cutting speed (m/s).")]
    [SerializeField] [Range(0.05f, 1f)] private float _maxSpeed = 0.5f;

    [Header("UI Elements (optional — assign if Canvas UI is used)")]
    [Tooltip("Text element that shows the current machine state.")]
    [SerializeField] private TMP_Text _stateLabel;

    [Tooltip("Text element that shows the current mode (Manual/Auto).")]
    [SerializeField] private TMP_Text _modeLabel;

    [Tooltip("Text element that shows the current cutting speed.")]
    [SerializeField] private TMP_Text _speedLabel;

    [Tooltip("Image used as a status indicator light (green = cutting, red = idle).")]
    [SerializeField] private Image _statusIndicator;

    [Tooltip("Image used as a mode indicator light.")]
    [SerializeField] private Image _modeIndicator;

    [Header("State Indicator Colors")]
    [SerializeField] private Color _colorIdle = Color.red;
    [SerializeField] private Color _colorPositioning = Color.yellow;
    [SerializeField] private Color _colorCutting = new Color(0f, 0.8f, 0f);
    [SerializeField] private Color _colorDone = Color.cyan;

    [Header("Mode Indicator Colors")]
    [SerializeField] private Color _colorManual = new Color(0.2f, 0.5f, 1f); // Blue
    [SerializeField] private Color _colorAuto = new Color(0f, 0.8f, 0.4f);   // Green

    // ── Private state ─────────────────────────────────────────────────────────

    private float _currentSpeed;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();

        if (_machine == null)
            _machine = GetComponentInParent<CNCMachine>();

        if (_machine == null)
            Debug.LogWarning("[CNCControlPanel] No CNCMachine assigned or found in parent.", this);

        if (_woodSpawner == null)
            _woodSpawner = GetComponentInParent<WoodSpawner>();

        // Read the starting speed from the cutter if possible
        _currentSpeed = _minSpeed + (_maxSpeed - _minSpeed) * 0.5f; // default: midpoint
        ApplySpeedToCutter();
    }

    private void OnEnable()
    {
        if (_machine != null)
        {
            _machine.OnStateChanged += HandleStateChanged;
            _machine.OnModeChanged += HandleModeChanged;
        }
    }

    private void OnDisable()
    {
        if (_machine != null)
        {
            _machine.OnStateChanged -= HandleStateChanged;
            _machine.OnModeChanged -= HandleModeChanged;
        }
    }

    // ── ControlPanel overrides ────────────────────────────────────────────────

    public override void PressStart()
    {
        Debug.Log("[CNCControlPanel] Start button pressed.");
        base.PressStart();

        _machine?.StartCut();
    }

    public override void PressStop()
    {
        Debug.Log("[CNCControlPanel] Stop button pressed.");
        base.PressStop();
        _machine?.StopCut();

    }

    /// <summary>Refreshes all display elements to reflect current machine state and mode.</summary>
    public override void UpdateDisplay()
    {
        if (_machine == null) return;

        CNCMachine.CNCState state = _machine.CurrentState;
        CNCMachine.CNCMode mode = _machine.CurrentMode;

        // State label
        if (_stateLabel != null)
            _stateLabel.text = state.ToString().ToUpper();

        // Mode label
        if (_modeLabel != null)
            _modeLabel.text = mode.ToString().ToUpper();

        // Speed label
        if (_speedLabel != null)
            _speedLabel.text = $"SPD: {_currentSpeed:F2} m/s";

        // State indicator colour
        if (_statusIndicator != null)
        {
            _statusIndicator.color = state switch
            {
                CNCMachine.CNCState.Idle        => _colorIdle,
                CNCMachine.CNCState.Positioning => _colorPositioning,
                CNCMachine.CNCState.Cutting     => _colorCutting,
                CNCMachine.CNCState.Done        => _colorDone,
                _                               => _colorIdle
            };
        }

        // Mode indicator colour
        if (_modeIndicator != null)
        {
            _modeIndicator.color = mode switch
            {
                CNCMachine.CNCMode.Manual => _colorManual,
                CNCMachine.CNCMode.Auto   => _colorAuto,
                _                         => _colorManual
            };
        }
    }

    // ── Mode control ──────────────────────────────────────────────────────────

    /// <summary>
    /// Toggles between Manual and Auto modes.
    /// Call from the 'switch' button on the control panel.
    /// </summary>
    public void PressMode()
    {
        Debug.Log("[CNCControlPanel] Mode button pressed.");
        if (_machine == null) return;

        _machine.SwitchMode();
        UpdateDisplay();
    }

    /// <summary>
    /// Sets the machine to Manual mode.
    /// </summary>
    public void SetManualMode()
    {
        if (_machine == null) return;

        _machine.SetMode(CNCMachine.CNCMode.Manual);
        UpdateDisplay();
    }

    /// <summary>
    /// Sets the machine to Auto mode.
    /// </summary>
    public void SetAutoMode()
    {
        if (_machine == null) return;

        _machine.SetMode(CNCMachine.CNCMode.Auto);
        UpdateDisplay();
    }

    // ── Speed control (call from physical panel buttons) ──────────────────────

    /// <summary>Increases cutting speed by one step, clamped to max.</summary>
    public void IncreaseSpeed()
    {
        _currentSpeed = Mathf.Clamp(_currentSpeed + _speedStep, _minSpeed, _maxSpeed);
        ApplySpeedToCutter();
        UpdateDisplay();
    }

    /// <summary>Decreases cutting speed by one step, clamped to min.</summary>
    public void DecreaseSpeed()
    {
        _currentSpeed = Mathf.Clamp(_currentSpeed - _speedStep, _minSpeed, _maxSpeed);
        ApplySpeedToCutter();
        UpdateDisplay();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void HandleStateChanged(CNCMachine.CNCState newState)
    {
        UpdateDisplay();
    }

    private void HandleModeChanged(CNCMachine.CNCMode newMode)
    {
        UpdateDisplay();
    }

    private void ApplySpeedToCutter()
    {
        if (_cutter == null) return;

        _cutter.SetSpeed(_currentSpeed);
    }
}
