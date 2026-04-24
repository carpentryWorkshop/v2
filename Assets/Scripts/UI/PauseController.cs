using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarpentryWorkshop.UI
{
    public class PauseController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The pause menu canvas GameObject. Activated when paused.")]
        [SerializeField] private GameObject pauseCanvas;

        [Header("Scene Names")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private string gameplaySceneName = "CoffeeShopInteriorDAY";

        [Header("Input")]
        [Tooltip("Key that toggles the pause menu.")]
        [SerializeField] private KeyCode pauseKey = KeyCode.Space;

        public static bool IsPaused { get; private set; }

        void Start()
        {
            if (pauseCanvas != null) pauseCanvas.SetActive(false);
            IsPaused = false;
            Time.timeScale = 1f;
            SetCursorLocked(true);
        }

        void Update()
        {
            if (Input.GetKeyDown(pauseKey))
            {
                if (IsPaused) Resume();
                else Pause();
            }
        }

        public void Pause()
        {
            IsPaused = true;
            if (pauseCanvas != null) pauseCanvas.SetActive(true);
            Time.timeScale = 0f;
            SetCursorLocked(false);
        }

        public void Resume()
        {
            IsPaused = false;
            if (pauseCanvas != null) pauseCanvas.SetActive(false);
            Time.timeScale = 1f;
            SetCursorLocked(true);
        }

        /// <summary>Restart the gameplay scene from a fresh state.</summary>
        public void NewGame()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            PlayerPrefs.SetInt("HasSave", 1);
            PlayerPrefs.Save();
            // Phase 2 TODO: wipe any in-progress save data here.
            SceneManager.LoadScene(gameplaySceneName);
        }

        /// <summary>Return to the main menu scene.</summary>
        public void QuitToMainMenu()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
        }

        void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}