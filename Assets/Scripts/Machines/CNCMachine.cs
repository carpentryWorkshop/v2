using System;
using UnityEngine;

/// <summary>
/// State machine for the CNC router machine with Manual and Auto modes.
/// External systems (control panel, task manager) call <see cref="StartCut"/> and
/// <see cref="StopCut"/>; internal state transitions drive <see cref="CNCCutter"/>.
///
/// Modes:
///   - Manual: User controls movement with keyboard (I/J/K/L/W/X)
///   - Auto: Machine follows predefined shape paths automatically
///
/// State flow:
///   Idle ──StartCut()──► Positioning ──Ready()──► Cutting ──StopCut()/done──► Done ──Reset()──► Idle
/// </summary>
public class CNCMachine : MonoBehaviour
{
    // ── Enums ─────────────────────────────────────────────────────────────────

    public enum CNCState
    {
        Idle,
        Positioning,
        Cutting,
        Done
    }

    public enum CNCMode
    {
        Manual,
        Auto
    }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The CNCCutter component that physically moves the tool head.")]
    [SerializeField] private CNCCutter _cutter;

    [Tooltip("The CNCAutoController for automatic shape cutting.")]
    [SerializeField] private CNCAutoController _autoController;

    [Tooltip("The CNCInputHandler for manual keyboard control.")]
    [SerializeField] private CNCInputHandler _inputHandler;

    [Header("Behaviour")]
    [Tooltip("Seconds spent in the Positioning state before cutting begins.")]
    [SerializeField] [Range(0f, 5f)] private float _positioningDuration = 1f;

    [Tooltip("Starting mode for the CNC machine.")]
    [SerializeField] private CNCMode _startingMode = CNCMode.Manual;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fires whenever the machine transitions to a new state.</summary>
    public event Action<CNCState> OnStateChanged;

    /// <summary>Fires when the machine reaches the Done state.</summary>
    public event Action OnCutComplete;

    /// <summary>Fires when the machine mode changes.</summary>
    public event Action<CNCMode> OnModeChanged;

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>Current state of the CNC machine.</summary>
    public CNCState CurrentState { get; private set; } = CNCState.Idle;

    /// <summary>Current mode of the CNC machine (Manual or Auto).</summary>
    public CNCMode CurrentMode { get; private set; } = CNCMode.Manual;

    /// <summary>The CNCCutter component.</summary>
    public CNCCutter Cutter => _cutter;

    /// <summary>The CNCAutoController component.</summary>
    public CNCAutoController AutoController => _autoController;

    // ── Private state ─────────────────────────────────────────────────────────

    private float _positioningTimer;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (_cutter == null)
            _cutter = GetComponentInChildren<CNCCutter>();

        if (_autoController == null)
            _autoController = GetComponent<CNCAutoController>();

        if (_inputHandler == null)
            _inputHandler = GetComponentInChildren<CNCInputHandler>();

        if (_cutter == null)
            Debug.LogWarning($"[CNCMachine] No CNCCutter found on {name} or its children. " +
                             "Assign it in the Inspector.", this);

