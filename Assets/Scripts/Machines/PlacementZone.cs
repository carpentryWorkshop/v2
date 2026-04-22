using UnityEngine;

/// <summary>
/// Marks a trigger area on the CNC bed where a plank can be snapped into place.
///
/// ── SETUP ON THE CNC BED ─────────────────────────────────────────────────
///   1. Select the CNC bed GameObject (or a dedicated child).
///   2. Add a Box Collider → tick "Is Trigger" → size it to cover the bed surface.
///   3. Attach this PlacementZone script.
///   4. Create a child empty GameObject named "SnapPoint":
///        • Position it at the exact world location where the plank centre
///          should rest (flush on the bed surface).
///        • Rotate it so the plank aligns with the CNC axis (usually
///          forward = along the cutting direction).
///      If no "SnapPoint" child exists, the zone's own transform is used.
///
/// ── HOW IT WORKS ─────────────────────────────────────────────────────────
///   PlankInteractable.PutDown() fires Physics.OverlapSphere at the plank's
///   position. If this zone's collider is within snapRadius, PutDown() calls
///   PlacementZone.SnapPlank() which teleports the plank to SnapPoint and
///   keeps it kinematic so it rests stably on the machine.
///
///   A public IsOccupied flag lets other systems (e.g. CNCMachine) check
///   whether a plank is currently loaded.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PlacementZone : MonoBehaviour
{
    [Header("Snap Target")]
    [Tooltip("Where the plank centre snaps to. Auto-found as child 'SnapPoint' if blank.")]
    [SerializeField] private Transform snapPoint;

    [Header("Optional Feedback")]
    [Tooltip("GameObject shown while the zone is empty (e.g. a ghost outline).")]
    [SerializeField] private GameObject emptyIndicator;

    [Tooltip("GameObject shown while a plank occupies the zone.")]
    [SerializeField] private GameObject occupiedIndicator;

    // ── State ─────────────────────────────────────────────────────────────────

    /// True when a plank has been snapped here and not yet removed.
    public bool IsOccupied { get; private set; } = false;

    /// The plank currently resting in this zone (null when empty).
    public Transform OccupantTransform { get; private set; } = null;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        // Auto-locate SnapPoint as a direct child named "SnapPoint".
        if (snapPoint == null)
        {
            snapPoint = transform.Find("SnapPoint");

            // Fall back to the zone's own transform so the snap still works.
            if (snapPoint == null)
            {
                snapPoint = transform;
                Debug.LogWarning("[PlacementZone] No 'SnapPoint' child found on '"
                    + gameObject.name + "'. The zone's own position will be used. "
                    + "Add a child named 'SnapPoint' for precise alignment.", this);
            }
        }

        // Ensure the collider is a trigger (safety net for setup mistakes).
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning("[PlacementZone] Collider on '" + gameObject.name
                + "' was not a trigger — fixed automatically.", this);
        }

        RefreshIndicators();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// Called by PlankInteractable.PutDown() when the plank is released over
    /// this zone. Teleports the plank to SnapPoint and keeps it kinematic.
    public void SnapPlank(Transform plank, Rigidbody rb)
    {
        if (IsOccupied)
        {
            Debug.Log("[PlacementZone] Zone '" + gameObject.name
                + "' is already occupied — snap rejected.", this);
            return;
        }

        // Align the plank to the snap point.
        plank.position = snapPoint.position;
        plank.rotation = snapPoint.rotation;

        // Keep kinematic: plank is resting on the machine, not a physics actor.
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
            rb.linearVelocity        = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Optionally parent the plank to the snap point so it moves with
        // the machine if the CNC bed can translate (comment out if not needed).
        plank.SetParent(snapPoint, worldPositionStays: false);
        plank.localPosition = Vector3.zero;
        plank.localRotation = Quaternion.identity;

        IsOccupied       = true;
        OccupantTransform = plank;

        RefreshIndicators();
        Debug.Log("[PlacementZone] Plank snapped to '" + snapPoint.name
            + "' on '" + gameObject.name + "'.", this);
    }

    /// Removes the plank from this zone and restores its physics.
    /// Call this from CNCMachine or any script that wants to eject the plank.
    public void ReleasePlank(bool restorePhysics = true)
    {
        if (!IsOccupied || OccupantTransform == null) return;

        // Detach from snap point.
        OccupantTransform.SetParent(null, worldPositionStays: true);

        if (restorePhysics)
        {
            Rigidbody rb = OccupantTransform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity  = true;
            }
        }

        OccupantTransform = null;
        IsOccupied        = false;

        RefreshIndicators();
        Debug.Log("[PlacementZone] Plank released from '" + gameObject.name + "'.", this);
    }

    // ── Indicator helper ──────────────────────────────────────────────────────

    void RefreshIndicators()
    {
        if (emptyIndicator    != null) emptyIndicator.SetActive(!IsOccupied);
        if (occupiedIndicator != null) occupiedIndicator.SetActive(IsOccupied);
    }

    // ── Gizmo ─────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        // Draw a yellow wire cube at the snap point so you can see the
        // target in the Scene view without entering Play mode.
        Transform sp = snapPoint != null ? snapPoint : transform;

        Gizmos.color = IsOccupied ? new Color(0f, 1f, 0f, 0.4f)
                                  : new Color(1f, 0.9f, 0f, 0.4f);
        Gizmos.matrix = Matrix4x4.TRS(sp.position, sp.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.6f, 0.05f, 1.2f));

        Gizmos.color  = Color.yellow;
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawLine(transform.position, sp.position);
    }
#endif
}
