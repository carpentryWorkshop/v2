// =============================================================================
//  CNC2State.cs
//  Shared enums for the CNC2 machine state machine.
//  Place this file anywhere inside Assets/Scripts – it has no MonoBehaviour.
// =============================================================================

/// <summary>
/// Every valid state the CNC2 machine can occupy.
///
/// Allowed transitions:
///   PoweredOff   ──[Power]──►  Idle
///   Idle         ──[LogoSelected]──►  LogoSelected
///   LogoSelected ──[Switch]──►  ModeSelected
///   ModeSelected ──[Switch]──►  ModeSelected  (toggles mode, stays in state)
///   ModeSelected ──[Start]──►  AutoRunning | ManualRunning
///   AutoRunning  ──[complete]──►  Finished
///   AutoRunning  ──[Stop]──►  Stopped
///   ManualRunning──[Stop]──►  Stopped
///   Stopped / Finished ──[Power]──►  Idle   (reset)
/// </summary>
public enum CNC2State
{
    PoweredOff,      // Machine is completely off; all screens dark.
    Idle,            // Powered on; StartPanel + SelectionPanel visible.
    LogoSelected,    // A logo has been chosen; SelectionPanel still visible.
    ModeSelected,    // Mode (Manual/Auto) chosen; WorkflowPanel visible.
    ManualRunning,   // Player is manually driving the spindle (K/J/H/I/T/Y).
    AutoRunning,     // Machine executes the engraving path automatically.
    Stopped,         // Emergency-stopped mid-run; WorkflowPanel shows STOPPED.
    Finished         // Auto-engraving completed successfully.
}

/// <summary>Operating mode selected by the Switch button.</summary>
public enum CNC2Mode
{
    Auto,            // CNC follows the computed toolpath for the selected logo.
    Manual           // Player drives the spindle with the keyboard.
}
