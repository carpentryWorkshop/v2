// =============================================================================
//  CNC2LogoManager.cs
//  Dynamically loads logo sprites from Resources/CNC2Logos/ and displays
//  them in the SelectionPanel as 3-D quad renderers or UI Image slots.
//
//  Attach to:  a child of the SelectionPanel (or any CNC2 child).
//
//  Two display modes (choose one in the Inspector):
//    A) 3D Quad mode  – assign LogoSlots3D: an array of MeshRenderer components
//       on flat quad GameObjects placed on the physical screen. Logos are shown
//       by setting each renderer's main texture.
//    B) UI Image mode – assign LogoSlotsUI: an array of UnityEngine.UI.Image
//       components inside a World-Space Canvas on the SelectionPanel.
//
//  Selection input:
//    • Press number keys 1–9 to select the corresponding logo while the
//      SelectionPanel is visible (CNC2State = Idle or LogoSelected).
//    • Alternatively, attach CNC2LogoButton to each 3-D slot and it will
//      call SelectLogoByIndex() directly.
//
//  Resources folder setup:
//    1. Create folder  Assets/Resources/CNC2Logos/
//    2. Import your logo images there.
//    3. For texture-based toolpath generation: set Read/Write = enabled in
//       the Texture Import Settings of each image.
//    4. For Sprite display: set Texture Type = Sprite (2D and UI).
//
//  Events:
//    OnLogoSelected(string logoName) – subscribed by CNC2Controller.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CNC2LogoManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    [Header("─── Resources ───")]
    [Tooltip("Sub-folder inside Resources/ that holds the logo sprites/textures")]
    [SerializeField] private string _resourcesFolder = "CNC2Logos";

    [Header("─── Display: 3-D Quad Renderers (Mode A) ───")]
    [Tooltip("MeshRenderers on flat quads placed on the SelectionPanel screen")]
    [SerializeField] private Renderer[] _logoSlots3D;

    [Header("─── Display: UI Image slots (Mode B – World-Space Canvas) ───")]
    [Tooltip("Image components inside a World-Space Canvas on the SelectionPanel")]
    [SerializeField] private Image[] _logoSlotsUI;

    [Header("─── Selection Highlight ───")]
    [Tooltip("Emission/tint color applied to the selected slot in 3-D mode")]
    [SerializeField] private Color _selectedTint   = new Color(0.3f, 1f, 0.4f, 1f);
    [Tooltip("Tint color for unselected slots in 3-D mode")]
    [SerializeField] private Color _unselectedTint = Color.white;
    [Tooltip("Color of the selected slot's border in UI mode")]
    [SerializeField] private Color _selectedBorder = new Color(0.2f, 0.9f, 0.3f, 1f);

    // -------------------------------------------------------------------------
    /// <summary>Fired when the player selects a logo. Argument = resource name.</summary>
    public event Action<string> OnLogoSelected;

    // -------------------------------------------------------------------------
    // Internal state
    private readonly List<LogoEntry> _logos = new();
    private int _selectedIndex = -1;

    // Cached controller – used to gate keyboard input to valid states.
    private CNC2Controller _controller;

    private struct LogoEntry
    {
        public string    Name;
        public Sprite    Sprite;      // for UI mode
        public Texture2D Texture;     // for 3-D mode + toolpath generation
    }

    // =========================================================================
    private void Awake()
    {
        _controller = FindFirstObjectByType<CNC2Controller>();
    }

    private void Start()
    {
        LoadLogos();
    }

    // =========================================================================
    // Logo loading
    // =========================================================================

    private void LoadLogos()
    {
        // Load sprites first (works when Texture Type = Sprite in Import Settings)
        Sprite[] sprites = Resources.LoadAll<Sprite>(_resourcesFolder);

        // Fall back to plain Texture2D if no sprites were found
        Texture2D[] textures = sprites.Length == 0
            ? Resources.LoadAll<Texture2D>(_resourcesFolder)
            : System.Array.Empty<Texture2D>();

        if (sprites.Length == 0 && textures.Length == 0)
        {
            Debug.LogWarning($"[CNC2LogoManager] No assets found in Resources/{_resourcesFolder}/. " +
                             "Create the folder and add logo images.");
            return;
        }

        // Build unified LogoEntry list
        foreach (Sprite spr in sprites)
            _logos.Add(new LogoEntry { Name = spr.name, Sprite = spr, Texture = spr.texture });

        foreach (Texture2D tex in textures)
            _logos.Add(new LogoEntry { Name = tex.name, Texture = tex });

        PopulateDisplaySlots();
        Debug.Log($"[CNC2LogoManager] Loaded {_logos.Count} logo(s) from Resources/{_resourcesFolder}/.");
    }

    private void PopulateDisplaySlots()
    {
        // 3-D quad mode
        for (int i = 0; i < _logoSlots3D.Length && i < _logos.Count; i++)
        {
            if (_logoSlots3D[i] == null) continue;
            _logoSlots3D[i].material.mainTexture = _logos[i].Texture;
            _logoSlots3D[i].material.color       = _unselectedTint;
        }

        // UI Image mode
        for (int i = 0; i < _logoSlotsUI.Length && i < _logos.Count; i++)
        {
            if (_logoSlotsUI[i] == null) continue;
            if (_logos[i].Sprite != null)
                _logoSlotsUI[i].sprite = _logos[i].Sprite;
        }
    }

    // =========================================================================
    // Selection input (keyboard 1-9)
    // =========================================================================

    private void Update()
    {
        if (!SelectionAllowed()) return;

        // Number keys 1-9 map to logo indices 0-8
        for (int k = 0; k < 9 && k < _logos.Count; k++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + k))
            {
                SelectLogoByIndex(k);
                return;
            }
        }
    }

    private bool SelectionAllowed()
    {
        if (_controller == null) return false;
        CNC2State s = _controller.State;
        return s == CNC2State.Idle || s == CNC2State.LogoSelected || s == CNC2State.ModeSelected;
    }

    // -------------------------------------------------------------------------
    /// <summary>
    /// Select a logo by its list index.
    /// Can be called from CNC2LogoButton (3-D interactive button) or keyboard input.
    /// </summary>
    public void SelectLogoByIndex(int index)
    {
        if (index < 0 || index >= _logos.Count)
        {
            Debug.LogWarning($"[CNC2LogoManager] Index {index} out of range ({_logos.Count} logos).");
            return;
        }

        _selectedIndex = index;
        UpdateHighlight();
        OnLogoSelected?.Invoke(_logos[index].Name);
        Debug.Log($"[CNC2LogoManager] Selected logo: '{_logos[index].Name}'");
    }

    // -------------------------------------------------------------------------
    private void UpdateHighlight()
    {
        // 3-D quads: change material tint
        for (int i = 0; i < _logoSlots3D.Length && i < _logos.Count; i++)
        {
            if (_logoSlots3D[i])
                _logoSlots3D[i].material.color = (i == _selectedIndex)
                    ? _selectedTint : _unselectedTint;
        }

        // UI Images: change outline color
        for (int i = 0; i < _logoSlotsUI.Length && i < _logos.Count; i++)
        {
            if (_logoSlotsUI[i])
                _logoSlotsUI[i].color = (i == _selectedIndex)
                    ? _selectedBorder : Color.white;
        }
    }

    // =========================================================================
    // Public accessors used by CNC2AutoEngraver
    // =========================================================================

    /// <returns>The Texture2D of the currently selected logo, or null.</returns>
    public Texture2D GetSelectedTexture()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _logos.Count) return null;
        return _logos[_selectedIndex].Texture;
    }

    /// <returns>The Sprite of the currently selected logo, or null.</returns>
    public Sprite GetSelectedSprite()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _logos.Count) return null;
        return _logos[_selectedIndex].Sprite;
    }

    public int LogoCount => _logos.Count;
}
