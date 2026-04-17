using System;
using UnityEngine;

/// <summary>
/// Handles keyboard input for manual CNC machine control.
/// Replaces the joystick controller with direct keyboard mapping:
///   - I/K: Forward/Backward (Z-axis) - moves spindleHolder
///   - J/L: Left/Right (X-axis) - moves cncCutter
///   - W/X: Up/Down (Y-axis) - moves spindleFinal
///
/// Attach this to the CNC machine root or a dedicated input handler object.
/// Wire it to <see cref="CNCCutter"/> via the Inspector.
/// </summary>
public class CNCInputHandler : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Key Bindings")]
    [Tooltip("Key to move forward (positive Z).")]
    [SerializeField] private KeyCode _forwardKey = KeyCode.I;

    [Tooltip("Key to move backward (negative Z).")]
    [SerializeField] private KeyCode _backwardKey = KeyCode.K;

    [Tooltip("Key to move left (negative X).")]
    [SerializeField] private KeyCode _leftKey = KeyCode.J;

    [Tooltip("Key to move right (positive X).")]
    [SerializeField] private KeyCode _rightKey = KeyCode.L;

    // R/F used instead of W/X to avoid conflict with player WASD movement.
    [Tooltip("Key to move up (positive Y).")]
    [SerializeField] private KeyCode _upKey = KeyCode.R;

    [Tooltip("Key to move down (negative Y).")]
    [SerializeField] private KeyCode _downKey = KeyCode.F;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fires every frame with the current movement input.
    /// X = lateral (J/L), Y = vertical (W/X), Z = depth (I/K).
    /// Values are -1, 0, or 1 per axis.
    /// </summary>
    public event Action<Vector3> OnMovementInput;

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>Whether input reading is currently enabled.</summary>
    public bool IsEnabled { get; private set; } = true;

    /// <summary>Current movement input as a normalized Vector3.</summary>
    public Vector3 CurrentInput { get; private set; }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Update()
    {
        if (!IsEnabled)
        {
            CurrentInput = Vector3.zero;
            return;
        }

        ReadInput();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Enables or disables input reading.
    /// When disabled, CurrentInput is zeroed and no events fire.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        if (enabled != IsEnabled)
        {
            Debug.Log($"[CNCInputHandler] Input {(enabled ? "ENABLED" : "DISABLED")}");
        }
        IsEnabled = enabled;
        if (!enabled)
            CurrentInput = Vector3.zero;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void ReadInput()
    {
        float x = 0f;
        float y = 0f;
        float z = 0f;

        // X-axis: J (left) / L (right)
        if (Input.GetKey(_leftKey)) x -= 1f;
        if (Input.GetKey(_rightKey)) x += 1f;

        // Y-axis: W (up) / X (down)
        if (Input.GetKey(_upKey)) y += 1f;
        if (Input.GetKey(_downKey)) y -= 1f;

        // Z-axis: I (forward) / K (backward)
        if (Input.GetKey(_forwardKey)) z += 1f;
        if (Input.GetKey(_backwardKey)) z -= 1f;

        Vector3 newInput = new Vector3(x, y, z);

        // Log when input starts (transition from zero to non-zero)
        if (CurrentInput.sqrMagnitude < 0.001f && newInput.sqrMagnitude > 0.001f)
        {
            Debug.Log($"[CNCInputHandler] Moving: X={x}, Y={y}, Z={z}");
        }

        CurrentInput = newInput;

        // Only fire event if there's actual input
        if (CurrentInput.sqrMagnitude > 0.001f)
        {
            OnMovementInput?.Invoke(CurrentInput);
        }
    }
}
