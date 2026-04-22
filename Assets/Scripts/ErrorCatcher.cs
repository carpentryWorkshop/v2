using UnityEngine;
using System.IO;

public class ErrorCatcher : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Application.logMessageReceived += OnLog;
        Directory.CreateDirectory("C:/Temp");
        File.WriteAllText("C:/Temp/unity_errors.txt", "=== Error log started " + System.DateTime.Now + " ===\n");
    }

    private void OnLog(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception)
        {
            File.AppendAllText("C:/Temp/unity_errors.txt",
                "[" + System.DateTime.Now.ToString("HH:mm:ss") + "] " + type + ": " + message + "\n" + stackTrace + "\n===\n");
        }
    }
}
