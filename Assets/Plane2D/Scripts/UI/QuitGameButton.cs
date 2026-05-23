using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// This class handles quitting out of the game
/// </summary>
public class QuitGameButton : MonoBehaviour
{
    /// <summary>
    /// Description:
    /// Closes the game or exits play mode depending on the case
    /// Input:
    /// none
    /// Return:
    /// void (no return)
    /// </summary>
    public void QuitGame()
    {
        SceneManager.LoadScene(SceneNames.Hub);
    }
}
