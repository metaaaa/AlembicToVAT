namespace AlembicToVAT
{
    using System;
    using System.IO;
    using UnityEngine;
    using UnityEditor;
    using UnityEngine.Formats.Alembic.Importer;

    public class AlembicToVATGUI : EditorWindow
    {
        // Properties
        public AlembicStreamPlayer alembic = null;
        public int samplingRate = 20;
        public float adjugstTime = -0.04166667f;
        public MaxTextureWitdh maxTextureWitdh = MaxTextureWitdh.w8192;
        public string folderName = "AlembicToVAT/Results";
        public string shaderName = "AlembicToVAT/TextureAnimPlayer";
        private Shader _playShader = null;


        [MenuItem("metaaa/AlembicToVAT")]
        static void Create()
        {
            GetWindow<AlembicToVATGUI>("AlembicToVAT");
        }

        private void OnGUI()
        {
            try
            {
                alembic = (AlembicStreamPlayer)EditorGUILayout.ObjectField("alembic", alembic, typeof(AlembicStreamPlayer), true);
                samplingRate = EditorGUILayout.IntField("samplingRate", samplingRate);
                adjugstTime = EditorGUILayout.FloatField("adjugstTime", adjugstTime);
                maxTextureWitdh = (MaxTextureWitdh)EditorGUILayout.EnumPopup("maxTextureWitdh", maxTextureWitdh);
                folderName = EditorGUILayout.TextField("folderName", folderName);
                shaderName = EditorGUILayout.TextField("shaderName", shaderName);
                if (GUILayout.Button("process")) Make();
            }
            catch (System.FormatException) { }
        }

        private void Make()
        {
            _playShader = Shader.Find(shaderName);

            // validate
            if (!InputValidate()) return;

            var alembicToVat = new AlembicToVAT(alembic, maxTextureWitdh, samplingRate, adjugstTime);
            var result = alembicToVat.Exec();
            if (result == null) return;

            // create assets
            SaveAssets(result);
        }

        private bool InputValidate()
        {
            bool valid = true;
            if (alembic == null)
            {
                Debug.LogError("alembicが設定されていません");
                valid = false;
            }
            if (_playShader == null)
            {
                Debug.LogError("playShaderが設定されていません");
                valid = false;
            }
            return valid;
        }


        private string InitializeFolder()
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

            var subFolder = alembic.gameObject.name;
            var path = Path.Combine(folderPath, subFolder);
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(folderPath, subFolder);
            return path;
        }

        private void SaveAssets(ConvertResult result)
        {
            var posTex = result.posTex;
            var normTex = result.normTex;
            var mainTex = result.mainTex;
            var mesh = result.mesh;
            var topologyType = result.topologyType;
            var path = InitializeFolder();

            AssetDatabase.CreateAsset(posTex, Path.Combine(path, posTex.name + ".asset"));
            AssetDatabase.CreateAsset(normTex, Path.Combine(path, normTex.name + ".asset"));
            AssetDatabase.CreateAsset(mesh, Path.Combine(path, string.Format("{0}_mesh.asset", alembic.gameObject.name)));

            var mat = new Material(_playShader);
            mat.SetTexture("_MainTex", mainTex);
            mat.SetTexture("_PosTex", posTex);
            mat.SetTexture("_NmlTex", normTex);
            mat.SetFloat("_Length", alembic.Duration);
            mat.SetInt("_VertCount", mesh.vertexCount);
            mat.SetFloat("_IsFluid", Convert.ToInt32(topologyType == TopologyType.Liquid));
            if (topologyType == TopologyType.Liquid)
            {
                mat.EnableKeyword("IS_FLUID");
            }
            else
            {
                mat.DisableKeyword("IS_FLUID");
            }

            var go = new GameObject(alembic.gameObject.name);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            AssetDatabase.CreateAsset(mat, Path.Combine(path, string.Format("{0}_mat.asset", alembic.gameObject.name)));
            var prefabObj = PrefabUtility.SaveAsPrefabAssetAndConnect(go, Path.Combine(path, go.name + ".prefab").Replace("\\", "/"), InteractionMode.UserAction);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

    }
}
