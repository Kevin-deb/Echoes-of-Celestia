using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 将 Hub 中进入飞机大战的门替换为空间站模型。
/// 只替换视觉表现，保留原 Trigger/HubDoorInteractable 的 F 键进入逻辑。
/// </summary>
public sealed class HubPlaneShooterSpaceStationPortal : MonoBehaviour
{
    const string HubSceneName = "Hub";
    const string DoorName = "Door_PlaneShooter";
    const string VisualName = "SpaceStationPortalVisual";
    static readonly Vector3 InteractionTriggerSize = new Vector3(6.5f, 3.2f, 6.5f);

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
        if (FindObjectOfType<HubPlaneShooterSpaceStationPortal>() != null) return;

        var runner = new GameObject("HubPlaneShooterSpaceStationPortal");
        runner.AddComponent<HubPlaneShooterSpaceStationPortal>();
    }

    IEnumerator Start()
    {
        yield return null;
        ReplacePlaneShooterDoorVisual();
    }

    void ReplacePlaneShooterDoorVisual()
    {
        var door = GameObject.Find(DoorName);
        if (door == null) return;

        HideOldDoorVisuals(door.transform);

        var existing = door.transform.Find(VisualName);
        if (existing != null && existing.GetComponentInChildren<MeshFilter>() != null && existing.GetComponentInChildren<MeshRenderer>() != null)
        {
            ExpandInteractionTrigger(door.transform);
            return;
        }

        if (existing != null)
            Destroy(existing.gameObject);

        var glbAsset = Resources.Load<TextAsset>("SpaceStation/space_station_glb");
        var baseColor = Resources.Load<Texture2D>("SpaceStation/space_station_basecolor");
        if (glbAsset == null || baseColor == null) return;
        if (!RuntimeGlbMeshBuilder.TryCreateMesh(glbAsset.bytes, "SpaceStationPortalMesh", out var mesh)) return;

        var visual = new GameObject(VisualName);
        visual.transform.SetParent(door.transform, false);
        visual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        var meshFilter = visual.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        var meshRenderer = visual.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = CreateMaterial(baseColor);

        AddBlockingCollider(visual, mesh.bounds);

        FitVisualToPortal(visual.transform, mesh.bounds);
        ExpandInteractionTrigger(door.transform);
    }

    static void HideOldDoorVisuals(Transform door)
    {
        foreach (var renderer in door.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.transform.name == VisualName) continue;
            renderer.enabled = false;
        }

        foreach (var collider in door.GetComponentsInChildren<Collider>(true))
        {
            if (collider.isTrigger) continue;
            collider.enabled = false;
        }
    }

    static Material CreateMaterial(Texture2D baseColor)
    {
        var shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Unlit/Texture");

        var material = new Material(shader)
        {
            name = "SpaceStationPortal_BaseColor"
        };
        material.mainTexture = baseColor;

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", baseColor);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", 0.35f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.35f);

        return material;
    }

    static void FitVisualToPortal(Transform visual, Bounds meshBounds)
    {
        var maxSize = Mathf.Max(meshBounds.size.x, meshBounds.size.y, meshBounds.size.z);
        if (maxSize <= 0.0001f) return;

        var scale = 5.6f / maxSize;
        visual.localScale = Vector3.one * scale;

        var localCenterOffset = -meshBounds.center * scale;
        var localBottomOffset = -meshBounds.min.y * scale;
        visual.localPosition = new Vector3(localCenterOffset.x, localBottomOffset + 0.08f, localCenterOffset.z);
    }

    static void AddBlockingCollider(GameObject visual, Bounds meshBounds)
    {
        var collider = visual.AddComponent<BoxCollider>();
        collider.isTrigger = false;
        collider.center = meshBounds.center;
        collider.size = meshBounds.size;
    }

    static void ExpandInteractionTrigger(Transform door)
    {
        var interactable = door.GetComponentInChildren<HubDoorInteractable>(true);
        if (interactable == null) return;

        var trigger = interactable.GetComponent<BoxCollider>();
        if (trigger == null) return;

        trigger.isTrigger = true;
        trigger.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        trigger.size = InteractionTriggerSize;
    }
}
