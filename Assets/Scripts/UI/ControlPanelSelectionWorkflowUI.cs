using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Orchestrates the UI flow for the machine screen panels.
/// When CNC2UIManager is present in the scene, panel switching is deferred to
/// the CNC2 state machine. Logo selections are forwarded to CNC2LogoManager so
/// the state machine can advance correctly.
/// </summary>
public class ControlPanelSelectionWorkflowUI : MonoBehaviour
{
    [Header("Trigger Button")]
    [SerializeField] private CNC3DButton _btn3PhysicalButton;
    [SerializeField] private Transform _btn3Root;
    [SerializeField] private Camera _inputCamera;
    [SerializeField] private LayerMask _btn3RaycastMask = ~0;
    [SerializeField] private float _btn3RaycastDistance = 1000f;

    [Header("Panels")]
    [SerializeField] private GameObject _selectionPanel;
    [SerializeField] private GameObject _workflowPanel;
    [SerializeField] private bool _showSelectionPanelOnStart = false;

    [Header("Logo Buttons")]
    [SerializeField] private Button _logo1Button;
    [SerializeField] private Button _logo2Button;

    /// <summary>Last logo sprite selected by the user in SelectionPanel.</summary>
    public Sprite SelectedLogoSprite { get; private set; }

    /// <summary>Raised after a logo has been selected and panels switched to workflow.</summary>
    public event Action<Sprite> OnLogoSelected;

    // CNC2 state-machine components – auto-found; presence suppresses direct panel control
    private CNC2UIManager    _cnc2UIManager;
    private CNC2LogoManager  _cnc2LogoManager;

