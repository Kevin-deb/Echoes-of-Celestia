using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pulls player and enemy ships toward a black hole, and handles what happens when they touch it.
/// </summary>
public class Attract : MonoBehaviour
{
    [Header("Attraction Settings")]
    [Tooltip("Objects inside this radius can be pulled toward the black hole.")]
    public float attractionRadius = 5.0f;
    [Tooltip("How strongly the black hole pulls nearby objects.")]
    public float attractionStrength = 15.0f;
    [Tooltip("Which physics layers can be pulled by this black hole.")]
    public LayerMask affectedLayers = ~0;

    [Header("Target Settings")]
    [Tooltip("Whether or not the player can be pulled by this black hole.")]
    public bool attractPlayer = true;
    [Tooltip("Whether or not enemies can be pulled by this black hole.")]
    public bool attractEnemies = true;

    private readonly HashSet<GameObject> attractedObjects = new HashSet<GameObject>();

    /// <summary>
    /// Description:
    /// Standard Unity function called at a fixed interval for physics updates
    /// Inputs:
    /// none
    /// Returns:
    /// void (no return)
    /// </summary>
    private void FixedUpdate()
    {
        AttractNearbyObjects();
    }

    /// <summary>
    /// Description:
    /// Finds nearby player and enemy ships and pulls them toward this black hole
    /// Inputs:
    /// none
    /// Returns:
    /// void (no return)
    /// </summary>
    private void AttractNearbyObjects()
    {
        attractedObjects.Clear();
        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(transform.position, attractionRadius, affectedLayers);

        foreach (Collider2D nearbyCollider in nearbyColliders)
        {
            GameObject attractedObject = GetAttractableObject(nearbyCollider.gameObject);
            if (attractedObject != null && attractedObjects.Add(attractedObject))
            {
                PullObject(attractedObject);
            }
        }
    }

    /// <summary>
    /// Description:
    /// Returns the player or enemy object attached to a nearby collider, if there is one
    /// Inputs:
    /// GameObject nearbyObject
    /// Returns:
    /// GameObject
    /// </summary>
    /// <param name="nearbyObject">The nearby object found by the attraction radius check</param>
    /// <returns>GameObject: the object that should be attracted, or null</returns>
    private GameObject GetAttractableObject(GameObject nearbyObject)
    {
        Enemy enemy = nearbyObject.GetComponentInParent<Enemy>();
        if (attractEnemies && enemy != null)
        {
            return enemy.gameObject;
        }

        Controller playerController = nearbyObject.GetComponentInParent<Controller>();
        Health playerHealth = nearbyObject.GetComponentInParent<Health>();
        if (attractPlayer && (playerController != null || (playerHealth != null && playerHealth.CompareTag("Player"))))
        {
            return playerController != null ? playerController.gameObject : playerHealth.gameObject;
        }

        return null;
    }

    /// <summary>
    /// Description:
    /// Applies black hole pull to an object
    /// Inputs:
    /// GameObject attractedObject
    /// Returns:
    /// void (no return)
    /// </summary>
    /// <param name="attractedObject">The object to pull toward the black hole</param>
    private void PullObject(GameObject attractedObject)
    {
        Vector2 directionToBlackHole = transform.position - attractedObject.transform.position;
        float distanceToBlackHole = directionToBlackHole.magnitude;

        if (distanceToBlackHole <= 0.001f)
        {
            return;
        }

        float distancePercent = Mathf.Clamp01(distanceToBlackHole / attractionRadius);
        float pullAmount = attractionStrength * (1.0f - distancePercent);
        Vector2 pullDirection = directionToBlackHole.normalized;
        Rigidbody2D attractedRigidbody = attractedObject.GetComponent<Rigidbody2D>();

        if (attractedRigidbody != null)
        {
            attractedRigidbody.AddForce(pullDirection * pullAmount, ForceMode2D.Force);
        }
        else
        {
            attractedObject.transform.position = Vector3.MoveTowards(
                attractedObject.transform.position,
                transform.position,
                pullAmount * Time.fixedDeltaTime);
        }
    }

    /// <summary>
    /// Description:
    /// Standard Unity function called whenever a Collider2D enters any attached 2D trigger collider
    /// Inputs:
    /// Collider2D collision
    /// Returns:
    /// void (no return)
    /// </summary>
    /// <param name="collision">The Collider2D that touched the black hole</param>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        HandleBlackHoleCollision(collision.gameObject);
    }

    /// <summary>
    /// Description:
    /// Standard Unity function called when a Collider2D hits another Collider2D
    /// Inputs:
    /// Collision2D collision
    /// Returns:
    /// void (no return)
    /// </summary>
    /// <param name="collision">The Collision2D that touched the black hole</param>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleBlackHoleCollision(collision.gameObject);
    }

    /// <summary>
    /// Description:
    /// Destroys enemies or damages the player when they touch the black hole
    /// Inputs:
    /// GameObject collisionObject
    /// Returns:
    /// void (no return)
    /// </summary>
    /// <param name="collisionObject">The object that touched the black hole</param>
    private void HandleBlackHoleCollision(GameObject collisionObject)
    {
        Enemy enemy = collisionObject.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            DestroyEnemy(enemy);
            return;
        }

        Controller playerController = collisionObject.GetComponentInParent<Controller>();
        Health playerHealth = collisionObject.GetComponentInParent<Health>();
        if (playerHealth != null && (playerController != null || playerHealth.CompareTag("Player")))
        {
            int damageAmount = Mathf.Max(1, playerHealth.currentHealth);
            playerHealth.TakeDamage(damageAmount);
        }
    }

    /// <summary>
    /// Description:
    /// Destroys an enemy through its health component so existing death effects are played
    /// Inputs:
    /// Enemy enemy
    /// Returns:
    /// void (no return)
    /// </summary>
    /// <param name="enemy">The enemy that touched the black hole</param>
    private void DestroyEnemy(Enemy enemy)
    {
        Health enemyHealth = enemy.GetComponent<Health>();
        if (enemyHealth != null)
        {
            enemyHealth.Die();
        }
        else
        {
            enemy.DoBeforeDestroy();
            Destroy(enemy.gameObject);
        }
    }

    /// <summary>
    /// Description:
    /// Draws the attraction radius in the editor when this object is selected
    /// Inputs:
    /// none
    /// Returns:
    /// void (no return)
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, attractionRadius);
    }
}
