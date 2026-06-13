using System.Collections.Generic;
using UnityEngine;

namespace PixelDungeon
{
    /// <summary>
    /// Single source of truth for *asset references* (prefabs, sprites, audio, generated tiles).
    /// Populated once by the editor generator (GameSetup) and loaded at runtime from Resources.
    /// Gameplay *tuning* lives in code (GameContent) so it can be iterated without re-running the generator.
    /// </summary>
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "PixelDungeon/GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        [System.Serializable] public class NamedPrefab { public string key; public GameObject prefab; }
        [System.Serializable] public class NamedSprite { public string key; public Sprite sprite; }
        [System.Serializable]
        public class Theme { public string name; public Sprite floor; public Sprite wall; }

        public GameObject playerPrefab;
        public List<NamedPrefab> monsters = new();
        public List<NamedSprite> sprites = new();
        public List<Theme> themes = new();

        public AudioClip bgm;
        public AudioClip sfxFire;
        public AudioClip sfxEquip;
        public Font font;

        private Dictionary<string, GameObject> _monsterMap;
        private Dictionary<string, Sprite> _spriteMap;

        public GameObject GetMonster(string key)
        {
            _monsterMap ??= Build(monsters, m => m.key, m => m.prefab);
            return _monsterMap.TryGetValue(key, out var p) ? p : null;
        }

        public Sprite GetSprite(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            _spriteMap ??= Build(sprites, s => s.key, s => s.sprite);
            return _spriteMap.TryGetValue(key, out var s2) ? s2 : null;
        }

        public Theme GetTheme(int floorIndex)
        {
            if (themes.Count == 0) return null;
            return themes[Mathf.Clamp(floorIndex, 0, themes.Count - 1)];
        }

        private static Dictionary<string, V> Build<E, V>(List<E> list, System.Func<E, string> k, System.Func<E, V> v)
        {
            var d = new Dictionary<string, V>();
            foreach (var e in list) { var key = k(e); if (!string.IsNullOrEmpty(key) && !d.ContainsKey(key)) d[key] = v(e); }
            return d;
        }
    }
}