        CurrentMode = _startingMode;
    }

    private void Start()
    {
        // Ensure input handler starts disabled
        if (_inputHandler != null)
            _inputHandler.SetEnabled(false);
    }

    private void Update()
    {
        switch (CurrentState)
        {
            case CNCState.Positioning:
                TickPositioning();
                break;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Switches between Manual and Auto modes.
    /// Can only switch when machine is in Idle state.
    /// </summary>
    public void SwitchMode()
    {
        if (CurrentState != CNCState.Idle)
        {
            Debug.Log($"[CNCMachine] SwitchMode() ignored — machine must be Idle. Current state: {CurrentState}");
            return;
        }

        CurrentMode = CurrentMode == CNCMode.Manual ? CNCMode.Auto : CNCMode.Manual;
        OnModeChanged?.Invoke(CurrentMode);

        Debug.Log($"[CNCMachine] Mode switched to {CurrentMode}");
    }

    /// <summary>
    /// Sets the mode directly.
    /// Can only change when machine is in Idle state.
    /// </summary>
    public void SetMode(CNCMode mode)
    {
        if (CurrentState != CNCState.Idle)
        {
            Debug.Log($"[CNCMachine] SetMode() ignored — machine must be Idle. Current state: {CurrentState}");
            return;
        }

        if (CurrentMode == mode) return;

        CurrentMode = mode;
        OnModeChanged?.Invoke(CurrentMode);

        Debug.Log($"[CNCMachine] Mode set to {CurrentMode}");
    }

    /// <summary>
    /// Begins the cut sequence from Idle. Transitions through Positioning → Cutting.
    /// In Manual mode, enables keyboard control.
    /// In Auto mode, starts automatic shape cutting.
    /// No-op if the machine is not in the Idle state.
    /// </summary>
    public void StartCut()
    {
        if (CurrentState != CNCState.Idle)
        {
            Debug.Log($"[CNCMachine] StartCut() ignored — current state is {CurrentState}.");
            return;
        }

        Debug.Log($"[CNCMachine] StartCut() in {CurrentMode} mode");
        TransitionTo(CNCState.Positioning);
    }

    /// <summary>
    /// Halts cutting and transitions to Done, then back to Idle after one frame.
    /// Can be called from the control panel Stop button or by the task manager.
    /// </summary>
    public void StopCut()
    {
        if (CurrentState != CNCState.Cutting && CurrentState != CNCState.Positioning)
        {
            Debug.Log($"[CNCMachine] StopCut() ignored — current state is {CurrentState}.");
            return;
        }

        TransitionTo(CNCState.Done);
    }

    /// <summary>
    /// Resets the machine to Idle. Must only be called from the Done state
    /// (it is called automatically via Invoke after the Done state is entered).
    /// Calling this mid-cut (Positioning or Cutting) is a no-op and logs a warning.
    /// </summary>
    public void Reset()
    {
        if (CurrentState == CNCState.Idle) return;

        if (CurrentState != CNCState.Done)
        {
            Debug.LogWarning($"[CNCMachine] Reset() called while in {CurrentState} state. " +
                             "Call StopCut() first to end the current operation.", this);
            return;
        }

        TransitionTo(CNCState.Idle);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void TickPositioning()
    {
        _positioningTimer += Time.deltaTime;
        if (_positioningTimer >= _positioningDuration)
            TransitionTo(CNCState.Cutting);
    }

    private void TransitionTo(CNCState next)
    {
        CancelInvoke(nameof(Reset));
        ExitState(CurrentState);
        CurrentState = next;
        EnterState(next);
        OnStateChanged?.Invoke(next);
    }

    private void EnterState(CNCState state)
    {
        Debug.Log($"[CNCMachine] Entering state: {state}");
        
        switch (state)
        {
            case CNCState.Idle:
                Debug.Log("[CNCMachine] Machine STOPPED - Idle state");
                _cutter?.SetEnabled(false);
                if (_inputHandler != null)
                    _inputHandler.SetEnabled(false);
                break;

            case CNCState.Positioning:
                Debug.Log("[CNCMachine] Machine STARTED - Positioning...");
                _positioningTimer = 0f;
                _cutter?.SetEnabled(false);
                if (_inputHandler != null)
                    _inputHandler.SetEnabled(false);
                break;

            case CNCState.Cutting:
                Debug.Log($"[CNCMachine] Machine CUTTING in {CurrentMode} mode");
                EnterCuttingState();
                break;

            case CNCState.Done:
                Debug.Log("[CNCMachine] Cut cycle DONE");
                ExitCuttingState();
                OnCutComplete?.Invoke();
                // Auto-return to Idle so the panel can start a new cycle
                Invoke(nameof(Reset), 0.1f);
                break;
        }
    }

    private void EnterCuttingState()
    {
        if (CurrentMode == CNCMode.Manual)
        {
            // Enable manual control
            _cutter?.SetEnabled(true);
            if (_inputHandler != null)
                _inputHandler.SetEnabled(true);
        }
        else // Auto mode
        {
            // Start automatic cutting
            _cutter?.SetEnabled(false); // Disable manual input
            if (_inputHandler != null)
                _inputHandler.SetEnabled(false);

            if (_autoController != null)
            {
                _autoController.StartAutoCut();
            }
            else
            {
                Debug.LogWarning("[CNCMachine] Auto mode selected but no CNCAutoController assigned.");
                // Fall back to manual
                _cutter?.SetEnabled(true);
            }
        }
    }

    private void ExitCuttingState()
    {
        _cutter?.SetEnabled(false);

        if (_inputHandler != null)
            _inputHandler.SetEnabled(false);

        if (CurrentMode == CNCMode.Auto && _autoController != null)
        {
            _autoController.StopAutoCut();
        }
    }

    private void ExitState(CNCState state)
    {
        // Reserved for per-state cleanup if needed in future phases
        _ = state;
    }
}
