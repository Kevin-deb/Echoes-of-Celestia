using System.Collections.Generic;
using UnityEngine;

namespace PixelDungeon
{
    /// <summary>
    /// Minimal component pool. Bullet-hell games must never Instantiate/Destroy per shot,
    /// so projectiles and particles are recycled through here.
    /// </summary>
    public class ObjectPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Stack<T> _free = new();

        public ObjectPool(T prefab, Transform parent, int prewarm = 0)
        {
            _prefab = prefab;
            _parent = parent;
            for (int i = 0; i < prewarm; i++)
            {
                var t = Object.Instantiate(_prefab, _parent);
                t.gameObject.SetActive(false);
                _free.Push(t);
            }
        }

        public T Get()
        {
            T t = _free.Count > 0 ? _free.Pop() : Object.Instantiate(_prefab, _parent);
            t.gameObject.SetActive(true);
            return t;
        }

        public void Release(T t)
        {
            if (t == null) return;
            t.gameObject.SetActive(false);
            _free.Push(t);
        }
    }
}
