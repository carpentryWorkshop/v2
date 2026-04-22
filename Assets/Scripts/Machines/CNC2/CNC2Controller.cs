// =============================================================================
//  CNC2Controller.cs
//  Master state machine for the CNC2 machine.
//
//  Attach to:  the CNC2 root GameObject.
//
//  Inspector setup:
//    Physical Buttons  – drag the child GameObjects that carry CNC2Button.
//    Sub-systems       – drag the GameObjects carrying each sub-script.
//    The sub-systems must live somewhere in the CNC2 hierarchy (or the scene).
//
//  Valid state graph (see CNC2State.cs for full description):
//    PoweredOff → Idle → LogoSelected → ModeSelected → AutoRunning / ManualRunning
//    AutoRunning / ManualRunning → Stopped / Finished
//    Stopped / Finished → Idle  (via Power button)
// =============================================================================

using System;
using UnityEngine;

public class CNC2Controller : MonoBehaviour
{
    // -------------------------------------------------------------------------
    [Header("─── Physical Buttons (assign GameObjects with CNC3DButton component) ───")]
    [Tooltip("Child of 'ctrl' – powers the machine on/off and resets after stop")]
    [SerializeField] private CNC3DButton _powerButton;
    [Tooltip("Toggles between Auto and Manual mode (valid from LogoSelected onward)")]
    [SerializeField] private CNC3DButton _switchButton;
    [Tooltip("Starts engraving (valid in ModeSelected only)")]
    [SerializeField] private CNC3DButton _startButton;
    [Tooltip("Emergency-stops an active run")]
    [SerializeField] private CNC3DButton _stopButton;

    [Header("─── Sub-systems ───")]
    [SerializeField] private CNC2UIManager      _uiManager;
    [SerializeField] private CNC2LogoManager    _logoManager;
    [SerializeField] private CNC2SpindleController _spindle;
    [SerializeField] private CNC2AutoEngraver   _autoEngraver;
    [SerializeField] private CNC2ManualController _manualController;

    // -------------------------------------------------------------------------
    // Public read-only state
    public CNC2State State           { get; private set; } = CNC2State.PoweredOff;
    public CNC2Mode  Mode            { get; private set; } = CNC2Mode.Auto;
    public string    SelectedLogoName{ get; private set; } = string.Empty;

    // -------------------------------------------------------------------------
    // Events – subscribe in other scripts to react to changes
    public event Action<CNC2State> OnStateChanged;
    public event Action<CNC2Mode>  OnModeChanged;

    // -------------------------------------------------------------------------
    private void Awake()
    {
        // Wire physical buttons (CNC3DButton.AddPressListener uses UnityAction) ----
        WireButton(_powerButton,  HandlePower);
        WireButton(_switchButton, HandleSwitch);
        WireButton(_startButton,  HandleStart);
        WireButton(_stopButton,   HandleStop);

        // Wire logo-selection callback -----------------------------------------
        if (_logoManager)
            _logoManager.OnLogoSelected += HandleLogoSelected;

        // Wire auto-engraver completion ----------------------------------------
        if (_autoEngraver)
            _autoEngraver.OnEngraveFinished += HandleAutoFinished;
    }

    // CNC3DButton fires UnityAction (no argument), so we wrap the handlers.
    private static void WireButton(CNC3DButton btn, Action<string> handler)
    {
        if (btn != null) btn.AddPressListener(() => handler(btn.name));
    }

    // =========================================================================
    // Button handlers – each enforces which states the action is legal in.
    // =========================================================================

    private void HandlePower(string _)
    {
        switch (State)
        {
            case CNC2State.PoweredOff:
                // First power-on
                TransitionTo(CNC2State.Idle);
                break;

            case CNC2State.Stopped:
            case CNC2State.Finished:
                // Power after a run = reset the machine
                _spindle?.ReturnToHome();
                TransitionTo(CNC2State.Idle);
                break;

            default:
                // Power mid-run acts as emergency stop
                if (State == CNC2State.AutoRunning || State == CNC2State.ManualRunning)
                    TransitionTo(CNC2State.Stopped);
                break;
        }
    }

