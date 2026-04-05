using System;
using UnityEngine;

/// <summary>
/// Controls the CNC tool-head movement along three axes using keyboard input:
///   - X-axis (J/L): Moves the cncCutter left/right
///   - Z-axis (I/K): Moves the spindleHolder forward/backward  
///   - Y-axis (W/X): Moves the spindleFinal up/down
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
    [Tooltip("Transform of cncCutter - moves on X-axis (left/right with J/L).")]
    [SerializeField] private Transform _cutterTransform;

    [Tooltip("Transform of spindleHolder - moves on Z-axis (forward/backward with I/K).")]
    [SerializeField] private Transform _spindleHolderTransform;

    [Tooltip("Transform of spindleFinal - moves on Y-axis (up/down with W/X).")]
    [SerializeField] private Transform _spindleTransform;

    [Header("Input")]
    [Tooltip("Input handler for keyboard controls. If null, searches in scene.")]
    [SerializeField] private CNCInputHandler _inputHandler;

    [Header("Bounds")]
    [Tooltip("ScriptableObject that defines work-area bounds and height limits.")]
    [SerializeField] private CuttingPath _cuttingPath;

    [Header("Movement")]
    [Tooltip("Movement speed on X-axis (metres per second).")]
    [SerializeField] [Range(0.01f, 1f)] private float _xSpeed = 0.15f;

    [Tooltip("Movement speed on Z-axis (metres per second).")]
    [SerializeField] [Range(0.01f, 1f)] private float _zSpeed = 0.15f;

    [Tooltip("Movement speed on Y-axis (metres per second).")]
    [SerializeField] [Range(0.01f, 1f)] private float _ySpeed = 0.1f;

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

    /// <summary>The CuttingPath ScriptableObject for bounds.</summary>
    public CuttingPath CuttingPath => _cuttingPath;

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
        if (_cuttingPath == null)
        {
            Debug.LogWarning("[CNCCutter] No CuttingPath assigned — cutter cannot move.", this);
            return;
        }

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
        _xSpeed = Mathf.Max(0.001f, speed);
        _zSpeed = Mathf.Max(0.001f, speed);
        _ySpeed = Mathf.Max(0.001f, speed);
    }

    /// <summary>
    /// Sets the movement speed for a specific axis.
    /// </summary>
    public void SetAxisSpeed(float xSpeed, float ySpeed, float zSpeed)
    {
        _xSpeed = Mathf.Max(0.001f, xSpeed);
        _ySpeed = Mathf.Max(0.001f, ySpeed);
        _zSpeed = Mathf.Max(0.001f, zSpeed);
    }

    /// <summary>
    /// Returns the current cutter position normalised within the work area (0–1 on X/Z axes).
    /// Useful for mapping to screen UV coordinates in <see cref="CNCScreenDisplay"/>.
    /// </summary>
    public Vector2 GetNormalisedPosition()
    {
        if (_cuttingPath == null) return Vector2.one * 0.5f;

        Vector3 pos = GetToolPosition();
        return _cuttingPath.Normalise(new Vector2(pos.x, pos.z));
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
        float x = _cutterTransform != null ? _cutterTransform.localPosition.x : 0f;
        float z = _spindleHolderTransform != null ? _spindleHolderTransform.localPosition.z : 0f;
        float y = _spindleTransform != null ? _spindleTransform.localPosition.y : 0f;

        return new Vector3(x, y, z);
    }

    private void MoveCutter()
    {
        Vector3 input = _inputHandler.CurrentInput;

        if (input.sqrMagnitude < 0.001f) return;

        // Move X-axis (cutter) with J/L
        if (_cutterTransform != null && Mathf.Abs(input.x) > 0.001f)
        {
            Vector3 cutterPos = _cutterTransform.localPosition;
            cutterPos.x += input.x * _xSpeed * Time.deltaTime;

            // Clamp to bounds
            Vector2 clampedXZ = _cuttingPath.Clamp(new Vector2(cutterPos.x, 0f));
            cutterPos.x = clampedXZ.x;

            _cutterTransform.localPosition = cutterPos;
        }

        // Move Z-axis (spindleHolder) with I/K
        if (_spindleHolderTransform != null && Mathf.Abs(input.z) > 0.001f)
        {
            Vector3 holderPos = _spindleHolderTransform.localPosition;
            holderPos.z += input.z * _zSpeed * Time.deltaTime;

            // Clamp to bounds
            Vector2 clampedXZ = _cuttingPath.Clamp(new Vector2(0f, holderPos.z));
            holderPos.z = clampedXZ.y;

            _spindleHolderTransform.localPosition = holderPos;
        }

        // Move Y-axis (spindle) with W/X
        if (_spindleTransform != null && Mathf.Abs(input.y) > 0.001f)
        {
            Vector3 spindlePos = _spindleTransform.localPosition;
            spindlePos.y += input.y * _ySpeed * Time.deltaTime;

            // Clamp to height bounds
            spindlePos.y = _cuttingPath.ClampHeight(spindlePos.y);

            _spindleTransform.localPosition = spindlePos;
        }

        OnCutterMoved?.Invoke(GetToolPosition());
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (!_showGizmos || _cuttingPath == null) return;

        // Draw work-area bounds
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = new Color(0f, 1f, 0.4f, 0.5f);

        Vector2 min = _cuttingPath.WorkAreaMin;
        Vector2 max = _cuttingPath.WorkAreaMax;
        float minY = _cuttingPath.MinHeight;
        float maxY = _cuttingPath.MaxHeight;

        // Draw bottom rectangle
        Gizmos.DrawLine(new Vector3(min.x, minY, min.y), new Vector3(max.x, minY, min.y));
        Gizmos.DrawLine(new Vector3(max.x, minY, min.y), new Vector3(max.x, minY, max.y));
        Gizmos.DrawLine(new Vector3(max.x, minY, max.y), new Vector3(min.x, minY, max.y));
        Gizmos.DrawLine(new Vector3(min.x, minY, max.y), new Vector3(min.x, minY, min.y));

        // Draw top rectangle
        Gizmos.DrawLine(new Vector3(min.x, maxY, min.y), new Vector3(max.x, maxY, min.y));
        Gizmos.DrawLine(new Vector3(max.x, maxY, min.y), new Vector3(max.x, maxY, max.y));
        Gizmos.DrawLine(new Vector3(max.x, maxY, max.y), new Vector3(min.x, maxY, max.y));
        Gizmos.DrawLine(new Vector3(min.x, maxY, max.y), new Vector3(min.x, maxY, min.y));

        // Draw vertical lines
        Gizmos.DrawLine(new Vector3(min.x, minY, min.y), new Vector3(min.x, maxY, min.y));
        Gizmos.DrawLine(new Vector3(max.x, minY, min.y), new Vector3(max.x, maxY, min.y));
        Gizmos.DrawLine(new Vector3(max.x, minY, max.y), new Vector3(max.x, maxY, max.y));
        Gizmos.DrawLine(new Vector3(min.x, minY, max.y), new Vector3(min.x, maxY, max.y));

        // Draw cutter position marker
        Gizmos.color = Color.red;
        Vector3 toolPos = GetToolPosition();
        Gizmos.DrawSphere(toolPos, 0.01f);

        Gizmos.matrix = oldMatrix;
    }
}
