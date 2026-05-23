using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Moves the Pyramid Space Station and repairs the player when nearby.
/// </summary>
public class PyramidSpaceStation : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("The diagonal direction the space station moves along.")]
    public Vector2 diagonalDirection = new Vector2(1, -1);
    [Tooltip("The total distance between the two ends of the diagonal path.")]
    public float travelDistance = 4.0f;
    [Tooltip("The speed at which the space station moves.")]
    public float moveSpeed = 2.0f;

    [Header("Repair Settings")]
    [Tooltip("How close the player must be to start repairing.")]
    public float interactionRadius = 2.5f;
    [Tooltip("How long the repair lasts, in seconds.")]
    public float repairDuration = 3.0f;
    [Tooltip("How long before this station can repair again, in seconds.")]
    public float repairCooldown = 30.0f;
    [Tooltip("The key used to start repairing.")]
    public Key repairKey = Key.F;
    [Tooltip("The layers that can contain the player.")]
    public LayerMask playerLayers = ~0;

    private Vector3 firstPathPoint;
    private Vector3 secondPathPoint;
    private Vector3 currentTargetPoint;
    private bool isRepairing = false;
    private float nextRepairTime = 0;

    /// <summary>
    /// Description:
    /// Standard Unity function called once before the first frame update
    /// Inputs:
    /// none
    /// Returns:
    /// void (no return)
    /// </summary>
    private void Start()
    {
        SetUpMovementPath();
    }

    /// <summary>
    /// Description:
    /// Standard Unity function called once per frame
    /// Inputs:
    /// none
    /// Returns:
    /// void (no return)
    /// </summary>
    private void Update()
    {
        MoveSpaceStation();
        TryStartRepair();
    }

    /// <summary>
    /// Description:
    /// Creates a diagonal path centered on the station's starting position
    /// Inputs:
    /// none
    /// Returns:
    /// void (no return)
    /// </summary>
    private void SetUpMovementPath()
    {
        Vector3 movementDirection = new Vector3(diagonalDirection.x, diagonalDirection.y, 0);
        if (movementDirection.sqrMagnitude <= 0.001f)
        {
            movementDirection = new Vector3(1, -1, 0);
        }

        movementDirection = movementDirection.normalized;
        Vector3 halfPath = movementDirection * travelDistance * 0.5f;
        firstPathPoint = transform.position - halfPath;
        secondPathPoint = transform.position + halfPath;
        currentTargetPoint = secondPathPoint;
    }

    /// <summary>
    /// Description:
    /// Moves the space station back and forth along its diagonal path
    /// Inputs:
    /// none
    /// Returns:
    /// void (no return)
    /// </summary>
    private void MoveSpaceStation()
    {
        if (isRepairing)
        {
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, currentTargetPoint, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, currentTargetPoint) <= 0.01f)
        {
            currentTargetPoint = currentTargetPoint == secondPathPoint ? firstPathPoint : secondPathPoint;
        }
    }

    /// <summary>
    /// Description:
    /// Starts repairing if the player is close enough and presses the repair key
    /// Inputs:
    /// none
    /// Returns:
    /// void (no return)
    /// </summary>
    private void TryStartRepair()
    {
        if (isRepairing || Time.time < nextRepairTime || Keyboard.current == null)
        {
            return;
        }

        var repairKeyControl = Keyboard.current[repairKey];
        if (repairKeyControl == null || !repairKeyControl.wasPressedThisFrame)
        {
            return;
        }

        PlayerRepairController playerRepairController = FindNearbyPlayer();
        if (playerRepairController != null)
        {
            StartCoroutine(RepairPlayer(playerRepairController));
        }
    }

    /// <summary>
    /// Description:
    /// Finds a nearby player that can be repaired
    /// Inputs:
    /// none
    /// Returns:
    /// PlayerRepairController
    /// </summary>
    /// <returns>PlayerRepairController: the nearby player repair component, or null</returns>
    private PlayerRepairController FindNearbyPlayer()
    {
        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(transform.position, interactionRadius, playerLayers);

        foreach (Collider2D nearbyCollider in nearbyColliders)
        {
            PlayerRepairController playerRepairController = nearbyCollider.GetComponentInParent<PlayerRepairController>();
            if (playerRepairController != null)
            {
                return playerRepairController;
            }
        }

        return null;
    }

    /// <summary>
    /// Description:
    /// Repairs the player, pausing both player movement and space station movement
    /// Inputs:
    /// PlayerRepairController playerRepairController
    /// Returns:
    /// IEnumerator
    /// </summary>
    /// <param name="playerRepairController">The player to repair</param>
    private IEnumerator RepairPlayer(PlayerRepairController playerRepairController)
    {
        if (!playerRepairController.BeginRepair())
        {
            yield break;
        }

        isRepairing = true;
        yield return new WaitForSeconds(repairDuration);

        if (playerRepairController != null)
        {
            playerRepairController.FinishRepair(1);
        }

        isRepairing = false;
        nextRepairTime = Time.time + repairCooldown;
    }

    /// <summary>
    /// Description:
    /// Draws the movement path and interaction radius in the editor
    /// Inputs:
    /// none
    /// Returns:
    /// void (no return)
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Vector3 movementDirection = new Vector3(diagonalDirection.x, diagonalDirection.y, 0);
        if (movementDirection.sqrMagnitude <= 0.001f)
        {
            movementDirection = new Vector3(1, -1, 0);
        }

        Vector3 halfPath = movementDirection.normalized * travelDistance * 0.5f;
        Vector3 previewFirstPoint = transform.position - halfPath;
        Vector3 previewSecondPoint = transform.position + halfPath;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(previewFirstPoint, previewSecondPoint);
        Gizmos.DrawWireSphere(previewFirstPoint, 0.15f);
        Gizmos.DrawWireSphere(previewSecondPoint, 0.15f);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
