using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AlembicToVAT
{
    public class AlembicToVat
    {
        // Properties
        private readonly float _adjustTime;
        private readonly AlembicStreamPlayer _alembic;
        private readonly ComputeShader _infoTexGen;
        private readonly MaxTextureWidth _maxTextureWidth = MaxTextureWidth.w8192;
        private readonly MeshFilter[] _meshFilters;
        private readonly int _samplingRate = 20;
        private readonly float _startTime;
        private readonly bool _packNormalsIntoAlpha;
        private int _maxTriangleCount;
        private readonly List<MeshPartInfo> _meshParts = new();
        private int _minTriangleCount = int.MaxValue;
        private readonly Vector3 _rootScale = Vector3.one;
        private TopologyType _topologyType = TopologyType.Soft;

        public AlembicToVat(AlembicStreamPlayer alembic, MaxTextureWidth maxTextureWidth,
            int samplingRate = 20, float adjustTime = -0.04166667f, bool packNormalsIntoAlpha = false)
        {
            _alembic = alembic;
            _samplingRate = samplingRate;
            _adjustTime = adjustTime;
            _maxTextureWidth = maxTextureWidth;
            _packNormalsIntoAlpha = packNormalsIntoAlpha;

            _infoTexGen = (ComputeShader)Resources.Load("AlembicInfoToTexture");
            _startTime = _alembic.StartTime + _adjustTime;
            _meshFilters = _alembic.gameObject.GetComponentsInChildren<MeshFilter>();

            _rootScale = _alembic.transform.localScale;

            foreach (var meshFilter in _meshFilters)
            {
                var meshPart = new MeshPartInfo
                {
                    meshFilter = meshFilter,
                    parentTrans = meshFilter.gameObject.transform.parent
                };

                _meshParts.Add(meshPart);
            }
        }

        public ConvertResult Exec()
        {
            // check VAT Type
            _topologyType = GetTopologyType();

            // bake mesh
            var mesh = BakeMesh();

            // bake texture
            var texTuple = BakeTextures(mesh);
            if (texTuple.posTex == null || texTuple.normTex == null) return null;
            // reset
            _alembic.UpdateImmediately(_startTime);

            var mainTex = _meshFilters.First().gameObject.GetComponent<MeshRenderer>().sharedMaterial.mainTexture;

            return new ConvertResult
            {
                posTex = texTuple.posTex,
                normTex = texTuple.normTex,
                mainTex = mainTex,
                mesh = mesh,
                topologyType = _topologyType
            };
        }

        private TopologyType GetTopologyType()
        {
            _maxTriangleCount = 0;
            _minTriangleCount = int.MaxValue;

            var frames = (int)(_alembic.Duration * _samplingRate);
            var dt = _alembic.Duration / frames;

            for (var frame = 0; frame < frames; frame++)
            {
                _alembic.UpdateImmediately(_startTime + dt * frame);

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
            // reset
            _alembic.UpdateImmediately(_startTime);
            return type;
        }

        private Mesh BakeMesh()
        {
            var bakedMesh = new Mesh();
            bakedMesh.indexFormat = IndexFormat.UInt32;

            var verticesCount = 0;
            var trianglesIndexCount = 0;

            var hasNormal = false;
            var hasUVs = false;
            var hasColors = false;

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

                    hasNormal |= sharedMesh.normals.Length > 0;
                    hasColors |= sharedMesh.colors.Length > 0;
                    hasUVs |= sharedMesh.uv.Length > 0;
                }
            }

            var vertices = new Vector3[verticesCount];
            var uv = new Vector2[verticesCount];
            var normals = new Vector3[verticesCount];
            var colors = new Color[verticesCount];
            var triangles = new int[trianglesIndexCount];

            if (_topologyType == TopologyType.Liquid)
            {
                for (var i = 0; i < verticesCount; i++) // everything is initialized to 0
                {
                    triangles[i] = i;
                    vertices[i] = Vector3.zero;
                    normals[i] = Vector3.up;
                }
            }
            else
            {
                var currentTrianglesIndex = 0;
                var verticesOffset = 0;
                foreach (var meshFilter in _meshFilters)
                {
                    var sharedMesh = meshFilter.sharedMesh;
                    var vertCount = sharedMesh.vertices.Length;
                    for (var j = 0; j < vertCount; j++)
                    {
                        if (hasUVs)
                            uv[j + verticesOffset] = sharedMesh.uv[j];
                        if (hasColors)
                            colors[j + verticesOffset] = sharedMesh.colors[j];

                        vertices[j + verticesOffset] = sharedMesh.vertices[j];
                    }

                    var sharedTriangles = sharedMesh.triangles;
                    for (var j = 0; j < sharedTriangles.Length; j++)
                        triangles[currentTrianglesIndex++] = sharedTriangles[j] + verticesOffset;

                    verticesOffset += vertCount;
                }
            }

            vertices = vertices.Select(x => Vector3.Scale(x, _rootScale)).ToArray();

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
            var maxVertCount = 0;
            if (_topologyType == TopologyType.Liquid)
                maxVertCount = _maxTriangleCount * 3;
            else
                maxVertCount = _meshFilters.Select(x => x.sharedMesh.vertexCount).Sum();

            return maxVertCount;
        }

        private Vector2Int GetTextureSize()
        {
            var size = new Vector2Int();
            var maxVertCount = GetMaxVertexCount();

            var maxTextureWidth = (int)_maxTextureWidth;

            var x = Mathf.NextPowerOfTwo(maxVertCount);
            x = x > maxTextureWidth ? maxTextureWidth : x;
            var blockSize = (int)((maxVertCount - 0.1f) / maxTextureWidth) + 1;
            var frames = Mathf.FloorToInt(_alembic.Duration * _samplingRate) + 1;
            var y = frames * blockSize;
            size.x = x;
            size.y = y;
            if (y > maxTextureWidth) Debug.LogError("data size over");
            return size;
        }

        private (Texture2D posTex, Texture2D normTex) BakeTextures(Mesh mesh)
        {
            var maxVertCount = GetMaxVertexCount();
            var frames = Mathf.FloorToInt(_alembic.Duration * _samplingRate) + 1;
            var texSize = GetTextureSize();
            var dt = _alembic.Duration / frames;
            var alembicName = _alembic.gameObject.name;

            var pRt = new RenderTexture(texSize.x, texSize.y, 0, RenderTextureFormat.ARGBHalf)
            {
                name = $"{alembicName}.posTex",
            };
            var nRt = new RenderTexture(texSize.x, texSize.y, 0, RenderTextureFormat.ARGBHalf)
            {
                name = $"{alembicName}.normTex",
            };
            foreach (var rt in new[] { pRt, nRt })
            {
                rt.enableRandomWrite = true;
                rt.Create();
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.clear);
            }

            var infoList = new List<VertInfo>(texSize.y * maxVertCount);
            for (var frame = 0; frame <= frames; frame++)
            {
                var progress = frame / (float)frames;
                var progressText = frame % 2 == 0 ? "processing ₍₍(ง˘ω˘)ว⁾⁾" : "processing ₍₍(ว˘ω˘)ง⁾⁾";
                var isCancel = EditorUtility.DisplayCancelableProgressBar("AlembicToVAT", progressText, progress);
                _alembic.UpdateImmediately(_startTime + dt * frame);
                infoList.AddRange(GetVertInfos(maxVertCount));

                if (isCancel)
                {
                    EditorUtility.ClearProgressBar();
                    Debug.Log("Canceled");
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

            if (minBounds.magnitude < maxBounds.magnitude)
                minBounds = maxBounds * -1;
            else
                maxBounds = minBounds * -1;
            maxBounds = Vector3.Scale(maxBounds, _rootScale);
            minBounds = Vector3.Scale(minBounds, _rootScale);
            mesh.bounds = new Bounds { max = maxBounds, min = minBounds };

            var buffer = new ComputeBuffer(infoList.Count, Marshal.SizeOf(typeof(VertInfo)));
            buffer.SetData(infoList.ToArray());

            var rows = (maxVertCount - 1) / texSize.x + 1;

            var kernel = _infoTexGen.FindKernel("CSMain");
            _infoTexGen.GetKernelThreadGroupSizes(kernel, out var x, out var y, out var z);

            _infoTexGen.SetInt("MaxVertexCount", maxVertCount);
            _infoTexGen.SetInt("TextureWidth", texSize.x);
            _infoTexGen.SetBool("PackNormalsIntoAlpha", _packNormalsIntoAlpha);
            _infoTexGen.SetVector("RootScale", _rootScale);
            _infoTexGen.SetBuffer(kernel, "Info", buffer);
            _infoTexGen.SetTexture(kernel, "OutPosition", pRt);
            _infoTexGen.SetTexture(kernel, "OutNormal", nRt);
            _infoTexGen.Dispatch(kernel, Mathf.Clamp(maxVertCount / (int)x + 1, 1, texSize.x / (int)x + 1),
                frames * rows + 1, 1);

            buffer.Release();

            var posTex = RenderTextureToTexture2D(pRt);
            var normTex = RenderTextureToTexture2D(nRt);
            Graphics.CopyTexture(pRt, posTex);
            Graphics.CopyTexture(nRt, normTex);

            Object.DestroyImmediate(pRt);
            Object.DestroyImmediate(nRt);

            posTex.filterMode = FilterMode.Point;
            normTex.filterMode = FilterMode.Point;
            posTex.wrapMode = TextureWrapMode.Repeat;
            normTex.wrapMode = TextureWrapMode.Repeat;

            EditorUtility.ClearProgressBar();

            return (posTex, normTex);
        }

        private List<VertInfo> GetVertInfos(int maxVertCount)
        {
            var infoList = new List<VertInfo>();
            var meshes = _meshFilters.Select(meshFilter => meshFilter.sharedMesh);
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();

            if (_topologyType == TopologyType.Soft)
            {
                foreach (var meshPart in _meshParts)
                {
                    var mesh = meshPart.meshFilter.sharedMesh;
                    var meshParentTrans = meshPart.parentTrans;
                    var meshParentLocalScale = meshParentTrans.localScale;
                    var meshVerts = mesh.vertices
                        .Select(x => Vector3.Scale(x, meshParentTrans.localScale))
                        .Select(x => meshParentTrans.localRotation * x)
                        .Select(x => meshParentTrans.localPosition + x);
                    var meshNorms = mesh.normals.Select(x => meshParentTrans.localRotation * x);
                    vertices.AddRange(meshVerts);
                    normals.AddRange(meshNorms);
                }

                infoList.AddRange(Enumerable.Range(0, maxVertCount)
                    .Select(idx =>
                    {
                        var pos = idx < vertices.Count ? vertices[idx] : Vector3.zero;
                        var norm = idx < normals.Count ? normals[idx] : Vector3.zero;

                        return new VertInfo
                        {
                            position = pos,
                            normal = norm
                        };
                    })
                );
            }
            else if (_topologyType == TopologyType.Liquid)
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

                        return new VertInfo
                        {
                            position = pos,
                            normal = norm
                        };
                    })
                );
            }

            return infoList;
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
                    Debug.LogWarning("Unsupported RenderTextureFormat.");
                    break;
            }

            var tex2d = new Texture2D(rt.width, rt.height, format, false);
            var rect = Rect.MinMaxRect(0f, 0f, tex2d.width, tex2d.height);
            RenderTexture.active = rt;
            tex2d.ReadPixels(rect, 0, 0);
            RenderTexture.active = null;
            tex2d.name = rt.name;
            return tex2d;
        }
        
        private string InitializeFolder(string folderName)
        {
            var folderPath = Path.Combine("Assets", folderName);
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                var folderNameSplit = folderName.Split('/');
                var prevPath = "Assets";
                foreach (var item in folderNameSplit)
                {
                    var tmpPath = Path.Combine(prevPath, item);
                    if (!AssetDatabase.IsValidFolder(tmpPath)) AssetDatabase.CreateFolder(prevPath, item);
                    prevPath = tmpPath;
                }
            }

            var subFolder = _alembic.gameObject.name;
            var path = Path.Combine(folderPath, subFolder);
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(folderPath, subFolder);
            return path;
        }

        public GameObject SaveAssets(ConvertResult result, Shader playShader, string folderName)
        {
            var posTex = result.posTex;
            var normTex = result.normTex;
            var mainTex = result.mainTex;
            var mesh = result.mesh;
            var topologyType = result.topologyType;
            var path = InitializeFolder(folderName);

            AssetDatabase.CreateAsset(posTex, Path.Combine(path, posTex.name + ".asset"));
            AssetDatabase.CreateAsset(normTex, Path.Combine(path, normTex.name + ".asset"));
            AssetDatabase.CreateAsset(mesh, Path.Combine(path, $"{_alembic.gameObject.name}_mesh.asset"));

            var mat = new Material(playShader);
            mat.SetTexture("_MainTex", mainTex);
            mat.SetTexture("_PosTex", posTex);
            if(!_packNormalsIntoAlpha) mat.SetTexture("_NmlTex", normTex);
            mat.SetFloat("_Length", _alembic.Duration);
            mat.SetInt("_VertCount", mesh.vertexCount);
            mat.SetFloat("_IsFluid", Convert.ToInt32(topologyType == TopologyType.Liquid));
            mat.SetFloat("_AlphaIsNormal", Convert.ToInt32(_packNormalsIntoAlpha));
            if (topologyType == TopologyType.Liquid)
                mat.EnableKeyword("IS_FLUID");
            else
                mat.DisableKeyword("IS_FLUID");

            var go = new GameObject(_alembic.gameObject.name);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            AssetDatabase.CreateAsset(mat,
                Path.Combine(path, $"{_alembic.gameObject.name}_mat.asset"));
            PrefabUtility.SaveAsPrefabAssetAndConnect(go,
                Path.Combine(path, go.name + ".prefab").Replace("\\", "/"), InteractionMode.UserAction);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return go;
        }
    }
}