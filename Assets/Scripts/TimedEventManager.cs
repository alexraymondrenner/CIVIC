using Photon.Pun;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public enum TimedSimulationEventType
{
    SystemMessage,
    PlayVideo,
    StopVideo,
    StartPublishing,
    SpawnInject,
    SpawnResource,
    OpenSlack
}

[Serializable]
public class TimedSimulationEvent
{
    [Header("General")]
    [Tooltip("Optional name used only to make the Inspector easier to read.")]
    public string eventName;

    [Tooltip("Seconds from simulation start.")]
    public float eventTime;

    public TimedSimulationEventType eventType;

    [Header("System Message")]
    [TextArea]
    public string message;

    [Header("Video")]
    public VideoClip videoClip;

    [Header("Inject")]
    public ElementType elementType;
    [Tooltip("Whether this inject should use the General icon.")]
    public bool isGeneralIcon = true;
    public Vector2 positionRatio = new Vector2(0.5f, 0.5f);

    [Header("Resource")]
    public ElementType resourceType;
    public UserType requiredUserType;
    public Vector2 resourcePositionRatio = new Vector2(0.5f, 0.5f);
}

public class TimedEventManager : MonoBehaviourPunCallbacks
{
    [Header("Simulation Events")]
    [SerializeField]
    private List<TimedSimulationEvent> events = new List<TimedSimulationEvent>();

