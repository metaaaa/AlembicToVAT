namespace AlembicToVAT
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;
    using UnityEngine.Formats.Alembic.Importer;

    public class AlembicToVAT
    {
        // Properties
        private readonly AlembicStreamPlayer _alembic = null;
        private readonly int _samplingRate = 20;
        private readonly float _adjugstTime = -0.04166667f;
        private readonly MaxTextureWitdh _maxTextureWitdh = MaxTextureWitdh.w8192;
        private readonly ComputeShader _infoTexGen = null;
        private readonly float _startTime = 0f;
        private readonly MeshFilter[] _meshFilters = null;
        private TopologyType _topologyType = TopologyType.Soft;
        private int _maxTriangleCount = 0;
        private int _minTriangleCount = Int32.MaxValue;
        private List<MeshPartInfo> _meshParts = new List<MeshPartInfo>();
        private Vector3 _rootScale = Vector3.one;

        public AlembicToVAT(AlembicStreamPlayer alembic, MaxTextureWitdh maxTextureWitdh,
            int samplingRate = 20, float adjugstTime = -0.04166667f)
        {
            _alembic = alembic;
            _samplingRate = samplingRate;
            _adjugstTime = adjugstTime;
            _maxTextureWitdh = maxTextureWitdh;

            _infoTexGen = (ComputeShader)Resources.Load("AlembicInfoToTexture");
            _startTime = _alembic.StartTime + _adjugstTime;
            _meshFilters = _alembic.gameObject.GetComponentsInChildren<MeshFilter>();

            _rootScale = _alembic.transform.localScale;

            foreach (var meshFilter in _meshFilters)
            {
                var meshPart = new MeshPartInfo();
                meshPart.meshFilter = meshFilter;
                meshPart.parentTrans = meshFilter.gameObject.transform.parent;
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

            return new ConvertResult(texTuple.posTex, texTuple.normTex, mainTex, mesh, _topologyType);
        }

        private TopologyType GetTopologyType()
        {
            _maxTriangleCount = 0;
            _minTriangleCount = Int32.MaxValue;

            int frames = ((int)(_alembic.Duration * _samplingRate));
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

            int maxTextureWitdh = (int)this._maxTextureWitdh;

            var x = Mathf.NextPowerOfTwo(maxVertCount);
            x = x > maxTextureWitdh ? maxTextureWitdh : x;
            var y = ((int)(_alembic.Duration * _samplingRate) * ((int)((maxVertCount - 1) / maxTextureWitdh) + 1)) + 1;
            size.x = x;
            size.y = y;
            if (y > maxTextureWitdh)
            {
                Debug.LogError("data size over");
            }
            return size;
        }

        private (Texture2D posTex, Texture2D normTex) BakeTextures(Mesh mesh)
        {
            var maxVertCount = GetMaxVertexCount();
            var frames = (int)(_alembic.Duration * _samplingRate);
            var texSize = GetTextureSize();
            var dt = _alembic.Duration / frames;

            var pRt = new RenderTexture(texSize.x, texSize.y, 0, RenderTextureFormat.ARGBHalf);
            pRt.name = string.Format("{0}.posTex", _alembic.gameObject.name);
            var nRt = new RenderTexture(texSize.x, texSize.y, 0, RenderTextureFormat.ARGBHalf);
            nRt.name = string.Format("{0}.normTex", _alembic.gameObject.name);
            foreach (var rt in new[] { pRt, nRt })
            {
                rt.enableRandomWrite = true;
                rt.Create();
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.clear);
            }

            var infoList = new List<VertInfo>(texSize.y * maxVertCount);
            float progress = 0f;
            for (var frame = 0; frame <= frames; frame++)
            {
                progress = (float)frame / (float)frames;
                string progressText = ((frame % 2) == 0) ? "processing ₍₍(ง˘ω˘)ว⁾⁾" : "processing ₍₍(ว˘ω˘)ง⁾⁾";
                bool isCancel = EditorUtility.DisplayCancelableProgressBar("AlembicToVAT", progressText, progress);
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
            {
                minBounds = maxBounds * -1;
            }
            else
            {
                maxBounds = minBounds * -1;
            }
            maxBounds = Vector3.Scale(maxBounds, _rootScale);
            minBounds = Vector3.Scale(minBounds, _rootScale);
            mesh.bounds = new Bounds() { max = maxBounds, min = minBounds };

            var buffer = new ComputeBuffer(infoList.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(VertInfo)));
            buffer.SetData(infoList.ToArray());

            int rows = (maxVertCount - 1) / texSize.x + 1;

            var kernel = _infoTexGen.FindKernel("CSMain");
            uint x, y, z;
            _infoTexGen.GetKernelThreadGroupSizes(kernel, out x, out y, out z);

            _infoTexGen.SetInt("MaxVertexCount", maxVertCount);
            _infoTexGen.SetInt("TextureWidth", texSize.x);
            _infoTexGen.SetVector("RootScale", _rootScale);
            _infoTexGen.SetBuffer(kernel, "Info", buffer);
            _infoTexGen.SetTexture(kernel, "OutPosition", pRt);
            _infoTexGen.SetTexture(kernel, "OutNormal", nRt);
            _infoTexGen.Dispatch(kernel, Mathf.Clamp(maxVertCount / (int)x + 1, 1, texSize.x / (int)x + 1), frames * rows + 1, 1);

            buffer.Release();

            var posTex = RenderTextureToTexture2D(pRt);
            var normTex = RenderTextureToTexture2D(nRt);
            Graphics.CopyTexture(pRt, posTex);
            Graphics.CopyTexture(nRt, normTex);

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

                        return new VertInfo()
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
            tex2d.name = rt.name;
            return tex2d;
        }
    }
}