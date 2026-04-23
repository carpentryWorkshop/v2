// =============================================================================
//  CNC2LogoButton.cs  (optional companion to CNC2LogoManager)
//  Attach to a 3-D quad/plane GameObject inside the SelectionPanel.
//  The player presses P (PlayerInteraction.TryPressButton) while aiming at it
//  to select the corresponding logo, just like pressing a physical button.
//
//  Inspector setup:
//    SlotIndex – zero-based index matching the logo in CNC2LogoManager._logoSlots3D.
//    Call CNC2LogoManager.SelectLogoByIndex(SlotIndex) on Interact().
//
//  Alternatively, leave this script off and use number keys 1-9 instead
//  (built into CNC2LogoManager.Update()).
// =============================================================================

using UnityEngine;

public class CNC2LogoButton : Interactable
{
    [Tooltip("Zero-based slot index – must match the logo position in CNC2LogoManager")]
    [SerializeField] private int _slotIndex;

    [Header("Press highlight (optional)")]
    [SerializeField] private Renderer _renderer;
    [SerializeField] private Color    _pressColor = new Color(0.3f, 1f, 0.4f, 1f);

    private CNC2LogoManager _logoManager;
    private Color           _originalColor;

    private void Awake()
    {
        _logoManager = FindFirstObjectByType<CNC2LogoManager>();
        if (_renderer) _originalColor = _renderer.material.color;
    }

    public override void Interact()
    {
        if (_logoManager == null)
        {
            Debug.LogWarning("[CNC2LogoButton] CNC2LogoManager not found.");
            return;
        }
        _logoManager.SelectLogoByIndex(_slotIndex);
        StartCoroutine(FlashHighlight());
    }

    private System.Collections.IEnumerator FlashHighlight()
    {
        if (!_renderer) yield break;
        _renderer.material.color = _pressColor;
        yield return new WaitForSeconds(0.25f);
        _renderer.material.color = _originalColor;
    }
}
