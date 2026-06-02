using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Hub 场景的 Klee 模型运行时适配。
/// 优先用 Hi3D 导出的 GLB 数据生成显示网格，保证 Mesh UV 与贴图来自同一个文件。
/// </summary>
public sealed class HubKleeModelRuntimeFit : MonoBehaviour
{
    const string HubSceneName = "Hub";
    const string PlayerTag = "Player";
    const string KleeModelName = "KleeModel";
    const string KleeGlbVisualName = "KleeGlbVisual";
    const float GlbVisualYawOffset = 195f;

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
        if (FindObjectOfType<HubKleeModelRuntimeFit>() != null) return;

        var runner = new GameObject("HubKleeModelRuntimeFit");
        runner.AddComponent<HubKleeModelRuntimeFit>();
    }

    IEnumerator Start()
    {
        yield return null;
        FitKleeModel();
    }

    void FitKleeModel()
    {
        if (SceneManager.GetActiveScene().name != HubSceneName)
        {
            Destroy(gameObject);
            return;
        }

        var player = GameObject.FindGameObjectWithTag(PlayerTag);
        if (player == null) return;

        var model = FindModelRoot(player.transform);
        if (model == null) return;

        foreach (var collider in model.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        if (TryBuildGlbVisual(model))
            DisableImportedFbxRenderers(model);

        var renderers = GetVisibleRenderers(model);
        if (renderers.Length == 0) return;

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        var controller = player.GetComponent<CharacterController>();
        var targetHeight = controller != null ? controller.height * 0.8f : 1.6f;
        if (bounds.size.y > 0.0001f)
        {
            var scaleFactor = targetHeight / bounds.size.y;
            model.localScale *= scaleFactor;
        }

        renderers = GetVisibleRenderers(model);
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        var groundY = GetControllerBottomY(player.transform, controller);
        model.position += Vector3.up * (groundY - bounds.min.y);

        var motion = model.GetComponent<KleeProceduralMotion>();
        if (motion == null)
            motion = model.gameObject.AddComponent<KleeProceduralMotion>();
        motion.RebindBaseTransform(player.transform);
    }

    static Transform FindModelRoot(Transform player)
    {
        var named = player.Find(KleeModelName);
        if (named != null)
            return named;

        foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.transform == player) continue;
            var root = renderer.transform;
            while (root.parent != null && root.parent != player)
                root = root.parent;
            return root;
        }

        return null;
    }

    static Renderer[] GetVisibleRenderers(Transform model)
    {
        var result = new List<Renderer>();
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.enabled)
                result.Add(renderer);
        }

        return result.ToArray();
    }

    static float GetControllerBottomY(Transform player, CharacterController controller)
    {
        if (controller == null)
            return player.position.y;

        return player.position.y + controller.center.y - controller.height * 0.5f + controller.skinWidth;
    }

    static bool TryBuildGlbVisual(Transform model)
    {
        var old = model.Find(KleeGlbVisualName);
        if (old != null && old.GetComponentInChildren<MeshFilter>() != null && old.GetComponentInChildren<MeshRenderer>() != null)
            return true;

        var glbAsset = Resources.Load<TextAsset>("Klee/klee_glb");
        var baseColor = Resources.Load<Texture2D>("Klee/klee_basecolor");
        if (glbAsset == null || baseColor == null) return false;

        if (!TryCreateMeshFromGlb(glbAsset.bytes, out var mesh)) return false;

        if (old != null)
            Destroy(old.gameObject);

        var visual = new GameObject(KleeGlbVisualName);
        visual.transform.SetParent(model, false);
        visual.transform.localRotation = Quaternion.Euler(0f, GlbVisualYawOffset, 0f);

        var meshFilter = visual.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        var meshRenderer = visual.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = CreateKleeMaterial(baseColor);
        return true;
    }

    static void DisableImportedFbxRenderers(Transform model)
    {
        var glbVisual = model.Find(KleeGlbVisualName);
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (glbVisual != null && renderer.transform.IsChildOf(glbVisual)) continue;
            renderer.enabled = false;
        }
    }

    static Material CreateKleeMaterial(Texture2D baseColor)
    {
        var shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Unlit/Texture");

        var material = new Material(shader)
        {
            name = "Klee_GLB_BaseColor"
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
            material.SetFloat("_Glossiness", 0.25f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.25f);

        // 月球场景整体偏暗，添加少量自发光让角色色彩更清晰。
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.28f, 0.26f, 0.22f, 1f));
        }

        return material;
    }

    static bool TryCreateMeshFromGlb(byte[] glb, out Mesh mesh)
    {
        mesh = null;
        if (glb == null || glb.Length < 20) return false;
        if (Encoding.ASCII.GetString(glb, 0, 4) != "glTF") return false;

        var offset = 12;
        string json = null;
        byte[] binary = null;

        while (offset + 8 <= glb.Length)
        {
            var chunkLength = BitConverter.ToInt32(glb, offset);
            var chunkType = BitConverter.ToUInt32(glb, offset + 4);
            offset += 8;

            if (offset + chunkLength > glb.Length) return false;

            if (chunkType == 0x4E4F534A)
                json = Encoding.UTF8.GetString(glb, offset, chunkLength).TrimEnd('\0', ' ', '\n', '\r', '\t');
            else if (chunkType == 0x004E4942)
            {
                binary = new byte[chunkLength];
                Buffer.BlockCopy(glb, offset, binary, 0, chunkLength);
            }

            offset += chunkLength;
        }

        if (string.IsNullOrEmpty(json) || binary == null) return false;

        var root = JsonUtility.FromJson<GltfRoot>(json);
        if (root == null || root.meshes == null || root.meshes.Length == 0) return false;

        var primitive = root.meshes[0].primitives[0];
        var positions = ReadVector3Accessor(root, binary, primitive.attributes.POSITION, true);
        var normals = ReadVector3Accessor(root, binary, primitive.attributes.NORMAL, true);
        var uvs = ReadVector2Accessor(root, binary, primitive.attributes.TEXCOORD_0, true);
        var triangles = ReadIndexAccessor(root, binary, primitive.indices);

        if (positions == null || positions.Length == 0 || triangles == null || triangles.Length == 0)
            return false;

        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            var tmp = triangles[i + 1];
            triangles[i + 1] = triangles[i + 2];
            triangles[i + 2] = tmp;
        }

        mesh = new Mesh
        {
            name = "Klee_GLB_Mesh",
            indexFormat = positions.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
        };
        mesh.vertices = positions;
        if (normals != null && normals.Length == positions.Length)
            mesh.normals = normals;
        if (uvs != null && uvs.Length == positions.Length)
            mesh.uv = uvs;
        mesh.triangles = triangles;
        if (normals == null || normals.Length != positions.Length)
            mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return true;
    }

    static Vector3[] ReadVector3Accessor(GltfRoot root, byte[] binary, int accessorIndex, bool convertCoordinates)
    {
        if (!TryGetAccessor(root, accessorIndex, out var accessor, out var bufferView)) return null;
        var result = new Vector3[accessor.count];
        var stride = bufferView.byteStride > 0 ? bufferView.byteStride : 12;
        var start = bufferView.byteOffset + accessor.byteOffset;

        for (int i = 0; i < accessor.count; i++)
        {
            var p = start + i * stride;
            var v = new Vector3(ReadFloat(binary, p), ReadFloat(binary, p + 4), ReadFloat(binary, p + 8));
            result[i] = convertCoordinates ? new Vector3(v.x, v.y, -v.z) : v;
        }

        return result;
    }

    static Vector2[] ReadVector2Accessor(GltfRoot root, byte[] binary, int accessorIndex, bool flipV)
    {
        if (!TryGetAccessor(root, accessorIndex, out var accessor, out var bufferView)) return null;
        var result = new Vector2[accessor.count];
        var stride = bufferView.byteStride > 0 ? bufferView.byteStride : 8;
        var start = bufferView.byteOffset + accessor.byteOffset;

        for (int i = 0; i < accessor.count; i++)
        {
            var p = start + i * stride;
            var u = ReadFloat(binary, p);
            var v = ReadFloat(binary, p + 4);
            result[i] = new Vector2(u, flipV ? 1f - v : v);
        }

        return result;
    }

    static int[] ReadIndexAccessor(GltfRoot root, byte[] binary, int accessorIndex)
    {
        if (!TryGetAccessor(root, accessorIndex, out var accessor, out var bufferView)) return null;
        var result = new int[accessor.count];
        var componentSize = accessor.componentType == 5125 ? 4 : 2;
        var stride = bufferView.byteStride > 0 ? bufferView.byteStride : componentSize;
        var start = bufferView.byteOffset + accessor.byteOffset;

        for (int i = 0; i < accessor.count; i++)
        {
            var p = start + i * stride;
            result[i] = accessor.componentType == 5125 ? (int)BitConverter.ToUInt32(binary, p) : BitConverter.ToUInt16(binary, p);
        }

        return result;
    }

    static bool TryGetAccessor(GltfRoot root, int accessorIndex, out GltfAccessor accessor, out GltfBufferView bufferView)
    {
        accessor = null;
        bufferView = null;
        if (root.accessors == null || accessorIndex < 0 || accessorIndex >= root.accessors.Length) return false;

        accessor = root.accessors[accessorIndex];
        if (root.bufferViews == null || accessor.bufferView < 0 || accessor.bufferView >= root.bufferViews.Length) return false;

        bufferView = root.bufferViews[accessor.bufferView];
        return true;
    }

    static float ReadFloat(byte[] data, int offset)
    {
        return BitConverter.ToSingle(data, offset);
    }

    [Serializable]
    sealed class GltfRoot
    {
        public GltfBufferView[] bufferViews;
        public GltfAccessor[] accessors;
        public GltfMesh[] meshes;
    }

    [Serializable]
    sealed class GltfBufferView
    {
        public int buffer;
        public int byteOffset;
        public int byteLength;
        public int byteStride;
    }

    [Serializable]
    sealed class GltfAccessor
    {
        public int bufferView;
        public int byteOffset;
        public int componentType;
        public int count;
        public string type;
    }

    [Serializable]
    sealed class GltfMesh
    {
        public GltfPrimitive[] primitives;
    }

    [Serializable]
    sealed class GltfPrimitive
    {
        public GltfAttributes attributes;
        public int indices;
        public int material;
    }

    [Serializable]
    sealed class GltfAttributes
    {
        public int POSITION;
        public int NORMAL;
        public int TEXCOORD_0;
    }
}
