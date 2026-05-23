# Class Record
## Group Name: Warriors
**group members:**
- Yajing Xu
- Yixuan Pan
- Yiran Xu
- Xuan Zhu
- Zeyuan Huang
- Zixuan Wang
- Cheng Zeng

## Script selected: `Health.cs`

Related script for the event chain: `Damage.cs`

## Selected Function

We chose to study this function from `Health.cs`:

```csharp
public void TakeDamage(int damageAmount)
{
    if (isInvincableFromDamage || isAlwaysInvincible)
    {
        return;
    }
    else
    {
        if (hitEffect != null)
        {
            Instantiate(hitEffect, transform.position, transform.rotation, null);
        }
        timeToBecomeDamagableAgain = Time.time + invincibilityTime;
        isInvincableFromDamage = true;
        currentHealth -= damageAmount;
        CheckDeath();
    }
}
```

## What We Did Not Understand At First

At first, we understood that `TakeDamage()` subtracts health, but we did not fully understand how the invincibility part works:

```csharp
timeToBecomeDamagableAgain = Time.time + invincibilityTime;
isInvincableFromDamage = true;
```

The new concept we researched is **time-based invincibility using `Time.time`**.

## What This Function Does

`TakeDamage(int damageAmount)` handles what happens after a game object is hit:

- It first checks whether the object is currently invincible.
- If the object is invincible, the function uses `return` and stops immediately.
- If the object can be damaged, it creates a hit effect if one is assigned.
- It starts a temporary invincibility period.
- It subtracts `damageAmount` from `currentHealth`.
- It calls `CheckDeath()` to see whether the object should die.

This means one hit does more than just reduce a number. It also starts a short protection window and connects damage to death logic.
