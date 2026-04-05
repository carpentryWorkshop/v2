using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A 3D button in world space that can be pressed via raycast interaction.
/// Works with the existing <see cref="PlayerInteraction"/> system (P key to press).
///
/// Attach this to the button mesh GameObjects on the control panel:
/// - 'start' button
/// - 'stop' button  
/// - 'switch' button (mode toggle)
///
/// Extends <see cref="Interactable"/> for compatibility with existing interaction system.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CNC3DButton : Interactable
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Button Settings")]
    [Tooltip("Event fired when button is pressed.")]
    [SerializeField] private UnityEvent _onPressed;

    [Tooltip("Cooldown between presses (prevents spam).")]
    [SerializeField] [Range(0.1f, 2f)] private float _cooldown = 0.3f;

    [Header("Visual Feedback")]
    [Tooltip("How far the button moves when pressed (local Z axis).")]
    [SerializeField] private float _pressDepth = 0.005f;

    [Tooltip("How fast the button returns to rest position.")]
    [SerializeField] private float _returnSpeed = 10f;

    [Header("Audio Feedback (optional)")]
    [Tooltip("Sound to play when pressed.")]
    [SerializeField] private AudioClip _pressSound;

    [Tooltip("AudioSource to play sounds. If null, creates one.")]
    [SerializeField] private AudioSource _audioSource;

    [Header("Color Feedback (optional)")]
    [Tooltip("Renderer to change color on press.")]
    [SerializeField] private Renderer _buttonRenderer;

    [Tooltip("Color when button is pressed.")]
    [SerializeField] private Color _pressedColor = new Color(0.5f, 0.5f, 0.5f);

    [Tooltip("Duration of color flash.")]
    [SerializeField] private float _colorFlashDuration = 0.1f;

    // ── Private state ─────────────────────────────────────────────────────────

    private Vector3 _restPosition;
    private Vector3 _pressedPosition;
    private bool _isPressed;
    private float _lastPressTime;
    private Color _originalColor;
    private Material _material;
    private float _colorFlashTimer;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _restPosition = transform.localPosition;
        _pressedPosition = _restPosition - transform.forward * _pressDepth;

        // Setup audio
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();

        // Setup color feedback
        if (_buttonRenderer != null)
        {
            _material = _buttonRenderer.material;
            _originalColor = _material.color;
        }
    }

    private void Update()
    {
        // Animate button return
        if (!_isPressed && Vector3.Distance(transform.localPosition, _restPosition) > 0.0001f)
        {
            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                _restPosition,
                _returnSpeed * Time.deltaTime
            );
        }

        // Handle color flash
        if (_colorFlashTimer > 0f)
        {
            _colorFlashTimer -= Time.deltaTime;
            if (_colorFlashTimer <= 0f && _material != null)
            {
                _material.color = _originalColor;
            }
        }
    }

    // ── Interactable Override ─────────────────────────────────────────────────

    /// <summary>
    /// Called by PlayerInteraction when the player presses this button.
    /// </summary>
    public override void Interact()
    {
        Press();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Presses the button, triggering visual/audio feedback and the OnPressed event.
    /// </summary>
    public void Press()
    {
        // Check cooldown
        if (Time.time - _lastPressTime < _cooldown)
            return;

        _lastPressTime = Time.time;
        _isPressed = true;

        // Visual feedback - move button
        transform.localPosition = _pressedPosition;

        // Color feedback
        if (_material != null)
        {
            _material.color = _pressedColor;
            _colorFlashTimer = _colorFlashDuration;
        }

        // Audio feedback
        if (_pressSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_pressSound);
        }

        // Fire event
        _onPressed?.Invoke();

        // Schedule release
        Invoke(nameof(Release), 0.1f);

        Debug.Log($"[CNC3DButton] {name} pressed.");
    }

    /// <summary>
    /// Releases the button (called automatically after press).
    /// </summary>
    public void Release()
    {
        _isPressed = false;
        Debug.Log($"[CNC3DButton] {name} released.");
    }

    /// <summary>
    /// Adds a listener to the OnPressed event.
    /// </summary>
    public void AddPressListener(UnityAction action)
    {
        _onPressed.AddListener(action);
    }

    /// <summary>
    /// Removes a listener from the OnPressed event.
    /// </summary>
    public void RemovePressListener(UnityAction action)
    {
        _onPressed.RemoveListener(action);
    }

    // ── Editor helpers ────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        // Show press depth
        Gizmos.color = Color.yellow;
        Vector3 pressPos = transform.position - transform.forward * _pressDepth;
        Gizmos.DrawWireSphere(pressPos, 0.005f);
        Gizmos.DrawLine(transform.position, pressPos);
    }
}
