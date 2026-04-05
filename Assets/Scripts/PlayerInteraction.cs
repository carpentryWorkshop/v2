using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    public float interactDistance = 3f;
    public Camera playerCamera;
    public HandPoser handPoser;

    private PlankInteractable heldPlank;

    void Update()
    {
        // E key — curl all fingers + try grab
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (handPoser != null)
                handPoser.SetPose("Grab");

            if (heldPlank == null)
                TryGrab();
            else
                Drop();
        }

        // Release E — open fingers (only if not holding plank)
        if (Input.GetKeyUp(KeyCode.E))
        {
            if (heldPlank == null)
            {
                if (handPoser != null)
                    handPoser.SetPose("Open");
            }
        }

        // P key — point index finger + try press button
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (handPoser != null)
                handPoser.SetPose("Point");

            TryPressButton();
        }

        // Release P — go back to open
        if (Input.GetKeyUp(KeyCode.P))
        {
            if (handPoser != null)
                handPoser.SetPose("Open");
        }
    }

    void TryGrab()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance))
        {
            Debug.Log("Hit: " + hit.collider.name);
            PlankInteractable plank = hit.collider.GetComponent<PlankInteractable>();

            if (plank != null)
            {
                heldPlank = plank;
                heldPlank.PickUp();
            }
        }
    }

    void TryPressButton()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance))
        {
            Debug.Log("Button hit: " + hit.collider.name);
            Interactable interactable = hit.collider.GetComponent<Interactable>();

            if (interactable != null)
                interactable.Interact();
        }
    }

    void Drop()
    {
        if (heldPlank != null)
        {
            heldPlank.PutDown();
            heldPlank = null;
            if (handPoser != null)
                handPoser.SetPose("Open");
        }
    }
}