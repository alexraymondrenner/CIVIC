using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Photon.Pun;
using Photon.Realtime;


[Serializable]
public class ElementTemplateData
{
    public Sprite sprite;

    public Vector3 worldWidthVector;
    public Vector3 worldHeightVector;

    public List<ElementKeyValue> keyValues =
        new List<ElementKeyValue>();
}


public class GameManager : MonoBehaviourPunCallbacks
{
    #region Private Serializable Fields

    [SerializeField]
    private Transform AddedElements,
        PublishedElements,
        ReceiveToggle,
        AssetPanel;

    [SerializeField, ReadOnly]
    private bool StartPublishing = false;

    // =========================================================
    // TEMPLATE CACHE
    // =========================================================

    private Dictionary<ElementType, ElementTemplateData>
        templateLookup =
        new Dictionary<ElementType, ElementTemplateData>();


    // =========================================================
    // PROXIMITY
    // =========================================================

    [SerializeField]
    private float proximityRefreshInterval = 0.1f;

    private float nextProximityRefreshTime = 0f;

    private bool proximityNeedsRefresh = false;

    private List<ElementProximityChecker> proximityCheckers =
        new List<ElementProximityChecker>();

    // =========================================================
    // CACHE TEMPLATES
    // =========================================================

    private bool templatesCached = false;
    public bool AreTemplatesCached => templatesCached;
    private void OnEnable()
    {
        Canvas.willRenderCanvases += CacheTemplatesOnFirstRender;
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= CacheTemplatesOnFirstRender;
    }

    private void CacheTemplatesOnFirstRender()
    {
        if (templatesCached)
            return;

        CacheTemplates();

        templatesCached = true;

        // Only need this callback once.
        Canvas.willRenderCanvases -= CacheTemplatesOnFirstRender;

        Debug.Log("Element templates cached after UI layout.");
    }

    private void CacheTemplates()
    {
        templateLookup.Clear();


        ElementAttributes[] elements =
            AssetPanel.GetComponentsInChildren<ElementAttributes>(
                true
            );


        foreach (ElementAttributes element in elements)
        {
            if (element == null)
                continue;


            // Only cache one template for each ElementType.
            if (templateLookup.ContainsKey(
                element.type))
            {
                continue;
            }


            ElementTemplateData template =
                new ElementTemplateData();


            // -------------------------------------------------
            // CACHE ICON
            // -------------------------------------------------

            Image image =
                element.GetComponent<Image>();


            if (image != null)
            {
                template.sprite =
                    image.sprite;
            }


            // -------------------------------------------------
            // CACHE WORLD-SPACE SIZE
            // -------------------------------------------------

            RectTransform rect =
                element.GetComponent<RectTransform>();


            if (rect != null)
            {
                Vector3[] corners =
                    new Vector3[4];


                rect.GetWorldCorners(
                    corners
                );


                // corners:
                //
                // 0 = bottom-left
                // 1 = top-left
                // 2 = top-right
                // 3 = bottom-right


                // Width edge in world space.
                template.worldWidthVector =
                    corners[3] - corners[0];


                // Height edge in world space.
                template.worldHeightVector =
                    corners[1] - corners[0];
            }


            // -------------------------------------------------
            // CACHE KEY / VALUE ATTRIBUTES
            // -------------------------------------------------

            foreach (
                ElementKeyValue pair in
                element.keyValues
            )
            {
                if (pair == null)
                    continue;


                if (string.IsNullOrWhiteSpace(
                    pair.key))
                {
                    continue;
                }


                ElementKeyValue copy =
                    new ElementKeyValue();


                copy.key =
                    pair.key;

                copy.value =
                    pair.value;


                template.keyValues.Add(
                    copy
                );
            }


            templateLookup.Add(
                element.type,
                template );
        }

        GetComponent<PlayerRoleManager>().ApplyCurrentRole();
    }

    #endregion


    // =========================================================
    // START
    // =========================================================

    public bool InTutorial = false;
    private void Start()
    {
        InTutorial = false;
        if (SceneManager.GetActiveScene().name == "Tutorial") {
            InTutorial = true;
            Debug.Log("In Tutorial do not start Network Components");
            return;
        }
        ReceiveData();
    }


    // =========================================================
    // ROOM
    // =========================================================

    public void LeaveRoom()
    {
        PhotonNetwork.LeaveRoom();
    }


    // =========================================================
    // PROXIMITY
    // =========================================================

