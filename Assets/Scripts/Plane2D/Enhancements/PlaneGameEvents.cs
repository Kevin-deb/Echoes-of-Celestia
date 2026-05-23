using System;
using UnityEngine;

namespace EchoesOfCelestia.Plane2D
{
    /// <summary>
    /// 飞机大战增强模块的全局事件总线。
    /// 所有 Enhancement 组件通过订阅这里的事件实现解耦协作，
    /// 不需要修改 2D 原项目的任何脚本。
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
