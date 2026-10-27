using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using System.Collections;

public class PlayerNetManager : MonoBehaviourPunCallbacks
{
    [Tooltip("The local player instance.")]
    public static GameObject LocalPlayerInstance;

    private Transform publishedElements;
    private ElementData data;
    private GameManager gameManager;


    private void Awake()
    {
        if (photonView.IsMine)
        {
            LocalPlayerInstance = gameObject;
        }

        GameObject managerObject = GameObject.Find("GameManager");

        if (managerObject != null)
        {
            gameManager = managerObject.GetComponent<GameManager>();
        }

        SpawnElements();
    }

    // =========================================================
    // INITIAL NETWORK ELEMENT SETUP
    // =========================================================

    public void SpawnElements()
    {
        if (photonView.InstantiationData == null)
            return;

        object[] receivedData = photonView.InstantiationData;

        if (receivedData == null)
            return;

        string id = (string)receivedData[0];
        ElementType type = (ElementType)(int)receivedData[1];

        Vector2 posRatio = new Vector2(
            (float)receivedData[2],
            (float)receivedData[3]
        );

        Vector2 visualOffset = new Vector2(
             (float)receivedData[4],
            (float)receivedData[5]

            );

        bool generalInjuct = (bool)receivedData[6];
        bool draggable = (bool)receivedData[7];

        List<string> text =
            new List<string>((string[])receivedData[8]);

        List<string> value =
            new List<string>((string[])receivedData[9]);

        name = id;

        ElementAttributes attributes =
            GetComponent<ElementAttributes>();

        // Create local ElementData mirror.
        UserDataManager.Instance.CreateData(
            id,
            attributes
        );

        ElementData target =
            UserDataManager.Instance.GetData(id);

        if (target == null)
            return;

        // Initialize data received from Photon.
        UserDataManager.Instance.SetPos(
            id,
            posRatio
        );

        UserDataManager.Instance.SetVisualOffset(id, visualOffset);

        UserDataManager.Instance.SetGeneralInjuct(
            id,
            generalInjuct
        );

        UserDataManager.Instance.SetType(
            id,
            type
        );

        UserDataManager.Instance.SetDraggable(
            id,
            draggable
        );

        for (int i = 0; i < text.Count; i++)
        {
            if (i >= value.Count)
                break;

            UserDataManager.Instance.SetValue(
                id,
                attributes,
                text[i],
                value[i]
            );
        }


        attributes.SyncTypeFromElementData(
            target
        );

        if (gameManager != null)
        {
            gameManager.ApplyElementTemplate(
                gameObject,
                type,
                generalInjuct
            );
        }
       

        Transform addedModels =
            GameObject.Find("AddedModels")?.transform;

        if (addedModels != null)
        {
            Transform model =
                addedModels.Find(id + "_model");

            if (model != null)
            {
                GetComponent<DragAndDrop>().SpownModel =
                    model.gameObject;
            }
            else
            {
                GetComponent<DragAndDrop>().SpawnPrefab();
            }
        }

        GetComponent<DragAndDrop>()
            .DisableDragging(!draggable);
    }

    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        if (photonView.InstantiationData != null)
        {
            GameObject publishedObject = GameObject.Find("PublishedElements");

            if (publishedObject != null)
            {
                publishedElements = publishedObject.transform;
                transform.SetParent(publishedElements, true);
            }
        }
        
        data = UserDataManager.Instance.GetData(name);
        UserType currentRole = UserDataManager.Instance.GetUserType();
        
        if (gameManager != null)
        {
            if (currentRole != UserType.Facilitator)
            {
                gameManager.ApplyElementTemplate(
                gameObject,
                data.type,
                data.generalInjuct
            );
            }
            else if (currentRole == UserType.Facilitator)
            {
                gameManager.ApplyElementTemplate(
                gameObject,
                data.type,
                false
            );//Icon is revealed to facilitator at anypoint
            }
        }
        if (data != null)
        {
            data.OnTransformChanged += OnTransformChanged;
            data.OnValueChanged += OnValueChanged;
        }

        // Important:
        // This network element is now fully initialized.
        // Ask the Master to reevaluate proximity.

        StartCoroutine(InitializeElementSize());
    }

    private IEnumerator InitializeElementSize()
    {
        yield return new WaitUntil(() => gameManager != null && data != null && gameManager.AreTemplatesCached);
        gameManager.ApplyElementSize(gameObject, data.type);
        gameManager.RequestProximityRefresh();
       

    }

    // =========================================================
    // TRANSFORM SYNC
    // =========================================================

    private void OnTransformChanged()
    {
        if (!photonView.IsMine)
            return;

        if (data == null)
            return;

        /*
         * IMPORTANT:
         *
         * If Master owns this object, this triggers the
         * authoritative proximity check locally.
         *
         * If a non-Master owns it, GameManager ignores this
         * because RequestProximityRefresh() is Master-only.
         */
        if (gameManager != null)
        {
            gameManager.RequestProximityRefresh();
        }

        photonView.RPC(
            nameof(RPC_Transform),
            RpcTarget.Others,
            data.id,
            data.posRatio.x,
            data.posRatio.y
        );

      
    }
   

    [PunRPC]
    private void RPC_Transform(
        string id,
        float px,
        float py
    )
    {
        ElementData target =
            UserDataManager.Instance.GetData(id);

        if (target == null)
            return;

        // Update the local mirror without firing
        // another transform-changed event.
        target.posRatio =
            new Vector2(px, py);

        /*
         * Only Master will actually queue the proximity check.
         * Other clients return immediately inside GameManager.
         */
        if (gameManager != null)
        {
            gameManager.RequestProximityRefresh();
        }
    }

    public void SyncVisualOffset(Vector2 offset)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        photonView.RPC(
            nameof(RPC_VisualOffset),
            RpcTarget.Others,
            name,
            offset.x,
            offset.y
        );
    }

    [PunRPC]
    private void RPC_VisualOffset(string id, float x, float y)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            ElementData target = UserDataManager.Instance.GetData(id);
            if (target == null) return;

            UserDataManager.Instance.SetVisualOffset(id, new Vector2(x, y));
        }
    }

    // =========================================================
    // KEY-VALUE SYNC
    // =========================================================

    private void OnValueChanged(string key)
    {
        if (!photonView.IsMine)
            return;

        if (data == null)
            return;

        string value =
            data.GetString(key);

        photonView.RPC(
            nameof(RPC_KeyValue),
            RpcTarget.Others,
            data.id,
            key,
            value
        );
    }

    [PunRPC]
    private void RPC_KeyValue(
        string id,
        string key,
        string value
    )
    {
        ElementData target =
            UserDataManager.Instance.GetData(id);

        if (target == null)
            return;

        int index =
            target.text.IndexOf(key);

        if (index >= 0)
        {
            target.value[index] = value;
        }
        else
        {
            target.text.Add(key);
            target.value.Add(value);
        }

        // Also update ElementAttributes on this client.
        ElementAttributes attributes =
            GetComponent<ElementAttributes>();

        if (attributes != null)
        {
            attributes.SetAttributeValue(
                key,
                value
            );
        }
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDestroy()
    {
        if (data == null)
            return;

        data.OnTransformChanged -= OnTransformChanged;
        data.OnValueChanged -= OnValueChanged;
    }
}