    public void RequestProximityRefresh()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        proximityNeedsRefresh = true;
    }


    private void LateUpdate()
    {
        if (!proximityNeedsRefresh)
            return;


        if (Time.time <
            nextProximityRefreshTime)
        {
            return;
        }


        proximityNeedsRefresh = false;


        nextProximityRefreshTime =
            Time.time +
            proximityRefreshInterval;


        RefreshProximityCheckers();//This is where it check for nearby elements
    }


    public void RegisterProximityChecker(
        ElementProximityChecker checker)
    {
        if (checker == null)
            return;


        if (!proximityCheckers.Contains(
            checker))
        {
            proximityCheckers.Add(
                checker
            );
        }
    }


    public void UnregisterProximityChecker(
        ElementProximityChecker checker)
    {
        if (checker == null)
            return;


        proximityCheckers.Remove(
            checker
        );
    }


    private void RefreshProximityCheckers()
    {
        for (
            int i = proximityCheckers.Count - 1;
            i >= 0;
            i--
        )
        {
            ElementProximityChecker checker =
                proximityCheckers[i];


            if (checker == null)
            {
                proximityCheckers.RemoveAt(i);

                continue;
            }


            checker.CheckNearbyElements();
        }
    }

    public void CheckProximityForResourceAtPosition(string resourceId, Bounds resourceBounds)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        for (int i = proximityCheckers.Count - 1; i >= 0; i--)
        {
            ElementProximityChecker checker = proximityCheckers[i];

            if (checker == null)
            {
                proximityCheckers.RemoveAt(i);
                continue;
            }

            checker.CheckNearbyElements(resourceId, resourceBounds);
        }
    }



    // =========================================================
    // RECEIVE DATA
    // =========================================================

    public void ReceiveData()
    {
        if (ReceiveToggle.GetComponent<Toggle>().isOn)
        {
            Debug.Log(
                "Data Receive Toggle On"
            );

            NetworkManager.Instance.StartSync();
        }
        else
        {
            Debug.Log(
                "Data Receive Toggle Off"
            );

            NetworkManager.Instance.StopSync();
        }


        foreach (Transform i in PublishedElements)
        {
            NetworkManager.Instance
                .TurnOffNetworkElement(
                    i.gameObject
                );
        }
    }


    // =========================================================
    // PUBLISH
    // =========================================================

    public void SetStartPublish(
        bool publishing)
    {


        StartPublishing =
            publishing;


        PublishAllElements();
    }


    public void PublishAllElements()
    {
        if (!PhotonNetwork.InRoom)
            return;

        if (!StartPublishing)
            return;


        // Copy first because publishing
        // destroys/reparents elements.
        List<Transform> elementsToPublish =
            new List<Transform>();


        foreach (
            Transform element in
            AddedElements
        )
        {
            elementsToPublish.Add(
                element
            );
        }


        foreach (
            Transform element in
            elementsToPublish
        )
        {
            PublishElement(
                element
            );
        }


        foreach (
            Transform element in
            PublishedElements
        )
        {
            NetworkManager.Instance
                .TurnOffNetworkElement(
                    element.gameObject
                );
        }
    }


    public string PublishElement(
        Transform element)
    {
        if (!PhotonNetwork.InRoom)
            return null;

        if (!StartPublishing)
            return null;

        if (element == null)
            return null;

        if (element.parent ==
            PublishedElements)
        {
            return null;
        }


        ElementData originalData =
            UserDataManager.Instance.GetData(
                element.name
            );


        if (originalData == null)
        {
            Debug.LogWarning(
                $"ElementData not found for {element.name}"
            );

            return null;
        }

        if (originalData.GetString("Element Category") == "Resource")
        {
            ResourceArrivalManager arrivals = GetComponent<ResourceArrivalManager>();

            if (arrivals != null && arrivals.IsResourceEnRoute(element.name))
            {
                SystemUpdateManager.Instance?.AddUpdate( $"{originalData.type} dispatch canceled. Resource returned to base.", SystemUpdateType.General, originalData.type.ToString());

                arrivals.CancelRemovedResource(element.name);
                AssetLimit assetLimit = GetComponent<PlayerRoleManager>().GetAssetLimit(element.GetComponent<ElementAttributes>().type);
                if (assetLimit != null)
                {
                    assetLimit.ReleaseAsset();
                }
                GetComponent<PlayerRoleManager>().UpdateUI();
                Destroy(element.gameObject);
                return null;
            }
        }

        // -----------------------------------------------------
        // COPY ELEMENT DATA
        // -----------------------------------------------------

        ElementData newData =
            new ElementData
            {
                id =
                    Guid.NewGuid().ToString(),

                type =
                    originalData.type,

                posRatio =
                    originalData.posRatio,

                visualOffset =
                    originalData.visualOffset,

                generalInjuct =
                    originalData.generalInjuct,

                draggable =
                    originalData.draggable,

                text =
                    new List<string>(
                        originalData.text
                    ),

                value =
                    new List<string>(
                        originalData.value
                    )
            };


        object[] initData =
            new object[]
            {
                newData.id,

                (int)newData.type,

                newData.posRatio.x,
                newData.posRatio.y,
                newData.visualOffset.x,
                newData.visualOffset.y,

                newData.generalInjuct,

                newData.draggable,

                newData.text.ToArray(),
                newData.value.ToArray()
            };


        // -----------------------------------------------------
        // PHOTON INSTANTIATE
        // -----------------------------------------------------

        GameObject newElement =
            PhotonNetwork.Instantiate(
                "General",
                element.position,
                element.rotation,
                0,
                initData
            );


        newElement.name = newData.id;
        Debug.Log($"PUBLISHED RESOURCE: old={element.name}, new={newData.id}");
        ElementAttributes attributes =
            newElement.GetComponent<ElementAttributes>();


        if (attributes != null)
        {
            attributes.SyncTypeFromElementData(
                newData
            );
        }


        newElement.transform.SetParent(
            PublishedElements,
            true
        );


        ApplyElementTemplate(
            newElement,
            newData.type,
            newData.generalInjuct
        );

        Destroy(
            element.gameObject
        );
        return newData.id;
    }


    // =========================================================
    // APPLY TEMPLATE
    // =========================================================

    public void ApplyElementTemplate(
        GameObject target,
        ElementType type,
        bool syncIcon)
    {
        ApplyElementAttributes(
            target,
            type
        );


        /*ApplyElementSize(
            target,
            type
        );*/


        if (!syncIcon)
        {
            ApplyElementIcon(
                target,
                type
            );
        }
    }


    // =========================================================
    // GET TEMPLATE
    // =========================================================

    private ElementTemplateData GetTemplateByType(
        ElementType type)
    {
        if (templateLookup.TryGetValue(
            type,
            out ElementTemplateData template))
        {
            return template;
        }


        Debug.LogWarning(
            $"No template found for ElementType: {type}"
        );


        return null;
    }


    // =========================================================
    // APPLY ICON
    // =========================================================

    public void ApplyElementIcon(
        GameObject target,
        ElementType type)
    {
        ElementTemplateData template =
            GetTemplateByType(
                type
            );


        if (template == null)
            return;


        Image targetImage =
            target.GetComponent<Image>();


        if (targetImage == null)
        {
            Debug.LogWarning(
                $"No Image found on {target.name}"
            );

            return;
        }


        targetImage.sprite =
            template.sprite;
    }


    // =========================================================
    // APPLY ATTRIBUTES
    // =========================================================

    private void ApplyElementAttributes(
        GameObject target,
        ElementType type)
    {
        ElementTemplateData template =
            GetTemplateByType(
                type
            );


        if (template == null)
            return;


        ElementAttributes targetAttributes =
            target.GetComponent<ElementAttributes>();


        if (targetAttributes == null)
        {
            Debug.LogWarning(
                $"No ElementAttributes found on {target.name}"
            );

            return;
        }


        targetAttributes.keyValues.Clear();


        foreach (
            ElementKeyValue pair in
            template.keyValues
        )
        {
            if (pair == null ||
                string.IsNullOrWhiteSpace(
                    pair.key))
            {
                continue;
            }


            UserDataManager.Instance.SetValue(
                targetAttributes.name,
                targetAttributes,
                pair.key,
                pair.value
            );
        }
    }


    // =========================================================
    // APPLY SIZE
    // =========================================================

    public void ApplyElementSize(
        GameObject target,
        ElementType type)
    {
        ElementTemplateData template =
            GetTemplateByType(
                type
            );


        if (template == null)
            return;


        RectTransform targetRect =
            target.GetComponent<RectTransform>();


        if (targetRect == null)
        {
            Debug.LogWarning(
                $"No RectTransform found on {target.name}"
            );

            return;
        }


        RectTransform targetParent =
            targetRect.parent as RectTransform;


        if (targetParent == null)
        {
            Debug.LogWarning(
                $"Target {target.name} does not have " +
                $"a RectTransform parent."
            );

            return;
        }


        // -----------------------------------------------------
        // WIDTH
        // -----------------------------------------------------

        Vector3 localWidthVector =
            targetParent.InverseTransformVector(
                template.worldWidthVector
            );


        float width =
            localWidthVector.magnitude;


        // -----------------------------------------------------
        // HEIGHT
        // -----------------------------------------------------

        Vector3 localHeightVector =
            targetParent.InverseTransformVector(
                template.worldHeightVector
            );


        float height =
            localHeightVector.magnitude;


        targetRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            width
        );


        targetRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            height
        );
    }


    // =========================================================
    // CHANGE SCENE
    // =========================================================

    public void ChangeScene(
        string sceneName)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;


        PhotonNetwork.LoadLevel(
            sceneName
        );
    }
}