using System;
using UnityEngine;

namespace EchoesOfCelestia.Plane2D
{
    /// <summary>
    /// Global event bus for the plane-shooter enhancement modules. Every
    /// Enhancement component talks through these events instead of referencing
    /// one another, which keeps the original 2D project's scripts out of it.
    /// </summary>
    public static class PlaneGameEvents
    {
        public static event Action<GameObject, int> EnemyDamaged;
        public static event Action<Vector3, int> EnemyKilled;
        public static event Action<GameObject, int> PlayerDamaged;
        public static event Action<Vector3> PlayerKilled;

        public static void RaiseEnemyDamaged(GameObject target, int damage) => EnemyDamaged?.Invoke(target, damage);
        public static void RaiseEnemyKilled(Vector3 position, int score) => EnemyKilled?.Invoke(position, score);
        public static void RaisePlayerDamaged(GameObject target, int damage) => PlayerDamaged?.Invoke(target, damage);
        public static void RaisePlayerKilled(Vector3 position) => PlayerKilled?.Invoke(position);

        public static void ResetAllSubscribers()
        {
            EnemyDamaged = null;
            EnemyKilled = null;
            PlayerDamaged = null;
            PlayerKilled = null;
        }
    }
}
