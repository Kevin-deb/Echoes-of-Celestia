using UnityEngine;

/// <summary>
/// Handles player state changes while being repaired by a space station.
/// </summary>
public class PlayerRepairController : MonoBehaviour
{
    [Header("Component References")]
    [Tooltip("The player's movement controller.")]
    public Controller playerController = null;
    [Tooltip("The player's health component.")]
    public Health playerHealth = null;
    [Tooltip("The player's Rigidbody2D, if one is used.")]
    public Rigidbody2D playerRigidbody = null;

    private bool isRepairing = false;
    private RigidbodyConstraints2D originalConstraints;

    /// <summary>
    /// Description:
    /// Standard Unity function called when the script instance is being loaded
    /// Inputs:
    /// none
    /// Returns:
    /// void (no return)
    /// </summary>
    private void Awake()
    {
        FindMissingComponents();
    }

    /// <summary>
    /// Description:
    /// Finds required player components if they have not been assigned in the Inspector
    /// Inputs:
    /// none
    /// Returns:
    /// void (no return)
    /// </summary>
    private void FindMissingComponents()
    {
        if (playerController == null)
        {
            playerController = GetComponent<Controller>();
        }
        if (playerHealth == null)
        {
            playerHealth = GetComponent<Health>();
        }
        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponent<Rigidbody2D>();
        }
    }

    /// <summary>
    /// Description:
    /// Starts the repair state, stopping player movement while leaving shooting scripts enabled
    /// Inputs:
    /// none
    /// Returns:
    /// bool
    /// </summary>
    /// <returns>Bool: whether repair successfully started</returns>
    public bool BeginRepair()
    {
        if (isRepairing)
        {
            return false;
        }

        FindMissingComponents();
        isRepairing = true;

        if (playerController != null)
        {
            playerController.enabled = false;
        }

        if (playerRigidbody != null)
        {
            originalConstraints = playerRigidbody.constraints;
            playerRigidbody.velocity = Vector2.zero;
            playerRigidbody.angularVelocity = 0;
            playerRigidbody.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        return true;
    }

    /// <summary>
    /// Description:
    /// Finishes the repair state, restores movement, and restores player lives
    /// Inputs:
    /// int livesToRestore
    /// Returns:
    /// void (no return)
    /// </summary>
    /// <param name="livesToRestore">The number of lives to restore</param>
    public void FinishRepair(int livesToRestore)
    {
        RestoreLives(livesToRestore);
        EndRepair();
    }

    /// <summary>
    /// Description:
    /// Restores player movement without changing lives
    /// Inputs:
    /// none
    /// Returns:
    /// void (no return)
    /// </summary>
    public void EndRepair()
    {
        if (!isRepairing)
        {
            return;
        }

        if (playerRigidbody != null)
        {
            playerRigidbody.constraints = originalConstraints;
        }

        if (playerController != null)
        {
            playerController.enabled = true;
        }

        isRepairing = false;
    }

    /// <summary>
    /// Description:
    /// Restores player lives when lives are enabled, otherwise restores health
    /// Inputs:
    /// int livesToRestore
    /// Returns:
    /// void (no return)
    /// </summary>
    /// <param name="livesToRestore">The number of lives to restore</param>
    private void RestoreLives(int livesToRestore)
    {
        if (playerHealth == null)
        {
            return;
        }

        if (playerHealth.useLives)
        {
            playerHealth.currentLives = Mathf.Min(playerHealth.currentLives + livesToRestore, playerHealth.maximumLives);
        }
        else
        {
            playerHealth.ReceiveHealing(livesToRestore);
        }

        GameManager.UpdateUIElements();
    }

    /// <summary>
    /// Description:
    /// Restores movement if this component is disabled during repair
    /// Inputs:
    /// none
    /// Returns:
    /// void (no return)
    /// </summary>
    private void OnDisable()
    {
        EndRepair();
    }
}
