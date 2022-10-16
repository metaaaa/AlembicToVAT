using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Formats.Alembic.Importer;

public class AlembicToVAT : EditorWindow
{
    public AlembicStreamPlayer alembic = null;
    public int samplingRate = 20;
    public float adjugstTime = -0.04166667f;
    public ComputeShader infoTexGen;
    public string folderName = "__WorkSpace/BakedAlembicAnimationTex";
    public Shader playShader;

    public enum TopologyType
    {
        Soft,
        Liquid,
    }

    public struct VertInfo
    {
        public Vector3 position;
        public Vector3 normal;
    }

    private TopologyType _topologyType = TopologyType.Soft;
    private int _maxTriangleCount = 0;
    private int _minTriangleCount = 10000000;
    private MeshFilter[] _meshFilters = null;
    private Mesh _mesh = null;
    private string _folderPath = "";
    private string _subFolderPath = "";

    private float _startTime = 0f;

    private readonly int[] _textureSize = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };
    private const int MaxTextureSize = 8192;

    [MenuItem("Custom/AlembicToVAT")]
    static void Create()
    {
        // 生成
        GetWindow<AlembicToVAT>("AlembicToVAT");
    }

    private void OnGUI()
    {
        try
        {
            alembic = (AlembicStreamPlayer)EditorGUILayout.ObjectField("alembic", alembic, typeof(AlembicStreamPlayer), true);
            samplingRate = EditorGUILayout.IntField("samplingRate", samplingRate);
            adjugstTime = EditorGUILayout.FloatField("adjugstTime", adjugstTime);
            infoTexGen = (ComputeShader)EditorGUILayout.ObjectField("infoTexGen", infoTexGen, typeof(ComputeShader), true);
            folderName = EditorGUILayout.TextField("folderName", folderName);
            playShader = (Shader)EditorGUILayout.ObjectField("playShader", playShader, typeof(Shader), true);
            if (GUILayout.Button("process")) Make();
        }
        catch (System.FormatException) { }
    }

    private void Make()
    {
        // validate
        if (!InputValidate()) return;

        _startTime = alembic.StartTime + adjugstTime;

        // check VAT Type
        _topologyType = GetVATType();

        // bake mesh
        _mesh = BakeMesh();

        // bake texture
        var texTuple = BakeTextures();

        // create assets
        SaveAssets(texTuple.posTex, texTuple.normTex);

        // 初期状態に戻す
        alembic.UpdateImmediately(_startTime);
    }

    private bool InputValidate()
    {
        return true;
    }

    private TopologyType GetVATType()
    {
        _maxTriangleCount = 0;
        _minTriangleCount = 10000000;

        int frameCount = Mathf.NextPowerOfTwo((int)(alembic.Duration * samplingRate));
        var dt = alembic.Duration / frameCount;

        _meshFilters = alembic.gameObject.GetComponentsInChildren<MeshFilter>();

        for (var frame = 0; frame < frameCount; frame++)
        {
            alembic.UpdateImmediately(_startTime + dt * frame);

            int triangleCount = 0;
            foreach (var meshFilter in _meshFilters)
            {
                triangleCount += meshFilter.sharedMesh.triangles.Length / 3;
            }
            if (triangleCount > _maxTriangleCount)
                _maxTriangleCount = triangleCount;
            if (triangleCount < _minTriangleCount)
                _minTriangleCount = triangleCount;
        }
        var type = _maxTriangleCount == _minTriangleCount ? TopologyType.Soft : TopologyType.Liquid;
        // 初期状態に戻す
        alembic.UpdateImmediately(_startTime);
        return type;
    }

    private Mesh BakeMesh()
    {
        var bakedMesh = new Mesh();
        bakedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        var verticesCount = 0;
        var trianglesIndexCount = 0;

        bool hasNormal = false;
        bool hasUVs = false;
        bool hasColors = false;

        if (_topologyType == TopologyType.Liquid)
        {
            hasNormal = true;
            verticesCount = _maxTriangleCount * 3;
            trianglesIndexCount = _maxTriangleCount * 3;
        }
        else
        {
            foreach (var meshFilter in _meshFilters)
            {
                var sharedMesh = meshFilter.sharedMesh;
                verticesCount += sharedMesh.vertices.Length;
                trianglesIndexCount += sharedMesh.triangles.Length;

                hasNormal |= (sharedMesh.normals.Length > 0);
                hasColors |= (sharedMesh.colors.Length > 0);
                hasUVs |= (sharedMesh.uv.Length > 0);
            }
        }

        var vertices = new Vector3[verticesCount];
        var uv = new Vector2[verticesCount];
        var normals = new Vector3[verticesCount];
        var colors = new Color[verticesCount];
        var triangles = new int[trianglesIndexCount];

        if (_topologyType == TopologyType.Liquid)
        {
            for (int i = 0; i < verticesCount; i++) // everything is initialized to 0
            {
                triangles[i] = i;
                vertices[i] = Vector3.zero;
                normals[i] = Vector3.up;
            }
        }
        else
        {
            int currentTrianglesIndex = 0;
            int verticesOffset = 0;
            foreach (var meshFilter in _meshFilters)
            {
                float random = UnityEngine.Random.value;
                var sharedMesh = meshFilter.sharedMesh;
                var vertCount = sharedMesh.vertices.Length;
                for (int j = 0; j < vertCount; j++)
                {
                    if (hasUVs)
                        uv[j + verticesOffset] = sharedMesh.uv[j];
                    if (hasColors)
                        colors[j + verticesOffset] = sharedMesh.colors[j];

                    vertices[j + verticesOffset] = sharedMesh.vertices[j];
                }

                var sharedTriangles = sharedMesh.triangles;
                for (int j = 0; j < sharedTriangles.Length; j++)
                {
                    triangles[currentTrianglesIndex++] = sharedTriangles[j] + verticesOffset;
                }

                verticesOffset += vertCount;
            }
        }

        bakedMesh.vertices = vertices;
        if (hasUVs)
            bakedMesh.uv = uv;
        if (hasNormal)
            bakedMesh.normals = normals;
        if (hasColors)
            bakedMesh.colors = colors;
        bakedMesh.triangles = triangles;

        bakedMesh.RecalculateBounds();

        return bakedMesh;
    }

    private int GetMaxVertexCount()
    {
        int maxVertCount = 0;
        if (_topologyType == TopologyType.Liquid)
        {
            maxVertCount = _maxTriangleCount * 3;
        }
        else
        {
            maxVertCount = _meshFilters.Select(x => x.sharedMesh.vertexCount).Sum();
        }

        return maxVertCount;
    }

    private Vector2Int GetTextureSize()
    {
        var size = new Vector2Int();
        int maxVertCount = GetMaxVertexCount();

        var x = Mathf.NextPowerOfTwo(maxVertCount);
        x = x > MaxTextureSize ? MaxTextureSize : x;
        var y = Mathf.NextPowerOfTwo((int)(alembic.Duration * samplingRate) * ((int)((maxVertCount - 1) / MaxTextureSize) + 1));
        size.x = x;
        size.y = y;
        if (y > MaxTextureSize)
        {
            Debug.LogError("data size over");
        }
        return size;
    }

    private (Texture2D posTex, Texture2D normTex) BakeTextures()
    {
        var maxVertCount = GetMaxVertexCount();
        var frames = Mathf.NextPowerOfTwo((int)(alembic.Duration * samplingRate));
        var texSize = GetTextureSize();
        var dt = alembic.Duration / frames;

        var pRt = new RenderTexture(texSize.x, texSize.y, 0, RenderTextureFormat.ARGBHalf);
        pRt.name = string.Format("{0}.posTex", alembic.gameObject.name);
        var nRt = new RenderTexture(texSize.x, texSize.y, 0, RenderTextureFormat.ARGBHalf);
        nRt.name = string.Format("{0}.normTex", alembic.gameObject.name);
        foreach (var rt in new[] { pRt, nRt })
        {
            rt.enableRandomWrite = true;
            rt.Create();
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.clear);
        }

        var infoList = new List<VertInfo>(texSize.y * maxVertCount);
        float progress = 0f;
        for (var frame = 0; frame < frames; frame++)
        {
            progress = (float)frame / (float)frames;
            string progressText = ((frame % 2) == 0) ? "処理中 ₍₍(ง˘ω˘)ว⁾⁾" : "処理中 ₍₍(ว˘ω˘)ง⁾⁾";
            bool isCancel = EditorUtility.DisplayCancelableProgressBar("AlembicToVAT", progressText, progress);
            alembic.UpdateImmediately(_startTime + dt * frame);
            infoList.AddRange(GetVertInfos(maxVertCount));

            if (isCancel)
            {
                EditorUtility.ClearProgressBar();
                return (null, null);
            }
        }

        var maxBounds = Vector3.zero;
        var minBounds = Vector3.zero;
        foreach (var info in infoList)
        {
            minBounds = Vector3.Min(minBounds, info.position);
            maxBounds = Vector3.Max(maxBounds, info.position);
        }
        if(minBounds.magnitude < maxBounds.magnitude)
        {
            minBounds = maxBounds * -1;
        }
        else
        {
            maxBounds = minBounds * -1;
        }
        _mesh.bounds = new Bounds(){max = maxBounds, min = minBounds};

        var buffer = new ComputeBuffer(infoList.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(VertInfo)));
        buffer.SetData(infoList.ToArray());

        int rows = (int)((float)maxVertCount / (float)texSize.x - 0.00001f) + 1;

        var kernel = infoTexGen.FindKernel("CSMain");
        uint x, y, z;
        infoTexGen.GetKernelThreadGroupSizes(kernel, out x, out y, out z);

        infoTexGen.SetInt("MaxVertexCount", maxVertCount);
        infoTexGen.SetInt("TextureWidth", texSize.x);
        infoTexGen.SetBuffer(kernel, "Info", buffer);
        infoTexGen.SetTexture(kernel, "OutPosition", pRt);
        infoTexGen.SetTexture(kernel, "OutNormal", nRt);
        infoTexGen.Dispatch(kernel, Mathf.Clamp(maxVertCount / (int)x + 1 , 1, texSize.x / (int)x + 1) , (frames / (int)y) * rows + 1, 1);

        buffer.Release();

        var posTex = RenderTextureToTexture2D(pRt);
        var normTex = RenderTextureToTexture2D(nRt);
        Graphics.CopyTexture(pRt, posTex);
        Graphics.CopyTexture(nRt, normTex);

        posTex.filterMode = FilterMode.Point;
        normTex.filterMode = FilterMode.Point;
        posTex.wrapMode = TextureWrapMode.Clamp;
        normTex.wrapMode = TextureWrapMode.Clamp;

        FolderInit();

        AssetDatabase.CreateAsset(posTex, Path.Combine(_subFolderPath, pRt.name + ".asset"));
        AssetDatabase.CreateAsset(normTex, Path.Combine(_subFolderPath, nRt.name + ".asset"));

        EditorUtility.ClearProgressBar();

        return (posTex, normTex);
    }

    private List<VertInfo> GetVertInfos(int maxVertCount)
    {
        var infoList = new List<VertInfo>();
        var meshes = _meshFilters.Select(meshFilter => meshFilter.sharedMesh);
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();

        if(_topologyType == TopologyType.Soft)
        {
            foreach (var mesh in meshes)
            {
                vertices.AddRange(mesh.vertices);
                normals.AddRange(mesh.normals);
            }

            infoList.AddRange(Enumerable.Range(0, maxVertCount)
                .Select(idx =>
                {
                    var pos = idx < vertices.Count ? vertices[idx] : Vector3.zero;
                    var norm = idx < normals.Count ? normals[idx] : Vector3.zero;

                    return new VertInfo()
                    {
                        position = pos,
                        normal = norm
                    };
                })
            );
        }
        else if(_topologyType == TopologyType.Liquid)
        {
            var mesh = meshes.First();
            var tris = mesh.GetTriangles(0);
            var verts = mesh.vertices;
            var norms = mesh.normals;
            foreach (var tri in tris)
            {
                vertices.Add(verts[tri]);
                normals.Add(norms[tri]);
            }

            infoList.AddRange(Enumerable.Range(0, maxVertCount)
                .Select(idx =>
                {
                    var pos = idx < vertices.Count ? vertices[idx] : Vector3.zero;
                    var norm = idx < normals.Count ? normals[idx] : Vector3.zero;

                    return new VertInfo()
                    {
                        position = pos,
                        normal = norm
                    };
                })
            );
        }

        return infoList;
    }

    private void FolderInit()
    {
        _folderPath = Path.Combine("Assets", folderName);
        if (!AssetDatabase.IsValidFolder(_folderPath))
            AssetDatabase.CreateFolder("Assets", folderName);

        var subFolder = name;
        _subFolderPath = Path.Combine(_folderPath, subFolder);
        if (!AssetDatabase.IsValidFolder(_subFolderPath))
            AssetDatabase.CreateFolder(_folderPath, subFolder);
    }

    private void SaveAssets(Texture2D posTex, Texture2D normTex)
    {
        var mat = new Material(playShader);
        mat.SetTexture("_MainTex", _meshFilters.First().gameObject.GetComponent<MeshRenderer>().sharedMaterial.mainTexture);
        mat.SetTexture("_PosTex", posTex);
        mat.SetTexture("_NmlTex", normTex);
        mat.SetFloat("_Length", alembic.Duration);
        mat.SetInt("_VertCount", _mesh.vertexCount);
        mat.SetFloat("_IsFluid", Convert.ToInt32(_topologyType == TopologyType.Liquid));
        if(_topologyType == TopologyType.Liquid)
        {
            mat.EnableKeyword("IS_FLUID");
        }
        else
        {
            mat.DisableKeyword("IS_FLUID");
        }

        var go = new GameObject(alembic.gameObject.name);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        go.AddComponent<MeshFilter>().sharedMesh = _mesh;

        AssetDatabase.CreateAsset(mat, Path.Combine(_subFolderPath, string.Format("{0}_mat.asset", alembic.gameObject.name)));
        AssetDatabase.CreateAsset(_mesh, Path.Combine(_subFolderPath, string.Format("{0}_mesh.asset", alembic.gameObject.name)));
        var prefabObj = PrefabUtility.SaveAsPrefabAssetAndConnect(go, Path.Combine(_subFolderPath, go.name + ".prefab").Replace("\\", "/"), InteractionMode.UserAction);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private Texture2D RenderTextureToTexture2D(RenderTexture rt)
    {
        TextureFormat format;

        switch (rt.format)
        {
            case RenderTextureFormat.ARGBFloat:
                format = TextureFormat.RGBAFloat;
                break;
            case RenderTextureFormat.ARGBHalf:
                format = TextureFormat.RGBAHalf;
                break;
            case RenderTextureFormat.ARGBInt:
                format = TextureFormat.RGBA32;
                break;
            case RenderTextureFormat.ARGB32:
                format = TextureFormat.ARGB32;
                break;
            default:
                format = TextureFormat.ARGB32;
                Debug.LogWarning("Unsuported RenderTextureFormat.");
                break;
        }

        var tex2d = new Texture2D(rt.width, rt.height, format, false);
        var rect = Rect.MinMaxRect(0f, 0f, tex2d.width, tex2d.height);
        RenderTexture.active = rt;
        tex2d.ReadPixels(rect, 0, 0);
        RenderTexture.active = null;
        return tex2d;
    }

}
