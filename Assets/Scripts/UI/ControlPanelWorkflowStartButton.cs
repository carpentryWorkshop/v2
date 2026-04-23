using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Handles workflow start button click only via Button.onClick.
/// Optionally starts CNCMachine and forwards selected logo sprite via UnityEvent.
/// </summary>
public class ControlPanelWorkflowStartButton : MonoBehaviour
{
    [Header("Start Trigger")]
    [SerializeField] private Button _startButton;

    [Header("Workflow Dependencies")]
    [SerializeField] private ControlPanelSelectionWorkflowUI _workflowUI;
    [SerializeField] private CNCMachine _machine;

    // CNC2 state machine – auto-found; when present, TryStart() is called instead
    // of CNCMachine.StartCut() so that ManualRunning/AutoRunning are entered properly.
    private CNC2Controller _cnc2Controller;

    [Header("Workflow Events")]
    [SerializeField] private SpriteEvent _onStartWorkflow;

    [System.Serializable]
    public class SpriteEvent : UnityEvent<Sprite>
    {
    }

    private void Awake()
    {
        if (_startButton == null)
            Debug.LogWarning("[ControlPanelWorkflowStartButton] StartButton is not assigned.", this);

        if (_workflowUI == null)
            Debug.LogWarning("[ControlPanelWorkflowStartButton] Workflow UI controller is not assigned.", this);

        _cnc2Controller = FindFirstObjectByType<CNC2Controller>();
    }

    private void OnEnable()
    {
        if (_startButton != null)
            _startButton.onClick.AddListener(HandleStartClicked);
    }

    private void OnDisable()
    {
        if (_startButton != null)
            _startButton.onClick.RemoveListener(HandleStartClicked);
    }

    public void HandleStartClicked()
    {
        // CNC2 path: delegate to the state machine so ManualRunning/AutoRunning
        // is properly entered and the spindle/engraver sub-systems are activated.
        if (_cnc2Controller != null)
        {
            _cnc2Controller.TryStart();

            Sprite selectedLogo = _workflowUI != null ? _workflowUI.SelectedLogoSprite : null;
            _onStartWorkflow?.Invoke(selectedLogo);
            return;
        }

        // Legacy path: old CNCMachine (no CNC2Controller in scene)
        Sprite logo = _workflowUI != null ? _workflowUI.SelectedLogoSprite : null;
        if (logo == null)
        {
            Debug.LogWarning("[ControlPanelWorkflowStartButton] Start blocked: no logo selected.", this);
            return;
        }

        _onStartWorkflow?.Invoke(logo);

        if (_machine != null)
            _machine.StartCut();
    }
}
