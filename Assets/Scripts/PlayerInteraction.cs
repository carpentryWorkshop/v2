using UnityEngine;

/// <summary>
/// Handles player input for grabbing planks and pressing buttons.
/// Both playerCamera and handPoser are auto-found at Awake; assign them in
/// the Inspector to override (useful if the hierarchy has multiple cameras).
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    public float interactDistance = 3f;

    // Optional Inspector overrides — auto-found if left blank.
    [SerializeField] private Camera    playerCamera;
    [SerializeField] private HandPoser handPoser;

    private PlankInteractable heldPlank;

    void Awake()
    {
        // Find the main camera if not assigned.
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (playerCamera == null)
            Debug.LogWarning("[PlayerInteraction] No camera found. Assign playerCamera in the Inspector.", this);

        // Find HandPoser anywhere in the scene if not assigned.
        if (handPoser == null)
            handPoser = FindObjectOfType<HandPoser>();

        if (handPoser == null)
            Debug.LogWarning("[PlayerInteraction] No HandPoser found in the scene.", this);
    }

    void Update()
    {
        // E — grab/drop + curl fingers
        if (Input.GetKeyDown(KeyCode.E))
        {
            handPoser?.SetPose("Grab");

            if (heldPlank == null) TryGrab();
            else                   Drop();
        }

        if (Input.GetKeyUp(KeyCode.E))
        {
            if (heldPlank == null)
                handPoser?.SetPose("Open");
        }

        // P — point + press button
        if (Input.GetKeyDown(KeyCode.P))
        {
            handPoser?.SetPose("Point");
            TryPressButton();
        }

        if (Input.GetKeyUp(KeyCode.P))
            handPoser?.SetPose("Open");
    }

    void TryGrab()
    {
        if (playerCamera == null) return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
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
        if (playerCamera == null) return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            Debug.Log("Button hit: " + hit.collider.name);
            Interactable interactable = hit.collider.GetComponent<Interactable>();
            interactable?.Interact();
        }
    }

    void Drop()
    {
        if (heldPlank == null) return;
        heldPlank.PutDown();
        heldPlank = null;
        handPoser?.SetPose("Open");
    }
}
