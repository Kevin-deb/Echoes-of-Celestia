using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PixelDungeon.Integration
{
    /// <summary>
    /// Hub 集成:在 Hub 场景加载时自动给 P_Base_ComStation_A 装上交互触发器,
    /// 玩家走近后按 F 进入 Pixel Dungeon 小游戏。
    /// 与 HubPlaneShooterSpaceStationPortal / PlaneGameBootstrap 相同的运行时注入模式 ——
    /// 不修改 Hub 场景文件和任何现有脚本。
    /// </summary>
    public static class HubPixelDungeonPortal
    {
        public const string HubSceneName = "Hub";
        public const string StationObjectName = "P_Base_ComStation_A";
        public const string PixelDungeonSceneName = "PixelDungeon";
        public const string PortalObjectName = "PixelDungeonPortal";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryInstall(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryInstall(scene);

        static void TryInstall(Scene scene)
        {
            if (!scene.IsValid() || scene.name != HubSceneName) return;
            if (Object.FindObjectOfType<PixelDungeonStationInteractable>() != null) return;

            var runner = new GameObject("HubPixelDungeonPortalInstaller");
            runner.AddComponent<PixelDungeonPortalInstaller>();
        }
    }

    /// <summary>Waits for the Hub hierarchy to be ready, then attaches the interactable
    /// to the com-station. Retries a few frames in case of delayed activation.</summary>
    public sealed class PixelDungeonPortalInstaller : MonoBehaviour
    {
        IEnumerator Start()
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var station = GameObject.Find(HubPixelDungeonPortal.StationObjectName);
                if (station != null)
                {
                    Install(station);
                    Destroy(gameObject);
                    yield break;
                }
                yield return null;
            }
            Debug.LogWarning($"[PixelDungeon] '{HubPixelDungeonPortal.StationObjectName}' not found in Hub — portal not installed.");
            Destroy(gameObject);
        }

        static void Install(GameObject station)
        {
            if (station.GetComponentInChildren<PixelDungeonStationInteractable>(true) != null) return;

            var portal = new GameObject(HubPixelDungeonPortal.PortalObjectName);
            portal.transform.SetParent(station.transform, false);
            portal.transform.localPosition = Vector3.zero;

            // 与飞机大战门相同的触发器手感(世界尺寸,不随站台缩放)。
            var trigger = portal.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            var scale = station.transform.lossyScale;
            trigger.center = new Vector3(0f, SafeDiv(1.5f, scale.y), 0f);
            trigger.size = new Vector3(SafeDiv(6.5f, scale.x), SafeDiv(3.2f, scale.y), SafeDiv(6.5f, scale.z));

            portal.AddComponent<PixelDungeonStationInteractable>();
        }

        static float SafeDiv(float v, float s) => Mathf.Abs(s) < 0.0001f ? v : v / s;
    }

    /// <summary>
    /// 玩家进入触发器后显示「Press F to enter Pixel Dungeon」提示,按 F 加载小游戏场景。
    /// 提示 UI 自带(运行时创建),不依赖场景里已有的 UI 对象。
    /// </summary>
    public sealed class PixelDungeonStationInteractable : MonoBehaviour
    {
        bool _playerInside;
        GameObject _promptRoot;
        Text _promptText;

        void Start() => BuildPrompt();

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInside = true;
            RefreshPrompt();
            if (_promptRoot != null) _promptRoot.SetActive(true);
        }

        void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInside = false;
            if (_promptRoot != null) _promptRoot.SetActive(false);
        }

        void Update()
        {
            if (!_playerInside) return;
            if (!Input.GetKeyDown(KeyCode.F)) return;
            if (!Application.CanStreamedLevelBeLoaded(HubPixelDungeonPortal.PixelDungeonSceneName))
            {
                Debug.LogWarning("[PixelDungeon] Scene 'PixelDungeon' is not in Build Settings — run Tools ▸ Pixel Dungeon ▸ Build Game Assets.");
                return;
            }
            Time.timeScale = 1f;
            SceneManager.LoadScene(HubPixelDungeonPortal.PixelDungeonSceneName, LoadSceneMode.Single);
        }

        void OnDestroy()
        {
            if (_promptRoot != null) Destroy(_promptRoot);
        }

        void RefreshPrompt()
        {
            if (_promptText == null) return;
            _promptText.text = Application.CanStreamedLevelBeLoaded(HubPixelDungeonPortal.PixelDungeonSceneName)
                ? "Press F to enter Pixel Dungeon"
                : "Pixel Dungeon is not built yet";
        }

        void BuildPrompt()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _promptRoot = new GameObject("PixelDungeonPrompt");
            var canvas = _promptRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32750;
            var scaler = _promptRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGo.transform.SetParent(_promptRoot.transform, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0f);
            bgRt.pivot = new Vector2(0.5f, 0f);
            bgRt.anchoredPosition = new Vector2(0f, 96f);
            bgRt.sizeDelta = new Vector2(560f, 56f);
            bgGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(bgGo.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            _promptText = textGo.GetComponent<Text>();
            _promptText.font = font;
            _promptText.fontSize = 26;
            _promptText.alignment = TextAnchor.MiddleCenter;
            _promptText.color = Color.white;
            _promptText.text = "Press F to enter Pixel Dungeon";

            _promptRoot.SetActive(false);
        }
    }
}
