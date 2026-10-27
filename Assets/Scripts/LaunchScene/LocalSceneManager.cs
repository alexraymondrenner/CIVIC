
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class LocalSceneManager : MonoBehaviour
{
    private string localSceneName = "Tutorial";

    private bool isTransitioning = false;

    // =========================================================
    // ENTER LOCAL SCENE
    // =========================================================

    public void EnterLocalScene()
    {
        if (isTransitioning)
            return;

        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("Player is not in a Photon room.");
            return;
        }

        if (SceneManager.GetSceneByName(localSceneName).isLoaded)
            return;

        StartCoroutine(LoadLocalScene());
    }

    private IEnumerator LoadLocalScene()
    {
        isTransitioning = true;

        // Load locally without replacing the shared scene.
        AsyncOperation operation =
            SceneManager.LoadSceneAsync(
                localSceneName,
                LoadSceneMode.Additive
            );

        if (operation == null)
        {
            isTransitioning = false;
            yield break;
        }

        yield return operation;

        Scene localScene =
            SceneManager.GetSceneByName(localSceneName);

        if (localScene.IsValid() && localScene.isLoaded)
        {
            // Make the local scene active for this client.
            SceneManager.SetActiveScene(localScene);
        }

        Debug.Log("Entered local scene. Still in Photon room: " +
                  PhotonNetwork.InRoom);

        isTransitioning = false;
    }

    // =========================================================
    // RETURN TO SHARED SCENE
    // =========================================================

    public void ReturnToSharedScene()
    {
        if (isTransitioning)
            return;

        if (!SceneManager.GetSceneByName(localSceneName).isLoaded)
            return;

        StartCoroutine(UnloadLocalScene());
    }

    private IEnumerator UnloadLocalScene()
    {
        isTransitioning = true;

        Scene localScene =
            SceneManager.GetSceneByName(localSceneName);

        // Restore the original shared scene as active.
        Scene sharedScene = SceneManager.GetSceneByName(
            "FarmersMarketBefore"
        );

        if (sharedScene.IsValid() && sharedScene.isLoaded)
        {
            SceneManager.SetActiveScene(sharedScene);
        }

        yield return SceneManager.UnloadSceneAsync(localScene);

        Debug.Log("Returned to shared simulation.");

        isTransitioning = false;
    }
}
