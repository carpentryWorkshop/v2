// =============================================================================
//  CNC2SpindleController.cs
//  Controls the three-axis spindle assembly of CNC2.
//
//  Hierarchy assumed (exact GameObject names from the scene):
//    cncCutter              ← moves on local Z axis (forward / back sweep)
//      └── spindleHolder    ← moves on local X axis (left / right)
//            └── spindleFinal  ← moves on local Z axis (cutting depth / plunge)
//                  └── meche   ← the drill bit; rotates continuously while cutting
//
//  Attach to:  the spindleHolder GameObject (or any ancestor – references are
//              assignable in the Inspector or auto-discovered at runtime).
//
//  Inspector setup:
//    Meche         – drag the 'meche' GameObject
//    SpindlFinal   – drag the 'spindleFinal' GameObject
//    SpindleHolder – drag the 'spindleHolder' GameObject
//    CncCutter     – drag the 'cncCutter' GameObject  (whole assembly, Z sweep)
//
//  Axis limits (local-space values matching the actual scene transforms):
//    X (spindleHolder):  min = 0.0044   max = 0.06139
//    Y / depth (spindleFinal local Z):  min = -0.02  max = -0.01602
//    Z (cncCutter):      min = -7       max = 5
//
//  Used by:
//    CNC2ManualController  – calls MoveX / MoveY / MoveZ each frame.
//    CNC2AutoEngraver      – calls SetNormalisedPosition for smooth path following.
//    CNC2Controller        – calls StartSpinning / StopSpinning / ReturnToHome.
// =============================================================================

using System.Collections;
using UnityEngine;

public class CNC2SpindleController : MonoBehaviour
{
    // -------------------------------------------------------------------------
    [Header("─── Spindle Transforms ───")]
    [Tooltip("The drill bit – rotates when the spindle is on")]
    [SerializeField] private Transform _meche;
    [Tooltip("'spindleFinal' – moves on its local Z axis (cutting depth)")]
    [SerializeField] private Transform _spindlFinal;
    [Tooltip("'spindleHolder' – moves on its local X axis (left / right)")]
    [SerializeField] private Transform _spindleHolder;
    [Tooltip("'cncCutter' – the whole assembly; moves on its local Z axis (forward / back)")]
    [SerializeField] private Transform _cncCutter;

    [Header("─── Rotation (meche spin) ───")]
    [Tooltip("Target speed in RPM (Rotations Per Minute)")]
    [SerializeField] private float _rpm          = 3000f;
    [Tooltip("Seconds to accelerate from 0 to full RPM")]
    [SerializeField] private float _spinUpTime   = 0.5f;
    [Tooltip("Seconds to decelerate from full RPM to 0")]
    [SerializeField] private float _spinDownTime = 0.3f;
    [Tooltip("Local axis the meche rotates around – Z = drill axis")]
    [SerializeField] private Vector3 _rotationAxis = Vector3.forward;

    [Header("─── X-Axis (spindleHolder left / right) ───")]
    [SerializeField] private float _xMoveSpeed = 0.02f;
    [SerializeField] private float _xMin = 0.0044f;
    [SerializeField] private float _xMax = 0.06139f;

    [Header("─── Y-Axis (spindleFinal local Z = cutting depth) ───")]
    [Tooltip("Negative = plunged into wood; _yMin is the deepest cut position")]
    [SerializeField] private float _yMoveSpeed = 0.002f;
    [SerializeField] private float _yMin = -0.02f;     // deepest plunge (most negative local Z)
    [SerializeField] private float _yMax = -0.01602f;  // lifted clear of wood

    [Header("─── Z-Axis (cncCutter forward / back) ───")]
    [SerializeField] private float _zMoveSpeed = 3.0f;
    [SerializeField] private float _zMin = -7f;
    [SerializeField] private float _zMax =  5f;

