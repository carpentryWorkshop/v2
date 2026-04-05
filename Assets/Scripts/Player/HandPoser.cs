using UnityEngine;
using System.Collections;

public class HandPoser : MonoBehaviour
{
    [Header("Finger Bones - Right")]
    public Transform index_R;
    public Transform middle_R;
    public Transform ring_R;
    public Transform pinky_R;
    public Transform thumb_R;

    [Header("Finger Tip Bones - Right")]
    public Transform index_R_tip;
    public Transform middle_R_tip;
    public Transform ring_R_tip;
    public Transform pinky_R_tip;
    public Transform thumb_R_tip;

    [Header("Finger Bones - Left")]
    public Transform index_L;
    public Transform middle_L;
    public Transform ring_L;
    public Transform pinky_L;
    public Transform thumb_L;

    [Header("Finger Tip Bones - Left")]
    public Transform index_L_tip;
    public Transform middle_L_tip;
    public Transform ring_L_tip;
    public Transform pinky_L_tip;
    public Transform thumb_L_tip;

    [Header("Hand Visibility")]
    public float moveSpeed = 0.8f;
    public Vector3 visiblePosition = new Vector3(-0.006f, -0.925f, 0.65f);
    public Vector3 hiddenPosition = new Vector3(-0.006f, -2.5f, 0.65f);

    [Header("Finger Settings")]
    public float animationSpeed = 5f;

    private Quaternion[] openRotations;
    private Quaternion[] grabRotations;
    private Quaternion[] pointRotations;
    private Quaternion[] currentTargetRotations;
    private Transform[] allFingers;
    private bool handsVisible = false;
    private bool initialized = false;

    // Default/rest pose for bone.02
    private Quaternion defaultPose02 = Quaternion.Euler(11.7f, 16.37f, 3.44f);

    // E key — grab pose
    private Quaternion grabPose02 = Quaternion.Euler(55.03f, 20.4f, 5.88f);
    private Quaternion grabPose03 = Quaternion.Euler(24.405f, 0.661f, -0.476f);

    // P key — point pose
    private Quaternion pointThumb02 = Quaternion.Euler(45.06f, -4.807f, -30.24f);
    private Quaternion pointIndex02 = Quaternion.Euler(26.312f, 0.419f, -0.138f);
    private Quaternion pointIndex03 = Quaternion.Euler(0.261f, -0.854f, 0.422f);
    private Quaternion pointOthers02 = Quaternion.Euler(118.7f, 9.38f, -7.08f);

    void Start()
    {
        allFingers = new Transform[]
        {
            // bone.02 — index 0-4
            index_R, middle_R, ring_R, pinky_R, thumb_R,
            // bone.03 — index 5-9
            index_R_tip, middle_R_tip, ring_R_tip, pinky_R_tip, thumb_R_tip,
            // bone.02 left — index 10-14
            index_L, middle_L, ring_L, pinky_L, thumb_L,
            // bone.03 left — index 15-19
            index_L_tip, middle_L_tip, ring_L_tip, pinky_L_tip, thumb_L_tip
        };

        transform.localPosition = hiddenPosition;
        StartCoroutine(InitializePoses());
    }

    IEnumerator InitializePoses()
    {
        yield return null;
        yield return null;

        openRotations = new Quaternion[allFingers.Length];
        grabRotations = new Quaternion[allFingers.Length];
        pointRotations = new Quaternion[allFingers.Length];

        for (int i = 0; i < allFingers.Length; i++)
        {
            if (allFingers[i] == null) continue;

            bool isBone02 = (i >= 0 && i <= 4) || (i >= 10 && i <= 14);
            bool isBone03 = (i >= 5 && i <= 9) || (i >= 15 && i <= 19);

            bool isThumb02 = (i == 4 || i == 14);
            bool isThumb03 = (i == 9 || i == 19);
            bool isIndexR02 = (i == 0);
            bool isIndexR03 = (i == 5);

            Quaternion restRot = allFingers[i].localRotation;

            // Open pose
            if (isThumb02 || isThumb03)
                openRotations[i] = restRot;
            else if (isBone02)
                openRotations[i] = defaultPose02;
            else
                openRotations[i] = restRot;

            // Grab pose (E)
            if (isThumb02 || isThumb03)
                grabRotations[i] = restRot;
            else if (isBone02)
                grabRotations[i] = grabPose02;
            else if (isBone03)
                grabRotations[i] = grabPose03;
            else
                grabRotations[i] = restRot;

            // Point pose (P)
            if (i >= 10) // Left hand — stay in default open pose
            {
                pointRotations[i] = openRotations[i];
            }
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

    void Update()
    {
        if (!initialized) return;
        HandleVisibility();
        MoveHands();
        AnimateFingers();
    }

    void HandleVisibility()
    {
        if (Input.GetKeyDown(KeyCode.R))
            handsVisible = !handsVisible;
    }

    void MoveHands()
    {
        Vector3 target = handsVisible ? visiblePosition : hiddenPosition;
        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition,
            target,
            moveSpeed * Time.deltaTime
        );
    }

    void AnimateFingers()
    {
        for (int i = 0; i < allFingers.Length; i++)
        {
            if (allFingers[i] != null)
            {
                allFingers[i].localRotation = Quaternion.Lerp(
                    allFingers[i].localRotation,
                    currentTargetRotations[i],
                    Time.deltaTime * animationSpeed
                );
            }
        }
    }

    public void SetPose(string poseName)
    {
        if (!initialized) return;

        switch (poseName)
        {
            case "Open":
                currentTargetRotations = openRotations;
                break;
            case "Grab":
                currentTargetRotations = grabRotations;
                break;
            case "Point":
                currentTargetRotations = pointRotations;
                break;
        }
    }
}