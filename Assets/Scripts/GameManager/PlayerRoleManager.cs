using System;
using System.Collections.Generic;
using System.Data;
using TMPro;
using UnityEngine;

[Serializable]
public class RoleObjectSetting
{
    [Tooltip("Parent object shown for this role.")]
    public GameObject target;

    [Tooltip("-1 = unlimited, 0 = cannot spawn, 1 = once, 2 = twice, etc.")]
    public int spawnLimit = -1;

}

[Serializable]
public class RoleObjectGroup
{
    public UserType role;

    [Tooltip("Objects shown for this role and their spawn limits.")]
    public List<RoleObjectSetting> objects =
        new List<RoleObjectSetting>();
}

public class PlayerRoleManager : MonoBehaviour
{
    [Header("Role-Based Objects")]
    [SerializeField]
    private List<RoleObjectGroup> roleObjectGroups =
        new List<RoleObjectGroup>();

    [Header("Objects Shown For Every Role")]
    [SerializeField]
    private GameObject Assets;



    private void Start()
    {
        //ApplyCurrentRole();
        //UpdateUI();
    }


    public void ApplyCurrentRole()
    {
        if (UserDataManager.Instance == null)
        {
            Debug.LogError(
                "UserDataManager Instance is null."
            );

            return;
        }

        UserType currentRole =
            UserDataManager.Instance.GetUserType();

        ApplyRole(currentRole);
    }


    private void ApplyRole(UserType role)
    {

        if (this.GetComponent<GameManager>().InTutorial)
        {
            role = UserType.Tutorial;
            Debug.Log("In Tutorial do not start Network Components");
            return;
        }

        foreach (Transform setting in Assets.transform)
        {
            if (setting == null)
            {
                continue;
            }

            setting.gameObject.SetActive(false);
        }



        // Find settings for current role.
        RoleObjectGroup matchingGroup =
            roleObjectGroups.Find(
                group =>
                    group != null &&
                    group.role == role
            );


        if (matchingGroup != null)
        {
            foreach (
                RoleObjectSetting setting in
                matchingGroup.objects
            )
            {
                if (setting == null ||
                    setting.target == null)
                {
                    continue;
                }

                setting.target.SetActive(true);


                AssetLimit assetLimit =
                    setting.target.GetComponent<AssetLimit>();

                if (assetLimit != null)
                {
                    // Give assetLimit the spawn limit
                    // and the UI text it should update.
                    assetLimit.SetSpawnLimit(
                        setting.spawnLimit
                    );
                }
                else
                {
                    Debug.LogWarning(
                        $"No DragAndDrop component found under " +
                        $"{setting.target.name}."
                    );
                }
            }
        }
        else
        {
            Debug.LogWarning(
                $"No RoleObjectGroup found for role: {role}"
            );
        }

        Debug.Log(
            $"Applied role settings for: {role}"
        );
        UpdateUI();

    }

    public AssetLimit GetAssetLimit(
    ElementType type)
    {
        foreach (Transform setting in Assets.transform)
        {
            if (setting == null)
            {
                continue;
            }

            if (setting.GetComponentInChildren<ElementAttributes>().type == type)
            {
                return setting.transform.GetComponentInParent<AssetLimit>();
            }
           
        }

        return null;

    }

    public void UpdateUI()
    {
        // Always-visible objects.
        foreach (Transform target in Assets.transform)
        {
            if (target != null && target.gameObject.activeSelf)
            {
                if (target.GetComponent<AssetLimit>().spawnLimit == -1) 
                { target.GetComponent<AssetLimit>().LimitText.GetComponent<TMP_Text>().text = "n"; }
                else
                {
                    target.GetComponent<AssetLimit>().LimitText.GetComponent<TMP_Text>().text = "(" + (target.GetComponent<AssetLimit>().spawnLimit - target.GetComponent<AssetLimit>().spawnCount).ToString() + ")";
                }
                target.GetComponentInChildren<DragAndDrop>().enabled = !target.GetComponent<AssetLimit>().spawnDisabled; 
            }
        }


    }

}