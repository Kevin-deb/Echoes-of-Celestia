using UnityEngine;

namespace EchoesOfCelestia.Plane2D
{
    /// <summary>
    /// Forces the plane-scene camera into a top-down lock on the player
    /// (CameraStyles.Overhead). The prefab ships as Free/Locked, which leaves the
    /// view off-centre. Has to run before CameraController.Update.
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
