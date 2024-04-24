using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;

namespace AlembicToVAT
{
    public class AlembicToVatGui : EditorWindow
    {
        private const string UserSettingsConfigKeyPrefix = "AlembicToVAT.";

        private float _adjustTime = -0.04166667f;

        // Properties
        private AlembicStreamPlayer _alembic;
        private string _folderName = "AlembicToVAT/Results";
        private MaxTextureWidth _maxTextureWidth = MaxTextureWidth.w8192;
        private Shader _playShader;
        private int _samplingRate = 20;
        private string _shaderName = "AlembicToVAT/TextureAnimPlayer";

        private void OnGUI()
        {
            try
            {
                _alembic = (AlembicStreamPlayer)EditorGUILayout.ObjectField("alembic", _alembic,
                    typeof(AlembicStreamPlayer), true);
                _samplingRate = EditorGUILayout.IntField("samplingRate", _samplingRate);
                _adjustTime = EditorGUILayout.FloatField("adjustTime", _adjustTime);
                _maxTextureWidth = (MaxTextureWidth)EditorGUILayout.EnumPopup("maxTextureWidth", _maxTextureWidth);
                var folderName = EditorGUILayout.TextField("folderName", _folderName);
                if (folderName != _folderName)
                {
                    _folderName = folderName;
                    EditorUserSettings.SetConfigValue(GetConfigKey(nameof(_folderName)), _folderName);
                }

                var shaderName = EditorGUILayout.TextField("shaderName", _shaderName);
                if (shaderName != _shaderName)
                {
                    _shaderName = shaderName;
                    EditorUserSettings.SetConfigValue(GetConfigKey(nameof(_shaderName)), _shaderName);
                }

                if (GUILayout.Button("process")) Make();
            }
            catch (FormatException)
            {
            }
        }

        [MenuItem("AlembicToVAT/AlembicToVAT")]
        private static void Create()
        {
            var window = GetWindow<AlembicToVatGui>("AlembicToVAT");

            window._folderName = EditorUserSettings.GetConfigValue(GetConfigKey(nameof(_folderName))) ??
                                 window._folderName;
            window._shaderName = EditorUserSettings.GetConfigValue(GetConfigKey(nameof(_shaderName))) ??
                                 window._shaderName;
        }

        private static string GetConfigKey(string varName)
        {
            return UserSettingsConfigKeyPrefix + varName;
        }

        private void Make()
        {
            _playShader = Shader.Find(_shaderName);

            // validate
            if (!InputValidate()) return;

            var alembicToVat = new AlembicToVat(_alembic, _maxTextureWidth, _samplingRate, _adjustTime);
            var result = alembicToVat.Exec();
            if (result == null) return;

            // create assets
            SaveAssets(result);
        }

        private bool InputValidate()
        {
            var valid = true;
            if (_alembic == null)
            {
                Debug.LogError("alembic not found");
                valid = false;
            }

            if (_playShader == null)
            {
                Debug.LogError("shader not found");
                valid = false;
            }

            return valid;
        }


        private string InitializeFolder()
        {
            var folderPath = Path.Combine("Assets", _folderName);
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                var folderNameSplit = _folderName.Split('/');
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
            AssetDatabase.CreateAsset(mesh,
                Path.Combine(path, string.Format("{0}_mesh.asset", _alembic.gameObject.name)));

            var mat = new Material(_playShader);
            mat.SetTexture("_MainTex", mainTex);
            mat.SetTexture("_PosTex", posTex);
            mat.SetTexture("_NmlTex", normTex);
            mat.SetFloat("_Length", _alembic.Duration);
            mat.SetInt("_VertCount", mesh.vertexCount);
            mat.SetFloat("_IsFluid", Convert.ToInt32(topologyType == TopologyType.Liquid));
            if (topologyType == TopologyType.Liquid)
                mat.EnableKeyword("IS_FLUID");
            else
                mat.DisableKeyword("IS_FLUID");

            var go = new GameObject(_alembic.gameObject.name);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            AssetDatabase.CreateAsset(mat,
                Path.Combine(path, string.Format("{0}_mat.asset", _alembic.gameObject.name)));
            PrefabUtility.SaveAsPrefabAssetAndConnect(go,
                Path.Combine(path, go.name + ".prefab").Replace("\\", "/"), InteractionMode.UserAction);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}