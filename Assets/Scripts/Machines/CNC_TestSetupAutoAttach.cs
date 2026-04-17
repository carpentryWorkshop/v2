using UnityEngine;

public static class CNC_TestSetupAutoAttach
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Setup()
    {
        AttachTo("cncCutter", typeof(CNC_CutterAxisZ));
        AttachTo("spindleHolder", typeof(CNC_SpindleHolderAxisX));
        AttachTo("spindleFinal", typeof(CNC_SpindleFinalAxisZ));
        AttachTo("meche", typeof(CNC_MecheRotation));

        DisableLegacyIfPresent<CNCCutter>("cncCutter");
        DisableLegacyIfPresent<MecheRotator>("meche");
    }

    private static void AttachTo(string gameObjectName, System.Type componentType)
    {
        GameObject target = GameObject.Find(gameObjectName);
        if (target == null)
        {
            Debug.LogWarning($"[CNC_TestSetupAutoAttach] GameObject not found: {gameObjectName}");
            return;
        }

        if (target.GetComponent(componentType) == null)
        {
            target.AddComponent(componentType);
            Debug.Log($"[CNC_TestSetupAutoAttach] Added {componentType.Name} to {gameObjectName}");
        }
    }

    private static void DisableLegacyIfPresent<T>(string gameObjectName) where T : Behaviour
    {
        GameObject target = GameObject.Find(gameObjectName);
        if (target == null) return;

        T legacy = target.GetComponent<T>();
        if (legacy != null)
        {
            legacy.enabled = false;
            Debug.Log($"[CNC_TestSetupAutoAttach] Disabled legacy {typeof(T).Name} on {gameObjectName}");
        }
    }
}
