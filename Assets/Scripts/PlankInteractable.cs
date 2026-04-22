using UnityEngine;

/// <summary>
/// Makes a wooden plank pickable and snappable onto the CNC bed.
///
/// ── CARRYING (PickUp) ─────────────────────────────────────────────────────
///   The plank is parented directly to HoldPoint the moment it is grabbed.
///   Parenting + isKinematic = true means the plank follows the player with
///   zero lag — no per-frame position lerp needed.
///
///   HoldPoint lives on the Hands object (HandPoser), so the plank automatically
///   inherits the camera's translation and rotation.  Recommended local transform:
///     Position : (0, -0.10, 0.45)   — centred between both palms, slightly forward
///     Rotation : (0,  0,   0)       — plank faces forward, lies flat
///
/// ── RELEASING (PutDown) ───────────────────────────────────────────────────
///   1. The plank is unparented from HoldPoint (world position preserved).
///   2. An OverlapSphere checks whether a PlacementZone trigger is nearby.
///   3. If yes  → PlacementZone.SnapPlank() aligns the plank to the CNC bed
///               and keeps it kinematic (resting on the machine).
///   4. If no   → physics are restored (isKinematic = false, gravity = true).
///
/// ── CNC PLACEMENT ZONE SETUP ─────────────────────────────────────────────
///   On the CNC bed GameObject:
///   • Add a Box Collider set to Is Trigger (covers the plank drop area).
///   • Attach the PlacementZone script.
///   • Add a child empty GameObject named "SnapPoint" at the exact world
///     position / rotation where the plank should rest on the bed.
/// </summary>
public class PlankInteractable : Interactable
{
    [Header("Placement")]
    [Tooltip("Radius (metres) within which a PlacementZone triggers a snap on release.")]
    public float snapRadius = 0.8f;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private Rigidbody  rb;
    private bool       isHeld    = false;
    private Transform  holdPoint = null;   // cached at PickUp, cleared at PutDown

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Start kinematic so the plank doesn't fall before being picked up.
        if (rb != null) rb.isKinematic = true;
    }

    public override void Interact() { }

    // ── Pick up ───────────────────────────────────────────────────────────────

    public void PickUp()
    {
        holdPoint = FindHoldPoint();

        if (holdPoint == null)
        {
            Debug.LogWarning("[PlankInteractable] HoldPoint not found — cannot pick up. "
                + "Ensure a GameObject named 'HoldPoint' exists under the Hands object.", this);
            return;
        }

        isHeld = true;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
        }

        // Parent to HoldPoint so the plank moves rigidly with the camera.
        // worldPositionStays:false  → plank snaps to HoldPoint immediately.
        // localPosition/Rotation zero → plank centre aligns with HoldPoint.
        transform.SetParent(holdPoint, worldPositionStays: false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        Debug.Log("[PlankInteractable] Plank picked up and parented to HoldPoint.");
    }

    // ── Put down ──────────────────────────────────────────────────────────────

    public void PutDown()
    {
        isHeld = false;

        // Detach from HoldPoint first so world position is preserved for
        // the upcoming placement check.
        transform.SetParent(null, worldPositionStays: true);

        // Try to find a PlacementZone (CNC bed trigger) within snapRadius.
        PlacementZone zone = FindNearestPlacementZone();

        if (zone != null)
        {
            // Snap to the CNC bed and keep the plank kinematic (it is now
            // resting on the machine, physics shouldn't throw it around).
            zone.SnapPlank(transform, rb);
        }
        else
        {
            // No placement zone nearby — restore physics normally.
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity  = true;
            }
            Debug.Log("[PlankInteractable] Plank released — physics restored.");
        }

        holdPoint = null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// Searches for a PlacementZone collider within snapRadius of the plank's
    /// current world position. Returns the nearest one, or null.
    PlacementZone FindNearestPlacementZone()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, snapRadius);
        PlacementZone nearest  = null;
        float         bestDist = float.MaxValue;

        foreach (Collider c in hits)
        {
            PlacementZone zone = c.GetComponent<PlacementZone>();
            if (zone == null) continue;

            float dist = Vector3.Distance(transform.position, c.transform.position);
            if (dist < bestDist) { bestDist = dist; nearest = zone; }
        }

        return nearest;
    }

    /// Finds the HoldPoint Transform.
    /// Search order: tag → name anywhere in scene → Camera hierarchy path.
    static Transform FindHoldPoint()
    {
        // 1. Tag (fastest — assign the "HoldPoint" tag to the HoldPoint GameObject)
        GameObject tagged = GameObject.FindWithTag("HoldPoint");
        if (tagged != null) return tagged.transform;

        // 2. Name search across the whole scene
        GameObject named = GameObject.Find("HoldPoint");
        if (named != null) return named.transform;

        // 3. Walk the main camera's hierarchy as a last resort
        if (Camera.main != null)
        {
            Transform direct = Camera.main.transform.Find("Hands/HoldPoint");
            if (direct != null) return direct;

            return DeepFind(Camera.main.transform, "HoldPoint");
        }

        return null;
    }

    static Transform DeepFind(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == targetName) return child;
            Transform result = DeepFind(child, targetName);
            if (result != null) return result;
        }
        return null;
    }
}