    [Header("─── Audio (optional) ───")]
    [Tooltip("AudioSource on the meche or spindleHolder for the drill sound")]
    [SerializeField] private AudioSource _audioSource;
    [Tooltip("Looping clip played while the drill is running")]
    [SerializeField] private AudioClip   _spindleClip;
    [Tooltip("Audio pitch at full RPM")]
    [SerializeField] [Range(0.5f, 3f)] private float _pitchHigh = 1.4f;
    [Tooltip("Audio pitch when just starting to spin (low idle hum)")]
    [SerializeField] [Range(0.1f, 1f)] private float _pitchLow  = 0.6f;

    // -------------------------------------------------------------------------
    // Runtime state
    private float     _currentRpm;
    private bool      _isSpinning;
    private Coroutine _rampCoroutine;

    // Home positions recorded at startup so ReturnToHome() works correctly
    private Vector3 _holderHomeLocal;
    private Vector3 _finalHomeLocal;
    private Vector3 _cutterHomeLocal;

    // =========================================================================
    private void Awake()
    {
        // Auto-discover spindleFinal and meche (try both scene spellings)
        string[] spindleNames = { "spindleFinal", "spindlFinal" };
        foreach (string sn in spindleNames)
        {
            if (!_meche)       _meche       = transform.Find(sn + "/meche");
            if (!_spindlFinal) _spindlFinal = transform.Find(sn);
            if (_meche && _spindlFinal) break;
        }

        if (!_spindleHolder)
            _spindleHolder = transform;   // assume this script sits on spindleHolder

        // Auto-discover cncCutter by walking up the hierarchy
        if (!_cncCutter)
        {
            Transform t = transform.parent;
            while (t != null)
            {
                if (t.name == "cncCutter") { _cncCutter = t; break; }
                t = t.parent;
            }
        }
        if (!_cncCutter && transform.parent != null)
            _cncCutter = transform.parent;  // fallback: direct parent

        // Record rest positions for ReturnToHome()
        if (_spindleHolder) _holderHomeLocal = _spindleHolder.localPosition;
        if (_spindlFinal)   _finalHomeLocal  = _spindlFinal.localPosition;
        if (_cncCutter)     _cutterHomeLocal = _cncCutter.localPosition;
    }

    private void Update()
    {
        if (_currentRpm > 0f && _meche)
        {
            float degreesThisFrame = (_currentRpm * 6f) * Time.deltaTime; // 1 RPM = 6°/s
            _meche.Rotate(_rotationAxis, degreesThisFrame, Space.Self);
        }
        UpdateAudio();
    }

