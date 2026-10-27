using UnityEngine;

public class ReturnButton : MonoBehaviour
{
    public void ReturnToSharedSimulation()
    {
        LocalSceneManager manager =
            FindFirstObjectByType<LocalSceneManager>();

        if (manager != null)
        {
            manager.ReturnToSharedScene();
        }
    }
}