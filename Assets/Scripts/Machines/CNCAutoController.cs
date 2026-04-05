using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls automatic shape cutting in Auto mode.
/// When activated, moves the CNC tool head along a predefined path
/// to cut the selected shape.
///
/// Attach this to the CNC machine root alongside <see cref="CNCMachine"/>.
/// </summary>
public class CNCAutoController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The CNC machine state controller.")]
    [SerializeField] private CNCMachine _machine;

    [Tooltip("Transform of the cncCutter (moves on X-axis).")]
    [SerializeField] private Transform _cutterTransform;

    [Tooltip("Transform of the spindleHolder (moves on Z-axis).")]
    [SerializeField] private Transform _spindleHolderTransform;

    [Tooltip("Transform of the spindleFinal (moves on Y-axis).")]
    [SerializeField] private Transform _spindleTransform;

    [Tooltip("Cutting path bounds configuration.")]
    [SerializeField] private CuttingPath _cuttingPath;

    [Header("Auto-Cut Settings")]
    [Tooltip("Movement speed during auto-cutting (units per second).")]
    [SerializeField] [Range(0.01f, 0.5f)] private float _autoSpeed = 0.1f;

    [Tooltip("Size of the shape to cut (relative to work area).")]
    [SerializeField] [Range(0.1f, 0.8f)] private float _shapeSize = 0.5f;

    [Tooltip("Cutting depth (Y position when cutting).")]
    [SerializeField] private float _cutDepth = -0.02f;

    [Tooltip("Idle height (Y position when not cutting).")]
    [SerializeField] private float _idleHeight = 0.05f;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fires when auto-cutting starts.</summary>
    public event Action OnAutoCutStarted;

    /// <summary>Fires when auto-cutting completes.</summary>
    public event Action OnAutoCutComplete;

    /// <summary>Fires when the selected shape changes.</summary>
    public event Action<ShapePathGenerator.ShapeType> OnShapeChanged;

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>Currently selected shape type.</summary>
    public ShapePathGenerator.ShapeType SelectedShape { get; private set; } = ShapePathGenerator.ShapeType.Rectangle;

    /// <summary>Whether auto-cutting is currently in progress.</summary>
    public bool IsAutoCutting { get; private set; }

    /// <summary>Current progress through the path (0-1).</summary>
    public float Progress { get; private set; }

    // ── Private state ─────────────────────────────────────────────────────────

    private List<Vector3> _currentPath;
    private Coroutine _autoCutCoroutine;
    private Vector3 _cutterStartLocal;
    private Vector3 _spindleHolderStartLocal;
    private Vector3 _spindleStartLocal;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (_machine == null)
            _machine = GetComponent<CNCMachine>();

        CacheStartPositions();
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

        StopAutoCut();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the shape to cut in auto mode.
    /// </summary>
    public void SelectShape(ShapePathGenerator.ShapeType shape)
    {
        Debug.Log($"[CNCAutoController] Shape selected: {shape}");
        SelectedShape = shape;
        OnShapeChanged?.Invoke(shape);
    }

    /// <summary>
    /// Cycles to the next shape type.
    /// </summary>
    public void NextShape()
    {
        int current = (int)SelectedShape;
        int count = System.Enum.GetValues(typeof(ShapePathGenerator.ShapeType)).Length;
        SelectShape((ShapePathGenerator.ShapeType)((current + 1) % count));
    }

    /// <summary>
    /// Starts the auto-cutting process for the selected shape.
    /// Called by CNCMachine when in Auto mode and StartCut() is invoked.
    /// </summary>
    public void StartAutoCut()
    {
        if (IsAutoCutting) return;
        if (_cuttingPath == null)
        {
            Debug.LogWarning("[CNCAutoController] No CuttingPath assigned.", this);
            return;
        }

        Debug.Log($"[CNCAutoController] Starting auto engrave: {SelectedShape}");

        // Generate the path
        Vector2 center = new Vector2(
            (_cuttingPath.WorkAreaMin.x + _cuttingPath.WorkAreaMax.x) / 2f,
            (_cuttingPath.WorkAreaMin.y + _cuttingPath.WorkAreaMax.y) / 2f
        );

        float actualSize = _shapeSize * Mathf.Min(_cuttingPath.Width, _cuttingPath.Depth);

        _currentPath = ShapePathGenerator.GeneratePath(
            SelectedShape,
            actualSize,
            center,
            _cutDepth,
            _idleHeight
        );

        if (_currentPath.Count < 2)
        {
            Debug.LogWarning("[CNCAutoController] Generated path is too short.", this);
            return;
        }

        Debug.Log($"[CNCAutoController] Path generated with {_currentPath.Count} points.");
        _autoCutCoroutine = StartCoroutine(AutoCutRoutine());
    }

    /// <summary>
    /// Stops the auto-cutting process.
    /// </summary>
    public void StopAutoCut()
    {
        if (_autoCutCoroutine != null)
        {
            Debug.Log("[CNCAutoController] Auto engrave CANCELLED");
            StopCoroutine(_autoCutCoroutine);
            _autoCutCoroutine = null;
        }

        IsAutoCutting = false;
        Progress = 0f;
    }

    /// <summary>
    /// Returns the tool head to its starting position.
    /// </summary>
    public void ReturnToHome()
    {
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

    private void HandleStateChanged(CNCMachine.CNCState newState)
    {
        // Auto-cut is managed by CNCMachine calling StartAutoCut/StopAutoCut
        if (newState == CNCMachine.CNCState.Done || newState == CNCMachine.CNCState.Idle)
        {
            StopAutoCut();
        }
    }

    private IEnumerator AutoCutRoutine()
    {
        IsAutoCutting = true;
        Progress = 0f;
        OnAutoCutStarted?.Invoke();

        int pathIndex = 0;
        Vector3 currentTarget = _currentPath[0];

        Debug.Log($"[CNCAutoController] Engraving point 1/{_currentPath.Count}");

        // Move to first point
        yield return StartCoroutine(MoveToPosition(currentTarget));

        // Follow the path
        for (int i = 1; i < _currentPath.Count; i++)
        {
            pathIndex = i;
            currentTarget = _currentPath[i];

            // Log progress every 10 points or at key milestones
            if (i % 10 == 0 || i == _currentPath.Count - 1)
            {
                Debug.Log($"[CNCAutoController] Engraving point {i + 1}/{_currentPath.Count}");
            }

            yield return StartCoroutine(MoveToPosition(currentTarget));

            Progress = (float)i / (_currentPath.Count - 1);
        }

        IsAutoCutting = false;
        Progress = 1f;
        
        Debug.Log("[CNCAutoController] Auto engrave COMPLETED");
        OnAutoCutComplete?.Invoke();

        // Notify machine that cut is complete
        _machine?.StopCut();
    }

    private IEnumerator MoveToPosition(Vector3 targetLocal)
    {
        // Target position is in local space of the work area
        // We need to distribute movement across the three transforms

        Vector3 currentPos = GetCurrentToolPosition();

        while (Vector3.Distance(currentPos, targetLocal) > 0.001f)
        {
            currentPos = GetCurrentToolPosition();
            Vector3 direction = (targetLocal - currentPos).normalized;
            float step = _autoSpeed * Time.deltaTime;

            Vector3 newPos = Vector3.MoveTowards(currentPos, targetLocal, step);
            SetToolPosition(newPos);

            yield return null;
        }

        // Snap to exact position
        SetToolPosition(targetLocal);
    }

    private Vector3 GetCurrentToolPosition()
    {
        float x = _cutterTransform != null ? _cutterTransform.localPosition.x : 0f;
        float z = _spindleHolderTransform != null ? _spindleHolderTransform.localPosition.z : 0f;
        float y = _spindleTransform != null ? _spindleTransform.localPosition.y : 0f;

        return new Vector3(x, y, z);
    }

    private void SetToolPosition(Vector3 position)
    {
        // X-axis: cutter
        if (_cutterTransform != null)
        {
            Vector3 cutterPos = _cutterTransform.localPosition;
            cutterPos.x = position.x;
            _cutterTransform.localPosition = cutterPos;
        }

        // Z-axis: spindle holder
        if (_spindleHolderTransform != null)
        {
            Vector3 holderPos = _spindleHolderTransform.localPosition;
            holderPos.z = position.z;
            _spindleHolderTransform.localPosition = holderPos;
        }

        // Y-axis: spindle
        if (_spindleTransform != null)
        {
            Vector3 spindlePos = _spindleTransform.localPosition;
            spindlePos.y = position.y;
            _spindleTransform.localPosition = spindlePos;
        }
    }
}