    private void UpdateAudio()
    {
        if (_audioSource == null || _spindleClip == null) return;

        if (_currentRpm > 10f)
        {
            if (!_audioSource.isPlaying)
            {
                _audioSource.clip = _spindleClip;
                _audioSource.loop = true;
                _audioSource.Play();
            }
            _audioSource.pitch = Mathf.Lerp(_pitchLow, _pitchHigh, _currentRpm / Mathf.Max(_rpm, 1f));
        }
        else if (_audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
    }

    // =========================================================================
    // Spindle on / off
    // =========================================================================

    public void StartSpinning()
    {
        if (_isSpinning) return;
        _isSpinning = true;
        RestartRamp(_currentRpm, _rpm, _spinUpTime);
    }

    public void StopSpinning()
    {
        if (!_isSpinning) return;
        _isSpinning = false;
        RestartRamp(_currentRpm, 0f, _spinDownTime);
    }

    private void RestartRamp(float from, float to, float duration)
    {
        if (_rampCoroutine != null) StopCoroutine(_rampCoroutine);
        _rampCoroutine = StartCoroutine(RampRpm(from, to, duration));
    }

    private IEnumerator RampRpm(float from, float to, float duration)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(duration, 0.01f))
        {
            _currentRpm = Mathf.Lerp(from, to, t);
            yield return null;
        }
        _currentRpm = to;
    }

    // =========================================================================
    // Manual movement – called each frame by CNC2ManualController.
    // dir is a signed unit (-1 or +1).
    // =========================================================================

    /// <summary>Move spindleHolder left (-1) or right (+1) on its local X axis.</summary>
    public void MoveX(float dir)
    {
        if (!_spindleHolder) return;
        Vector3 p = _spindleHolder.localPosition;
        p.x = Mathf.Clamp(p.x + dir * _xMoveSpeed * Time.deltaTime, _xMin, _xMax);
        _spindleHolder.localPosition = p;
    }

    /// <summary>
    /// Move spindleFinal deeper (-1) or lift (+1) on its local Z axis.
    /// Note: spindleFinal's Z axis is the cutting-depth axis in this machine.
    /// _yMin is the deepest position (most negative local Z).
    /// </summary>
    public void MoveY(float dir)
    {
        if (!_spindlFinal) return;
        Vector3 p = _spindlFinal.localPosition;
        p.z = Mathf.Clamp(p.z + dir * _yMoveSpeed * Time.deltaTime, _yMin, _yMax);
        _spindlFinal.localPosition = p;
    }

    /// <summary>Move cncCutter forward (+1) or backward (-1) on its local Z axis.</summary>
    public void MoveZ(float dir)
    {
        if (!_cncCutter) return;
        Vector3 p = _cncCutter.localPosition;
        p.z = Mathf.Clamp(p.z + dir * _zMoveSpeed * Time.deltaTime, _zMin, _zMax);
        _cncCutter.localPosition = p;
    }

    // =========================================================================
    // Auto-mode absolute positioning – called by CNC2AutoEngraver
    // =========================================================================

    /// <summary>
    /// Teleport the spindle to a normalised position (0 = min, 1 = max per axis).
    ///   nx → spindleHolder local X
    ///   ny → spindleFinal  local Z (depth: 0 = deepest, 1 = lifted)
    ///   nz → cncCutter     local Z (sweep)
    /// </summary>
    public void SetNormalisedPosition(float nx, float ny, float nz)
    {
        if (_spindleHolder)
        {
            Vector3 p = _spindleHolder.localPosition;
            p.x = Mathf.Lerp(_xMin, _xMax, Mathf.Clamp01(nx));
            _spindleHolder.localPosition = p;
        }
        if (_spindlFinal)
        {
            Vector3 p = _spindlFinal.localPosition;
            p.z = Mathf.Lerp(_yMin, _yMax, Mathf.Clamp01(ny));
            _spindlFinal.localPosition = p;
        }
        if (_cncCutter)
        {
            Vector3 p = _cncCutter.localPosition;
            p.z = Mathf.Lerp(_zMin, _zMax, Mathf.Clamp01(nz));
            _cncCutter.localPosition = p;
        }
    }

    /// <summary>Returns the current position as a normalised (0-1) Vector3.</summary>
    public Vector3 GetNormalisedPosition()
    {
        float nx = 0f, ny = 0f, nz = 0f;
        if (_spindleHolder)
            nx = Mathf.InverseLerp(_xMin, _xMax, _spindleHolder.localPosition.x);
        if (_spindlFinal)
            ny = Mathf.InverseLerp(_yMin, _yMax, _spindlFinal.localPosition.z);
        if (_cncCutter)
            nz = Mathf.InverseLerp(_zMin, _zMax, _cncCutter.localPosition.z);
        return new Vector3(nx, ny, nz);
    }

    // =========================================================================
    // Utility
    // =========================================================================

    /// <summary>World-space position of the meche tip – used for engraving contact.</summary>
    public Vector3 TipWorldPosition => _meche ? _meche.position : transform.position;

    /// <summary>True while the spindle is spinning (even during spin-up / spin-down).</summary>
    public bool IsSpinning => _currentRpm > 10f;

    /// <summary>Current RPM (animated).</summary>
    public float CurrentRpm => _currentRpm;

    /// <summary>Snap all axes back to their positions at scene load.</summary>
    public void ReturnToHome()
    {
        if (_spindleHolder) _spindleHolder.localPosition = _holderHomeLocal;
        if (_spindlFinal)   _spindlFinal.localPosition   = _finalHomeLocal;
        if (_cncCutter)     _cncCutter.localPosition     = _cutterHomeLocal;
    }
}
