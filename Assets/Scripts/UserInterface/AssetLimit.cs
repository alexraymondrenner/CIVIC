using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AssetLimit : MonoBehaviour
{
    [SerializeField, ReadOnly]
    public bool spawnDisabled = false;

    [SerializeField, ReadOnly]
    public int spawnLimit = -1;
    // -1 = unlimited
    // 0 = cannot spawn
    // 1 = once
    // 2 = twice, etc.

    [SerializeField, ReadOnly]
    public int spawnCount = 0;

    [SerializeField, ReadOnly]
    public GameObject LimitText;
    private PlayerRoleManager playerRoleManager;

    private void Start()
    {
        playerRoleManager = GameObject.Find("GameManager").GetComponent<PlayerRoleManager>();
    }
    public void SetSpawnLimit(int limit)
    {
        spawnLimit = limit;

        RefreshState();
    }


    public void AddSpawn()
    {
        if (spawnLimit < 0)
            return;

        spawnCount++;

        RefreshState();
    }


    public void ReleaseAsset()
    {
        if (spawnLimit < 0)
            return;

        spawnCount =
            Mathf.Max(0, spawnCount - 1);

        RefreshState();
        playerRoleManager.UpdateUI();

        Debug.Log(
            $"{gameObject.name} released. " +
            $"Count: {spawnCount}/{spawnLimit}"
        );
    }


    private void RefreshState()
    {
        spawnDisabled =
            spawnLimit >= 0 &&
            spawnCount >= spawnLimit;

    }
}