using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;


#region USER TYPE
public enum UserType
{
    Unassigned,
    Facilitator,
    Police,
    Firefighter,
    EMS,
    Tutorial
}
#endregion


#region USER DATA ROOT

[Serializable]
public class UserData
{
    // For visibility/debugging
    public string RoomID;
    public UserType userType = UserType.Unassigned;
    public string name;
    //need to add a score in here 
    // All elements
    public List<ElementData> elements = new List<ElementData>();
}

#endregion

#region ELEMENT TYPE

public enum ElementType
{
    Police1,
    Police2,
    EMS1,
    EMS2,
    Firefighter1,
    Firefighter2,
    Gator1,
    People,
    Debris,
    Building,
    ICP,
    Inject,
    Generic
}

#endregion

#region ELEMENT DATA (PURE DATA ONLY)

[Serializable]
public class ElementData
{
    public string id;
    public ElementType type;

    public Vector2 posRatio;
    public Vector2 visualOffset = Vector2.zero;

    public bool generalInjuct;
    public bool draggable;

    // Key-value storage
    public List<string> text = new List<string>();   // keys
    public List<string> value = new List<string>();  // values

    // Fired when position or size changes, currently size actually never changed but was added in for future work
    public event Action OnTransformChanged;
    public event Action OnTypeChanged;
    public event Action OnVisualOffsetChanged;


    // Fired when a specific key changes
    public event Action<string> OnValueChanged;

    // =========================
    // MARK CHANGE 
    // =========================
    public void MarkTransformChanged()
    {
        OnTransformChanged?.Invoke();
    }
    public void MarkVisualOffsetChanged()
    {
        OnVisualOffsetChanged?.Invoke();
    }

    public void MarkTypeChanged()
    {
        OnTypeChanged?.Invoke();
    }

    public void MarkValueChanged(string key)
    {
        OnValueChanged?.Invoke(key);
    }
    public string GetString(string key)
{
    int index = text.IndexOf(key);

    if (index < 0 || index >= value.Count)
    {
        return string.Empty;
    }

    return value[index];
}
}

#endregion

public class UserDataManager : MonoBehaviour
{
    public static UserDataManager Instance { get; private set; }

    [ReadOnly] public UserData userdata = new UserData();

    private Dictionary<string, ElementData> elementMap = new Dictionary<string, ElementData>();



