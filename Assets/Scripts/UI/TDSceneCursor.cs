using UnityEngine;

/// <summary>
/// Drop this on anything in a tower-defence scene: it frees the cursor on entry
/// so you can click the UI and place towers with SimpleGridBuilder.
/// </summary>
public sealed class TDSceneCursor : MonoBehaviour
{
    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
