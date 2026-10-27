using System;
using System.Collections.Generic;

using ExitGames.Client.Photon;

using Photon.Pun;
using Photon.Realtime;

using UnityEngine;
using UnityEngine.Video;

using System.Collections;

[Serializable]
public class InjuctVideoSetting
{
    public ElementType type;
    public VideoClip videoClip;
}

[Serializable]
public class ResourceArrivalState
{
    public string injuctId;
    public string resourceId;
    public double arrivalTime;
    public Vector2 startPosition;
    public Vector2 destinationPosition;
}

public class ResourceArrivalManager : MonoBehaviour, IOnEventCallback
{
    // =========================================================
    // PHOTON EVENT CODES
    // =========================================================

    private const byte TRAVEL_REQUEST_EVENT = 15;
    private const byte TRAVEL_STARTED_EVENT = 16;
    private const byte TRAVEL_FINISHED_EVENT = 17;
    private const byte RESOURCE_EN_ROUTE_EVENT = 10;
    private const byte RESOURCE_CANCELLED_EVENT = 11;
    private const byte RESOURCE_ARRIVED_EVENT = 12;
    private const byte INJUCT_RESOLVE_REQUEST_EVENT = 13;
    private const byte INJUCT_RESOLVED_EVENT = 14;
    private const byte RESOURCE_REMOVED_EVENT = 19;
    private const byte IMMEDIATE_ARRIVAL_EVENT = 20;

    // =========================================================
    // REFERENCES
    // =========================================================

    private SimulationTimeManager simulationTimeManager;
    private GameManager gameManager;

    // =========================================================
    // MASTER-ONLY STATE
    // =========================================================

    private Dictionary<string, ResourceArrivalState> pendingArrivals = new Dictionary<string, ResourceArrivalState>();

    [Header("Travel settings: map units per full normalized axis")]
    [SerializeField] private float xMapUnits = 100f;
    [SerializeField] private float yMapUnits = 100f;
    [SerializeField] private float resourceSpeedUnitsPerSecond = 10f;

    // Mirrored by Photon travel-start and travel-finish events on every client.
    private HashSet<string> resourcesEnRoute = new HashSet<string>();
    private Dictionary<string, string> nearbyInjuctByResource = new Dictionary<string, string>();

    private HashSet<string> completedArrivals = new HashSet<string>();
    // injuctId -> Photon ActorNumber allowed to resolve it
    private Dictionary<string, int> resolverByInjuct = new Dictionary<string, int>();

    // =========================================================
    // UNITY
    // =========================================================
    [Header("Injuct Reveal Videos")]
    [SerializeField]
    private List<InjuctVideoSetting> injuctVideos = new List<InjuctVideoSetting>();

    private TimedEventManager timedEventManager;
    private void Awake()
    {
        simulationTimeManager = GetComponent<SimulationTimeManager>();
        gameManager = GetComponent<GameManager>();
        timedEventManager = GetComponent<TimedEventManager>();
        if (simulationTimeManager == null)
        {
            Debug.LogError("ResourceArrivalManager could not find SimulationTimeManager.");
        }

        if (gameManager == null)
        {
            Debug.LogError("ResourceArrivalManager could not find GameManager.");
        }
        if (timedEventManager == null)
        {
            Debug.LogError("ResourceArrivalManager could not find TimedEventManager.");
        }
    }

    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (simulationTimeManager == null)
            return;

        if (!simulationTimeManager.HasStarted || !simulationTimeManager.IsRunning)
        {
            return;
        }

