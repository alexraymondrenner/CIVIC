using System.Collections.Generic;

using Photon.Pun;

using UnityEngine;

public class ElementProximityChecker : MonoBehaviour
{
    [Header("Proximity Settings")]
     private float proximityThreshold = 0.8f;
     private float leaveBuffer = 1f;
     private float timeToWait = 12f;
    [Header("Visual Layout")]
    private float radiusMultiplier = 0.9f;
     private int resourcesPerRing = 6;
    private GameManager gameManager;
    private ResourceArrivalManager resourceArrivalManager;
    private ActionManager actionManager;

    // Resource IDs that were near this injuct
    // during the previous proximity check.
    private HashSet<string> currentNearbyElementIds = new HashSet<string>();

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        GameObject managerObject = GameObject.Find("GameManager");

        if (managerObject == null)
        {
            Debug.LogError(
                $"{name}: Could not find GameManager GameObject."
            );

            return;
        }

        gameManager = managerObject.GetComponent<GameManager>();

        resourceArrivalManager = managerObject.GetComponent<ResourceArrivalManager>();

        if (gameManager == null)
        {
            Debug.LogError(
                $"{name}: Could not find GameManager component."
            );
        }

        if (resourceArrivalManager == null)
        {
            Debug.LogError(
                $"{name}: Could not find ResourceArrivalManager."
            );
        }
    }

    private void Start()
    {
        actionManager = GameObject.Find("Action").GetComponent<ActionManager>();

        if (gameManager != null)
        {
            currentNearbyElementIds.Clear();

            gameManager.RegisterProximityChecker(this);
            gameManager.RequestProximityRefresh();
        }



    }

    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.UnregisterProximityChecker(this);
        }

        currentNearbyElementIds.Clear();
    }

    // =========================================================
    // PROXIMITY CHECK
    // =========================================================

    public void CheckNearbyElements(string overrideResource = null,
        Bounds? overrideResourceBounds = null)
    {
        // Only Master makes gameplay proximity decisions.
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (UserDataManager.Instance == null)
            return;

        if (UserDataManager.Instance.userdata == null ||
            UserDataManager.Instance.userdata.elements == null)
        {
            return;
        }

        // Only injuct objects evaluate nearby resources.
        if (!IsInjuct())
            return;

        ElementData injuctData = UserDataManager.Instance.GetData(gameObject.name);

        if (injuctData == null)
            return;

        HashSet<string> nearbyThisCheck = new HashSet<string>();

        // =====================================================
        // FIND ALL RESOURCES NEAR THIS INJUCT
        // =====================================================

        foreach (ElementData otherData
                 in UserDataManager.Instance.userdata.elements)
        {
            if (otherData == null)
                continue;

            if (otherData.id == injuctData.id)
                continue;

            if (!IsResource(otherData))
                continue;

            DragAndDrop[] allElements = FindObjectsByType<DragAndDrop>(FindObjectsSortMode.None);

            DragAndDrop resourceObject = null;

            foreach (DragAndDrop element in allElements)
            {
                if (element == null)
                    continue;

                if (element.gameObject.name == otherData.id)
                {
                    resourceObject = element;
                    break;
                }
            }

            if (resourceObject == null)
                continue;

            RectTransform injuctRect = GetComponent<RectTransform>();

            RectTransform resourceRect = resourceObject.GetComponent<RectTransform>();

            if (injuctRect == null ||
                resourceRect == null)
            {
                continue;
            }

            // Use the actual map location; a resource's visual offset must not affect proximity.
            Bounds resourceBounds = resourceObject.GetLogicalWorldBounds();

            float distance =
                GetBoundsDistance(
                    GetWorldBounds(injuctRect),
                    resourceBounds
                );

            // Hysteresis: harder to leave than to enter.
            bool wasNearby = currentNearbyElementIds.Contains(otherData.id);
            float allowed = wasNearby
                ? proximityThreshold + leaveBuffer
                : proximityThreshold;

            if (distance > allowed)
                continue;

            nearbyThisCheck.Add(
                otherData.id
            );

            // Resource was not nearby during the previous check,
            // so this is a new enter event.
            if (!currentNearbyElementIds.Contains(otherData.id))
            {
                HandleResourceEntered(
                    injuctData,
                    otherData
                );
            }
        }

        // =====================================================
        // FIND RESOURCES THAT LEFT
        // =====================================================

        List<string> resourcesThatLeft =
            new List<string>();

        foreach (string resourceId in currentNearbyElementIds)
        {
            if (!nearbyThisCheck.Contains(resourceId))
            {
                resourcesThatLeft.Add(
                    resourceId
                );
            }
        }

        foreach (string resourceId in resourcesThatLeft)
        {
            HandleResourceLeft(
                injuctData.id,
                resourceId
            );
        }

        currentNearbyElementIds = nearbyThisCheck;
        UpdateVisualLayout(nearbyThisCheck, resourcesThatLeft);
    }

    // =========================================================
    // Calculate Visual Location for Resources
    // =========================================================

    private void UpdateVisualLayout(HashSet<string> resourceIds)
    {
        List<string> sortedIds = new List<string>(resourceIds);
        sortedIds.Sort(System.StringComparer.Ordinal);

        for (int i = 0; i < sortedIds.Count; i++)
        {
            // Calculate each resource's visual slot around this injuct.
        }
    }

    // =========================================================
    // RESOURCE ENTERED
    // =========================================================

    private void HandleResourceEntered(
        ElementData injuctData,
        ElementData resourceData
    )
    {
        if (injuctData == null || resourceData == null)
            return;

        Debug.Log(
            $"{resourceData.type} entered proximity of " +
            $"{injuctData.type}"
        );

        if (resourceArrivalManager == null)
            return;

        resourceArrivalManager.StartArrival(
            injuctData.id,
            resourceData.id,
            timeToWait
        );
    }

    // =========================================================
    // RESOURCE LEFT
    // =========================================================

    private void HandleResourceLeft(
        string injuctId,
        string resourceId
    )
    {
        Debug.Log(
            $"Resource {resourceId} left injuct {injuctId}"
        );

        actionManager.TurnOffAll();

        if (resourceArrivalManager == null)
            return;

        resourceArrivalManager.CancelArrival(
            injuctId,
            resourceId
        );
    }

    // =========================================================
    // RESOURCE CHECK
    // =========================================================

    private bool IsResource(ElementData data)
    {
        if (data == null)
            return false;

        return data.GetString("Element Category") == "Resource";
    }

    // =========================================================
    // INJUCT CHECK
    // =========================================================

    private bool IsInjuct()
    {
        ElementData data =
            UserDataManager.Instance.GetData(gameObject.name);

        if (data == null)
            return false;

        return data.GetString("Element Category") == "Injuct";
    }

    private float GetRectTransformDistance(
            RectTransform a,
            RectTransform b)
    {
        Vector3[] aCorners = new Vector3[4];
        Vector3[] bCorners = new Vector3[4];

        a.GetWorldCorners(aCorners);
        b.GetWorldCorners(bCorners);

        Bounds aBounds =
            new Bounds(
                aCorners[0],
                Vector3.zero
            );

        for (int i = 1; i < 4; i++)
        {
            aBounds.Encapsulate(aCorners[i]);
        }

        Bounds bBounds =
            new Bounds(
                bCorners[0],
                Vector3.zero
            );

        for (int i = 1; i < 4; i++)
        {
            bBounds.Encapsulate(bCorners[i]);
        }

        float xDistance =
            Mathf.Max(
                aBounds.min.x - bBounds.max.x,
                bBounds.min.x - aBounds.max.x,
                0f
            );

        float yDistance =
            Mathf.Max(
                aBounds.min.y - bBounds.max.y,
                bBounds.min.y - aBounds.max.y,
                0f
            );

        return Mathf.Sqrt(
            xDistance * xDistance +
            yDistance * yDistance
        );
    }


    // =========================================================
    // VisualLayout
    // =========================================================


    private void UpdateVisualLayout(HashSet<string> nearbyResources, List<string> resourcesThatLeft)
    {
        List<string> sortedIds = new List<string>(nearbyResources);
        sortedIds.Sort(System.StringComparer.Ordinal);

        RectTransform injuctRect = GetComponent<RectTransform>();
        RectTransform mapRect = GameObject.Find("2DViewer")?.GetComponent<RectTransform>();

        if (injuctRect == null || mapRect == null) return;

        Bounds injuctBounds = GetWorldBounds(injuctRect);
        Bounds mapBounds = GetWorldBounds(mapRect);

        DragAndDrop[] elements = FindObjectsByType<DragAndDrop>(FindObjectsSortMode.None);
        Dictionary<string, DragAndDrop> elementLookup = new Dictionary<string, DragAndDrop>();

        foreach (DragAndDrop element in elements)
        {
            if (element == null) continue;
            if (!elementLookup.ContainsKey(element.name))
                elementLookup.Add(element.name, element);
        }

        int perRing = Mathf.Max(1, resourcesPerRing);

        for (int i = 0; i < sortedIds.Count; i++)
        {
            string resourceId = sortedIds[i];

            if (!elementLookup.TryGetValue(resourceId, out DragAndDrop resource))
                continue;

            RectTransform resourceRect = resource.GetComponent<RectTransform>();
            if (resourceRect == null) continue;

            Bounds resourceBounds = resource.GetLogicalWorldBounds();

            int ring = i / perRing;
            int slot = i % perRing;

            float angle = slot * Mathf.PI * 2f / perRing;
            float radius = Mathf.Max(injuctBounds.extents.x, injuctBounds.extents.y)
                * (radiusMultiplier + ring * 0.5f);

            Vector3 desiredCenter = injuctBounds.center + new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f
            );

            // Keep the displayed icon within the map.
            desiredCenter.x = Mathf.Clamp(desiredCenter.x,
                mapBounds.min.x + resourceBounds.extents.x,
                mapBounds.max.x - resourceBounds.extents.x);

            desiredCenter.y = Mathf.Clamp(desiredCenter.y,
                mapBounds.min.y + resourceBounds.extents.y,
                mapBounds.max.y - resourceBounds.extents.y);

            Vector3 worldOffset = desiredCenter - resourceBounds.center;
            Vector3 localOffset = resourceRect.parent.InverseTransformVector(worldOffset);

            Vector2 normalizedOffset = new Vector2(
                localOffset.x / mapRect.rect.width,
                -localOffset.y / mapRect.rect.height
            );

            ApplyAndSyncVisualOffset(resource, normalizedOffset);
        }

        // Resources that have left should return to their logical positions.
        foreach (string resourceId in resourcesThatLeft)
        {
            UserDataManager.Instance.SetVisualOffset(resourceId, Vector2.zero);
        }
    }

    private void ApplyAndSyncVisualOffset(DragAndDrop resource, Vector2 offset)
    {
        if (resource == null) return;

        string id = resource.name;
        ElementData data = UserDataManager.Instance.GetData(id);
        if (data == null) return;

        // Avoid unnecessary updates and Photon messages.
        if ((data.visualOffset - offset).sqrMagnitude < 0.00000001f)
            return;

        // Always update the Master's local data.
        UserDataManager.Instance.SetVisualOffset(id, offset);

        // Unpublished elements have no PlayerNetManager.
        // Published elements need network synchronization.
        PlayerNetManager net = resource.GetComponent<PlayerNetManager>();
        if (net != null)
            net.SyncVisualOffset(offset);
    }


    private Bounds GetWorldBounds(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);

        Bounds bounds = new Bounds(corners[0], Vector3.zero);
        for (int i = 1; i < 4; i++)
            bounds.Encapsulate(corners[i]);

        return bounds;
    }

    private float GetBoundsDistance(Bounds a, Bounds b)
    {
        float xDistance = Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x, 0f);
        float yDistance = Mathf.Max(a.min.y - b.max.y, b.min.y - a.max.y, 0f);
        return Mathf.Sqrt(xDistance * xDistance + yDistance * yDistance);
    }
}