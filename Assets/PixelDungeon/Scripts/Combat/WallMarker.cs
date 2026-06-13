using UnityEngine;

namespace PixelDungeon
{
    /// <summary>Tags an object as solid level geometry so projectiles know to stop on it.
    /// Used instead of physics layers to avoid editing the project's TagManager.</summary>
    public class WallMarker : MonoBehaviour { }
}
