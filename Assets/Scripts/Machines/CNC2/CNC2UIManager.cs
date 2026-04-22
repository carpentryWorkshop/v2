// =============================================================================
//  CNC2UIManager.cs
//  Manages the three physical screen panels on the CNC2 control panel.
//
//  Attach to:  MachineScreenCanvas (or any parent of the three panels).
//
//  Scene panel structure (already present in scene):
//    MachineScreenCanvas
//      ├── StartPanel          → active in Idle state  (shows "Start Engraving")
//      │     └── StartButton
//      │           └── Text (TMP)   ← _startText
//      ├── SelectionPanel      → active in Idle + LogoSelected states
//      │     ├── Text (TMP)        ← header (not touched)
//      │     └── LogoGrid
//      │           ├── logo1   ← logo button 0
//      │           └── logo2   ← logo button 1
//      └── WorkflowPanel       → active from ModeSelected onward
//            ├── Text (TMP)        ← _wfMainText  (state/mode/logo/progress)
//            ├── selectedLogoPreview  ← _wfLogoPreview (logo sprite)
//            └── StartButton (1)   ← (wired by CNC2LogoManager or ignored)
// =============================================================================

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CNC2UIManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    [Header("─── Screen Panel Root GameObjects ───")]
    [Tooltip("Shown when machine first powers on (Idle state)")]
    [SerializeField] private GameObject _startPanel;
    [Tooltip("Shown while player selects a logo (Idle + LogoSelected states)")]
    [SerializeField] private GameObject _selectionPanel;
    [Tooltip("Shown from ModeSelected onward – displays live run info")]
    [SerializeField] private GameObject _workflowPanel;

    [Header("─── StartPanel ───")]
    [Tooltip("The Text(TMP) on the StartPanel's button – shows 'CNC ONLINE'")]
    [SerializeField] private TextMeshProUGUI _startText;

    [Header("─── WorkflowPanel ───")]
    [Tooltip("The Text(TMP) directly inside WorkflowPanel – shows all run info")]
    [SerializeField] private TextMeshProUGUI _wfMainText;
    [Tooltip("'selectedLogoPreview' Image – shows the chosen logo sprite")]
    [SerializeField] private Image _wfLogoPreview;

    [Header("─── Splash ───")]
    [Tooltip("Seconds the StartPanel (boot splash) stays visible before the SelectionPanel appears")]
    [SerializeField] private float _splashDuration = 2f;

    // -------------------------------------------------------------------------
    private CNC2Controller       _controller;
    private Coroutine            _splashCoroutine;
    private CNC2AutoEngraver     _autoEngraver;
    private CNC2SpindleController _spindle;
    private CNC2LogoManager      _logoManager;

    // -------------------------------------------------------------------------
    private void Awake()
    {
        _controller   = FindFirstObjectByType<CNC2Controller>();
        _autoEngraver = FindFirstObjectByType<CNC2AutoEngraver>();
        _spindle      = FindFirstObjectByType<CNC2SpindleController>();
        _logoManager  = FindFirstObjectByType<CNC2LogoManager>();

        if (_controller)
        {
            _controller.OnStateChanged += HandleStateChanged;
            _controller.OnModeChanged  += _ => RefreshWorkflowPanel();
        }
        else
            Debug.LogWarning("[CNC2UIManager] CNC2Controller not found in scene.");

        // All panels off at start (machine is PoweredOff)
        SetActive(_startPanel,     false);
        SetActive(_selectionPanel, false);
        SetActive(_workflowPanel,  false);
    }

    // -------------------------------------------------------------------------
    private void Update()
    {
        if (_controller == null) return;
        CNC2State s = _controller.State;
        if (s == CNC2State.AutoRunning || s == CNC2State.ManualRunning)
            RefreshWorkflowPanel();
    }

    // =========================================================================
    // State-driven panel switching
    // =========================================================================

    private void HandleStateChanged(CNC2State state)
    {
        // Cancel any pending splash-to-selection transition
        if (_splashCoroutine != null)
        {
            StopCoroutine(_splashCoroutine);
            _splashCoroutine = null;
        }

        switch (state)
        {
            case CNC2State.Idle:
                // Show boot splash, then auto-switch to SelectionPanel
                SetActive(_startPanel,     true);
                SetActive(_selectionPanel, false);
                SetActive(_workflowPanel,  false);
                RefreshStartPanel();
                _splashCoroutine = StartCoroutine(SplashThenSelection());
                break;

            case CNC2State.LogoSelected:
                SetActive(_startPanel,     false);
                SetActive(_selectionPanel, true);
                SetActive(_workflowPanel,  false);
                break;

            case CNC2State.ModeSelected:
            case CNC2State.AutoRunning:
            case CNC2State.ManualRunning:
            case CNC2State.Stopped:
            case CNC2State.Finished:
                SetActive(_startPanel,     false);
                SetActive(_selectionPanel, false);
                SetActive(_workflowPanel,  true);
                RefreshWorkflowPanel();
                break;

            default: // PoweredOff
                SetActive(_startPanel,     false);
                SetActive(_selectionPanel, false);
                SetActive(_workflowPanel,  false);
                break;
        }
    }

    private IEnumerator SplashThenSelection()
    {
        yield return new WaitForSeconds(_splashDuration);
        // Only switch if the machine is still Idle (player hasn't pressed anything)
        if (_controller != null && _controller.State == CNC2State.Idle)
        {
            SetActive(_startPanel,     false);
            SetActive(_selectionPanel, true);
        }
        _splashCoroutine = null;
    }

    // -------------------------------------------------------------------------
    private void RefreshStartPanel()
    {
        if (_startText) _startText.text = "CNC ONLINE\nStart Engraving";
    }

    // -------------------------------------------------------------------------
    private void RefreshWorkflowPanel()
    {
        if (_controller == null) return;

        CNC2State state    = _controller.State;
        CNC2Mode  mode     = _controller.Mode;
        string    logoName = _controller.SelectedLogoName;

        float progress = (_autoEngraver != null && mode == CNC2Mode.Auto)
                         ? _autoEngraver.Progress : 0f;

        // All info packed into the single WorkflowPanel text field
        string posInfo = string.Empty;
        if (_spindle != null && mode == CNC2Mode.Manual)
        {
            Vector3 n = _spindle.GetNormalisedPosition();
            posInfo = $"\nX:{n.x:F2}  Y:{n.y:F2}  Z:{n.z:F2}";
        }

        if (_wfMainText)
        {
            _wfMainText.text =
                $"{FormatState(state)}\n" +
                $"Mode  : {mode}\n" +
                $"Logo  : {(logoName.Length > 0 ? logoName : "—")}\n" +
                $"Progress : {progress * 100f:F0} %" +
                posInfo;
        }

        // Show logo preview sprite in the selectedLogoPreview Image
        if (_wfLogoPreview && _logoManager != null)
        {
            Sprite spr = _logoManager.GetSelectedSprite();
            _wfLogoPreview.sprite  = spr;
            _wfLogoPreview.enabled = spr != null;
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static string FormatState(CNC2State s) => s switch
    {
        CNC2State.PoweredOff    => "[ OFFLINE ]",
        CNC2State.Idle          => "[ READY ]",
        CNC2State.LogoSelected  => "[ LOGO SELECTED ]",
        CNC2State.ModeSelected  => "[ AWAITING START ]",
        CNC2State.AutoRunning   => ">>> AUTO - ENGRAVING",
        CNC2State.ManualRunning => ">>> MANUAL - RUNNING",
        CNC2State.Stopped       => "!!! STOPPED !!!",
        CNC2State.Finished      => "=== FINISHED ===",
        _                       => s.ToString()
    };

    private static void SetActive(GameObject go, bool active)
    {
        if (go) go.SetActive(active);
    }
}
