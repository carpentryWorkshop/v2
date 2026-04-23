// =============================================================================
//  CNC2ManualController.cs
//  Reads keyboard input and drives CNC2SpindleController during Manual mode.
//  Also performs engraving contact detection while the spindle is running.
//
//  Attach to:  CNC2 root or any child.
//
//  Key mapping (six keys for three axes):
//    H / J  →  X-axis   left  (−)  /  right (+)
//    T / Y  →  Y-axis   up    (+)  /  down  (−)    [depth: Y up = lift, Y down = plunge]
//    I / K  →  Z-axis   forward (+) / backward (−)
//
//  Engraving contact:
//    Each frame while enabled, a sphere-cast from the meche tip detects
//    WoodPiece components within _contactRadius.  If the spindle is spinning
//    and a WoodPiece is found, ApplyEngraveAtWorldPoint() is called to stamp
//    a drill mark on the wood surface.
//
//  Inspector setup:
//    Spindle         – drag the GameObject carrying CNC2SpindleController.
//    ContactRadius   – radius of the engraving sphere test (metres).
//    EngraveBrushSize/ EngraveDepth – passed to WoodPiece.ApplyEngraveAtWorldPoint.
// =============================================================================

using UnityEngine;

public class CNC2ManualController : MonoBehaviour
{
    // -------------------------------------------------------------------------
    [Header("─── References ───")]
    [SerializeField] private CNC2SpindleController _spindle;

    [Header("─── Engraving Contact ───")]
    [Tooltip("Sphere radius around the meche tip used to detect wood contact")]
    [SerializeField] private float _contactRadius    = 0.018f;
    [Tooltip("Visual brush radius passed to WoodPiece")]
    [SerializeField] private float _engraveBrushSize = 0.010f;
    [Tooltip("Engraving depth passed to WoodPiece")]
    [SerializeField] private float _engraveDepth     = 0.003f;
    [Tooltip("LayerMask for the overlap test – set to the layer of your wood objects")]
    [SerializeField] private LayerMask _woodLayer    = Physics.AllLayers;

    // -------------------------------------------------------------------------
    private bool _enabled;

    // =========================================================================
    private void Awake()
    {
        if (!_spindle)
            _spindle = GetComponentInParent<CNC2SpindleController>()
                    ?? FindFirstObjectByType<CNC2SpindleController>();
    }

    // -------------------------------------------------------------------------
    /// <summary>
    /// Enable or disable keyboard input.
    /// Called by CNC2Controller on state transitions.
    /// </summary>
    public void SetEnabled(bool enabled) => _enabled = enabled;

    // =========================================================================
    private void Update()
    {
        if (!_enabled || _spindle == null) return;

        ReadMovementInput();
        TryEngrave();
    }

    // =========================================================================
    // Input
    // =========================================================================

    private void ReadMovementInput()
    {
        // ── X-axis: H = left, J = right ──────────────────────────────────────
        float dx = 0f;
        if (Input.GetKey(KeyCode.H)) dx = -1f;
        if (Input.GetKey(KeyCode.J)) dx =  1f;

        // ── Y-axis: T = up (lift), Y = down (plunge) ─────────────────────────
        float dy = 0f;
        if (Input.GetKey(KeyCode.T)) dy =  1f;
        if (Input.GetKey(KeyCode.Y)) dy = -1f;

        // ── Z-axis: I = forward, K = backward ────────────────────────────────
        float dz = 0f;
        if (Input.GetKey(KeyCode.I)) dz =  1f;
        if (Input.GetKey(KeyCode.K)) dz = -1f;

        if (dx != 0f) _spindle.MoveX(dx);
        if (dy != 0f) _spindle.MoveY(dy);
        if (dz != 0f) _spindle.MoveZ(dz);
    }

    // =========================================================================
    // Engraving contact detection
    // =========================================================================

    private void TryEngrave()
    {
        // Only engrave when the spindle is actually spinning
        if (!_spindle.IsSpinning) return;

        Vector3 tip = _spindle.TipWorldPosition;

        // Collect all colliders inside the contact sphere
        Collider[] hits = Physics.OverlapSphere(tip, _contactRadius, _woodLayer);
        foreach (Collider col in hits)
        {
            WoodPiece wood = col.GetComponent<WoodPiece>();
            if (wood == null) continue;

            // Surface normal: for a horizontal plank, upward is correct.
            // If the plank can be oriented arbitrarily, use -col.transform.up instead.
            Vector3 surfaceNormal = col.transform.up;

            wood.ApplyEngraveAtWorldPoint(tip, surfaceNormal, _engraveDepth, _engraveBrushSize);
            break;   // One wood piece per frame is sufficient
        }
    }

    // -------------------------------------------------------------------------
    // Editor helper – draw the contact sphere in the Scene view
    private void OnDrawGizmosSelected()
    {
        if (_spindle == null) return;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
        Gizmos.DrawSphere(_spindle.TipWorldPosition, _contactRadius);
    }
}
