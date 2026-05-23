using UnityEngine;

namespace EchoesOfCelestia.Plane2D
{
    /// <summary>
    /// 飞机大战相机改为俯视紧贴玩家（CameraStyles.Overhead），避免预制体默认 Free/Locked 导致镜头不居中。
    /// 需在 CameraController.Update 之前运行。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlaneSceneCameraFollow : MonoBehaviour
    {
        CameraController _cameraController;
        Transform _player;

        void Update()
        {
            if (_cameraController == null)
            {
                var cam = Camera.main;
                if (cam != null)
                    _cameraController = cam.GetComponent<CameraController>();
            }

            if (_cameraController == null)
                return;

            _cameraController.cameraMovementStyle = CameraController.CameraStyles.Overhead;

            if (_player == null)
                _player = ResolvePlayerTransform();

            if (_player != null)
                _cameraController.target = _player;
        }

        static Transform ResolvePlayerTransform()
        {
            if (GameManager.instance != null && GameManager.instance.player != null)
                return GameManager.instance.player.transform;

            var controller = FindObjectOfType<Controller>();
            return controller != null ? controller.transform : null;
        }
    }
}
