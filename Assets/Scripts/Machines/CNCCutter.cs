using System;
using UnityEngine;

/// <summary>
/// Controls the CNC tool-head movement along three axes using keyboard input:
///   - Z-axis (I/K): Moves the cncCutter forward/backward
///   - X-axis (J/L): Moves the spindleHolder left/right
///   - Z-axis (R/F): Moves the spindleFinal depth
///
/// The hierarchy is: cncCutter > spindleHolder > spindleFinal > meche
/// Each transform only moves on its designated axis.
///
/// Attach this to the CNC machine root and assign the three transform references.
/// </summary>
public class CNCCutter : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Transform References")]
    [Tooltip("Transform of cncCutter - moves on Z-axis.")]
    [SerializeField] private Transform _cutterTransform;

    [Tooltip("Transform of spindleHolder - moves on X-axis.")]
    [SerializeField] private Transform _spindleHolderTransform;

    [Tooltip("Transform of spindleFinal - moves on Z-axis.")]
    [SerializeField] private Transform _spindleTransform;

    [Header("Input")]
    [Tooltip("Input handler for keyboard controls. If null, searches in scene.")]
    [SerializeField] private CNCInputHandler _inputHandler;

    [Header("Movement Speeds")]
    [Tooltip("Movement speed of cncCutter on local Z (units/second).")]
    [SerializeField] [Range(0.01f, 1f)] private float _cutterZSpeed = 0.15f;

    [Tooltip("Movement speed of spindleHolder on local X (units/second).")]
    [SerializeField] [Range(0.01f, 1f)] private float _spindleHolderXSpeed = 0.15f;

    [Tooltip("Movement speed of spindleFinal on local Z (units/second).")]
    [SerializeField] [Range(0.001f, 0.2f)] private float _spindleFinalZSpeed = 0.05f;

    [Header("Axis Limits (Local)")]
    [SerializeField] private float _cutterMinZ = -7f;
    [SerializeField] private float _cutterMaxZ = 5f;
    [SerializeField] private float _spindleHolderMinX = 0.0044f;
    [SerializeField] private float _spindleHolderMaxX = 0.06139f;
    [SerializeField] private float _spindleFinalMinZ = -0.02f;
    [SerializeField] private float _spindleFinalMaxZ = -0.01602f;

    [Header("Debug")]
    [Tooltip("Draw the work-area bounds as a Gizmo in the Scene view.")]
    [SerializeField] private bool _showGizmos = true;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fires every frame the cutter moves. Provides the combined tool position
    /// (X from cutter, Y from spindle, Z from spindleHolder).
    /// </summary>
    public event Action<Vector3> OnCutterMoved;

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>Whether the cutter is currently active (set by CNCMachine).</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>Combined tool position in local space.</summary>
    public Vector3 ToolPosition => GetToolPosition();

    /// <summary>Compatibility accessor kept for dependent scripts.</summary>
    public CuttingPath CuttingPath => null;

    // ── Private state ─────────────────────────────────────────────────────────

    private Vector3 _cutterStartLocal;
    private Vector3 _spindleHolderStartLocal;
    private Vector3 _spindleStartLocal;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        CacheStartPositions();

        if (_inputHandler == null)
            _inputHandler = FindObjectOfType<CNCInputHandler>();
    }

    private void Update()
    {
        if (!IsEnabled) return;
        if (_inputHandler == null) return;

        MoveCutter();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Enables or disables cutter movement. Called by <see cref="CNCMachine"/>
    /// when transitioning into/out of the Cutting state.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        if (enabled != IsEnabled)
        {
            Debug.Log($"[CNCCutter] Cutter movement {(enabled ? "ENABLED" : "DISABLED")}");
        }
        IsEnabled = enabled;

        if (_inputHandler != null)
            _inputHandler.SetEnabled(enabled);
    }

    /// <summary>
    /// Sets the movement speed at runtime for all axes.
    /// </summary>
    public void SetSpeed(float speed)
    {
        float safe = Mathf.Max(0.001f, speed);
        _cutterZSpeed = safe;
        _spindleHolderXSpeed = safe;
        _spindleFinalZSpeed = safe;
    }

    /// <summary>
    /// Sets the movement speed for a specific axis.
    /// </summary>
    public void SetAxisSpeed(float xSpeed, float ySpeed, float zSpeed)
    {
        _spindleHolderXSpeed = Mathf.Max(0.001f, xSpeed);
        _spindleFinalZSpeed = Mathf.Max(0.001f, ySpeed);
        _cutterZSpeed = Mathf.Max(0.001f, zSpeed);
    }

    /// <summary>
    /// Returns the current cutter position normalised within the work area (0–1 on X/Z axes).
    /// Useful for mapping to screen UV coordinates in <see cref="CNCScreenDisplay"/>.
    /// </summary>
    public Vector2 GetNormalisedPosition()
    {
        Vector3 pos = GetToolPosition();
        return new Vector2(
            Mathf.InverseLerp(_spindleHolderMinX, _spindleHolderMaxX, pos.x),
            Mathf.InverseLerp(_cutterMinZ, _cutterMaxZ, pos.z)
        );
    }

    /// <summary>
    /// Returns the tool to its starting position.
    /// </summary>
    public void ReturnToHome()
    {
        Debug.Log("[CNCCutter] Returning to home position.");
        
        if (_cutterTransform != null)
            _cutterTransform.localPosition = _cutterStartLocal;

        if (_spindleHolderTransform != null)
            _spindleHolderTransform.localPosition = _spindleHolderStartLocal;

        if (_spindleTransform != null)
            _spindleTransform.localPosition = _spindleStartLocal;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void CacheStartPositions()
    {
        if (_cutterTransform != null)
            _cutterStartLocal = _cutterTransform.localPosition;

        if (_spindleHolderTransform != null)
            _spindleHolderStartLocal = _spindleHolderTransform.localPosition;

        if (_spindleTransform != null)
            _spindleStartLocal = _spindleTransform.localPosition;
    }

    private Vector3 GetToolPosition()
    {
        float x = _spindleHolderTransform != null ? _spindleHolderTransform.localPosition.x : 0f;
        float z = _cutterTransform != null ? _cutterTransform.localPosition.z : 0f;
        float y = _spindleTransform != null ? _spindleTransform.localPosition.z : 0f;

        return new Vector3(x, y, z);
    }

    private void MoveCutter()
    {
        Vector3 input = _inputHandler.CurrentInput;

        if (input.sqrMagnitude < 0.001f) return;

        // cncCutter moves on local Z only (I/K input).
        if (_cutterTransform != null && Mathf.Abs(input.z) > 0.001f)
        {
            Vector3 cutterPos = _cutterTransform.localPosition;
            cutterPos.z += input.z * _cutterZSpeed * Time.deltaTime;
            cutterPos.z = Mathf.Clamp(cutterPos.z, _cutterMinZ, _cutterMaxZ);
            _cutterTransform.localPosition = cutterPos;
        }

        // spindleHolder moves on local X only (J/L input).
        if (_spindleHolderTransform != null && Mathf.Abs(input.x) > 0.001f)
        {
            Vector3 holderPos = _spindleHolderTransform.localPosition;
            holderPos.x += input.x * _spindleHolderXSpeed * Time.deltaTime;
            holderPos.x = Mathf.Clamp(holderPos.x, _spindleHolderMinX, _spindleHolderMaxX);
            _spindleHolderTransform.localPosition = holderPos;
        }

        // spindleFinal moves on local Z only (R/F input).
        if (_spindleTransform != null && Mathf.Abs(input.y) > 0.001f)
        {
            Vector3 spindlePos = _spindleTransform.localPosition;
            spindlePos.z += input.y * _spindleFinalZSpeed * Time.deltaTime;
            spindlePos.z = Mathf.Clamp(spindlePos.z, _spindleFinalMinZ, _spindleFinalMaxZ);
            _spindleTransform.localPosition = spindlePos;
        }

        OnCutterMoved?.Invoke(GetToolPosition());
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (!_showGizmos) return;
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.TransformPoint(GetToolPosition()), 0.01f);
    }
}