    [Header("Full-Screen Video UI")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private GameObject fullScreenVideoPanel;
    [SerializeField] private VideoPlayer videoPlayer;

    private SimulationTimeManager simulationTimeManager;
    private GameManager gameManager;
    private Transform addedElements;

    private int currentEventIndex = 0;

    private void Start()
    {
        simulationTimeManager = GetComponent<SimulationTimeManager>();
        gameManager = GetComponent<GameManager>();

        if (simulationTimeManager == null)
        {
            Debug.LogError(
                "TimedEventManager could not find SimulationTimeManager."
            );
        }

        if (gameManager == null)
        {
            Debug.LogError(
                "TimedEventManager could not find GameManager."
            );
        }

        GameObject addedElementObject = GameObject.Find("AddedElement");

        if (addedElementObject != null)
        {
            addedElements = addedElementObject.transform;
        }
        else
        {
            Debug.LogError(
                "TimedEventManager could not find AddedElement."
            );
        }

        // Sort events by simulation time.
        events.Sort((a, b) => a.eventTime.CompareTo(b.eventTime));

        currentEventIndex = 0;

        if (fullScreenVideoPanel != null)
        {
            fullScreenVideoPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (simulationTimeManager == null)
            return;

        if (!simulationTimeManager.HasStarted)
            return;

        if (!simulationTimeManager.IsRunning)
            return;

        double elapsedTime = simulationTimeManager.ElapsedTime;

        // Use while so missed events are caught up if a frame skips
        // across more than one event time.
        while (currentEventIndex < events.Count &&
               elapsedTime >= events[currentEventIndex].eventTime)
        {
            ExecuteEvent(events[currentEventIndex]);
            currentEventIndex++;
        }
    }

    // =========================================================
    // EXECUTE TIMED EVENT
    // =========================================================

    private void ExecuteEvent(TimedSimulationEvent simulationEvent)
    {
        if (simulationEvent == null)
            return;

        Debug.Log(
            $"Timed Event: {simulationEvent.eventName} | " +
            $"Type: {simulationEvent.eventType} | " +
            $"Time: {simulationEvent.eventTime}"
        );

        switch (simulationEvent.eventType)
        {
            case TimedSimulationEventType.SystemMessage:
                // Message is handled below.
                break;

            case TimedSimulationEventType.PlayVideo:
                ShowFullScreenVideo(simulationEvent.videoClip);
                break;

            case TimedSimulationEventType.StopVideo:
                HideFullScreenVideo();
                break;

            case TimedSimulationEventType.StartPublishing:
                StartPublishing();
                break;

            case TimedSimulationEventType.SpawnInject:
                SpawnIcon(
                    simulationEvent.elementType,
                    simulationEvent.positionRatio,
                    simulationEvent.isGeneralIcon
                );
                break;

            case TimedSimulationEventType.OpenSlack:
                OpenSlack();
                break;

            case TimedSimulationEventType.SpawnResource:
                SpawnResource(
                    simulationEvent.resourceType,
                    simulationEvent.resourcePositionRatio,
                    simulationEvent.requiredUserType
                );
                break;

            default:
                Debug.LogWarning(
                    $"Unknown TimedSimulationEventType: {simulationEvent.eventType}"
                );
                break;
        }

        // Every event can optionally display a system message.
        if (!string.IsNullOrWhiteSpace(simulationEvent.message))
        {
            ShowSystemMessage(simulationEvent.message);
        }
    }

    // =========================================================
    // SYSTEM MESSAGE
    // =========================================================

    private void ShowSystemMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (SystemUpdateManager.Instance == null)
            return;

        SystemUpdateManager.Instance.AddUpdate(
            message,
            SystemUpdateType.General
        );
    }

    // =========================================================
    // START PUBLISHING
    // =========================================================

    private void StartPublishing()
    {
        if (gameManager == null)
            return;

        /*
         * Runs on every client.
         *
         * Each user may have created local icons before publishing
         * begins, so each client publishes its own existing icons.
         */
        gameManager.SetStartPublish(true);

        Debug.Log("Publishing enabled.");
    }

    // =========================================================
    // OPEN SLACK
    // =========================================================

    public void OpenSlack()
    {
        Application.OpenURL(
            "slack://channel?team=T6MTPC1QQ&id=C0BQ3QD935X"
        );
    }

    // =========================================================
    // SHOW FULL-SCREEN VIDEO
    // =========================================================

    public void ShowFullScreenVideo(VideoClip clipToPlay)
    {
        if (fullScreenVideoPanel == null || videoPlayer == null)
        {
            Debug.LogError(
                "Missing fullScreenVideoPanel or VideoPlayer."
            );

            return;
        }

        if (clipToPlay == null)
        {
            Debug.LogError(
                "ShowFullScreenVideo called without a VideoClip."
            );

            return;
        }

        // Make the panel fill the canvas.
        if (targetCanvas != null)
        {
            fullScreenVideoPanel.transform.SetParent(
                targetCanvas.transform,
                false
            );
        }

        RectTransform rt =
            fullScreenVideoPanel.GetComponent<RectTransform>();

        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        fullScreenVideoPanel.SetActive(true);

        // Stop anything currently playing before starting a new clip.
        videoPlayer.Stop();

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.clip = clipToPlay;

        // Remove old callbacks first so they cannot accumulate.
        videoPlayer.prepareCompleted -= OnVideoPrepared;
        videoPlayer.errorReceived -= OnVideoError;
        videoPlayer.loopPointReached -= OnVideoFinished;

        videoPlayer.prepareCompleted += OnVideoPrepared;
        videoPlayer.errorReceived += OnVideoError;
        videoPlayer.loopPointReached += OnVideoFinished;

        videoPlayer.Prepare();
    }

    // =========================================================
    // VIDEO PREPARED
    // =========================================================

    private void OnVideoPrepared(VideoPlayer vp)
    {
        if (vp == null)
            return;

        Debug.Log(
            $"Video prepared: {vp.clip?.name}"
        );

        vp.Play();

        vp.prepareCompleted -= OnVideoPrepared;
    }

    // =========================================================
    // VIDEO FINISHED
    // =========================================================

    private void OnVideoFinished(VideoPlayer vp)
    {
        Debug.Log(
            $"Video finished: {vp.clip?.name}"
        );

        HideFullScreenVideo();

        if (vp != null)
        {
            vp.loopPointReached -= OnVideoFinished;
        }
    }

    // =========================================================
    // VIDEO ERROR
    // =========================================================

    private void OnVideoError(VideoPlayer vp, string message)
    {
        Debug.LogError(
            "VideoPlayer error: " + message
        );
    }

    // =========================================================
    // HIDE FULL-SCREEN VIDEO
    // =========================================================

    public void HideFullScreenVideo()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnVideoPrepared;
            videoPlayer.errorReceived -= OnVideoError;
            videoPlayer.loopPointReached -= OnVideoFinished;

            videoPlayer.Stop();
        }

