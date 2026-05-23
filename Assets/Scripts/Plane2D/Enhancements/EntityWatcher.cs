using System.Collections.Generic;
using UnityEngine;

namespace EchoesOfCelestia.Plane2D
{
    /// <summary>
    /// 每帧扫描 Health/Enemy 状态变化，把伤害与死亡事件转换为 PlaneGameEvents。
    /// 不修改任何原脚本。
    /// </summary>
    public sealed class EntityWatcher : MonoBehaviour
    {
        struct State
        {
            public int Health;
            public Vector3 LastPos;
            public bool IsPlayer;
            public int ScoreValue;
            public GameObject Go;
        }

        readonly Dictionary<int, State> _tracked = new Dictionary<int, State>(64);
        readonly List<int> _scratchDeadIds = new List<int>(8);
        Health[] _scratchHealths = new Health[0];

        void Update()
        {
            _scratchHealths = FindObjectsOfType<Health>();
            var seen = new HashSet<int>();

            for (int i = 0; i < _scratchHealths.Length; i++)
            {
                var h = _scratchHealths[i];
                if (h == null) continue;

                var id = h.GetInstanceID();
                seen.Add(id);

                var go = h.gameObject;
                var isPlayer = go.CompareTag("Player");
                var enemy = go.GetComponent<Enemy>();
                var scoreValue = enemy != null ? enemy.scoreValue : 0;
                var pos = go.transform.position;

                if (_tracked.TryGetValue(id, out var prev))
                {
                    if (h.currentHealth < prev.Health)
                    {
                        var damage = prev.Health - h.currentHealth;
                        if (isPlayer) PlaneGameEvents.RaisePlayerDamaged(go, damage);
                        else PlaneGameEvents.RaiseEnemyDamaged(go, damage);
                    }
                }

                _tracked[id] = new State
                {
                    Health = h.currentHealth,
                    LastPos = pos,
                    IsPlayer = isPlayer,
                    ScoreValue = scoreValue,
                    Go = go,
                };
            }

            _scratchDeadIds.Clear();
            foreach (var kv in _tracked)
            {
                if (!seen.Contains(kv.Key))
                    _scratchDeadIds.Add(kv.Key);
            }

            for (int i = 0; i < _scratchDeadIds.Count; i++)
            {
                var id = _scratchDeadIds[i];
                var st = _tracked[id];
                if (st.IsPlayer) PlaneGameEvents.RaisePlayerKilled(st.LastPos);
                else PlaneGameEvents.RaiseEnemyKilled(st.LastPos, st.ScoreValue);
                _tracked.Remove(id);
            }
        }
    }
}