    private void HandleSwitch(string _)
    {
        // Switch is only meaningful when a logo has been chosen but the run
        // has not started yet (or is not running).
        if (State != CNC2State.LogoSelected && State != CNC2State.ModeSelected)
        {
            Debug.Log("[CNC2] Switch ignored – select a logo first.");
            return;
        }

        Mode = (Mode == CNC2Mode.Auto) ? CNC2Mode.Manual : CNC2Mode.Auto;
        OnModeChanged?.Invoke(Mode);

        // Always land in ModeSelected after toggling
        TransitionTo(CNC2State.ModeSelected);
    }

    private void HandleStart(string _)
    {
        if (State != CNC2State.ModeSelected)
        {
            Debug.Log("[CNC2] Start ignored – power on, select a logo, then choose a mode first.");
            return;
        }

        TransitionTo(Mode == CNC2Mode.Auto ? CNC2State.AutoRunning : CNC2State.ManualRunning);
    }

    private void HandleStop(string _)
    {
        if (State == CNC2State.AutoRunning || State == CNC2State.ManualRunning)
            TransitionTo(CNC2State.Stopped);
        else
            Debug.Log("[CNC2] Stop ignored – machine is not running.");
    }

    // -------------------------------------------------------------------------
    // Called by CNC2LogoManager when the player selects a logo.
    private void HandleLogoSelected(string logoName)
    {
        // Accept logo selection in Idle, LogoSelected or ModeSelected.
        // This lets the player change their mind before pressing Start.
        if (State == CNC2State.PoweredOff ||
            State == CNC2State.AutoRunning ||
            State == CNC2State.ManualRunning)
        {
            Debug.Log("[CNC2] Logo selection ignored in current state.");
            return;
        }

        SelectedLogoName = logoName;

        if (State == CNC2State.Idle || State == CNC2State.ModeSelected)
            // Changing logo while in ModeSelected resets mode choice – player must re-press Switch.
            TransitionTo(CNC2State.LogoSelected);
        else
            // Already in LogoSelected – stay in state, just refresh the UI.
            OnStateChanged?.Invoke(State);
    }

    // -------------------------------------------------------------------------
    // Called by CNC2AutoEngraver when the toolpath is exhausted.
    private void HandleAutoFinished()
    {
        if (State == CNC2State.AutoRunning)
            TransitionTo(CNC2State.Finished);
    }

    // =========================================================================
    // Public entry points – allow external UI scripts to drive the state machine
    // =========================================================================

    public void TryStart()  => HandleStart(string.Empty);
    public void TryStop()   => HandleStop(string.Empty);
    public void TrySwitch() => HandleSwitch(string.Empty);
    public void TryPower()  => HandlePower(string.Empty);

    // =========================================================================
    // State transition
    // =========================================================================

    private void TransitionTo(CNC2State next)
    {
        CNC2State prev = State;
        State = next;

        Debug.Log($"[CNC2] {prev} → {next}  (mode={Mode}, logo='{SelectedLogoName}')");

        ApplySideEffects(next);
        OnStateChanged?.Invoke(State);
    }

    /// <summary>
    /// Activate / deactivate sub-systems when entering each state.
    /// This is the single place where spindle, engraver and controller
    /// are switched on or off.
    /// </summary>
    private void ApplySideEffects(CNC2State entering)
    {
        switch (entering)
        {
            case CNC2State.PoweredOff:
                _spindle?.StopSpinning();
                _autoEngraver?.Stop();
                _manualController?.SetEnabled(false);
                break;

            case CNC2State.Idle:
            case CNC2State.LogoSelected:
            case CNC2State.ModeSelected:
                // Waiting for user input – spindle still, controls off.
                _spindle?.StopSpinning();
                _autoEngraver?.Stop();
                _manualController?.SetEnabled(false);
                break;

            case CNC2State.AutoRunning:
                _spindle?.StartSpinning();
                _autoEngraver?.StartEngrave(SelectedLogoName);
                _manualController?.SetEnabled(false);
                break;

            case CNC2State.ManualRunning:
                _spindle?.StartSpinning();
                _autoEngraver?.Stop();
                _manualController?.SetEnabled(true);
                break;

            case CNC2State.Stopped:
            case CNC2State.Finished:
                _spindle?.StopSpinning();
                _autoEngraver?.Stop();
                _manualController?.SetEnabled(false);
                break;
        }
    }
}
