using UnityEngine;

/// <summary>
/// 挂在塔防场景任意物体上：进入场景后解锁鼠标，便于点 UI 与 SimpleGridBuilder 造塔。
/// </summary>
public sealed class TDSceneCursor : MonoBehaviour
{
    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
