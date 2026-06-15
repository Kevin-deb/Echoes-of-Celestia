using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Wire this up to the tower-defence "Exit" button, or call it from any Button.
/// </summary>
public sealed class TDSceneExit : MonoBehaviour
{
    [SerializeField] string hubSceneName = SceneNames.Hub;

    public void ExitToHub()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(hubSceneName, LoadSceneMode.Single);
    }
}