    private void Awake()
    {
        ResolveMissingReferences();
        ValidateReferences();

        _cnc2UIManager   = FindFirstObjectByType<CNC2UIManager>();
        _cnc2LogoManager = FindFirstObjectByType<CNC2LogoManager>();

        // Only take ownership of panel visibility when no state machine is driving it
        if (_cnc2UIManager == null)
        {
            if (_selectionPanel != null)
                _selectionPanel.SetActive(_showSelectionPanelOnStart);

            if (_workflowPanel != null)
                _workflowPanel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (_btn3PhysicalButton != null)
            _btn3PhysicalButton.AddPressListener(ShowSelectionPanel);

        if (_logo1Button != null)
            _logo1Button.onClick.AddListener(HandleLogo1Clicked);

        if (_logo2Button != null)
            _logo2Button.onClick.AddListener(HandleLogo2Clicked);
    }

    private void OnDisable()
    {
        if (_btn3PhysicalButton != null)
            _btn3PhysicalButton.RemovePressListener(ShowSelectionPanel);

        if (_logo1Button != null)
            _logo1Button.onClick.RemoveListener(HandleLogo1Clicked);

        if (_logo2Button != null)
            _logo2Button.onClick.RemoveListener(HandleLogo2Clicked);
    }

    public void ShowSelectionPanel()
    {
        // When CNC2UIManager is present, the state machine drives panel visibility.
        // Calling SetExclusivePanel here would hide the StartPanel that CNC2UIManager
        // just activated, so we skip it.
        if (_cnc2UIManager == null)
            SetExclusivePanel(_selectionPanel);

        Debug.Log("[ControlPanelSelectionWorkflowUI] btn3 clicked -> SelectionPanel shown.", this);
    }

    private void Update()
    {
        if (HandleLogoDirectMouseClick())
            return;

        HandleBtn3DirectMouseClick();
    }

    public void HandleLogo1Clicked()
    {
        HandleLogoClicked(_logo1Button != null ? _logo1Button : TryGetClickedButton());
    }

    public void HandleLogo2Clicked()
    {
        HandleLogoClicked(_logo2Button != null ? _logo2Button : TryGetClickedButton());
    }

    public void HandleAnyLogoClicked()
    {
        HandleLogoClicked(TryGetClickedButton());
    }

    private void HandleLogoClicked(Button clickedButton)
    {
        if (clickedButton == null)
            return;

        var sourceImage = clickedButton.image;
        if (sourceImage == null)
            Debug.LogWarning("[ControlPanelSelectionWorkflowUI] Clicked logo button has no Image component. Workflow will still continue.", this);

        SelectedLogoSprite = sourceImage != null ? sourceImage.sprite : null;

        // Forward the selection to CNC2LogoManager so the state machine advances
        // from Idle → LogoSelected.  Without this, Switch and Start are both ignored.
        if (_cnc2LogoManager != null)
        {
            int logoIndex = (clickedButton == _logo1Button) ? 0 : 1;
            _cnc2LogoManager.SelectLogoByIndex(logoIndex);
        }

        // Only switch panels directly when CNC2UIManager is not managing them.
        // When it is, the state machine will show WorkflowPanel on ModeSelected.
        if (_cnc2UIManager == null)
            SetExclusivePanel(_workflowPanel);

        OnLogoSelected?.Invoke(SelectedLogoSprite);
        Debug.Log("[ControlPanelSelectionWorkflowUI] Logo clicked -> WorkflowPanel shown.", this);
    }

    private void ValidateReferences()
    {
        if (_btn3PhysicalButton == null)
            Debug.LogWarning("[ControlPanelSelectionWorkflowUI] Assign btn3 CNC3DButton.", this);

        if (_selectionPanel == null)
            Debug.LogWarning("[ControlPanelSelectionWorkflowUI] SelectionPanel is not assigned.", this);

        if (_workflowPanel == null)
            Debug.LogWarning("[ControlPanelSelectionWorkflowUI] WorkflowPanel is not assigned.", this);

        if (_logo1Button == null)
            Debug.LogWarning("[ControlPanelSelectionWorkflowUI] logo1 Button is not assigned.", this);

        if (_logo2Button == null)
            Debug.LogWarning("[ControlPanelSelectionWorkflowUI] logo2 Button is not assigned.", this);
    }

    private void ResolveMissingReferences()
    {
        if (_btn3PhysicalButton == null)
            _btn3PhysicalButton = GetComponentInChildren<CNC3DButton>(true);

        if (_btn3Root == null && _btn3PhysicalButton != null)
            _btn3Root = _btn3PhysicalButton.transform;

        if (_inputCamera == null)
            _inputCamera = Camera.main;
    }

    private void HandleBtn3DirectMouseClick()
    {
        if (_btn3PhysicalButton == null)
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        // Do not process 3D button raycast when a UI click is already in progress.
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        Camera cam = _inputCamera != null ? _inputCamera : Camera.main;
        if (cam == null)
            return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, _btn3RaycastDistance, _btn3RaycastMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
            return;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            var collider = hits[i].collider;
            if (collider == null)
                continue;

            var clicked3DButton = collider.GetComponentInParent<CNC3DButton>();
            if (clicked3DButton == _btn3PhysicalButton)
            {
                ShowSelectionPanel();
                return;
            }

            if (_btn3Root != null && collider.transform.IsChildOf(_btn3Root))
            {
                ShowSelectionPanel();
                return;
            }
        }
    }

    private bool HandleLogoDirectMouseClick()
    {
        if (!Input.GetMouseButtonDown(0))
            return false;

        if (_selectionPanel == null || !_selectionPanel.activeInHierarchy)
            return false;

        Camera cam = _inputCamera != null ? _inputCamera : Camera.main;

        if (IsScreenPointInsideButton(_logo1Button, Input.mousePosition, cam))
        {
            HandleLogoClicked(_logo1Button);
            return true;
        }

        if (IsScreenPointInsideButton(_logo2Button, Input.mousePosition, cam))
        {
            HandleLogoClicked(_logo2Button);
            return true;
        }

        return false;
    }

    private static bool IsScreenPointInsideButton(Button button, Vector2 screenPoint, Camera cam)
    {
        if (button == null || !button.interactable || !button.gameObject.activeInHierarchy)
            return false;

        var rect = button.transform as RectTransform;
        if (rect == null)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, cam);
    }

    private static Button TryGetClickedButton()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null)
            return null;

        var selected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
        return selected != null ? selected.GetComponent<Button>() : null;
    }

    private void SetExclusivePanel(GameObject panelToShow)
    {
        if (panelToShow == null)
            return;

        Transform parent = panelToShow.transform.parent;
        if (parent == null)
        {
            panelToShow.SetActive(true);
            return;
        }

        foreach (Transform child in parent)
            child.gameObject.SetActive(child.gameObject == panelToShow);
    }
}
