using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 挂在塔防场景 UI 的「退出」按钮上，或任意对象上由 Button 调用。
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
