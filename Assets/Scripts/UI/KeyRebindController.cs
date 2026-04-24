using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CarpentryWorkshop.InputSystem;

namespace CarpentryWorkshop.UI
{
    public class KeyRebindController : MonoBehaviour
    {
        [Header("Assign Buttons (order: Forward, Backward, StrafeLeft, StrafeRight, Interact)")]
        [SerializeField] private Button[] assignButtons = new Button[5];

        [Header("Labels (child 'notassigned' TMP_Text of each button, same order)")]
        [SerializeField] private TMP_Text[] keyLabels = new TMP_Text[5];

        [Header("Popup")]
        [SerializeField] private GameObject keyConfirmationPopup;
        [SerializeField] private Button cancelButton;

        bool waitingForKey;
        int waitingActionIndex = -1;
        int framesSinceStart;

        void Awake()
        {
            for (int i = 0; i < assignButtons.Length; i++)
            {
                if (assignButtons[i] == null) continue;
                int captured = i;
                assignButtons[i].onClick.RemoveAllListeners();
                assignButtons[i].onClick.AddListener(() => StartRebinding(captured));
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(CancelRebinding);
            }

            if (keyConfirmationPopup != null)
                keyConfirmationPopup.SetActive(false);
        }

        void OnEnable()
        {
            RefreshAllLabels();
        }

        void Update()
        {
            if (!waitingForKey) return;

            framesSinceStart++;
            if (framesSinceStart < 2) return; // ignore the same frame as the click

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelRebinding();
                return;
            }

            if (!Input.anyKeyDown) return;

            foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (key == KeyCode.None) continue;
                if (key == KeyCode.Escape) continue;
                if (key >= KeyCode.JoystickButton0) continue;
                if (Input.GetKeyDown(key))
                {
                    AssignKey(key);
                    return;
                }
            }
        }

        void StartRebinding(int actionIndex)
        {
            waitingForKey = true;
            waitingActionIndex = actionIndex;
            framesSinceStart = 0;
            if (keyConfirmationPopup != null)
                keyConfirmationPopup.SetActive(true);
        }

        void AssignKey(KeyCode key)
        {
            if (waitingActionIndex < 0 || waitingActionIndex >= 5) return;
            InputBindings.Set((PlayerAction)waitingActionIndex, key);
            waitingForKey = false;
            waitingActionIndex = -1;
            if (keyConfirmationPopup != null)
                keyConfirmationPopup.SetActive(false);
            RefreshAllLabels();
        }

        void CancelRebinding()
        {
            waitingForKey = false;
            waitingActionIndex = -1;
            if (keyConfirmationPopup != null)
                keyConfirmationPopup.SetActive(false);
        }

        void RefreshAllLabels()
        {
            for (int i = 0; i < keyLabels.Length; i++)
            {
                if (keyLabels[i] != null)
                    keyLabels[i].text = InputBindings.Get((PlayerAction)i).ToString();
            }
        }
    }
}