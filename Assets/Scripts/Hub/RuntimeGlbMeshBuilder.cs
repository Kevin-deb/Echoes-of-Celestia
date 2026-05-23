using System;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 轻量 GLB 运行时 Mesh 读取器。
/// 仅覆盖当前 Hi3D 导出的简单 GLB：单 buffer、POSITION/NORMAL/TEXCOORD_0/indices。
/// </summary>
public static class RuntimeGlbMeshBuilder
{
    public static bool TryCreateMesh(byte[] glb, string meshName, out Mesh mesh)
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
        if (root?.meshes == null || root.meshes.Length == 0) return false;
        if (root.meshes[0].primitives == null || root.meshes[0].primitives.Length == 0) return false;

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
            name = meshName,
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

    static float ReadFloat(byte[] data, int offset) => BitConverter.ToSingle(data, offset);

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
        public int byteOffset;
        public int byteStride;
    }

    [Serializable]
    sealed class GltfAccessor
    {
        public int bufferView;
        public int byteOffset;
        public int componentType;
        public int count;
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
    }

    [Serializable]
    sealed class GltfAttributes
    {
        public int POSITION;
        public int NORMAL;
        public int TEXCOORD_0;
    }
}