    void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildCache();
    }


    void BuildCache()
    {
        elementMap.Clear();
        foreach (var e in userdata.elements)
            elementMap[e.id] = e;
    }

    public ElementData GetData(string id)
    {
        elementMap.TryGetValue(id, out var data);

        return data;
    }

    public ElementData CreateData(
        string id,
        ElementAttributes attributes
    )
    {
        if (attributes == null)
        {
            Debug.LogError(
                $"Cannot create ElementData for {id}: " +
                "ElementAttributes is null."
            );

            return null;
        }

        // Prevent duplicate data.
        ElementData existingData = GetData(id);

        if (existingData != null)
        {
            return existingData;
        }

        ElementData data = new ElementData
        {
            id = id,
            type = attributes.type,
            posRatio = Vector2.zero,
            generalInjuct = false,
            draggable = true,

            // Copy the Inspector values into the runtime data.
            text = new List<string>(),
            value = new List<string>()
        };

        // Copy Inspector values into ElementData.
        foreach (ElementKeyValue pair in attributes.keyValues)
        {
            if (pair == null ||
                string.IsNullOrWhiteSpace(pair.key))
            {
                continue;
            }

            // Prevent duplicate keys.
            int existingIndex =
                data.text.IndexOf(pair.key);

            if (existingIndex >= 0)
            {
                data.value[existingIndex] = pair.value;
            }
            else
            {
                data.text.Add(pair.key);
                data.value.Add(pair.value);
            }
        }


        userdata.elements.Add(data);
        elementMap[id] = data;

        return data;
    }


    // =========================
    // TRANSFORM
    // =========================
    public void SetPos(string id, Vector2 pos)
    {
        var e = GetData(id);
        if (e == null)
        {
            Debug.LogError($"No Data Found: {id}"); //Alex: Aug 13 10:05am testing - police, fire, and ems: "No Data Found Firefighter1" 
            return;
            //After dragging the first two (one for each vehicle type) - dragging the next two - they both have their position set to upper left corner of map - maybe that's a default location?
        }

        e.posRatio = pos;

        e.MarkTransformChanged();
 
    }
 
    public Vector2 GetPos(string id)
    {
        var data = GetData(id);
        if (data == null)
        {
            Debug.LogError($"No Data Found: {id}");
            return Vector2.zero;
        }

        return data.posRatio;
    }

    public void SetVisualOffset(string id, Vector2 offset)
    {
        ElementData data = GetData(id);
        if (data == null) return;

        data.visualOffset = offset;
        data.MarkVisualOffsetChanged();
    }

    public Vector2 GetVisualOffset(string id)
    {
        ElementData data = GetData(id);
        return data != null ? data.visualOffset : Vector2.zero;
    }

    public void SetGeneralInjuct(string id, bool unknownInjuct)
    {
        var e = GetData(id);
        if (e == null)
        {
            Debug.LogError($"No Data Found: {id}");
            return;
        }

        e.generalInjuct = unknownInjuct;
    }


    public bool GetGeneralInjuct(string id)
    {
        var e = GetData(id);
        if (e == null)
        {
            Debug.LogError($"No Data Found: {id}");
            return false;
        }

        return e.generalInjuct;
    }
    // =========================
    // Element Attribute
    // =========================
    public void SetType(string id,ElementType type)
    {
        var e = GetData(id);
        if (e == null)
        {
            Debug.LogError($"No Data Found: {id}");
        }

        e.type=type;
        e.MarkTypeChanged();
    }

    public ElementType GetType(string id)
    {
        var e = GetData(id);
        if (e == null)
        {
            Debug.LogError($"No Data Found: {id}");
            return ElementType.Inject;
        }

        return e.type;
    }

    // =========================
    // KEY-VALUE DATA
    // =========================

    public void SetValue(
        string id,
        ElementAttributes EA,
        string key,
        string newValue
    )
    {
        ElementData data = GetData(id);

        if (data == null)
        {
            Debug.LogError($"No Data Found: {id}");
            return;
        }

        int index = data.text.IndexOf(key);

        if (index >= 0)
        {
            if (index >= data.value.Count)
            {
                Debug.LogError(
                    $"Key-value lists are inconsistent for: {id}"
                );

                return;
            }

            data.value[index] = newValue;
        }
        else
        {
            data.text.Add(key);
            data.value.Add(newValue);
        }

        if (EA != null)
        {
            EA.SetAttributeValue(
                    key,
                    newValue
                );
        }

        data.MarkValueChanged(key);
    }

    public string GetValue(
        string id,
        string key
    )
    {
        ElementData data = GetData(id);

        if (data == null)
        {
            Debug.LogError($"No Data Found: {id}");
            return string.Empty;
        }

        return data.GetString(key);
    }

    public bool HasValue(string id, string key)
    {
        ElementData data = GetData(id);

        return data != null &&
               data.text.Contains(key);
    }

    public bool RemoveValue(string id, string key)
    {
        ElementData data = GetData(id);

        if (data == null)
        {
            Debug.LogError($"No Data Found: {id}");
            return false;
        }

        int index = data.text.IndexOf(key);

        if (index < 0 || index >= data.value.Count)
        {
            return false;
        }

        data.text.RemoveAt(index);
        data.value.RemoveAt(index);

        data.MarkValueChanged(key);

        return true;
    }

    // =========================
    // Draggable 
    // =========================
    public void SetDraggable(string id, bool drag)
    {
        var e = GetData(id);
        if (e == null)
        {
            Debug.LogError($"No Data Found: {id}");
            return;
        }

        e.draggable = drag;

    }

    public bool GetDrag(string id)
    {
        var e = GetData(id);
        if (e == null)
        {
            Debug.LogError($"No Data Found: {id}");
            return false;
        }

        return e.draggable;
    }


    // =========================
    // USER TYPE
    // =========================

    public void SetUserType(UserType type)
    {
        userdata.userType = type;

        Debug.Log($"User type set to: {type}");
    }

    public UserType GetUserType()
    {
        return userdata.userType;
    }

    public void SetUserName(string name)
    {
        userdata.name = name;

        Debug.Log($"User name set to: {name}");
    }

    public string GetUserName()
    {
        return userdata.name;
    }
    //Store Room ID

    public void RefreshRoomID()
    {
        userdata.RoomID = PhotonNetwork.CurrentRoom.Name;
    }


    //Remove Element 
    public void RemoveElement(string id)
    {
        var e = GetData(id);

        if (e == null)
        {
           // Debug.LogWarning($"RemoveElement failed: {id} not found");
            return;
        }

        // 1. Remove from main list
        userdata.elements.Remove(e);

        // 2. Remove from cache
        elementMap.Remove(id);

        //Debug.Log($"Element removed: {id}");

        // 3. Optional: trigger network sync later if needed
        // e.g. MarkDeleted / RPC delete
    }
}