        if (fullScreenVideoPanel != null)
        {
            fullScreenVideoPanel.SetActive(false);
        }
    }

    // =========================================================
    // SPAWN INJUCT
    // =========================================================

    private void SpawnIcon(
     ElementType type,
     Vector2 positionRatio,
     bool isGeneralIcon
 )
    {
        /*
         * Every client reaches the timed SpawnInject event,
         * but only the Master creates the shared injuct.
         */
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (addedElements == null)
        {
            Debug.LogError(
                "Cannot spawn injuct because AddedElements is null."
            );

            return;
        }

        if (gameManager == null)
        {
            Debug.LogError(
                "Cannot spawn injuct because GameManager is null."
            );

            return;
        }

        if (UserDataManager.Instance == null)
        {
            Debug.LogError(
                "Cannot spawn injuct because UserDataManager is null."
            );

            return;
        }

        string elementId = Guid.NewGuid().ToString();

        GameObject prefab =
            Resources.Load<GameObject>("General");

        if (prefab == null)
        {
            Debug.LogError(
                $"Could not load General prefab for {type}."
            );

            return;
        }

        GameObject newIcon = Instantiate(prefab);

        newIcon.name = elementId;
        newIcon.transform.SetParent(
            addedElements,
            true
        );

        ElementAttributes attributes =
            newIcon.GetComponent<ElementAttributes>();

        if (attributes == null)
        {
            Debug.LogError(
                "General prefab does not contain ElementAttributes."
            );

            Destroy(newIcon);
            return;
        }

        // Create the local data that will then be published.
        UserDataManager.Instance.CreateData(
            elementId,
            attributes
        );

        ElementData target =
            UserDataManager.Instance.GetData(elementId);

        if (target == null)
        {
            Destroy(newIcon);
            return;
        }

        UserDataManager.Instance.SetPos(
            elementId,
            positionRatio
        );

        UserDataManager.Instance.SetGeneralInjuct(
    elementId,
    isGeneralIcon
);

        UserDataManager.Instance.SetType(
            elementId,
            type
        );


        attributes.SyncTypeFromElementData(target);

        gameManager.ApplyElementTemplate(
            newIcon,
            type,
            !isGeneralIcon
        );

        DragAndDrop dragAndDrop =
            newIcon.GetComponent<DragAndDrop>();

        if (dragAndDrop != null)
        {
            dragAndDrop.DisableDragging(true);
            dragAndDrop.SpawnPrefab();
        }

        // Publish the injuct to everyone.
        gameManager.PublishElement(
            newIcon.transform
        );
    }
    // =========================================================
    // SPAWN RESOURCE
    // =========================================================

    private void SpawnResource(
    ElementType type,
    Vector2 positionRatio,
    UserType requiredRole
)
    {
        // Get THIS player's role.
        UserType currentRole =
                    UserDataManager.Instance.GetUserType();

        // This resource does not belong to me.
        if (currentRole != requiredRole)
            return;

        // Create resource locally.

        if (addedElements == null)
        {
            Debug.LogError(
                "Cannot spawn injuct because AddedElements is null."
            );

            return;
        }

        if (gameManager == null)
        {
            Debug.LogError(
                "Cannot spawn injuct because GameManager is null."
            );

            return;
        }

        if (UserDataManager.Instance == null)
        {
            Debug.LogError(
                "Cannot spawn injuct because UserDataManager is null."
            );

            return;
        }

        string elementId = Guid.NewGuid().ToString();

        GameObject prefab =
            Resources.Load<GameObject>("General");

        if (prefab == null)
        {
            Debug.LogError(
                $"Could not load General prefab for {type}."
            );

            return;
        }

        GameObject newIcon = Instantiate(prefab);

        newIcon.name = elementId;
        newIcon.transform.SetParent(
            addedElements,
            true
        );

        ElementAttributes attributes =
            newIcon.GetComponent<ElementAttributes>();

        if (attributes == null)
        {
            Debug.LogError(
                "General prefab does not contain ElementAttributes."
            );

            Destroy(newIcon);
            return;
        }

        // Create the local data that will then be published.
        UserDataManager.Instance.CreateData(
            elementId,
            attributes
        );

        ElementData target =
            UserDataManager.Instance.GetData(elementId);

        if (target == null)
        {
            Destroy(newIcon);
            return;
        }

        UserDataManager.Instance.SetPos(
            elementId,
            positionRatio
        );

        UserDataManager.Instance.SetGeneralInjuct(
    elementId,
    false
);

        UserDataManager.Instance.SetType(
            elementId,
            type
        );


        attributes.SyncTypeFromElementData(target);

        gameManager.ApplyElementTemplate(newIcon,type,true);

        DragAndDrop dragAndDrop =
            newIcon.GetComponent<DragAndDrop>();

        if (dragAndDrop != null)
        {
            dragAndDrop.DisableDragging(false);
            dragAndDrop.SpawnPrefab();
        }
    }

}