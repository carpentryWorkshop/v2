using UnityEngine;

/// <summary>
/// Rotates the meche (drill bit) around its local axis when the CNC machine is running.
/// Simulates the spinning of the cutting tool during operation.
///
/// Attach this to the 'meche' GameObject inside the CNC hierarchy:
///   cncCutter > spindleHolder > spindleFinal > meche
///
/// The rotation starts when the CNC machine enters Positioning or Cutting state,
/// and stops when it returns to Idle or Done.
/// </summary>
public class MecheRotator : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The CNC machine to monitor for state changes. If null, searches in parent.")]
    [SerializeField] private CNCMachine _machine;

    [Header("Rotation Settings")]
    [Tooltip("Rotation speed in RPM (revolutions per minute).")]
    [SerializeField] [Range(100f, 10000f)] private float _rpm = 3000f;

    [Tooltip("The local axis to rotate around.")]
    [SerializeField] private Vector3 _rotationAxis = Vector3.up;

    [Tooltip("Spin-up time in seconds (how long to reach full speed).")]
    [SerializeField] [Range(0f, 2f)] private float _spinUpTime = 0.5f;

    [Tooltip("Spin-down time in seconds (how long to stop).")]
    [SerializeField] [Range(0f, 2f)] private float _spinDownTime = 0.3f;

    // ── Private state ─────────────────────────────────────────────────────────

    private bool _shouldSpin;
    private float _currentSpeedMultiplier;
    private float _targetSpeedMultiplier;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (_machine == null)
            _machine = GetComponentInParent<CNCMachine>();

        if (_machine == null)
            Debug.LogWarning($"[MecheRotator] No CNCMachine found in parent hierarchy of {name}. " +
                             "Assign it manually in the Inspector.", this);
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
    }

    private void Update()
    {
        UpdateSpeedMultiplier();
        Rotate();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Manually start the rotation (useful for testing without CNCMachine).
    /// </summary>
    public void StartSpinning()
    {
        if (!_shouldSpin)
        {
            Debug.Log("[MecheRotator] Rotation STARTED");
        }
        _shouldSpin = true;
        _targetSpeedMultiplier = 1f;
    }

    /// <summary>
    /// Manually stop the rotation.
    /// </summary>
    public void StopSpinning()
    {
        if (_shouldSpin)
        {
            Debug.Log("[MecheRotator] Rotation STOPPED");
        }
        _shouldSpin = false;
        _targetSpeedMultiplier = 0f;
    }

    /// <summary>
    /// Set the rotation speed at runtime.
    /// </summary>
    public void SetRPM(float rpm)
    {
        _rpm = Mathf.Max(0f, rpm);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void HandleStateChanged(CNCMachine.CNCState newState)
    {
        switch (newState)
        {
            case CNCMachine.CNCState.Positioning:
            case CNCMachine.CNCState.Cutting:
                StartSpinning();
                break;

            case CNCMachine.CNCState.Idle:
            case CNCMachine.CNCState.Done:
                StopSpinning();
                break;
        }
    }

    private void UpdateSpeedMultiplier()
    {
        float transitionTime = _shouldSpin ? _spinUpTime : _spinDownTime;

        if (transitionTime <= 0f)
        {
            _currentSpeedMultiplier = _targetSpeedMultiplier;
        }
        else
        {
            float speed = 1f / transitionTime;
            _currentSpeedMultiplier = Mathf.MoveTowards(
                _currentSpeedMultiplier,
                _targetSpeedMultiplier,
                speed * Time.deltaTime
            );
        }
    }

    private void Rotate()
    {
        if (_currentSpeedMultiplier <= 0.001f) return;

        // Convert RPM to degrees per second
        float degreesPerSecond = (_rpm / 60f) * 360f;
        float rotationThisFrame = degreesPerSecond * _currentSpeedMultiplier * Time.deltaTime;

        transform.Rotate(_rotationAxis.normalized, rotationThisFrame, Space.Self);
    }
}
