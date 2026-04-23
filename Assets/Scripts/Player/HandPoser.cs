using UnityEngine;
using System.Collections;

/// <summary>
/// Manages hand visibility, per-hand root references, and finger poses for
/// first-person desktop interaction (non-XR).
///
/// ── REQUIRED HIERARCHY ──────────────────────────────────────────────────────
///
///   Camera  (tag: MainCamera)
///   └── Hands                      ← attach HandPoser here
///       ├── HoldPoint              ← plank anchor — positioned between palms
///       │     Local position : (0, -0.10, 0.45)   ← centred, slightly forward
///       │     Local rotation : (0,  0,   0)        ← plank faces forward / flat
///       ├── RightHand              ← right hand mesh + rig root
///       │   └── … bones (finger.index.02.R, finger.middle.02.R, etc.)
///       └── LeftHand               ← left hand mesh + rig root
///           └── … bones (finger.index.02.L, finger.middle.02.L, etc.)
///
/// ── NAMING CONVENTIONS SUPPORTED ────────────────────────────────────────────
///   Hand roots : RightHand / Hand_R / HandRight / LeftHand / Hand_L / HandLeft
///   Bones (.02): finger.index.02.R  OR  index_R  OR  Index_R  (and L variants)
///   Bones (.03): finger.index.03.R  OR  index_R_tip           (and L variants)
///   HoldPoint  : HoldPoint / GrabAnchor / GrabPoint
///
/// Inspector fields act as manual overrides — leave them blank to rely on the
/// auto-discovery that runs in Awake (safe after any hierarchy rename).
///
/// ── INPUT ────────────────────────────────────────────────────────────────────
///   Tab  — toggle hand visibility (R was reserved by CNCInputHandler)
///   E    — grab / release (handled by PlayerInteraction, which calls SetPose)
///   P    — point / press button (handled by PlayerInteraction)
/// </summary>
public class HandPoser : MonoBehaviour
{
    // ── Inspector: hand root overrides (optional) ─────────────────────────────

    [Header("Hand Roots (optional — auto-found if blank)")]
    [SerializeField] private Transform rightHandRoot;
    [SerializeField] private Transform leftHandRoot;

    [Header("Hold Point (optional — auto-found if blank)")]
    [SerializeField] private Transform holdPoint;

    // ── Inspector: finger bone overrides (optional) ───────────────────────────

    [Header("Right Hand – bone.02 (optional override)")]
    [SerializeField] private Transform index_R;
    [SerializeField] private Transform middle_R;
    [SerializeField] private Transform ring_R;
    [SerializeField] private Transform pinky_R;
    [SerializeField] private Transform thumb_R;

    [Header("Right Hand – bone.03 / tips (optional override)")]
    [SerializeField] private Transform index_R_tip;
    [SerializeField] private Transform middle_R_tip;
    [SerializeField] private Transform ring_R_tip;
    [SerializeField] private Transform pinky_R_tip;
    [SerializeField] private Transform thumb_R_tip;

    [Header("Left Hand – bone.02 (optional override)")]
    [SerializeField] private Transform index_L;
    [SerializeField] private Transform middle_L;
    [SerializeField] private Transform ring_L;
    [SerializeField] private Transform pinky_L;
    [SerializeField] private Transform thumb_L;

    [Header("Left Hand – bone.03 / tips (optional override)")]
    [SerializeField] private Transform index_L_tip;
    [SerializeField] private Transform middle_L_tip;
    [SerializeField] private Transform ring_L_tip;
    [SerializeField] private Transform pinky_L_tip;
    [SerializeField] private Transform thumb_L_tip;

    // ── Tunable settings ──────────────────────────────────────────────────────

    [Header("Hand Visibility")]
    [Tooltip("How fast the hands slide up/down (units per second). Increase if movement feels invisible.")]
    public float   moveSpeed       = 6f;
    [Tooltip("Local position when hands are raised (visible). Relative to MainCamera.")]
    public Vector3 visiblePosition = new Vector3(-0.006f, -0.925f,  0.65f);
    [Tooltip("Local position when hands are lowered (hidden). Y should be much lower than visiblePosition.Y.")]
    public Vector3 hiddenPosition  = new Vector3(-0.006f, -2.5f,    0.65f);
    [Tooltip("If true, hand child GameObjects are also SetActive(false) when hidden (belt-and-suspenders).")]
    public bool    useSetActive    = true;

