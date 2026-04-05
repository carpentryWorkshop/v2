using UnityEngine;

public class GrabSystem : MonoBehaviour
{
    [Header("Grab Settings")]
    public float grabRange = 3f;
    public float holdDistance = 2f;
    public float throwForce = 10f;

    [Header("References")]
    public Camera playerCamera;
    public Transform holdPoint;
    public HandPoser handPoser;

    private Rigidbody heldObject;
    private bool isHolding = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (!isHolding)
                TryGrab();
            else
                Release();
        }

        if (isHolding)
            HoldObject();
    }

    void TryGrab()
    {
        Debug.Log("Trying to grab...");

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, grabRange))
        {
            Debug.Log("Hit: " + hit.collider.name + " Tag: " + hit.collider.tag);

            if (hit.collider.CompareTag("Grabbable"))
            {
                heldObject = hit.collider.GetComponent<Rigidbody>();

                if (heldObject != null)
                {
                    heldObject.useGravity = false;
                    heldObject.linearDamping = 10;
                    heldObject.angularDamping = 10;
                    isHolding = true;

                    if (handPoser != null)
                        handPoser.SetPose("Grab");

                    Debug.Log("Grabbed: " + heldObject.name);
                }
                else
                {
                    Debug.Log("No Rigidbody found on object!");
                }
            }
            else
            {
                Debug.Log("Object not tagged as Grabbable!");
            }
        }
        else
        {
            Debug.Log("Nothing hit - object might be out of range");
        }
    }

    void HoldObject()
    {
        Vector3 targetPosition = playerCamera.transform.position +
            playerCamera.transform.forward * holdDistance;

        Vector3 direction = targetPosition - heldObject.transform.position;
        heldObject.linearVelocity = direction * 10f;

        heldObject.transform.rotation = Quaternion.Lerp(
            heldObject.transform.rotation,
            playerCamera.transform.rotation,
            Time.deltaTime * 5f
        );
    }

    void Release()
    {
        if (heldObject != null)
        {
            heldObject.useGravity = true;
            heldObject.linearDamping = 1;
            heldObject.angularDamping = 0.05f;
            heldObject = null;
        }

        isHolding = false;

        if (handPoser != null)
            handPoser.SetPose("Open");
    }
}