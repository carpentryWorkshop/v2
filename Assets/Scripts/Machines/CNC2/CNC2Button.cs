// =============================================================================
//  CNC2Button.cs
//  Physical 3D button on the CNC2 control panel.
//  Attach to: Power, switch, Start, or Stop GameObjects (children of ctrl).
//
//  Works with PlayerInteraction (P key → raycast → Interactable.Interact()).
//  On press, dispatches to the matching CNC2Controller.TryXxx() method based
//  on ButtonId.  Press animation + optional emission flash are local.
//
//  NOTE: You can also use CNC3DButton on these GameObjects and wire its
//  _onPressed UnityEvent to CNC2Controller.TryPower / TrySwitch / etc.
//  CNC2Button is the simpler drop-in that needs no manual inspector wiring.
//
//  Inspector setup:
//    ButtonId      – type exactly: "Power" | "switch" | "Start" | "Stop"
//    PressDepth    – local –Y travel when pressed (metres, e.g. 0.004)
//    PressDuration – full press+release time in seconds (e.g. 0.14)
// =============================================================================

using System.Collections;
using UnityEngine;

public class CNC2Button : Interactable
{
    [Header("Identity")]
    [Tooltip("Must match exactly: Power | switch | Start | Stop")]
    public string ButtonId = "Power";

    [Header("Press Animation")]
    [Tooltip("Local -Y travel when pressed (metres)")]
    [SerializeField] private float _pressDepth    = 0.004f;
    [Tooltip("Total seconds for the press-and-release animation")]
    [SerializeField] private float _pressDuration = 0.14f;

    [Header("Visual Feedback (optional)")]
    [Tooltip("Renderer whose emission is flashed on press")]
    [SerializeField] private Renderer _indicatorRenderer;
    [SerializeField] private Color    _pressedEmission = Color.green;

    // -------------------------------------------------------------------------
    private CNC2Controller _controller;
    private Vector3 _restLocalPos;
    private bool    _isAnimating;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    // -------------------------------------------------------------------------
    private void Awake()
    {
        _restLocalPos = transform.localPosition;
        _controller   = FindFirstObjectByType<CNC2Controller>();

        if (_controller == null)
            Debug.LogWarning($"[CNC2Button '{ButtonId}'] CNC2Controller not found in scene. " +
                             "Ensure the CNC2 root GameObject is in the scene.");
    }

    // -------------------------------------------------------------------------
    /// <summary>Called by PlayerInteraction when the player aims and presses P.</summary>
    public override void Interact()
    {
        if (_isAnimating) return;
        Dispatch();
        StartCoroutine(PressAnimation());
    }

    // -------------------------------------------------------------------------
    /// <summary>Routes this button's press to the correct controller method.</summary>
    private void Dispatch()
    {
        if (_controller == null) return;

        switch (ButtonId)
        {
            case "Power":  _controller.TryPower();  break;
            case "switch": _controller.TrySwitch(); break;
            case "Start":  _controller.TryStart();  break;
            case "Stop":   _controller.TryStop();   break;
            default:
                Debug.LogWarning($"[CNC2Button] Unknown ButtonId: '{ButtonId}'. " +
                                 "Valid values: Power | switch | Start | Stop");
                break;
        }
    }

    // -------------------------------------------------------------------------
    private IEnumerator PressAnimation()
    {
        _isAnimating = true;
        Vector3 pressedPos = _restLocalPos + new Vector3(0f, -_pressDepth, 0f);
        float   halfTime   = _pressDuration * 0.5f;

        // depress
        for (float t = 0f; t < 1f; t += Time.deltaTime / halfTime)
        {
            transform.localPosition = Vector3.Lerp(_restLocalPos, pressedPos, t);
            yield return null;
        }

        // flash emission
        if (_indicatorRenderer)
        {
            _indicatorRenderer.material.EnableKeyword("_EMISSION");
            _indicatorRenderer.material.SetColor(EmissionColor, _pressedEmission);
        }

        // release
        for (float t = 0f; t < 1f; t += Time.deltaTime / halfTime)
        {
            transform.localPosition = Vector3.Lerp(pressedPos, _restLocalPos, t);
            yield return null;
        }

        transform.localPosition = _restLocalPos;

        if (_indicatorRenderer)
            _indicatorRenderer.material.SetColor(EmissionColor, Color.black);

        _isAnimating = false;
    }
}
