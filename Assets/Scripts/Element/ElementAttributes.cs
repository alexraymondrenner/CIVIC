using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

[Serializable]
public class ElementKeyValue
{
    public string key;
    public string value;
}

public class ElementAttributes : MonoBehaviourPun
{
    public ElementType type;

    [Header("Element Values")]
    public List<ElementKeyValue> keyValues =
        new List<ElementKeyValue>();

    public void SetAttributeValue(
        string key,
        string newValue
    )
    {
        foreach (ElementKeyValue pair in keyValues)
        {
            if (pair != null && pair.key == key)
            {
                pair.value = newValue;
                return;
            }
        }

        // Add the key if it does not already exist.
        keyValues.Add(new ElementKeyValue
        {
            key = key,
            value = newValue
        });
    }
    public bool HasKey(string key)
    {
        foreach (ElementKeyValue pair in keyValues)
        {
            if (pair != null && pair.key == key)
            {
                return true;
            }
        }

        return false;
    }
    public string GetValue(string key)
    {
        foreach (ElementKeyValue pair in keyValues)
        {
            if (pair != null && pair.key == key)
            {
                return pair.value;
            }
        }

        return string.Empty;
    }
    public void SyncTypeFromElementData(ElementData data)
    {
        if (data == null)
        {
            Debug.LogWarning("Cannot sync from null ElementData.");
            return;
        }

        type = data.type;
    }

    private void OnDestroy()
    {
        if (!string.IsNullOrEmpty(gameObject.name) &&
            UserDataManager.Instance != null)
        {
            UserDataManager.Instance.RemoveElement(
                gameObject.name
            );
        }
    }
}