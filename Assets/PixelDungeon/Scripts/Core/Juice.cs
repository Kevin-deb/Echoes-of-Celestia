using System.Collections;
using UnityEngine;

namespace PixelDungeon
{
    /// <summary>
    /// "Feel" layer (design doc 3.4 手感三件套): screen shake, hit-stop, and a quick
    /// camera-anchored flash. A lightweight singleton created by Bootstrap.
    /// </summary>
    public class Juice : MonoBehaviour
    {
        public static Juice Instance { get; private set; }

        /// <summary>Set while the game is intentionally paused (pause menu), so a finishing
        /// hit-stop never restores the time scale out from under the pause.</summary>
        public static bool ExternalPause;

        private Transform _camT;
        private Vector3 _camBase;
        private float _shakeAmt, _shakeDecay;
        private Coroutine _hitStop;

        public void Init(Camera cam)
        {
            Instance = this;
            _camT = cam.transform;
        }

        public static void Shake(float amount = 0.15f, float decay = 4f)
        {
            if (Instance == null) return;
            Instance._shakeAmt = Mathf.Max(Instance._shakeAmt, amount);
            Instance._shakeDecay = decay;
        }

        /// <summary>Freeze-frame on impact for crunchy hits (design doc: hit-stop 0.05s).</summary>
        public static void HitStop(float seconds = 0.05f)
        {
            if (Instance == null) return;
            if (Instance._hitStop != null) Instance.StopCoroutine(Instance._hitStop);
            Instance._hitStop = Instance.StartCoroutine(Instance.HitStopRoutine(seconds));
        }

        private IEnumerator HitStopRoutine(float seconds)
        {
            float prev = Time.timeScale;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(seconds);
            // Don't override an intentional pause (pause menu / timeScale set to 0 elsewhere).
            if (Time.timeScale == 0f && !ExternalPause) Time.timeScale = prev == 0f ? 1f : prev;
            _hitStop = null;
        }

        private void LateUpdate()
        {
            if (_camT == null) return;
            if (_shakeAmt > 0.0001f)
            {
                Vector2 r = Random.insideUnitCircle * _shakeAmt;
                _camT.localPosition = new Vector3(_camBase.x + r.x, _camBase.y + r.y, _camT.localPosition.z);
                _shakeAmt = Mathf.MoveTowards(_shakeAmt, 0f, _shakeDecay * Time.unscaledDeltaTime);
            }
        }

        /// <summary>The follow camera reports its base (un-shaken) position here each frame.</summary>
        public void SetCameraBase(Vector3 basePos) => _camBase = basePos;
    }
}
