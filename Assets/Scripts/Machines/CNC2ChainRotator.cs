using UnityEngine;

/// <summary>
/// Rotates a CNC2 decorative chain (belt, gear, etc.) while the machine is running.
///
/// Supports two drive sources — assign whichever is present in the scene:
///   • CNC2Controller (new CNC2 state machine)  – responds to AutoRunning / ManualRunning.
///   • CNCMachine + CNCAutoController (legacy)  – responds to Positioning / Cutting states.
///
/// Attach to: the chain / belt / gear GameObject that should rotate.
/// Inspector: set Axis and SpeedDegreesPerSecond to taste (220 is a good default).
/// </summary>
public class CNC2ChainRotator : MonoBehaviour
{
    public enum RotationAxis
    {
        X,
        Y,
        Z
    }

    [Header("References – CNC2 state machine (preferred)")]
    [Tooltip("Assign the CNC2Controller root. When set, this drives rotation.")]
    [SerializeField] private CNC2Controller _cnc2Controller;

    [Header("References – Legacy CNCMachine (fallback)")]
    [SerializeField] private CNCMachine machine;
    [SerializeField] private CNCAutoController autoController;

    [Header("Rotation")]
    [SerializeField] private RotationAxis axis = RotationAxis.Y;
    [SerializeField] [Range(10f, 1080f)] private float speedDegreesPerSecond = 220f;

    private bool shouldRotate;

    private void Awake()
    {
        // Auto-discover CNC2Controller in parent hierarchy first
        if (_cnc2Controller == null)
            _cnc2Controller = GetComponentInParent<CNC2Controller>();

        // Legacy fallbacks
        if (machine == null)
            machine = GetComponentInParent<CNCMachine>();
        if (autoController == null)
            autoController = GetComponentInParent<CNCAutoController>();
    }

    private void OnEnable()
    {
        // CNC2 path (preferred)
        if (_cnc2Controller != null)
            _cnc2Controller.OnStateChanged += HandleCNC2StateChanged;

        // Legacy path
        if (machine != null)
            machine.OnStateChanged += HandleMachineStateChanged;

        if (autoController != null)
        {
            autoController.OnAutoCutStarted  += StartRotation;
            autoController.OnAutoCutComplete += StopRotation;
        }
    }

    private void OnDisable()
    {
        if (_cnc2Controller != null)
            _cnc2Controller.OnStateChanged -= HandleCNC2StateChanged;

        if (machine != null)
            machine.OnStateChanged -= HandleMachineStateChanged;

        if (autoController != null)
        {
            autoController.OnAutoCutStarted  -= StartRotation;
            autoController.OnAutoCutComplete -= StopRotation;
        }

        shouldRotate = false;
    }

    // Responds to the CNC2 state machine
    private void HandleCNC2StateChanged(CNC2State state)
    {
        switch (state)
        {
            case CNC2State.AutoRunning:
            case CNC2State.ManualRunning:
                StartRotation();
                break;
            default:
                StopRotation();
                break;
        }
    }

    private void Update()
    {
        if (!shouldRotate)
            return;

        transform.Rotate(GetAxisVector(), speedDegreesPerSecond * Time.deltaTime, Space.Self);
    }

    public void StartRotation()
    {
        shouldRotate = true;
    }

    public void StopRotation()
    {
        shouldRotate = false;
    }

    private void HandleMachineStateChanged(CNCMachine.CNCState newState)
    {
        switch (newState)
        {
            case CNCMachine.CNCState.Positioning:
            case CNCMachine.CNCState.Cutting:
                StartRotation();
                break;
            case CNCMachine.CNCState.Idle:
            case CNCMachine.CNCState.Done:
                StopRotation();
                break;
        }
    }

    private Vector3 GetAxisVector()
    {
        switch (axis)
        {
            case RotationAxis.X:
                return Vector3.right;
            case RotationAxis.Z:
                return Vector3.forward;
            default:
                return Vector3.up;
        }
    }
}
