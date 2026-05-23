using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace EchoesOfCelestia.Plane2D
{
    /// <summary>
    /// 敌人死亡时在死亡位置上方弹出 "+score" 浮字，向上飘并淡出。
    /// 通过专属世界空间 Canvas 渲染，避免与原游戏 UI 冲突。
    /// </summary>
    public sealed class ScorePopupWatcher : MonoBehaviour
    {
        [SerializeField] float lifetime = 0.85f;
        [SerializeField] float floatUpDistance = 1.6f;
        [SerializeField] int fontSize = 36;
        [SerializeField] Color color = new Color(1f, 0.92f, 0.55f, 1f);

        Canvas _canvas;
        Font _font;

        void Awake()
        {
            var canvasGo = new GameObject("ScorePopupCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 100;
            canvasGo.AddComponent<GraphicRaycaster>();
            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(2f, 1f);
            canvasGo.transform.localScale = Vector3.one * 0.04f;

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        void OnEnable() => PlaneGameEvents.EnemyKilled += OnEnemyKilled;
        void OnDisable() => PlaneGameEvents.EnemyKilled -= OnEnemyKilled;

        void OnEnemyKilled(Vector3 position, int score)
        {
            if (score <= 0) return;
            var go = new GameObject("Popup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(_canvas.transform, false);
            go.transform.position = position + Vector3.up * 0.4f;

            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontStyle = FontStyle.Bold;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = color;
            text.text = "+" + score;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120f, 40f);

            StartCoroutine(Animate(go.transform, text));
        }

        IEnumerator Animate(Transform t, Text text)
        {
            var startPos = t.position;
            var elapsed = 0f;
            while (elapsed < lifetime && t != null)
            {
                elapsed += Time.deltaTime;
                var k = elapsed / lifetime;
                if (t == null) yield break;
                t.position = startPos + Vector3.up * (k * floatUpDistance);
                if (text != null)
                {
                    var c = text.color;
                    c.a = 1f - k;
                    text.color = c;
                }
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }
    }
}
