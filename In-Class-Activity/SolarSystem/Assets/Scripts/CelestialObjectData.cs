using UnityEngine;

/// <summary>
/// Attach to Sun, planets, or moons. Supplies a short kid-friendly fact when the object is selected.
/// </summary>
public class CelestialObjectData : MonoBehaviour
{
    [Tooltip("Name shown in the info panel when this body is selected")]
    public string displayName = "Celestial body";

    [Tooltip("Short, simple English for children")]
    [TextArea(3, 10)]
    public string kidFriendlyFact = "";

    [Tooltip("Optional short sound played on select")]
    public AudioClip clickSound;
}
