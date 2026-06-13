#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PixelDungeon.EditorTools
{
    /// <summary>Runs the asset/scene generator automatically the first time scripts compile,
    /// so the embedded mini-game becomes playable without manual steps.</summary>
    [InitializeOnLoad]
    public static class AutoBuilder
    {
        static AutoBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.LoadAssetAtPath<GameDatabase>(GameSetup.DbPath) == null)
                    GameSetup.BuildAll();
            };
        }
    }

    /// <summary>
    /// One-shot generator for the embedded Pixel Dungeon mini-game inside Echoes of Celestia.
    /// Wires the PixelFantasy pack assets into a GameDatabase, creates cleaned prefab variants
    /// (example-scene scripts stripped at build time), and authors the playable scene.
    /// Only ADDS to the host project: the PixelDungeon scene is appended to Build Settings,
    /// existing scenes/settings are never touched.
    /// </summary>
    public static class GameSetup
    {
        private const string PF = "Assets/PixelFantasy";
        public const string DbPath = "Assets/PixelDungeon/Resources/GameDatabase.asset";
        private const string DefaultScenePath = "Assets/Scenes/PixelDungeon.unity";
        private const string PrefabDir = "Assets/PixelDungeon/Prefabs";

        /// <summary>Existing scene location wins (the scene may have been moved, e.g. into
        /// Assets/Scenes/Space); otherwise the default path is used for a fresh scene.</summary>
        private static string ScenePath => SmokeTest.FindScenePath() ?? DefaultScenePath;

        /// <summary>Example-scene scripts shipped on the pack prefabs, stripped from our variants.
        /// Order matters: RequireComponent dependents must be removed before their dependencies.</summary>
        private static readonly string[] MonsterStrip = { "MonsterControls", "MonsterController2D", "MonsterAnimation", "InanimateControls" };
        private static readonly string[] PlayerStrip = { "CharacterControls", "CharacterController2D", "CharacterAnimation", "TimeScale" };

        [MenuItem("Tools/Pixel Dungeon/Build Game Assets")]
        public static void BuildAll()
        {
            EnsureFolder("Assets/PixelDungeon/Resources");
            EnsureFolder(PrefabDir);
            EnsureFolder(PrefabDir + "/Monsters");
            EnsureFolder("Assets/Scenes");

            BuildDatabase();
            var scenePath = ScenePath;   // resolve once: existing location wins
            BuildScene(scenePath);
            AppendSceneToBuildSettings(scenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PixelDungeon] Build complete — walk to the com-station in the Hub and press F, " +
                      "or open Assets/Scenes/PixelDungeon.unity directly.");
        }

        // ---------------- Database ----------------

        private static void BuildDatabase()
        {
            AssetDatabase.DeleteAsset(DbPath);
            var db = ScriptableObject.CreateInstance<GameDatabase>();
            AssetDatabase.CreateAsset(db, DbPath);

            var sourcePlayer = Load<GameObject>($"{PF}/PixelHeroes4D/FantasyHeroes/Prefabs/Character.prefab");
            db.playerPrefab = MakeCleanVariant(sourcePlayer, $"{PrefabDir}/Character.prefab", PlayerStrip);

            AddMonster(db, "Rat", "Pack1/Rat/BrownRat");
            AddMonster(db, "Bat", "Pack1/Bat/PurpleBat");
            AddMonster(db, "Spider", "Pack1/Spider/RedSpider");
            AddMonster(db, "Wolf", "Pack1/Wolf/BlackWolf");
            AddMonster(db, "Boar", "Pack1/Boar/BrownBoar");
            AddMonster(db, "Bee", "Pack1/Bee/YellowBee");
            AddMonster(db, "Scorpion", "Pack1/Scorpion/RedScorpion");
            AddMonster(db, "Mushroom", "Pack2/Mushroom/BrownMushroom");
            AddMonster(db, "Snake", "Pack2/Snake/GreenSnake");
            AddMonster(db, "HellDog", "Pack2/HellDog/HellDog");
            AddMonster(db, "Spirit", "Pack2/Spirit/BlueSpirit");
            AddMonster(db, "Salamandra", "Pack2/Salamandra/FireSalamandra");
            AddMonster(db, "Jelly", "Pack2/Jelly/GreenJelly");
            AddMonster(db, "FlyingEye", "Pack3/FlyingEye/FlyingEye");
            AddMonster(db, "Beholder", "Pack3/Beholder/Beholder");
            AddMonster(db, "Demon", "Pack3/Demon/Demon");
            AddMonster(db, "EarthGolem", "Pack3/EarthGolem/EarthGolem");
            AddMonster(db, "FlameGolem", "Pack3/FlameGolem/FlameGolem");
            AddMonster(db, "IceGolem", "Pack3/IceGolem/IceGolem");
            AddMonster(db, "Mimic", "BoxPack1/Mimic/Mimic");

            AddSprite(db, "CoinGold", "CoinGolden");
            AddSprite(db, "CoinSilver", "CoinSilver");
            AddSprite(db, "GemBlue", "GemstoneBlue");
            AddSprite(db, "GemRed", "GemstoneRed");
            AddSprite(db, "GemGreen", "GemstoneGreen");
            AddSprite(db, "GemYellow", "GemstoneYellow");
            AddSprite(db, "GemPurple", "GemstonePurple");
            AddSprite(db, "PotionRed", "PotionRedSmall");
            AddSprite(db, "PotionBlue", "PotionBlueSmall");
            AddSprite(db, "PotionGreen", "PotionGreenSmall");
            AddSprite(db, "PotionYellow", "PotionYellowSmall");
            AddSprite(db, "KeyIron", "KeyIronSmall");
            AddSprite(db, "KeySilver", "KeySilverSmall");
            AddSprite(db, "KeyGold", "KeyGoldenSmall");
            AddSprite(db, "ChestWooden", "ChestWoodenMedium");
            AddSprite(db, "ChestSilver", "ChestSilverMedium");
            AddSprite(db, "ChestGolden", "ChestGoldenMedium");
            AddSprite(db, "Barrel", "BarrelSmallA");
            AddSprite(db, "BoxSmall", "BoxSmallA");
            AddSprite(db, "Vase", "VaseBlue");
            AddSprite(db, "Explosives", "BarrelSmallB");
            AddSprite(db, "SpikeOn", "SpikedTrapOn");
            AddSprite(db, "SpikeOff", "SpikedTrapOff");
            AddSprite(db, "Saw", "Saw");
            AddSprite(db, "Altar", "AltarBloody");
            AddSprite(db, "Banner", "BannerRed");
            AddSprite(db, "Tree", "TreeLargeA");

            AddTheme(db, "Dungeon", "BricksA");
            AddTheme(db, "Cavern", "Rocks");
            AddTheme(db, "Magma", "Magma");
            AddTheme(db, "Frozen", "Frozen");
            AddTheme(db, "Sand", "Sand");

            db.bgm = Load<AudioClip>($"{PF}/PixelHeroes4D/Common/Music/The Cynic Project - A New Town.mp3");
            db.sfxFire = Load<AudioClip>($"{PF}/Common/Audio/Fire.ogg");
            db.sfxEquip = Load<AudioClip>($"{PF}/Common/Audio/Equip.wav");
            db.font = Load<Font>($"{PF}/Common/Fonts/Pribambas [by Misha Panfilov].ttf");

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
        }

        private static void AddMonster(GameDatabase db, string key, string rel)
        {
            var source = Load<GameObject>($"{PF}/PixelMonsters/{rel}.prefab");
            var clean = MakeCleanVariant(source, $"{PrefabDir}/Monsters/{key}.prefab", MonsterStrip);
            db.monsters.Add(new GameDatabase.NamedPrefab { key = key, prefab = clean });
        }

        private static GameObject MakeCleanVariant(GameObject source, string savePath, string[] stripTypeNames)
        {
            if (source == null) return null;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(source);
            foreach (var typeName in stripTypeNames)
                foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb != null && mb.GetType().Name == typeName)
                        Object.DestroyImmediate(mb);

            var saved = PrefabUtility.SaveAsPrefabAsset(inst, savePath);
            Object.DestroyImmediate(inst);
            return saved;
        }

        private static void AddSprite(GameDatabase db, string key, string file)
        {
            var sprite = LoadSprite($"{PF}/PixelTileEngine/Tiles/Props/{file}.png");
            db.sprites.Add(new GameDatabase.NamedSprite { key = key, sprite = sprite });
        }

        private static void AddTheme(GameDatabase db, string name, string ground)
        {
            string path = $"{PF}/PixelTileEngine/Tiles/Ground/{ground}.png";
            var atlas = GameUtil.LoadPng(Path.GetFullPath(path));
            if (atlas == null) { Debug.LogWarning($"[PixelDungeon] Missing ground atlas: {path}"); return; }

            var floor = GameUtil.ExtractTile(atlas, false);
            var wall = GameUtil.ExtractTile(atlas, true);
            floor.name = name + "_floor"; floor.texture.name = name + "_floorTex";
            wall.name = name + "_wall"; wall.texture.name = name + "_wallTex";

            AssetDatabase.AddObjectToAsset(floor.texture, db);
            AssetDatabase.AddObjectToAsset(floor, db);
            AssetDatabase.AddObjectToAsset(wall.texture, db);
            AssetDatabase.AddObjectToAsset(wall, db);

            db.themes.Add(new GameDatabase.Theme { name = name, floor = floor, wall = wall });
        }

        // ---------------- Scene ----------------

        private static void BuildScene(string scenePath)
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var dl = GameObject.Find("Directional Light");
            if (dl != null) Object.DestroyImmediate(dl);

            var cam = Camera.main;
            if (cam == null)
            {
                var g = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = g.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 6.2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.06f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.gameObject.AddComponent<CameraFollow>();

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            new GameObject("Systems").AddComponent<Bootstrap>();

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        /// <summary>Adds the scene to Build Settings if absent — never reorders or removes the
        /// host project's existing scenes.</summary>
        private static void AppendSceneToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Any(s => s.path == scenePath))
            {
                foreach (var s in scenes) if (s.path == scenePath) s.enabled = true;
            }
            else
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------------- Helpers ----------------

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static T Load<T>(string path) where T : Object
        {
            var o = AssetDatabase.LoadAssetAtPath<T>(path);
            if (o == null) Debug.LogWarning($"[PixelDungeon] Missing asset: {path}");
            return o;
        }

        private static Sprite LoadSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
            }

            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) return s;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Sprite sp) return sp;

            Debug.LogWarning($"[PixelDungeon] Missing sprite: {path}");
            return null;
        }
    }
}
#endif
