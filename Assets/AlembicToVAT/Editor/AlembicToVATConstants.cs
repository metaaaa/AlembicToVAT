using UnityEngine;

namespace AlembicToVAT
{
    public enum MaxTextureWidth
    {
        w32 = 32,
        w64 = 64,
        w128 = 128,
        w256 = 256,
        w512 = 512,
        w1024 = 1024,
        w2048 = 2048,
        w4096 = 4096,
        w8192 = 8192
    }

    public enum TopologyType
    {
        Soft,
        Liquid
    }

    public struct VertInfo
    {
        public Vector3 position;
        public Vector3 normal;
    }

    public struct MeshPartInfo
    {
        public MeshFilter meshFilter;
        public Transform parentTrans;
    }

    public class ConvertResult
    {
        public readonly Texture mainTex;
        public readonly Mesh mesh;
        public readonly Texture2D normTex;
        public readonly Texture2D posTex;
        public readonly TopologyType topologyType;

        public ConvertResult(Texture2D posTex, Texture2D normTex,
            Texture mainTex, Mesh mesh, TopologyType topologyType)
        {
            this.posTex = posTex;
            this.normTex = normTex;
            this.mainTex = mainTex;
            this.mesh = mesh;
            this.topologyType = topologyType;
        }
    }
}