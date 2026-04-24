using UnityEngine;
using CarpentryWorkshop.UI;
using CarpentryWorkshop.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float verticalSpeed = 5f;   // A/E (fly)
    public bool flyMode = true;

    [Header("Mouse Look")]
    public float mouseSensitivity = 2.0f;
    public Transform cameraRoot;       // Main Camera transform
    public float pitchMin = -90f;
    public float pitchMax = 90f;

    [Header("Cursor Lock")]
    public bool lockCursorOnStart = true;
    public KeyCode unlockKey = KeyCode.Escape;  // ESC toggles cursor lock

    CharacterController controller;
    float pitch;
    bool cursorLocked;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraRoot == null && Camera.main != null)
            cameraRoot = Camera.main.transform;

        cursorLocked = lockCursorOnStart;
        ApplyCursorState();
    }

    void Update()
    {
        if (PauseController.IsPaused) return;

        if (Input.GetKeyDown(unlockKey))
        {
            cursorLocked = !cursorLocked;
            ApplyCursorState();
        }

        if (controller == null || cameraRoot == null)
            return;

        // Mouse look only when cursor is locked
        if (cursorLocked)
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

            transform.Rotate(Vector3.up * mouseX);
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // Movement — reads from remappable InputBindings
        float moveX = 0f;
        if (Input.GetKey(InputBindings.Get(PlayerAction.StrafeLeft)))  moveX -= 1f;
        if (Input.GetKey(InputBindings.Get(PlayerAction.StrafeRight))) moveX += 1f;

        float moveZ = 0f;
        if (Input.GetKey(InputBindings.Get(PlayerAction.Forward)))  moveZ += 1f;
        if (Input.GetKey(InputBindings.Get(PlayerAction.Backward))) moveZ -= 1f;

        Vector3 move = (transform.right * moveX + transform.forward * moveZ).normalized * moveSpeed;

        // Vertical fly — Q/E in Unity KeyCode = AZERTY physical "A"/"E" keys. Not remappable for now.
        float upDown = 0f;
        if (flyMode)
        {
            if (Input.GetKey(KeyCode.Q)) upDown += 1f; // up   (AZERTY "A")
            if (Input.GetKey(KeyCode.E)) upDown -= 1f; // down (AZERTY "E")
        }

        Vector3 vertical = Vector3.up * (upDown * verticalSpeed);

        controller.Move((move + vertical) * Time.deltaTime);

        // Interact — bound key, no logic yet (hook up later)
        if (Input.GetKeyDown(InputBindings.Get(PlayerAction.Interact)))
        {
            // TODO: interact logic (pick up tools, activate CNC, etc.)
        }
    }

    void ApplyCursorState()
    {
        Cursor.lockState = cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !cursorLocked;
    }
}