using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class DragAndDrop : MonoBehaviourPun, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField]
    private bool IsOriginal = true;

    [SerializeField, ReadOnly]
    private bool inTopDown = false, IsHovering = false, IsDragging = false, isLocked = false;

    [SerializeField, ReadOnly]
    private GameObject AddedElements, View, AddedModels, Ground, Action, Gamemanager;

    [SerializeField, ReadOnly]
    private Canvas canvas;

    [SerializeField, ReadOnly]
    public GameObject SpownModel;

    private Vector2 dragStartMapPosition;
    private bool wasExistingPlacedElement;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private AssetLimit assetLimit;

    private Vector3 dragWorldOffset;
    private Camera dragCamera;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        AddedElements = GameObject.Find("AddedElement");
        Action = GameObject.Find("Action");
        View = GameObject.Find("2DViewer");
        AddedModels = GameObject.Find("AddedModels");
        Ground = GameObject.Find("GroundPlane");

        canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
        Gamemanager = GameObject.Find("GameManager");

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void DisableDragging(bool DisabelDrag)
    {
        isLocked = DisabelDrag;

        UserDataManager.Instance.SetDraggable(name, !isLocked);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Asset panel originals should not respond.
        if (IsOriginal)
            return;

        ElementData elementData = UserDataManager.Instance.GetData(gameObject.name);

        if (elementData == null)
            return;

        bool isInjuct = elementData.GetString("Element Category") == "Injuct";

        /*
         * Normal elements/resources:
         * only their Photon owner can click them.
         *
         * Injucts:
         * allow everyone to click, because the user who owns
         * the correct resource may need to resolve an injuct
         * that is owned by the Master Client.
         */
        if (!isInjuct && GetComponent<PhotonView>() != null && !photonView.IsMine)
        {
            return;
        }

        Debug.Log($"Clicked element: {gameObject.name} | " +
            $"Type: {elementData.type} | " + $"Category: {elementData.GetString("Element Category")}");

        ActionManager actionManager = Action.GetComponent<ActionManager>();

        if (actionManager == null)
            return;

        actionManager.SetActiveIcon(gameObject);

        // Normal resources/elements do not use the injuct
        // resolve/wait interface.
        if (!isInjuct)
            return;

        ResourceArrivalManager arrivalManager = Gamemanager.GetComponent<ResourceArrivalManager>();

        if (arrivalManager == null)
            return;

        bool canBeResolved = elementData.GetString("Can be resolved") == "Yes";

        bool localPlayerCanResolve = arrivalManager.CanLocalPlayerResolve(gameObject.name);

        Debug.Log($"Injuct Click | " +
            $"CanBeResolved: {canBeResolved} | " + $"LocalCanResolve: {localPlayerCanResolve} | " +
            $"LocalActor: {PhotonNetwork.LocalPlayer.ActorNumber}");

        if (canBeResolved && localPlayerCanResolve)
        {
            actionManager.TurnOnConfirm();
        }
        else
        {
            actionManager.TurnOnWait();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (GetComponent<PhotonView>() != null && !photonView.IsMine)
        {
            return;
        }

        if (isLocked)
        {
            Debug.Log("Resource is en route and cannot be moved yet.");
            return;
        }

        // Treat a newly spawned resource as traveling from the map origin (0, 0).
        // Existing resources depart from their previously stored map position.
        wasExistingPlacedElement = !IsOriginal;
        dragStartMapPosition = wasExistingPlacedElement
            ? UserDataManager.Instance.GetPos(gameObject.name)
            : Vector2.zero;

        if (IsOriginal && transform.parent.GetComponent<AssetLimit>().spawnDisabled)
        {
            return;
        }

        if (IsOriginal)
        {
            assetLimit = transform.parent.GetComponent<AssetLimit>();

            // Check spawn limit.
            if (assetLimit.spawnLimit >= 0 && assetLimit.spawnCount >= assetLimit.spawnLimit)
            {
                return;
            }

            // Instantiate a copy that remains in the Assets panel.
            GameObject uiElement = Instantiate(gameObject, transform.parent);

            uiElement.transform.position = transform.position;

            uiElement.GetComponent<DragAndDrop>().IsOriginal = true;

            // The current object becomes the placed/draggable object.
            IsOriginal = false;

            canvasGroup.blocksRaycasts = true;

            uiElement.GetComponent<DragAndDrop>().canvas = canvas;

            uiElement.gameObject.name = gameObject.name;

            uiElement.GetComponent<CanvasGroup>().blocksRaycasts = true;

            // ---------------------------
            // CREATE DATA
            // ---------------------------

            string id = System.Guid.NewGuid().ToString();

            UserDataManager.Instance.CreateData(id, uiElement.GetComponent<ElementAttributes>());

            UserDataManager.Instance.SetGeneralInjuct(id, false);

            gameObject.name = id;

            SpawnPrefab();

            assetLimit.AddSpawn();
        }
        else
        {
            canvasGroup.blocksRaycasts = false;
        }

        // Remember where the pointer grabbed the icon so it doesn't jump
        dragCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            (RectTransform)rectTransform.parent,
            eventData.position,
            dragCamera,
            out Vector3 pointerWorld);

        dragWorldOffset = rectTransform.position - pointerWorld;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (GetComponent<PhotonView>() != null && !photonView.IsMine)
        {
            return;
        }

        if (IsOriginal || isLocked)
            return;

        IsDragging = true;

        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            (RectTransform)rectTransform.parent,
            eventData.position,
            dragCamera,
            out Vector3 pointerWorld);

        rectTransform.position = pointerWorld + dragWorldOffset;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isLocked)
        {
            IsDragging = false;
            canvasGroup.blocksRaycasts = true;
            return;
        }

        if (GetComponent<PhotonView>() != null && !photonView.IsMine)
        {
            return;
        }

        canvasGroup.blocksRaycasts = true;

        PointerEventData pointerData = new PointerEventData(EventSystem.current);

        pointerData.position = Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();

        EventSystem.current.RaycastAll(pointerData, results);

        bool droppedOnTopDown = false;

        foreach (RaycastResult r in results)
        {
            if (r.gameObject.CompareTag("TopDown"))
            {
                inTopDown = true;
                droppedOnTopDown = true;

                if (GetComponent<PhotonView>() == null)
                {
                    transform.SetParent(AddedElements.transform);
                }

                break;
            }
        }

        if (!droppedOnTopDown)
        {
            inTopDown = false;

            assetLimit = Gamemanager.GetComponent<PlayerRoleManager>().GetAssetLimit(this.GetComponent<ElementAttributes>().type);
            if (assetLimit != null)
            {
                assetLimit.ReleaseAsset();
            }
            Gamemanager.GetComponent<PlayerRoleManager>().UpdateUI();
            Destroy(gameObject);

            return;
        }

        RectTransform mapRect = View.GetComponent<RectTransform>();
        Vector2 oldOffset = UserDataManager.Instance.GetVisualOffset(name);

        if (wasExistingPlacedElement)
        {
            rectTransform.localPosition -= new Vector3(
                oldOffset.x * mapRect.rect.width,
                -oldOffset.y * mapRect.rect.height,
                0f
            );
        }

        Lock(rectTransform, mapRect);

        IsDragging = false;

        string originalId = gameObject.name;
        ElementData movedData = UserDataManager.Instance.GetData(originalId);
        Vector2 destination = UserDataManager.Instance.GetPos(originalId);

        GameManager manager = Gamemanager.GetComponent<GameManager>();
        ResourceArrivalManager arrivals = Gamemanager.GetComponent<ResourceArrivalManager>();

        bool isResource = movedData != null && movedData.GetString("Element Category") == "Resource";

        string publishedId = manager.PublishElement(transform);
        string resourceId = publishedId ?? originalId;
        if (isResource && arrivals != null && PhotonNetwork.InRoom)
        {
            if (!wasExistingPlacedElement)//if want en route for first drag delete
                arrivals.RequestImmediateArrival(resourceId);
            else
                arrivals.RequestResourceTravel(resourceId, dragStartMapPosition, destination);
        }
       

        Gamemanager.GetComponent<PlayerRoleManager>().UpdateUI();
        manager.RequestProximityRefresh();

        if (publishedId == null)
        {
            Bounds dropBounds = GetLogicalWorldBounds();
            manager.CheckProximityForResourceAtPosition(resourceId, dropBounds);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (GetComponent<PhotonView>() != null && !photonView.IsMine)
        {
            return;
        }

        if (isLocked)
            return;

        IsHovering = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (GetComponent<PhotonView>() != null && !photonView.IsMine)
        {
            return;
        }

        if (isLocked)
            return;

        IsHovering = false;
    }

    private void Lock(RectTransform A, RectTransform B)
    {
        if (!A || !B)
            return;

        // x increases to the right.
        // y normally decreases when moving downward.
        Vector2 p = A.localPosition;

        float w = B.rect.width;
        float h = B.rect.height;

        if (w <= 0.001f || h <= 0.001f)
            return;

        float u = p.x / w;
        float v = -p.y / h;

        UserDataManager.Instance.SetPos(name, new Vector2(u, v));

        Apply(GetComponent<RectTransform>(), View.GetComponent<RectTransform>());
    }

    private void LateUpdate()
    {
        if (IsDragging || IsOriginal) return;

        Apply(rectTransform, View.GetComponent<RectTransform>());

        RectTransform mapRect = View.GetComponent<RectTransform>();
        Vector2 offset = UserDataManager.Instance.GetVisualOffset(name);

        rectTransform.localPosition += new Vector3(
            offset.x * mapRect.rect.width,
            -offset.y * mapRect.rect.height,
            0f
        );
    }

    public Bounds GetLogicalWorldBounds()
    {
        Bounds bounds = GetWorldBounds(rectTransform);

        ElementData data = UserDataManager.Instance.GetData(name);
        if (data == null) return bounds;

        RectTransform mapRect = View.GetComponent<RectTransform>();

        Vector3 logicalLocalPosition = new Vector3(
            data.posRatio.x * mapRect.rect.width,
            -data.posRatio.y * mapRect.rect.height,
            rectTransform.localPosition.z
        );

        Vector3 logicalWorldPosition = rectTransform.parent.TransformPoint(logicalLocalPosition);

        bounds.center += logicalWorldPosition - rectTransform.position;

        return bounds;
    }

    private void Apply(RectTransform A, RectTransform B)
    {
        if (!A || !B)
            return;

        float w = B.rect.width;
        float h = B.rect.height;

        Vector2 posratio = UserDataManager.Instance.GetPos(name);

        A.localPosition = new Vector2(posratio.x * w, -posratio.y * h);

        Renderer r = Ground.GetComponent<Renderer>();

        Bounds bounds = r.bounds;

        float x = Mathf.Lerp(bounds.min.x, bounds.max.x, posratio.x);

        float z = Mathf.Lerp(bounds.min.z, bounds.max.z, posratio.y);

        Vector3 spawnPos = new Vector3(x, 1, -z);

        if (SpownModel != null)
        {
            SpownModel.transform.position = spawnPos;
        }
    }

    public void SpawnPrefab()
    {
        // Currently no 3D model is needed.
        return;

        /*
        // If re-enabled, make sure all _model
        // prefabs are named correctly.

        string Prefabname = UserDataManager.Instance.GetType(name).ToString() + "_model";

        GameObject prefab = Resources.Load<GameObject>(Prefabname);

        if (prefab == null)
        {
            Debug.LogError("Prefab not found in Resources: " + Prefabname);

            return;
        }

        SpownModel = Instantiate(prefab, Vector3.zero, Quaternion.identity, AddedModels.transform);

        SpownModel.name = name + "_model";
        */
    }

    private void OnDisable()
    {
        Destroy(SpownModel);
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
}
