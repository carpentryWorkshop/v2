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
        Sprite selectedLogo = _workflowUI != null ? _workflowUI.SelectedLogoSprite : null;

        if (selectedLogo == null)
        {
            Debug.LogWarning("[ControlPanelWorkflowStartButton] Start blocked: no logo selected.", this);
            return;
        }

        _onStartWorkflow?.Invoke(selectedLogo);

        if (_machine != null)
            _machine.StartCut();
    }
}
