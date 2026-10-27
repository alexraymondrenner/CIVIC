using UnityEngine;
using System.Collections.Generic;

public class DebugDisplay : MonoBehaviour
{
    private static Queue<string> logs = new Queue<string>();
    private const int maxLogs = 15; // adjust this

    void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string message, string stackTrace, LogType type)
    {
        logs.Enqueue(message);

        if (logs.Count > maxLogs)
            logs.Dequeue();
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 1000, 500));
        GUILayout.Label("=== DEBUG LOG ===");

        foreach (string log in logs)
        {
            GUILayout.Label(log);
        }

        GUILayout.EndArea();
    }
}
