using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders a real-time top-down 2D preview of the CNC work area onto a
/// <see cref="RenderTexture"/>, which is then displayed on the Screen.obj mesh
/// via its material.
///
/// Supports two display modes:
///   - Manual Mode: Shows cutter position + trail line
///   - Auto Mode: Shows shape selection UI with 4 clickable regions
///
/// What is drawn each frame (using the GL immediate-mode API on a Camera):
///   - A border rectangle representing the work-area bounds
///   - A crosshair dot showing the current cutter position
///   - A continuous trail line tracing where the cutter has been
///   - Shape selection icons (in Auto mode)
///
/// Setup in the scene:
///   1. Attach this script to the Screen.obj GameObject.
///   2. Create a RenderTexture asset (e.g. 512x512, R8G8B8 format) and assign it
///      to <see cref="_renderTexture"/>.
///   3. Create a URP/Lit (or Unlit) material, set its Base Map to that same
///      RenderTexture, and assign it to the Screen mesh renderer.
///   4. Assign the <see cref="CNCCutter"/>, <see cref="CNCMachine"/>, and 
///      <see cref="CuttingPath"/> references.
/// </summary>
public class CNCScreenDisplay : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The CNCMachine to monitor for mode changes.")]
    [SerializeField] private CNCMachine _machine;

    [Tooltip("The CNCCutter whose position this display tracks.")]
    [SerializeField] private CNCCutter _cutter;

    [Tooltip("The CNCAutoController for shape selection.")]
    [SerializeField] private CNCAutoController _autoController;

    [Tooltip("ScriptableObject defining the work area (must match the one on CNCCutter).")]
    [SerializeField] private CuttingPath _cuttingPath;

    [Tooltip("RenderTexture that this display draws into. Assign to the Screen mesh material.")]
    [SerializeField] private RenderTexture _renderTexture;

    [Tooltip("Optional dedicated orthographic Camera for GL rendering.")]
    [SerializeField] private Camera _drawCamera;

    [Header("Display Settings")]
    [Tooltip("Background colour of the display screen.")]
    [SerializeField] private Color _backgroundColor = new Color(0.05f, 0.05f, 0.1f, 1f);

    [Tooltip("Colour of the work-area border rectangle.")]
    [SerializeField] private Color _borderColor = new Color(0f, 0.8f, 1f, 1f);

    [Tooltip("Colour of the cut trail line.")]
    [SerializeField] private Color _trailColor = new Color(1f, 0.4f, 0f, 1f);

    [Tooltip("Colour of the cutter position crosshair.")]
    [SerializeField] private Color _cursorColor = Color.white;

    [Tooltip("Crosshair size as a fraction of the display width.")]
    [SerializeField] [Range(0.01f, 0.05f)] private float _cursorSize = 0.025f;

    [Header("Trail Settings")]
    [Tooltip("Maximum number of trail points kept in memory.")]
    [SerializeField] [Range(64, 4096)] private int _maxTrailPoints = 1024;

    [Tooltip("Minimum distance (normalised 0-1) the cutter must move before a new trail point is recorded.")]
    [SerializeField] [Range(0.001f, 0.02f)] private float _trailMinDistance = 0.004f;

    [Header("Shape Selection (Auto Mode)")]
    [Tooltip("Colour of unselected shape icons.")]
    [SerializeField] private Color _shapeColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    [Tooltip("Colour of the selected shape icon.")]
    [SerializeField] private Color _shapeSelectedColor = new Color(0f, 1f, 0.5f, 1f);

    [Tooltip("Colour of the mode label text.")]
    [SerializeField] private Color _labelColor = new Color(0.8f, 0.8f, 0.8f, 1f);

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fires when a shape is selected in Auto mode.</summary>
    public event Action<ShapePathGenerator.ShapeType> OnShapeSelected;

    // ── Private state ─────────────────────────────────────────────────────────

    private readonly List<Vector2> _trailPoints = new List<Vector2>();
    private Vector2 _normalisedCursorPos = new Vector2(0.5f, 0.5f);
    private Material _glMaterial;
    private CNCMachine.CNCMode _currentDisplayMode = CNCMachine.CNCMode.Manual;
    private ShapePathGenerator.ShapeType _selectedShape = ShapePathGenerator.ShapeType.Rectangle;

    private const float MARGIN = 0.08f;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        CreateGLMaterial();

        if (_renderTexture == null)
        {
            Debug.LogWarning("[CNCScreenDisplay] No RenderTexture assigned. Creating a default 512x512 RT.", this);
            _renderTexture = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32);
            _renderTexture.name = "CNCScreen_RT";
            _renderTexture.Create();
        }

        var rend = GetComponent<Renderer>();
        if (rend != null)
            rend.sharedMaterial.mainTexture = _renderTexture;
    }

    private void OnEnable()
    {
        if (_cutter != null)
            _cutter.OnCutterMoved += HandleCutterMoved;

        if (_machine != null)
            _machine.OnModeChanged += HandleModeChanged;

        if (_autoController != null)
            _autoController.OnShapeChanged += HandleShapeChanged;

        if (_drawCamera != null)
            _drawCamera.targetTexture = _renderTexture;
    }

    private void OnDisable()
    {
        if (_cutter != null)
            _cutter.OnCutterMoved -= HandleCutterMoved;

        if (_machine != null)
            _machine.OnModeChanged -= HandleModeChanged;

        if (_autoController != null)
            _autoController.OnShapeChanged -= HandleShapeChanged;
    }

    private void Start()
    {
        if (_machine != null)
            _currentDisplayMode = _machine.CurrentMode;

        if (_autoController != null)
            _selectedShape = _autoController.SelectedShape;
    }

    private void Update()
    {
        DrawFrame();

        // Handle shape selection input in Auto mode
        if (_currentDisplayMode == CNCMachine.CNCMode.Auto)
        {
            HandleShapeSelectionInput();
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Clears the cut trail. Call this when a new cut session begins.</summary>
    public void ClearTrail()
    {
        _trailPoints.Clear();
    }

    /// <summary>Sets the display mode (called automatically when machine mode changes).</summary>
    public void SetMode(CNCMachine.CNCMode mode)
    {
        _currentDisplayMode = mode;
        if (mode == CNCMachine.CNCMode.Manual)
            ClearTrail();
    }

    /// <summary>Selects a shape for Auto mode cutting.</summary>
    public void SelectShape(ShapePathGenerator.ShapeType shape)
    {
        Debug.Log($"[CNCScreenDisplay] Shape selected via screen: {shape}");
        _selectedShape = shape;

        if (_autoController != null)
            _autoController.SelectShape(shape);

        OnShapeSelected?.Invoke(shape);
    }

    /// <summary>Cycles to the next shape.</summary>
    public void NextShape()
    {
        int current = (int)_selectedShape;
        int count = Enum.GetValues(typeof(ShapePathGenerator.ShapeType)).Length;
        SelectShape((ShapePathGenerator.ShapeType)((current + 1) % count));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void HandleCutterMoved(Vector3 localPosition)
    {
        if (_cuttingPath == null) return;

        Vector2 norm = _cuttingPath.Normalise(new Vector2(localPosition.x, localPosition.z));
        _normalisedCursorPos = norm;

        if (_trailPoints.Count == 0 ||
            Vector2.Distance(_trailPoints[_trailPoints.Count - 1], norm) >= _trailMinDistance)
        {
            if (_trailPoints.Count >= _maxTrailPoints)
                _trailPoints.RemoveAt(0);

            _trailPoints.Add(norm);
        }
    }

    private void HandleModeChanged(CNCMachine.CNCMode newMode)
    {
        Debug.Log($"[CNCScreenDisplay] Display mode changed to: {newMode}");
        SetMode(newMode);
    }

    private void HandleShapeChanged(ShapePathGenerator.ShapeType shape)
    {
        _selectedShape = shape;
    }

    private void HandleShapeSelectionInput()
    {
        // Use number keys 1-4 to select shapes in Auto mode
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            Debug.Log("[CNCScreenDisplay] Key 1 pressed -> Rectangle");
            SelectShape(ShapePathGenerator.ShapeType.Rectangle);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            Debug.Log("[CNCScreenDisplay] Key 2 pressed -> Circle");
            SelectShape(ShapePathGenerator.ShapeType.Circle);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            Debug.Log("[CNCScreenDisplay] Key 3 pressed -> Triangle");
            SelectShape(ShapePathGenerator.ShapeType.Triangle);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            Debug.Log("[CNCScreenDisplay] Key 4 pressed -> Star");
            SelectShape(ShapePathGenerator.ShapeType.Star);
        }
        else if (Input.GetKeyDown(KeyCode.Tab))
        {
            Debug.Log("[CNCScreenDisplay] Tab pressed -> Next shape");
            NextShape();
        }
    }

    private void DrawFrame()
    {
        if (_renderTexture == null || _glMaterial == null) return;

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = _renderTexture;

        GL.Clear(true, true, _backgroundColor);
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, _renderTexture.width, _renderTexture.height, 0);

        _glMaterial.SetPass(0);

        if (_currentDisplayMode == CNCMachine.CNCMode.Manual)
        {
            DrawManualModeDisplay();
        }
        else
        {
            DrawAutoModeDisplay();
        }

        GL.PopMatrix();
        RenderTexture.active = previous;
    }

    private void DrawManualModeDisplay()
    {
        DrawBorder();
        DrawModeLabel("MANUAL");
        DrawTrail();
        DrawCursor();
    }

    private void DrawAutoModeDisplay()
    {
        DrawBorder();
        DrawModeLabel("AUTO");
        DrawShapeSelection();
        DrawSelectedShapePreview();
    }

    private void DrawModeLabel(string text)
    {
        // Mode label at top center - simplified as we can't easily draw text with GL
        // Instead, draw a simple indicator
        float w = _renderTexture.width;
        float h = _renderTexture.height;

        GL.Begin(GL.LINES);
        GL.Color(_labelColor);

        // Draw a simple line indicator at top
        float labelY = MARGIN * h * 0.5f;
        float labelWidth = 0.2f * w;
        float centerX = w * 0.5f;

        DrawLine(new Vector2(centerX - labelWidth / 2, labelY), new Vector2(centerX + labelWidth / 2, labelY));

        GL.End();
    }

    private void DrawBorder()
    {
        float w = _renderTexture.width;
        float h = _renderTexture.height;
        float m = MARGIN;

        Vector2 tl = new Vector2(m * w, m * h);
        Vector2 tr = new Vector2((1f - m) * w, m * h);
        Vector2 br = new Vector2((1f - m) * w, (1f - m) * h);
        Vector2 bl = new Vector2(m * w, (1f - m) * h);

        GL.Begin(GL.LINES);
        GL.Color(_borderColor);

        DrawLine(tl, tr);
        DrawLine(tr, br);
        DrawLine(br, bl);
        DrawLine(bl, tl);

        float tick = 0.04f * w;
        DrawLine(tl, tl + new Vector2(tick, 0));
        DrawLine(tl, tl + new Vector2(0, tick));
        DrawLine(tr, tr + new Vector2(-tick, 0));
        DrawLine(tr, tr + new Vector2(0, tick));
        DrawLine(br, br + new Vector2(-tick, 0));
        DrawLine(br, br + new Vector2(0, -tick));
        DrawLine(bl, bl + new Vector2(tick, 0));
        DrawLine(bl, bl + new Vector2(0, -tick));

        GL.End();
    }

    private void DrawTrail()
    {
        if (_trailPoints.Count < 2) return;

        float w = _renderTexture.width;
        float h = _renderTexture.height;

        GL.Begin(GL.LINES);
        GL.Color(_trailColor);

        for (int i = 1; i < _trailPoints.Count; i++)
        {
            Vector2 a = NormToPixel(_trailPoints[i - 1], w, h);
            Vector2 b = NormToPixel(_trailPoints[i], w, h);
            DrawLine(a, b);
        }

        GL.End();
    }

    private void DrawCursor()
    {
        float w = _renderTexture.width;
        float h = _renderTexture.height;

        Vector2 c = NormToPixel(_normalisedCursorPos, w, h);
        float half = _cursorSize * w;

        GL.Begin(GL.LINES);
        GL.Color(_cursorColor);

        DrawLine(c + new Vector2(-half, 0), c + new Vector2(half, 0));
        DrawLine(c + new Vector2(0, -half), c + new Vector2(0, half));
        float d = half * 0.5f;
        DrawLine(c + new Vector2(-d, -d), c + new Vector2(d, d));
        DrawLine(c + new Vector2(d, -d), c + new Vector2(-d, d));

        GL.End();
    }

    private void DrawShapeSelection()
    {
        float w = _renderTexture.width;
        float h = _renderTexture.height;

        // Draw 4 shape icons in a 2x2 grid
        float gridSize = 0.35f;
        float iconSize = gridSize * 0.4f * w;
        float spacing = gridSize * w;
        float startX = w * 0.5f - spacing * 0.5f;
        float startY = h * 0.3f;

        // Rectangle (top-left) - key 1
        DrawShapeIcon(ShapePathGenerator.ShapeType.Rectangle, 
            new Vector2(startX, startY), iconSize);

        // Circle (top-right) - key 2
        DrawShapeIcon(ShapePathGenerator.ShapeType.Circle, 
            new Vector2(startX + spacing, startY), iconSize);

        // Triangle (bottom-left) - key 3
        DrawShapeIcon(ShapePathGenerator.ShapeType.Triangle, 
            new Vector2(startX, startY + spacing), iconSize);

        // Star (bottom-right) - key 4
        DrawShapeIcon(ShapePathGenerator.ShapeType.Star, 
            new Vector2(startX + spacing, startY + spacing), iconSize);
    }

    private void DrawShapeIcon(ShapePathGenerator.ShapeType shape, Vector2 center, float size)
    {
        bool isSelected = shape == _selectedShape;
        Color color = isSelected ? _shapeSelectedColor : _shapeColor;

        GL.Begin(GL.LINES);
        GL.Color(color);

        float half = size * 0.5f;

        switch (shape)
        {
            case ShapePathGenerator.ShapeType.Rectangle:
                DrawLine(center + new Vector2(-half, -half), center + new Vector2(half, -half));
                DrawLine(center + new Vector2(half, -half), center + new Vector2(half, half));
                DrawLine(center + new Vector2(half, half), center + new Vector2(-half, half));
                DrawLine(center + new Vector2(-half, half), center + new Vector2(-half, -half));
                break;

            case ShapePathGenerator.ShapeType.Circle:
                int segments = 16;
                for (int i = 0; i < segments; i++)
                {
                    float a1 = (i / (float)segments) * Mathf.PI * 2f;
                    float a2 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
                    Vector2 p1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * half;
                    Vector2 p2 = center + new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * half;
                    DrawLine(p1, p2);
                }
                break;

            case ShapePathGenerator.ShapeType.Triangle:
                Vector2 top = center + new Vector2(0, -half);
                Vector2 bottomLeft = center + new Vector2(-half, half);
                Vector2 bottomRight = center + new Vector2(half, half);
                DrawLine(top, bottomRight);
                DrawLine(bottomRight, bottomLeft);
                DrawLine(bottomLeft, top);
                break;

            case ShapePathGenerator.ShapeType.Star:
                int points = 5;
                float outerR = half;
                float innerR = half * 0.4f;
                for (int i = 0; i < points * 2; i++)
                {
                    float a1 = (i / (float)(points * 2)) * Mathf.PI * 2f - Mathf.PI / 2f;
                    float a2 = ((i + 1) / (float)(points * 2)) * Mathf.PI * 2f - Mathf.PI / 2f;
                    float r1 = (i % 2 == 0) ? outerR : innerR;
                    float r2 = ((i + 1) % 2 == 0) ? outerR : innerR;
                    Vector2 p1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r1;
                    Vector2 p2 = center + new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * r2;
                    DrawLine(p1, p2);
                }
                break;
        }

        // Draw selection highlight box if selected
        if (isSelected)
        {
            float boxSize = size * 0.7f;
            DrawLine(center + new Vector2(-boxSize, -boxSize), center + new Vector2(boxSize, -boxSize));
            DrawLine(center + new Vector2(boxSize, -boxSize), center + new Vector2(boxSize, boxSize));
            DrawLine(center + new Vector2(boxSize, boxSize), center + new Vector2(-boxSize, boxSize));
            DrawLine(center + new Vector2(-boxSize, boxSize), center + new Vector2(-boxSize, -boxSize));
        }

        GL.End();
    }

    private void DrawSelectedShapePreview()
    {
        // Draw a preview of the selected shape path in the work area
        float w = _renderTexture.width;
        float h = _renderTexture.height;

        var preview = ShapePathGenerator.GetShapePreview(_selectedShape);
        if (preview.Count < 2) return;

        GL.Begin(GL.LINES);
        GL.Color(new Color(_shapeSelectedColor.r, _shapeSelectedColor.g, _shapeSelectedColor.b, 0.3f));

        for (int i = 1; i < preview.Count; i++)
        {
            Vector2 a = NormToPixel(preview[i - 1], w, h);
            Vector2 b = NormToPixel(preview[i], w, h);
            DrawLine(a, b);
        }

        GL.End();
    }

    private Vector2 NormToPixel(Vector2 norm, float w, float h)
    {
        float innerW = (1f - 2f * MARGIN) * w;
        float innerH = (1f - 2f * MARGIN) * h;
        return new Vector2(
            MARGIN * w + norm.x * innerW,
            MARGIN * h + norm.y * innerH
        );
    }

    private static void DrawLine(Vector2 a, Vector2 b)
    {
        GL.Vertex3(a.x, a.y, 0f);
        GL.Vertex3(b.x, b.y, 0f);
    }

    private void CreateGLMaterial()
    {
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
        {
            Debug.LogError("[CNCScreenDisplay] Could not find a GL material shader.", this);
            return;
        }

        _glMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        _glMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _glMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _glMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        _glMaterial.SetInt("_ZWrite", 0);
    }

    private void OnDestroy()
    {
        if (_glMaterial != null)
            Destroy(_glMaterial);
    }
}
