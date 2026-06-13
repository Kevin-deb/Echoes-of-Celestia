using UnityEngine;

namespace PixelDungeon
{
    /// <summary>Smooth top-down camera follow that cooperates with Juice's screen shake by reporting
    /// its un-shaken base position each frame. Keeps a fixed Z so the orthographic camera never drifts
    /// onto the sprite plane (which would clip everything to black).</summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform Target;
        public float Smooth = 8f;

        private Vector3 _base;
        private float _z = -10f;

        private void Awake()
        {
            _z = transform.position.z;
            _base = transform.position;
        }

        private void LateUpdate()
        {
            if (Target != null)
            {
                Vector3 goal = new(Target.position.x, Target.position.y + 0.5f, _z);
                _base = Vector3.Lerp(_base, goal, 1f - Mathf.Exp(-Smooth * Time.unscaledDeltaTime));
            }
            _base.z = _z;
            transform.position = _base;
            if (Juice.Instance != null) Juice.Instance.SetCameraBase(_base);
        }

        public void Snap(Vector3 pos)
        {
            _base = new Vector3(pos.x, pos.y + 0.5f, _z);
            transform.position = _base;
        }
    }
}