        CheckPendingArrivals();
    }

    // =========================================================
    // RESOURCE ENTERED INJUCT
    // =========================================================

    // Called on the icon owner's client after a successful drop.
    // Coordinates are the normalized positions stored by UserDataManager.
    public void RequestResourceTravel(string resourceId, Vector2 from, Vector2 to)
    {
        if (string.IsNullOrEmpty(resourceId) || (to - from).sqrMagnitude < 0.00000001f)
            return;

        // Disable interaction immediately on the initiating client while
        // waiting for the authoritative Photon start event.
        SetResourceDragging(resourceId, false);

        object[] content = { resourceId, from.x, from.y, to.x, to.y };
        PhotonNetwork.RaiseEvent(TRAVEL_REQUEST_EVENT, content,
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    public bool IsResourceEnRoute(string resourceId)
    {
        return resourcesEnRoute.Contains(resourceId);
    }

    private void SetResourceDragging(string resourceId, bool canDrag)
    {
        GameObject obj = FindElementObject(resourceId);
        if (obj == null) return;
        DragAndDrop drag = obj.GetComponent<DragAndDrop>();
        if (drag != null) drag.DisableDragging(!canDrag);
    }

    private void HandleTravelRequest(object customData, int sender)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        object[] data = customData as object[];
        if (data == null || data.Length < 5) return;

        string resourceId = data[0] as string;
        if (string.IsNullOrEmpty(resourceId)) return;
        GameObject obj = FindElementObject(resourceId);
        if (obj == null || resourcesEnRoute.Contains(resourceId) || pendingArrivals.ContainsKey(resourceId)) return;

        PhotonView view = obj.GetComponent<PhotonView>();
        if (view != null && view.OwnerActorNr != sender) return;

        ElementData resource = UserDataManager.Instance.GetData(resourceId);
        if (resource == null || resource.GetString("Element Category") != "Resource")
            return;

        Vector2 from = new Vector2(Convert.ToSingle(data[1]), Convert.ToSingle(data[2]));
        Vector2 to = new Vector2(Convert.ToSingle(data[3]), Convert.ToSingle(data[4]));
        float travelUnits = Mathf.Abs(to.x - from.x) * xMapUnits + Mathf.Abs(to.y - from.y) * yMapUnits;
        if (travelUnits < 0.0001f) return;
        float duration = travelUnits / Mathf.Max(0.001f, resourceSpeedUnitsPerSecond);

        string nearbyInjuct = null;
        nearbyInjuctByResource.TryGetValue(resourceId, out nearbyInjuct);
        nearbyInjuctByResource.Remove(resourceId);
        pendingArrivals[resourceId] = new ResourceArrivalState
        {
            resourceId = resourceId,
            injuctId = nearbyInjuct,
            startPosition = from,
            destinationPosition = to,
            arrivalTime = simulationTimeManager.ElapsedTime + duration
        };
        resourcesEnRoute.Add(resourceId);

        PhotonNetwork.RaiseEvent(TRAVEL_STARTED_EVENT, new object[] { resourceId, duration, resource.type.ToString() },
            new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
    }

    // Called by existing GameManager proximity logic. It associates an
    // incident with the *already scheduled trip*, not a second timer.
    public void StartArrival(string injuctId, string resourceId, float waitTime)
    {
        if (!PhotonNetwork.IsMasterClient || string.IsNullOrEmpty(injuctId) || string.IsNullOrEmpty(resourceId)) return;
        string key = GetPairKey(injuctId, resourceId);
        if (completedArrivals.Contains(key)) return;
        if (UserDataManager.Instance.GetData(injuctId) == null ||
            UserDataManager.Instance.GetData(resourceId) == null) return;

        if (pendingArrivals.TryGetValue(resourceId, out ResourceArrivalState travel))
        {
            travel.injuctId = injuctId;
            return;
        }

        // Proximity may arrive before the Photon travel request.
        // Remember the injuct and attach it when that trip is created.
        nearbyInjuctByResource[resourceId] = injuctId;
    }

    // =========================================================
    // RESOURCE LEFT BEFORE ARRIVAL
    // =========================================================

    public void CancelArrival(string injuctId, string resourceId)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!pendingArrivals.TryGetValue(resourceId, out ResourceArrivalState state))
        {
            if (nearbyInjuctByResource.TryGetValue(resourceId, out string nearby) && nearby == injuctId)
                nearbyInjuctByResource.Remove(resourceId);
            return;
        }
        // Removing proximity must not cancel the resource's ongoing journey.
        if (state.injuctId == injuctId) state.injuctId = null;
    }

    // =========================================================
    // CHECK PENDING ARRIVALS
    // =========================================================

    private void CheckPendingArrivals()
    {
        if (pendingArrivals.Count == 0)
            return;

        double currentTime = simulationTimeManager.ElapsedTime;

        List<string> completedKeys = new List<string>();

        List<string> invalidKeys = new List<string>();

        foreach (KeyValuePair<string, ResourceArrivalState> pair in pendingArrivals)
        {
            ResourceArrivalState state = pair.Value;

            if (state == null)
            {
                invalidKeys.Add(pair.Key);
                continue;
            }

            ElementData resourceData = UserDataManager.Instance.GetData(state.resourceId);

            if (resourceData == null)
            {
                invalidKeys.Add(pair.Key);
                continue;
            }

            if (currentTime >= state.arrivalTime)
            {
                completedKeys.Add(pair.Key);
            }
        }

        foreach (string key in invalidKeys)
        {
            pendingArrivals.Remove(key);
        }

        foreach (string key in completedKeys)
        {
            if (!pendingArrivals.TryGetValue(key, out ResourceArrivalState state))
            {
                continue;
            }

            CompleteArrival(state);

            pendingArrivals.Remove(key);
            if (!string.IsNullOrEmpty(state.injuctId))
                completedArrivals.Add(GetPairKey(state.injuctId, state.resourceId));
            resourcesEnRoute.Remove(state.resourceId);
            PhotonNetwork.RaiseEvent(TRAVEL_FINISHED_EVENT, new object[] { state.resourceId },
                new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
        }
    }

    // =========================================================
    // MASTER COMPLETES ARRIVAL
    // =========================================================

    private void CompleteArrival(ResourceArrivalState state)
    {
        if (string.IsNullOrEmpty(state.injuctId)) return;

        ElementData injuctData = UserDataManager.Instance.GetData(state.injuctId);

        ElementData resourceData = UserDataManager.Instance.GetData(state.resourceId);

        if (injuctData == null || resourceData == null)
            return;

        bool isCorrectResource = IsCorrectResource(injuctData, resourceData);

        int resolverActorNumber = -1;

        if (isCorrectResource)
        {
            resolverActorNumber = GetResourceOwnerActorNumber(state.resourceId);

            if (resolverActorNumber > 0)
            {
                resolverByInjuct[state.injuctId] = resolverActorNumber;
            }
        }

        Debug.Log($"{resourceData.type} arrived at " + $"{injuctData.type}. Correct: {isCorrectResource}");
        RaiseResourceArrivedEvent(state.injuctId, state.resourceId, isCorrectResource, resolverActorNumber);

    }

    // =========================================================
    // CORRECT RESOURCE CHECK
    // =========================================================

    private bool IsCorrectResource(ElementData injuctData, ElementData resourceData)
    {
        if (injuctData == null || resourceData == null)
            return false;

        string correctResource = injuctData.GetString("Correct Resource");

        if (string.IsNullOrWhiteSpace(correctResource))
            return false;

        return resourceData.type.ToString().Contains(correctResource);
    }

    // =========================================================
    // Resource Owner CHECK
    // =========================================================
    private int GetResourceOwnerActorNumber(string resourceId)
    {
        GameObject resourceObject = GameObject.Find(resourceId);

        if (resourceObject == null)
            return -1;

        PhotonView view = resourceObject.GetComponent<PhotonView>();

        if (view == null || view.Owner == null)
            return -1;

        return view.OwnerActorNr;
    }

    // =========================================================
    // RESOLVE INJUCT
    // =========================================================

    public void ResolveInjuct(string injuctId)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (string.IsNullOrEmpty(injuctId))
            return;

        ElementData injuctData = UserDataManager.Instance.GetData(injuctId);

        if (injuctData == null)
            return;

        string canBeResolved = injuctData.GetString("Can be resolved");

        if (canBeResolved != "Yes")
        {
            Debug.LogWarning($"Cannot resolve injuct {injuctId}: " + $"Can be resolved = {canBeResolved}");

            return;
        }

        Debug.Log($"Resolving injuct: {injuctId}");

        RaiseInjuctResolvedEvent(injuctId);
    }

    // =========================================================
    // SEND: RESOURCE EN ROUTE
    // =========================================================

    private void RaiseResourceEnRouteEvent(string injuctId, string resourceId, string message)
    {
        object[] content =
        {
            injuctId, resourceId, message
        };

        RaiseEventOptions options = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.All
        };

        PhotonNetwork.RaiseEvent(RESOURCE_EN_ROUTE_EVENT, content, options, SendOptions.SendReliable);
    }

    // =========================================================
    // SEND: RESOURCE CANCELLED
    // =========================================================

    private void RaiseResourceCancelledEvent(string injuctId, string resourceId)
    {
        object[] content =
        {
            injuctId, resourceId
        };

        RaiseEventOptions options = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.All
        };

        PhotonNetwork.RaiseEvent(RESOURCE_CANCELLED_EVENT, content, options, SendOptions.SendReliable);
    }

    // =========================================================
    // SEND: RESOURCE ARRIVED
    // =========================================================

    private void RaiseResourceArrivedEvent(string injuctId, string resourceId, bool isCorrectResource,
    int resolverActorNumber)
    {
        object[] content =
        {
        injuctId, resourceId, isCorrectResource, resolverActorNumber
    };

        RaiseEventOptions options = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.All
        };

        PhotonNetwork.RaiseEvent(RESOURCE_ARRIVED_EVENT, content, options, SendOptions.SendReliable);
    }

    // =========================================================
    // SEND: INJUCT RESOLVED
    // =========================================================

    private void RaiseInjuctResolvedEvent(string injuctId)
    {
        object[] content =
        {
            injuctId
        };

        RaiseEventOptions options = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.All
        };

        PhotonNetwork.RaiseEvent(INJUCT_RESOLVED_EVENT, content, options, SendOptions.SendReliable);
    }

    public void CancelRemovedResource(string resourceId)
    {
        ClearRemovedResourceState(resourceId);

        if (!PhotonNetwork.InRoom) return;

        PhotonNetwork.RaiseEvent(
            RESOURCE_REMOVED_EVENT,
            new object[] { resourceId },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            SendOptions.SendReliable
        );
    }

    private void ClearRemovedResourceState(string resourceId)
    {
        resourcesEnRoute.Remove(resourceId);
        nearbyInjuctByResource.Remove(resourceId);

        if (PhotonNetwork.IsMasterClient)
            pendingArrivals.Remove(resourceId);
    }

    public void RequestImmediateArrival(string resourceId)
    {
        if (!PhotonNetwork.InRoom || string.IsNullOrEmpty(resourceId)) return;

        PhotonNetwork.RaiseEvent(
            IMMEDIATE_ARRIVAL_EVENT,
            new object[] { resourceId },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
            SendOptions.SendReliable
        );
    }

    private IEnumerator HandleImmediateArrival(string resourceId)
    {
        if (!PhotonNetwork.IsMasterClient) yield break;

        // Allow the published object to be created on the Master first.
        float timeout = Time.realtimeSinceStartup + 2f;

        while (FindElementObject(resourceId) == null &&
               Time.realtimeSinceStartup < timeout)
            yield return null;

        GameObject resourceObject = FindElementObject(resourceId);
        if (resourceObject == null) yield break;

        ElementData resourceData = UserDataManager.Instance.GetData(resourceId);
        if (resourceData == null || resourceData.GetString("Element Category") != "Resource")
            yield break;

        // Prevent a second arrival from being started for an active trip.
        if (resourcesEnRoute.Contains(resourceId) || pendingArrivals.ContainsKey(resourceId))
            yield break;

        // Make sure the Master processes the new resource's proximity.
        gameManager.RequestProximityRefresh();

        yield return new WaitForSeconds(0.2f);

        // No nearby injuct: just leave the resource at its position.
        if (!nearbyInjuctByResource.TryGetValue(resourceId, out string injuctId))
            yield break;

        nearbyInjuctByResource.Remove(resourceId);

        string key = GetPairKey(injuctId, resourceId);
        if (completedArrivals.Contains(key)) yield break;

        ResourceArrivalState state = new ResourceArrivalState
        {
            resourceId = resourceId,
            injuctId = injuctId,
            arrivalTime = simulationTimeManager.ElapsedTime,
            destinationPosition = resourceData.posRatio,
            startPosition = resourceData.posRatio
        };

        // Reuse the normal arrival/reveal/correct-resource workflow.
        CompleteArrival(state);
        completedArrivals.Add(key);
    }
    // =========================================================
    // RECEIVE PHOTON EVENTS
    // =========================================================

    public void OnEvent(EventData photonEvent)
    {
        switch (photonEvent.Code)
        {
            case TRAVEL_REQUEST_EVENT:
                HandleTravelRequest(photonEvent.CustomData, photonEvent.Sender);
                break;
            case TRAVEL_STARTED_EVENT:
                HandleTravelStarted(photonEvent.CustomData);
                break;
            case TRAVEL_FINISHED_EVENT:
                HandleTravelFinished(photonEvent.CustomData);
                break;
            case RESOURCE_EN_ROUTE_EVENT:
                HandleResourceEnRouteEvent(photonEvent.CustomData);
                break;

            case RESOURCE_CANCELLED_EVENT:
                HandleResourceCancelledEvent(photonEvent.CustomData);
                break;

            case RESOURCE_ARRIVED_EVENT:
                HandleResourceArrivedEvent(photonEvent.CustomData);
                break;
            case INJUCT_RESOLVE_REQUEST_EVENT:
                HandleResolveRequest(photonEvent.CustomData, photonEvent.Sender);
                break;

            case INJUCT_RESOLVED_EVENT:
                HandleInjuctResolvedEvent(photonEvent.CustomData);
                break;
            case RESOURCE_REMOVED_EVENT:
                string removedId = (string)((object[])photonEvent.CustomData)[0];
                ClearRemovedResourceState(removedId);
                break;
            case IMMEDIATE_ARRIVAL_EVENT:
                object[] values = photonEvent.CustomData as object[];
                if (values != null && values.Length > 0)
                    StartCoroutine(HandleImmediateArrival((string)values[0]));
                break;
        }
    }

    private void HandleTravelStarted(object customData)
    {
        object[] data = customData as object[];
        if (data == null || data.Length < 3) return;
        string resourceId = (string)data[0];
        float duration = Convert.ToSingle(data[1]);
        string type = (string)data[2];
        resourcesEnRoute.Add(resourceId);
        SetResourceDragging(resourceId, false);
        ShowSystemUpdate($"{type} en route. ETA: {duration:F1} s", type);
    }

    private void HandleTravelFinished(object customData)
    {
        object[] data = customData as object[];
        if (data == null || data.Length < 1) return;
        string resourceId = (string)data[0];
        resourcesEnRoute.Remove(resourceId);
        SetResourceDragging(resourceId, true);
        ElementData resource = UserDataManager.Instance.GetData(resourceId);
        if (resource != null)
            ShowSystemUpdate($"{resource.type} has arrived at its destination.", resource.type.ToString());
    }

    // =========================================================
    // RECEIVE: EN ROUTE
    // =========================================================

    private void HandleResourceEnRouteEvent(object customData)
    {
        object[] data = customData as object[];

        if (data == null || data.Length < 3)
            return;

        string injuctId = (string)data[0];
        string resourceId = (string)data[1];
        string message = (string)data[2];

        ElementData injuctData = UserDataManager.Instance.GetData(injuctId);

        if (injuctData == null)
            return;

        ShowSystemUpdate(message, injuctData.type.ToString());

        Debug.Log($"NETWORK: {resourceId} en route to {injuctId}");
    }

    // =========================================================
    // RECEIVE: CANCELLED
    // =========================================================

    private void HandleResourceCancelledEvent(object customData)
    {
        object[] data = customData as object[];

        if (data == null || data.Length < 2)
            return;

        string injuctId = (string)data[0];
        string resourceId = (string)data[1];

        Debug.Log($"NETWORK: arrival cancelled " + $"{resourceId} -> {injuctId}");
    }

    // =========================================================
    // RECEIVE: ARRIVED
    // =========================================================

    private void HandleResourceArrivedEvent(object customData)
    {
        object[] data = customData as object[];

        if (data == null || data.Length < 4)
            return;

        string injuctId = (string)data[0];
        string resourceId = (string)data[1];
        bool isCorrectResource = (bool)data[2];
        int resolverActorNumber = (int)data[3];

        if (isCorrectResource && resolverActorNumber > 0)
        {
            resolverByInjuct[injuctId] = resolverActorNumber;
        }

        ElementData injuctData = UserDataManager.Instance.GetData(injuctId);

        ElementData resourceData = UserDataManager.Instance.GetData(resourceId);

        if (injuctData == null || resourceData == null)
            return;

        GameObject injuctObject = FindElementObject(injuctId);

        bool alreadyRevealed = injuctData.GetString("Revealed") == "Yes";

        if (!alreadyRevealed)
        {
            // Mark it revealed FIRST so later resource arrivals
            // cannot trigger the reveal/video again.
            ElementAttributes attributes = null;

            if (injuctObject != null)
            {
                attributes = injuctObject.GetComponent<ElementAttributes>();
            }

            UserDataManager.Instance.SetValue(injuctId, attributes, "Revealed", "Yes");

            // Reveal the actual injuct icon.
            if (injuctObject != null && gameManager != null)
            {
                gameManager.ApplyElementIcon(injuctObject, injuctData.type);
            }

            // Play reveal video only the FIRST time.
            VideoClip revealVideo = GetInjuctVideo(injuctData.type);

            if (revealVideo != null && timedEventManager != null)
            {
                timedEventManager.ShowFullScreenVideo(revealVideo);
            }
        }

        ShowSystemUpdate(resourceData.type + " has arrived!", injuctData.type.ToString());

        if (isCorrectResource)
        {
            ElementAttributes attributes = null;

            if (injuctObject != null)
            {
                attributes = injuctObject.GetComponent<ElementAttributes>();
            }

            UserDataManager.Instance.SetValue(injuctId, attributes, "Can be resolved", "Yes");

            ShowSystemUpdate("The arrived resource is the correct resource.", injuctData.type.ToString());
        }
        else
        {
            ShowSystemUpdate("The arrived resource is not the correct resource.", injuctData.type.ToString());
        }
    }

    // =========================================================
    // RECEIVE: INJUCT RESOLVED
    // =========================================================

    private void HandleInjuctResolvedEvent(object customData)
    {
        object[] data = customData as object[];

        if (data == null || data.Length < 1)
            return;

        string injuctId = (string)data[0];

        // Remove pending arrival states related to this injuct
        // on the Master.
        if (PhotonNetwork.IsMasterClient)
        {
            ClearArrivalStateForInjuct(injuctId);
        }

        GameObject injuctObject = FindElementObject(injuctId);

        if (injuctObject == null)
            return;

        ElementData injuctData = UserDataManager.Instance.GetData(injuctId);

        if (injuctData != null)
        {
            ShowSystemUpdate(injuctData.type + " has been resolved.", injuctData.type.ToString());
        }

        Debug.Log($"NETWORK: injuct resolved {injuctId}");

        /*
         * Every client destroys its own local representation.
         *
         * ElementAttributes.OnDestroy() already calls
         * UserDataManager.RemoveElement(), so the local
         * ElementData will be cleaned up too.
         */
        Destroy(injuctObject);
    }

    private void HandleResolveRequest(object customData, int senderActorNumber)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        object[] data = customData as object[];

        if (data == null || data.Length < 1)
            return;

        string injuctId = (string)data[0];

        if (!resolverByInjuct.TryGetValue(injuctId, out int allowedActorNumber))
        {
            Debug.LogWarning($"No resolver assigned for injuct {injuctId}.");

            return;
        }

        if (senderActorNumber != allowedActorNumber)
        {
            Debug.LogWarning($"Player {senderActorNumber} is not allowed " + $"to resolve injuct {injuctId}.");

            return;
        }

        ElementData injuctData = UserDataManager.Instance.GetData(injuctId);

        if (injuctData == null)
            return;

        if (injuctData.GetString("Can be resolved") != "Yes")
            return;

        RaiseInjuctResolvedEvent(injuctId);
    }

    // =========================================================
    // CLEAR ARRIVAL STATE FOR RESOLVED INJUCT
    // =========================================================

    private void ClearArrivalStateForInjuct(string injuctId)
    {
        List<string> pendingKeysToRemove = new List<string>();

        foreach (KeyValuePair<string, ResourceArrivalState> pair in pendingArrivals)
        {
            if (pair.Value != null && pair.Value.injuctId == injuctId)
            {
                pendingKeysToRemove.Add(pair.Key);
            }
        }

        foreach (string key in pendingKeysToRemove)
        {
            pendingArrivals.Remove(key);
        }

        List<string> completedKeysToRemove = new List<string>();

        foreach (string key in completedArrivals)
        {
            if (key.StartsWith(injuctId + "|"))
            {
                completedKeysToRemove.Add(key);
            }
        }

        foreach (string key in completedKeysToRemove)
        {
            completedArrivals.Remove(key);
        }
    }

    // =========================================================
    // SYSTEM UPDATE
    // =========================================================

    private void ShowSystemUpdate(string message, string source)
    {
        if (SystemUpdateManager.Instance == null)
            return;

        if (string.IsNullOrWhiteSpace(message))
            return;

        SystemUpdateManager.Instance.AddUpdate(message, SystemUpdateType.Inject, source);
    }

    // =========================================================
    // FIND ELEMENT
    // =========================================================

    private GameObject FindElementObject(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        return GameObject.Find(id);
    }

    // =========================================================
    // PAIR KEY
    // =========================================================

    private string GetPairKey(string injuctId, string resourceId)
    {
        return injuctId + "|" + resourceId;
    }

    // =========================================================
    // Injuct Video
    // =========================================================
    private VideoClip GetInjuctVideo(ElementType type)
    {
        foreach (InjuctVideoSetting setting in injuctVideos)
        {
            if (setting.type == type)
            {
                return setting.videoClip;
            }
        }

        return null;
    }
    // =========================================================
    // Local Player
    // =========================================================

    public bool CanLocalPlayerResolve(string injuctId)
    {
        if (string.IsNullOrEmpty(injuctId))
            return false;

        if (!resolverByInjuct.TryGetValue(injuctId, out int resolverActorNumber))
        {
            return false;
        }

        return PhotonNetwork.LocalPlayer.ActorNumber == resolverActorNumber;
    }

    public void RequestResolveInjuct(string injuctId)
    {
        if (string.IsNullOrEmpty(injuctId))
            return;

        object[] content =
        {
        injuctId
    };

        RaiseEventOptions options = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.MasterClient
        };

        PhotonNetwork.RaiseEvent(INJUCT_RESOLVE_REQUEST_EVENT, content, options, SendOptions.SendReliable);
    }
}
