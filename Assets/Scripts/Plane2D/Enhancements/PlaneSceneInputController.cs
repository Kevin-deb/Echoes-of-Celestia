using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoesOfCelestia.Plane2D
{
    /// <summary>
    /// ESC：返回 3D Hub；空格：暂停/继续（含 Unpause 按钮）。
    /// 禁用原 UIManager 的 pause InputAction，避免与 ESC 暂停冲突。
    /// </summary>
    public sealed class PlaneSceneInputController : MonoBehaviour
    {
        Canvas _pauseCanvas;
        bool _paused;

        void Start()
        {
            StartCoroutine(DisableBuiltinPauseAfterFirstFrame());
            BuildPauseOverlay();
            if (_pauseCanvas != null)
                _pauseCanvas.gameObject.SetActive(false);
        }

        IEnumerator DisableBuiltinPauseAfterFirstFrame()
        {
            yield return null;
            DisableBuiltinPauseAction();
        }

        void OnDestroy()
        {
            if (_paused)
                Time.timeScale = 1f;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneNames.Hub, LoadSceneMode.Single);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space))
                TogglePause();
        }

        void DisableBuiltinPauseAction()
        {
            var ui = FindObjectOfType<UIManager>();
            if (ui == null || ui.pauseAction == null) return;
            ui.pauseAction.Disable();
        }

        void TogglePause()
        {
            if (GameManager.instance != null && GameManager.instance.gameIsOver)
                return;

            if (_paused)
                Resume();
            else
                Pause();
        }

        void Pause()
        {
            _paused = true;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (_pauseCanvas != null)
                _pauseCanvas.gameObject.SetActive(true);
        }

        void Resume()
        {
            _paused = false;
            Time.timeScale = 1f;
            if (_pauseCanvas != null)
                _pauseCanvas.gameObject.SetActive(false);
        }

        void BuildPauseOverlay()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var canvasGo = new GameObject("PlanePauseOverlay");
            canvasGo.transform.SetParent(transform, false);
            _pauseCanvas = canvasGo.AddComponent<Canvas>();
            _pauseCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _pauseCanvas.sortingOrder = 32766;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            var dim = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dim.transform.SetParent(canvasGo.transform, false);
            var dimRt = dim.GetComponent<RectTransform>();
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero;
            dimRt.offsetMax = Vector2.zero;
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            titleGo.transform.SetParent(canvasGo.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 0.62f);
            titleRt.sizeDelta = new Vector2(600f, 80f);
            var title = titleGo.GetComponent<Text>();
            title.font = font;
            title.fontSize = 42;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Color.white;
            title.text = "PAUSED";

            var hintGo = new GameObject("Hint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            hintGo.transform.SetParent(canvasGo.transform, false);
            var hintRt = hintGo.GetComponent<RectTransform>();
            hintRt.anchorMin = hintRt.anchorMax = new Vector2(0.5f, 0.52f);
            hintRt.sizeDelta = new Vector2(720f, 48f);
            var hint = hintGo.GetComponent<Text>();
            hint.font = font;
            hint.fontSize = 22;
            hint.alignment = TextAnchor.MiddleCenter;
            hint.color = new Color(1f, 1f, 1f, 0.85f);
            hint.text = "Press Space to resume / Esc to exit to Hub";

            var btnGo = new GameObject("Btn_Unpause", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(canvasGo.transform, false);
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 0.38f);
            btnRt.sizeDelta = new Vector2(280f, 56f);
            var btnImg = btnGo.GetComponent<Image>();
            btnImg.color = new Color(0.22f, 0.72f, 0.38f, 0.98f);
            var btn = btnGo.GetComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(Resume);

            var btnTextGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            btnTextGo.transform.SetParent(btnGo.transform, false);
            Stretch(btnTextGo.GetComponent<RectTransform>());
            var btnText = btnTextGo.GetComponent<Text>();
            btnText.font = font;
            btnText.fontSize = 26;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;
            btnText.text = "Unpause";
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