    [Header("Finger Animation")]
    public float animationSpeed = 5f;

    // ── Public API ────────────────────────────────────────────────────────────

    /// The anchor Transform that sits between both palms.
    /// PlankInteractable uses this to parent the plank for lag-free carrying.
    public Transform HoldPoint => holdPoint;

    /// Read-only access to the right hand root (e.g. for per-hand fx).
    public Transform RightHandRoot => rightHandRoot;

    /// Read-only access to the left hand root.
    public Transform LeftHandRoot  => leftHandRoot;

    // ── Internal state ────────────────────────────────────────────────────────

    private Quaternion[] openRotations;
    private Quaternion[] grabRotations;
    private Quaternion[] pointRotations;
    private Quaternion[] currentTargetRotations;
    private Transform[]  allFingers;
    private bool handsVisible = false;
    private bool initialized  = false;

    // Pose constants — angles tuned for the two-handed plank grip
    private readonly Quaternion defaultPose02 = Quaternion.Euler(11.7f,   16.37f,   3.44f);
    private readonly Quaternion grabPose02    = Quaternion.Euler(55.03f,  20.4f,    5.88f);
    private readonly Quaternion grabPose03    = Quaternion.Euler(24.405f,  0.661f, -0.476f);
    private readonly Quaternion pointThumb02  = Quaternion.Euler(45.06f,  -4.807f,-30.24f);
    private readonly Quaternion pointIndex02  = Quaternion.Euler(26.312f,  0.419f, -0.138f);
    private readonly Quaternion pointIndex03  = Quaternion.Euler( 0.261f, -0.854f,  0.422f);
    private readonly Quaternion pointOthers02 = Quaternion.Euler(118.7f,   9.38f,  -7.08f);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        // Auto-discover all references before Start so that scripts reading
        // HoldPoint (e.g. PlankInteractable) see valid values from the start.
        AutoFindHandRoots();
        AutoFindHoldPoint();
        AutoFindBones();
    }

    void Start()
    {
        // Build the flat finger array — layout must match the index ranges
        // used in InitializePoses() and AnimateFingers():
        //   [0–4]   right bone.02   [5–9]   right bone.03 (tips)
        //   [10–14] left  bone.02   [15–19] left  bone.03 (tips)
        allFingers = new Transform[]
        {
            index_R,     middle_R,     ring_R,     pinky_R,     thumb_R,
            index_R_tip, middle_R_tip, ring_R_tip, pinky_R_tip, thumb_R_tip,
            index_L,     middle_L,     ring_L,     pinky_L,     thumb_L,
            index_L_tip, middle_L_tip, ring_L_tip, pinky_L_tip, thumb_L_tip
        };

        transform.localPosition = hiddenPosition;

        // Diagnostic: confirm hand roots were found and force-activate them.
        // If either is null, the SetActive toggle in HandleVisibility() is a no-op.
        Debug.Log($"[HandPoser] Start — rightHandRoot={rightHandRoot?.name ?? "NULL"} " +
                  $"leftHandRoot={leftHandRoot?.name ?? "NULL"} " +
                  $"holdPoint={holdPoint?.name ?? "NULL"}", this);

        // Ensure meshes are visible regardless of saved scene state.
        // Position-based hiding (hiddenPosition Y) keeps them off-screen when handsVisible=false.
        if (rightHandRoot != null) rightHandRoot.gameObject.SetActive(true);
        if (leftHandRoot  != null) leftHandRoot.gameObject.SetActive(true);

        StartCoroutine(InitializePoses());
    }

    // ── Auto-discovery ────────────────────────────────────────────────────────

    /// Finds the left and right hand root GameObjects inside this hierarchy.
    /// Works after rename/refactor as long as the names match one of the
    /// supported conventions listed in the file header.
    void AutoFindHandRoots()
    {
        // Use Unity's overloaded == null (not C# ??) so destroyed/missing
        // Inspector references are treated as unset and auto-discovery runs.
        if (rightHandRoot == null)
            rightHandRoot = FindChildByAnyName(transform,
               "RightHand", "RightHandModel", "Hand_R", "HandRight", "R_Hand",
               "hand_R", "hand.R", "Right Hand", "right_hand");

        if (leftHandRoot == null)
            leftHandRoot = FindChildByAnyName(transform,
               "LeftHand", "LeftHandModel", "Hand_L", "HandLeft", "L_Hand",
               "hand_L", "hand.L", "Left Hand", "left_hand");

        if (rightHandRoot == null)
            Debug.LogWarning("[HandPoser] Could not auto-find RightHand root. "
                + "Rename the GameObject to 'RightHand' or assign it in the Inspector.", this);
        if (leftHandRoot == null)
            Debug.LogWarning("[HandPoser] Could not auto-find LeftHand root. "
                + "Rename the GameObject to 'LeftHand' or assign it in the Inspector.", this);
    }

    /// Finds the HoldPoint transform — the anchor centred between both palms.
    void AutoFindHoldPoint()
    {
        if (holdPoint != null) return;

        // 1. Tag-based (fastest) — tag the HoldPoint GameObject "HoldPoint"
        GameObject tagged = GameObject.FindWithTag("HoldPoint");
        if (tagged != null) { holdPoint = tagged.transform; return; }

        // 2. Child name search inside this Hands object
        holdPoint = FindChildByAnyName(transform, "HoldPoint", "GrabAnchor", "GrabPoint");

        if (holdPoint == null)
            Debug.LogWarning("[HandPoser] HoldPoint not found. "
                + "Add a child named 'HoldPoint' under the Hands object "
                + "at local position (0, -0.10, 0.45).", this);
    }

    /// Searches every child Transform for finger bones by name.
    /// Already-assigned Inspector values are never overwritten.
    void AutoFindBones()
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);

        // ── Right bone.02 ────────────────────────────────────────────────────
        index_R  = index_R  ?? FindBone(all, "finger.index.02.R",  "index_R",  "Index_R",  "r_index_02",  "Index.R");
        middle_R = middle_R ?? FindBone(all, "finger.middle.02.R", "middle_R", "Middle_R", "r_middle_02", "Middle.R");
        ring_R   = ring_R   ?? FindBone(all, "finger.ring.02.R",   "ring_R",   "Ring_R",   "r_ring_02",   "Ring.R");
        pinky_R  = pinky_R  ?? FindBone(all, "finger.pinky.02.R",  "pinky_R",  "Pinky_R",  "r_pinky_02",  "Pinky.R");
        thumb_R  = thumb_R  ?? FindBone(all, "finger.thumb.02.R",  "thumb_R",  "Thumb_R",  "r_thumb_02",  "Thumb.R");

        // ── Right bone.03 / tips ─────────────────────────────────────────────
        index_R_tip  = index_R_tip  ?? FindBone(all, "finger.index.03.R",  "index_R_tip",  "Index_R_tip",  "r_index_03");
        middle_R_tip = middle_R_tip ?? FindBone(all, "finger.middle.03.R", "middle_R_tip", "Middle_R_tip", "r_middle_03");
        ring_R_tip   = ring_R_tip   ?? FindBone(all, "finger.ring.03.R",   "ring_R_tip",   "Ring_R_tip",   "r_ring_03");
        pinky_R_tip  = pinky_R_tip  ?? FindBone(all, "finger.pinky.03.R",  "pinky_R_tip",  "Pinky_R_tip",  "r_pinky_03");
        thumb_R_tip  = thumb_R_tip  ?? FindBone(all, "finger.thumb.03.R",  "thumb_R_tip",  "Thumb_R_tip",  "r_thumb_03");

        // ── Left bone.02 ─────────────────────────────────────────────────────
        index_L  = index_L  ?? FindBone(all, "finger.index.02.L",  "index_L",  "Index_L",  "l_index_02",  "Index.L");
        middle_L = middle_L ?? FindBone(all, "finger.middle.02.L", "middle_L", "Middle_L", "l_middle_02", "Middle.L");
        ring_L   = ring_L   ?? FindBone(all, "finger.ring.02.L",   "ring_L",   "Ring_L",   "l_ring_02",   "Ring.L");
        pinky_L  = pinky_L  ?? FindBone(all, "finger.pinky.02.L",  "pinky_L",  "Pinky_L",  "l_pinky_02",  "Pinky.L");
        thumb_L  = thumb_L  ?? FindBone(all, "finger.thumb.02.L",  "thumb_L",  "Thumb_L",  "l_thumb_02",  "Thumb.L");

        // ── Left bone.03 / tips ──────────────────────────────────────────────
        index_L_tip  = index_L_tip  ?? FindBone(all, "finger.index.03.L",  "index_L_tip",  "Index_L_tip",  "l_index_03");
        middle_L_tip = middle_L_tip ?? FindBone(all, "finger.middle.03.L", "middle_L_tip", "Middle_L_tip", "l_middle_03");
        ring_L_tip   = ring_L_tip   ?? FindBone(all, "finger.ring.03.L",   "ring_L_tip",   "Ring_L_tip",   "l_ring_03");
        pinky_L_tip  = pinky_L_tip  ?? FindBone(all, "finger.pinky.03.L",  "pinky_L_tip",  "Pinky_L_tip",  "l_pinky_03");
        thumb_L_tip  = thumb_L_tip  ?? FindBone(all, "finger.thumb.03.L",  "thumb_L_tip",  "Thumb_L_tip",  "l_thumb_03");

        LogMissingBones();
    }

    // ── Helper: generic child-by-name search ──────────────────────────────────

    /// Returns the first child Transform (including inactive) whose name
    /// case-insensitively matches any of the provided candidate strings.
    Transform FindChildByAnyName(Transform root, params string[] candidates)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        foreach (string candidate in candidates)
            foreach (Transform t in all)
                if (string.Equals(t.name, candidate, System.StringComparison.OrdinalIgnoreCase))
                    return t;
        return null;
    }

    /// Same as FindChildByAnyName but scans a pre-collected pool (avoids
    /// repeated GetComponentsInChildren calls in the bone-search hot path).
    Transform FindBone(Transform[] pool, params string[] candidates)
    {
        foreach (string candidate in candidates)
            foreach (Transform t in pool)
                if (string.Equals(t.name, candidate, System.StringComparison.OrdinalIgnoreCase))
                    return t;
        return null;
    }

    void LogMissingBones()
    {
        if (!Debug.isDebugBuild && !Application.isEditor) return;

        var missing = new System.Text.StringBuilder();
        void Check(Transform t, string label) { if (t == null) missing.AppendLine("  • " + label); }

        Check(index_R,      "index_R");      Check(index_R_tip,  "index_R_tip");
        Check(middle_R,     "middle_R");     Check(middle_R_tip, "middle_R_tip");
        Check(ring_R,       "ring_R");       Check(ring_R_tip,   "ring_R_tip");
        Check(pinky_R,      "pinky_R");      Check(pinky_R_tip,  "pinky_R_tip");
        Check(thumb_R,      "thumb_R");      Check(thumb_R_tip,  "thumb_R_tip");
        Check(index_L,      "index_L");      Check(index_L_tip,  "index_L_tip");
        Check(middle_L,     "middle_L");     Check(middle_L_tip, "middle_L_tip");
        Check(ring_L,       "ring_L");       Check(ring_L_tip,   "ring_L_tip");
        Check(pinky_L,      "pinky_L");      Check(pinky_L_tip,  "pinky_L_tip");
        Check(thumb_L,      "thumb_L");      Check(thumb_L_tip,  "thumb_L_tip");

        if (missing.Length > 0)
            Debug.LogWarning("[HandPoser] Could not auto-find the following bones on '"
                + gameObject.name + "'.\nAssign them in the Inspector or rename to match "
                + "a supported convention (see file header).\n" + missing, this);
    }

    // ── Pose initialisation ───────────────────────────────────────────────────

    IEnumerator InitializePoses()
    {
        // Wait two frames so the rig Animator has settled into its rest pose
        // before we capture openRotations.
        yield return null;
        yield return null;

        openRotations  = new Quaternion[allFingers.Length];
        grabRotations  = new Quaternion[allFingers.Length];
        pointRotations = new Quaternion[allFingers.Length];

        for (int i = 0; i < allFingers.Length; i++)
        {
            if (allFingers[i] == null) continue;

            // Index ranges in allFingers (see Start):
            //   bone.02 : [0–4]  (R) and [10–14] (L)
            //   bone.03 : [5–9]  (R) and [15–19] (L)
            bool isBone02   = (i >= 0  && i <= 4)  || (i >= 10 && i <= 14);
            bool isBone03   = (i >= 5  && i <= 9)  || (i >= 15 && i <= 19);
            bool isThumb02  = (i == 4  || i == 14);
            bool isThumb03  = (i == 9  || i == 19);
            bool isIndexR02 = (i == 0);
            bool isIndexR03 = (i == 5);

            Quaternion restRot = allFingers[i].localRotation;

            // Open pose: non-thumb bone.02s use the calibrated default angle
            openRotations[i] = (isThumb02 || isThumb03) ? restRot
                              : isBone02                 ? defaultPose02
                              :                            restRot;

            // Grab pose: both hands curl symmetrically around the plank
            grabRotations[i] = (isThumb02 || isThumb03) ? restRot
                              : isBone02                 ? grabPose02
                              : isBone03                 ? grabPose03
                              :                            restRot;

            // Point pose: only right index extends; left hand stays open
            if (i >= 10)            // left hand mirrors open for pointing
                pointRotations[i] = openRotations[i];
            else if (isThumb02)
                pointRotations[i] = pointThumb02;
            else if (isThumb03)
                pointRotations[i] = restRot;
            else if (isIndexR02)
                pointRotations[i] = pointIndex02;
            else if (isIndexR03)
                pointRotations[i] = pointIndex03;
            else if (isBone02)
                pointRotations[i] = pointOthers02;
            else
                pointRotations[i] = grabPose03;
        }

        currentTargetRotations = openRotations;
        initialized = true;
    }

    // ── Per-frame update ──────────────────────────────────────────────────────

    void Update()
    {
        // Visibility and movement work even before bones are initialized.
        HandleVisibility();
        MoveHands();

        // Finger animation requires the pose arrays — skip until ready.
        if (!initialized) return;
        AnimateFingers();
    }

    void HandleVisibility()
    {
        // H toggles hands. R is taken by CNCInputHandler (spindle-up).
        if (!Input.GetKeyDown(KeyCode.H)) return;

        handsVisible = !handsVisible;
        Debug.Log($"[HandPoser] H pressed → handsVisible={handsVisible}  " +
                  $"target localY={(handsVisible ? visiblePosition.y : hiddenPosition.y):F3}  " +
                  $"current localY={transform.localPosition.y:F3}", this);

        // Belt-and-suspenders: also toggle the actual hand mesh children
        // so the hands appear/disappear even if position-slide is misconfigured.
        if (useSetActive)
        {
            if (rightHandRoot != null) rightHandRoot.gameObject.SetActive(handsVisible);
            if (leftHandRoot  != null) leftHandRoot.gameObject.SetActive(handsVisible);
        }
    }

    void MoveHands()
    {
        // Safety: if visible/hidden positions are accidentally equal in the Inspector,
        // log once and skip — nothing will ever move.
        if (visiblePosition == hiddenPosition)
        {
            Debug.LogWarning("[HandPoser] visiblePosition == hiddenPosition — hands cannot slide. " +
                             "Set different Y values in the Inspector.", this);
            return;
        }

        Vector3 target = handsVisible ? visiblePosition : hiddenPosition;
        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition, target, moveSpeed * Time.deltaTime);
    }

    void AnimateFingers()
    {
        // Both left and right fingers are driven by the same target array.
        // For the plank grip, this produces the correct symmetric two-handed pose.
        for (int i = 0; i < allFingers.Length; i++)
        {
            if (allFingers[i] == null) continue;
            allFingers[i].localRotation = Quaternion.Lerp(
                allFingers[i].localRotation,
                currentTargetRotations[i],
                Time.deltaTime * animationSpeed);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// Switch the finger pose on both hands simultaneously.
    /// Valid names: "Open", "Grab", "Point"
    public void SetPose(string poseName)
    {
        if (!initialized) return;
        switch (poseName)
        {
            case "Open":  currentTargetRotations = openRotations;  break;
            case "Grab":  currentTargetRotations = grabRotations;  break;
            case "Point": currentTargetRotations = pointRotations; break;
            default:
                Debug.LogWarning($"[HandPoser] Unknown pose name: '{poseName}'.", this);
                break;
        }
    }
}
