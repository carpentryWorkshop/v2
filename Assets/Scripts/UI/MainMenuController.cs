using UnityEngine;
using SlimUI.ModernMenu;

namespace CarpentryWorkshop.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Scene Names")]
        [Tooltip("Scene name (without .unity) loaded by New Game and Continue.")]
        [SerializeField] private string workshopSceneName = "CoffeeShopInteriorDAY";

        [Header("References")]
        [Tooltip("The UIMenuManager on the menu canvas. Used so the SlimUI loading screen still plays.")]
        [SerializeField] private UIMenuManager menuManager;

        [Tooltip("Continue button GameObject. Hidden unless a save exists.")]
        [SerializeField] private GameObject continueButton;

        const string HAS_SAVE_KEY = "HasSave";

        void Awake()
        {
            // Continue is only visible if the player has started a game before.
            if (continueButton != null)
            {
                bool hasSave = PlayerPrefs.GetInt(HAS_SAVE_KEY, 0) == 1;
                continueButton.SetActive(hasSave);
            }
        }

        /// <summary>Wire the New Game button's OnClick to this method.</summary>
        public void OnNewGame()
        {
            // Phase 2 TODO: delete any saved progress file here before starting fresh.
            PlayerPrefs.SetInt(HAS_SAVE_KEY, 1);
            PlayerPrefs.Save();
            LoadWorkshop();
        }

        /// <summary>Wire the Continue button's OnClick to this method.</summary>
        public void OnContinue()
        {
            // Phase 2 TODO: after the workshop scene loads, apply the saved state
            // (player position, completed tasks, inventory, etc.).
            LoadWorkshop();
        }

        void LoadWorkshop()
        {
            if (menuManager != null)
            {
                // Uses SlimUI's loading flow so the "Press RETURN to continue" screen plays.
                menuManager.LoadScene(workshopSceneName);
            }
            else
            {
                // Fallback if the reference wasn't wired in the Inspector.
                UnityEngine.SceneManagement.SceneManager.LoadScene(workshopSceneName);
                Debug.LogWarning("[MainMenuController] No UIMenuManager assigned — loading without loading screen.");
            }
        }
    }
}