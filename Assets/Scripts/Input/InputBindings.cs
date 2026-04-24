using UnityEngine;

namespace CarpentryWorkshop.InputSystem
{
    public enum PlayerAction
    {
        Forward,
        Backward,
        StrafeLeft,
        StrafeRight,
        Interact
    }

    public static class InputBindings
    {
        const string PREF_PREFIX = "Binding_";

        // NOTE: Unity KeyCodes map to physical QWERTY positions.
        // On AZERTY: KeyCode.W = physical Z key, KeyCode.A = physical Q key,
        // KeyCode.Q = physical A key. So these defaults produce ZQSD movement on AZERTY.
        static readonly KeyCode[] defaults =
        {
            KeyCode.W, // Forward  (AZERTY "Z" key)
            KeyCode.S, // Backward (AZERTY "S" key)
            KeyCode.A, // StrafeLeft  (AZERTY "Q" key)
            KeyCode.D, // StrafeRight (AZERTY "D" key)
            KeyCode.P  // Interact (same key on both layouts)
        };

        static KeyCode[] current;

        static InputBindings()
        {
            Load();
        }

        public static KeyCode Get(PlayerAction action)
        {
            if (current == null) Load();
            return current[(int)action];
        }

        public static void Set(PlayerAction action, KeyCode key)
        {
            if (current == null) Load();
            current[(int)action] = key;
            PlayerPrefs.SetInt(PREF_PREFIX + action, (int)key);
            PlayerPrefs.Save();
        }

        public static void ResetToDefaults()
        {
            for (int i = 0; i < defaults.Length; i++)
            {
                current[i] = defaults[i];
                PlayerPrefs.DeleteKey(PREF_PREFIX + (PlayerAction)i);
            }
            PlayerPrefs.Save();
        }

        static void Load()
        {
            current = new KeyCode[defaults.Length];
            for (int i = 0; i < defaults.Length; i++)
            {
                int saved = PlayerPrefs.GetInt(PREF_PREFIX + (PlayerAction)i, (int)defaults[i]);
                current[i] = (KeyCode)saved;
            }
        }
    }
}