using System;
using System.Collections.Generic;
using UnityEngine;

public enum SystemUpdateType
{
    General,
    Inject,
    Action_Taken,
    Action_Text
}

[Serializable]
public class SystemUpdateData
{
    public string message;
    public SystemUpdateType updateType;
    public string relatedElementType;
    public DateTime timestamp;

    public SystemUpdateData(
        string message,
        SystemUpdateType updateType,
        string relatedElementType = ""
    )
    {
        this.message = message;
        this.updateType = updateType;
        this.relatedElementType = relatedElementType;
        timestamp = DateTime.Now;
    }
}

/// <summary>
/// Stores system updates and notifies the UI when a new update is added.
/// Other scripts should call AddUpdate() instead of directly changing the UI.
/// </summary>
public class SystemUpdateManager : MonoBehaviour
{
    public static SystemUpdateManager Instance
    {
        get;
        private set;
    }

    [SerializeField]
    private int maximumStoredUpdates = 100;

    [SerializeField]
    private List<SystemUpdateData> updates =
        new List<SystemUpdateData>();

    // UI scripts can subscribe to this event.
    public event Action<SystemUpdateData> OnUpdateAdded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void AddUpdate(
        string message,
        SystemUpdateType updateType =
            SystemUpdateType.General,
        string relatedElementType = ""
    )
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        SystemUpdateData update =
            new SystemUpdateData(
                message,
                updateType,
                relatedElementType
            );

        updates.Add(update);

        // Remove the oldest update when the limit is exceeded.
        if (updates.Count > maximumStoredUpdates)
        {
            updates.RemoveAt(0);
        }

        OnUpdateAdded?.Invoke(update);

        Debug.Log(
            $"[System Update] {update.message}"
        );
    }

   

    public IReadOnlyList<SystemUpdateData> GetUpdates()
    {
        return updates;
    }

    public void ClearUpdates()
    {
        updates.Clear();
    }
}