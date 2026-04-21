using UnityEngine;

/// <summary>
/// Rotates CNC2 chain while the machine auto-process is running.
/// Starts on Positioning/Cutting and stops on Idle/Done.
/// </summary>
public class CNC2ChainRotator : MonoBehaviour
{
    public enum RotationAxis
    {
        X,
        Y,
        Z
    }  

    [Header("References")]
    [SerializeField] private CNCMachine machine;
    [SerializeField] private CNCAutoController autoController;

    [Header("Rotation")]
    [SerializeField] private RotationAxis axis = RotationAxis.Y;
    [SerializeField] [Range(10f, 1080f)] private float speedDegreesPerSecond = 220f;

    private bool shouldRotate;

    private void Awake()
    {
        if (machine == null)
            machine = GetComponentInParent<CNCMachine>();

        if (autoController == null)
            autoController = GetComponentInParent<CNCAutoController>();
    }

    private void OnEnable()
    {
        if (machine != null)
            machine.OnStateChanged += HandleMachineStateChanged;

        if (autoController != null)
        {
            autoController.OnAutoCutStarted += StartRotation;
            autoController.OnAutoCutComplete += StopRotation;
        }
    }

    private void OnDisable()
    {
        if (machine != null)
            machine.OnStateChanged -= HandleMachineStateChanged;

        if (autoController != null)
        {
            autoController.OnAutoCutStarted -= StartRotation;
            autoController.OnAutoCutComplete -= StopRotation;
        }

        shouldRotate = false;
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
