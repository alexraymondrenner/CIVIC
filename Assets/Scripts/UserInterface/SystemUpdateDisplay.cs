using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SystemUpdateDisplay : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField]
    private Transform updateContainer;

    [SerializeField]
    private TMP_Text updatePrefab;

    [SerializeField]
    private int maxVisibleUpdates = 20;

    [SerializeField]
    private ScrollRect scrollRect;


    [Header("Text Settings")]
    [SerializeField]
    private float updateFontSize = 24f;


    private void OnEnable()
    {
        if (SystemUpdateManager.Instance != null)
        {
            SystemUpdateManager.Instance.OnUpdateAdded +=
                AddUpdate;
        }
    }


    private void OnDisable()
    {
        if (SystemUpdateManager.Instance != null)
        {
            SystemUpdateManager.Instance.OnUpdateAdded -=
                AddUpdate;
        }
    }


    private void AddUpdate(
        SystemUpdateData update)
    {
        if (update == null ||
            updatePrefab == null ||
            updateContainer == null)
        {
            return;
        }


        // ---------------------------------------------
        // CREATE MESSAGE
        // ---------------------------------------------

        TMP_Text newEntry =
            Instantiate(
                updatePrefab,
                updateContainer
            );


        newEntry.enableAutoSizing = false;
        newEntry.fontSize = updateFontSize;


        string time =
            update.timestamp.ToString("HH:mm:ss");


        string typeLabel =
            GetTypeLabel(
                update.updateType
            );


        newEntry.text =
            $"{time}  {typeLabel}{update.message}";


        // Newest message goes to the bottom.
        newEntry.transform.SetAsLastSibling();


        // ---------------------------------------------
        // REMOVE OLDEST MESSAGE
        // ---------------------------------------------

        if (updateContainer.childCount >
            maxVisibleUpdates)
        {
            // First child = oldest message
            GameObject oldestEntry =
                updateContainer.GetChild(0).gameObject;

            Destroy(oldestEntry);
        }


        // ---------------------------------------------
        // UPDATE LAYOUT
        // ---------------------------------------------

        Canvas.ForceUpdateCanvases();


        if (updateContainer is RectTransform content)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                content
            );
        }


        // ---------------------------------------------
        // SCROLL TO NEWEST MESSAGE
        // ---------------------------------------------

        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();

            // 0 = bottom, 1f = top
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }


    private string GetTypeLabel(
        SystemUpdateType updateType)
    {
        switch (updateType)
        {
            case SystemUpdateType.Inject:
                return "[Inject] ";

            case SystemUpdateType.Action_Taken:
                return "[Action_Taken] ";

            case SystemUpdateType.Action_Text:
                return "[Text] ";

            default:
                return string.Empty;
        }
    }
}