// =============================================================================
//  CNC2AutoEngraver.cs
//  Drives the CNC2 spindle along a pre-computed toolpath in Auto mode.
//  The toolpath is generated from the selected logo's texture using a
//  boustrophedon (serpentine raster-scan) algorithm:
//    • The logo texture is sampled at regular pixel intervals.
//    • Dark pixels (brightness < threshold) become cutting waypoints.
//    • The spindle moves row-by-row, alternating direction each row.
//    • Between rows it lifts to an idle height to avoid scratching.
//
//  All positions are expressed as normalised values (0 = axis min, 1 = axis max)
//  so the toolpath is independent of the physical CNC2 dimensions.
//
//  Attach to:  CNC2 root or any child.
//
//  Inspector setup:
//    Spindle         – assign the CNC2SpindleController.
//    LogoManager     – assign the CNC2LogoManager (for texture access).
//    WoodPiece       – optional direct reference; otherwise found via Physics.
//    MoveSpeed       – normalised units per second (try 0.05–0.15).
//    IdleNormY       – normalised Y when travelling between cuts (1 = fully lifted).
//    CutNormY        – normalised Y while cutting (0 = deepest; 0.1–0.2 typical).
//    SampleStep      – pixel stride when reading the texture (larger = coarser/faster).
//    DarkThreshold   – pixels darker than this are treated as logo ink (0-1).
//
//  Events:
//    OnEngraveFinished – fired when the toolpath is fully executed.
// =============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CNC2AutoEngraver : MonoBehaviour
{
    // -------------------------------------------------------------------------
    [Header("─── References ───")]
    [SerializeField] private CNC2SpindleController _spindle;
    [SerializeField] private CNC2LogoManager       _logoManager;
    [Tooltip("The WoodPiece on the CNC bed. If null, found automatically via Physics.")]
    [SerializeField] private WoodPiece             _woodPiece;

    [Header("─── Toolpath ───")]
    [Tooltip("Normalised movement speed (units/sec in 0-1 space). 0.08 is a good start.")]
    [SerializeField] private float _moveSpeed      = 0.08f;
    [Tooltip("Normalised Y while the spindle is travelling (not cutting). Keep at 1.")]
    [SerializeField] private float _idleNormY      = 1.0f;
    [Tooltip("Normalised Y while the spindle is cutting. 0 = deepest axis position.")]
    [SerializeField] private float _cutNormY       = 0.15f;

    [Header("─── Logo Sampling ───")]
    [Tooltip("Read every Nth pixel in each row and column (8 = good balance).")]
    [SerializeField] private int   _sampleStep     = 8;
    [Tooltip("Pixels with grayscale value below this are treated as logo ink (0-1).")]
    [SerializeField] [Range(0f, 1f)] private float _darkThreshold = 0.45f;

    [Header("─── Engraving ───")]
    [SerializeField] private float _engraveBrushSize = 0.010f;
    [SerializeField] private float _engraveDepth     = 0.003f;

    // -------------------------------------------------------------------------
    /// <summary>Fired by CNC2Controller to know when to transition to Finished.</summary>
    public event Action OnEngraveFinished;

    /// <summary>Progress through the current toolpath, 0-1.  Read by CNC2UIManager.</summary>
    public float Progress { get; private set; }

    private Coroutine _engraveCoroutine;
    private bool      _running;

    // =========================================================================
    private void Awake()
    {
        if (!_spindle)      _spindle     = FindFirstObjectByType<CNC2SpindleController>();
        if (!_logoManager)  _logoManager = FindFirstObjectByType<CNC2LogoManager>();
    }

    // =========================================================================
    // Public API (called by CNC2Controller)
    // =========================================================================

    /// <summary>Begin the engraving sequence for the named logo.</summary>
    public void StartEngrave(string logoName)
    {
        Stop();           // cancel any prior run
        Progress = 0f;
        _running = true;
        _engraveCoroutine = StartCoroutine(EngraveRoutine(logoName));
    }

    /// <summary>Immediately halt engraving (e.g. on Stop button or emergency).</summary>
    public void Stop()
    {
        _running = false;
        if (_engraveCoroutine != null)
        {
            StopCoroutine(_engraveCoroutine);
            _engraveCoroutine = null;
        }
    }

    // =========================================================================
    // Engraving coroutine
    // =========================================================================

    private IEnumerator EngraveRoutine(string logoName)
    {
        // 1. Resolve toolpath --------------------------------------------------
        List<Waypoint> path = BuildToolpath(logoName);
        if (path == null || path.Count == 0)
        {
            Debug.LogWarning($"[CNC2AutoEngraver] Empty toolpath for '{logoName}'. " +
                             "Check Resources/CNC2Logos/ and texture Read/Write settings.");
            _running = false;
            OnEngraveFinished?.Invoke();
            yield break;
        }

        // 2. Resolve WoodPiece (may arrive on bed after Start is pressed) -----
        WoodPiece wood = GetWoodOnBed();

        int total = path.Count;
        Debug.Log($"[CNC2AutoEngraver] Starting Auto engrave: {total} waypoints for '{logoName}'.");

        // 3. Lift spindle to safe travel height --------------------------------
        Vector3 cur = _spindle.GetNormalisedPosition();
        yield return MoveToNorm(new Vector3(cur.x, _idleNormY, cur.z));

        // 4. Follow each waypoint ----------------------------------------------
        for (int i = 0; i < total && _running; i++)
        {
            Waypoint wp = path[i];
            yield return MoveToNorm(new Vector3(wp.nx, wp.ny, wp.nz));

            // Apply engraving mark when at cutting depth
            if (wp.isCutting && _spindle.IsSpinning)
            {
                // Refresh wood reference in case it snapped to bed mid-run
                if (wood == null) wood = GetWoodOnBed();
                if (wood != null)
                    wood.ApplyEngraveAtWorldPoint(
                        _spindle.TipWorldPosition, Vector3.up,
                        _engraveDepth, _engraveBrushSize);
            }

            Progress = (float)(i + 1) / total;
        }

        // 5. Return spindle to home position -----------------------------------
        cur = _spindle.GetNormalisedPosition();
        yield return MoveToNorm(new Vector3(cur.x, _idleNormY, cur.z));
        yield return MoveToNorm(new Vector3(0.5f,  _idleNormY, 0.5f));
        _spindle.ReturnToHome();

        Progress = 1f;
        _running = false;
        Debug.Log("[CNC2AutoEngraver] Engraving complete.");
        OnEngraveFinished?.Invoke();
    }

    // -------------------------------------------------------------------------
    private IEnumerator MoveToNorm(Vector3 target)
    {
        if (_spindle == null) yield break;
        const float threshold = 0.004f;

        while (_running)
        {
            Vector3 cur  = _spindle.GetNormalisedPosition();
            float   dist = Vector3.Distance(cur, target);
            if (dist < threshold) break;

            Vector3 next = Vector3.MoveTowards(cur, target, _moveSpeed * Time.deltaTime);
            _spindle.SetNormalisedPosition(next.x, next.y, next.z);
            yield return null;
        }
    }

    // =========================================================================
    // Toolpath generation from logo texture
    // =========================================================================

    private struct Waypoint
    {
        public float nx, ny, nz;  // normalised positions (0-1)
        public bool  isCutting;   // true = drill is in contact with wood
    }

    /// <summary>
    /// Builds a boustrophedon (serpentine) raster-scan toolpath from the logo texture.
    /// Returns null on failure so the caller can fall back to a simple rectangle.
    /// </summary>
    private List<Waypoint> BuildToolpath(string logoName)
    {
        Texture2D tex = ResolveTexture(logoName);

        if (tex == null)
        {
            Debug.LogWarning($"[CNC2AutoEngraver] Falling back to rectangle path for '{logoName}'.");
            return BuildFallbackPath();
        }
        if (!tex.isReadable)
        {
            Debug.LogWarning($"[CNC2AutoEngraver] Texture '{logoName}' is not readable. " +
                             "Enable Read/Write in Texture Import Settings. Using fallback.");
            return BuildFallbackPath();
        }

        var path = new List<Waypoint>();
        int w = tex.width, h = tex.height;
        bool leftToRight = true;

        for (int row = 0; row < h; row += _sampleStep)
        {
            // Collect the X positions of dark (ink) pixels in this row
            var cutCols = new List<int>(64);
            for (int col = 0; col < w; col += _sampleStep)
            {
                Color pixel = tex.GetPixel(col, row);
                if (pixel.a > 0.5f && pixel.grayscale < _darkThreshold)
                    cutCols.Add(col);
            }
            if (cutCols.Count == 0) continue;

            // Reverse for alternate rows (serpentine)
            if (!leftToRight) cutCols.Reverse();
            leftToRight = !leftToRight;

            // Divide by (dimension-1) so the last row/column maps exactly to 1.0
            float nz = h > 1 ? (float)row / (h - 1) : 0f;

            // Rapid traverse (idle height) to the start of this row
            float firstNx = w > 1 ? (float)cutCols[0] / (w - 1) : 0f;
            if (path.Count > 0)
            {
                // Lift from previous cut point
                Waypoint last = path[path.Count - 1];
                path.Add(new Waypoint { nx = last.nx, ny = _idleNormY, nz = last.nz });
                // Rapid to start of this row
                path.Add(new Waypoint { nx = firstNx, ny = _idleNormY, nz = nz });
            }
            else
            {
                path.Add(new Waypoint { nx = firstNx, ny = _idleNormY, nz = nz });
            }

            // Plunge and cut through each dark pixel in the row
            foreach (int col in cutCols)
            {
                float nx = w > 1 ? (float)col / (w - 1) : 0f;
                path.Add(new Waypoint { nx = nx, ny = _cutNormY, nz = nz, isCutting = true });
            }
        }

        // Final retract
        if (path.Count > 0)
        {
            Waypoint last = path[path.Count - 1];
            path.Add(new Waypoint { nx = last.nx, ny = _idleNormY, nz = last.nz });
        }

        return path;
    }

    // -------------------------------------------------------------------------
    private Texture2D ResolveTexture(string logoName)
    {
        // 1. Ask the LogoManager (fastest – texture is already loaded)
        if (_logoManager != null)
        {
            Texture2D t = _logoManager.GetSelectedTexture();
            if (t != null) return t;
        }
        // 2. Direct Resources load
        Texture2D fromResources = Resources.Load<Texture2D>($"CNC2Logos/{logoName}");
        if (fromResources != null) return fromResources;

        // 3. Sprite → texture
        Sprite spr = Resources.Load<Sprite>($"CNC2Logos/{logoName}");
        return spr != null ? spr.texture : null;
    }

    /// <summary>Simple square toolpath used when no texture is available.</summary>
    private List<Waypoint> BuildFallbackPath()
    {
        float[] rows = { 0.2f, 0.35f, 0.5f, 0.65f, 0.8f };
        var path = new List<Waypoint>();
        bool left = true;
        foreach (float nz in rows)
        {
            float xA = left ? 0.2f : 0.8f;
            float xB = left ? 0.8f : 0.2f;
            path.Add(new Waypoint { nx = xA, ny = _idleNormY, nz = nz });
            path.Add(new Waypoint { nx = xA, ny = _cutNormY,  nz = nz, isCutting = true });
            path.Add(new Waypoint { nx = xB, ny = _cutNormY,  nz = nz, isCutting = true });
            path.Add(new Waypoint { nx = xB, ny = _idleNormY, nz = nz });
            left = !left;
        }
        return path;
    }

    // =========================================================================
    // Utilities
    // =========================================================================

    /// <summary>
    /// Find a WoodPiece currently resting on the CNC bed.
    /// Checks a serialised reference first, then PlacementZone, then scene search.
    /// </summary>
    private WoodPiece GetWoodOnBed()
    {
        if (_woodPiece != null) return _woodPiece;

        PlacementZone zone = GetComponentInParent<PlacementZone>()
                          ?? FindFirstObjectByType<PlacementZone>();
        if (zone != null && zone.OccupantTransform != null)
        {
            WoodPiece wp = zone.OccupantTransform.GetComponent<WoodPiece>();
            if (wp) return wp;
        }

        return FindFirstObjectByType<WoodPiece>();
    }
}
