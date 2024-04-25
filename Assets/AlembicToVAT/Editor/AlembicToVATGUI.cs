using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;
using UnityEngine.SceneManagement;

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
        private bool _packNormalsIntoAlpha = false;	


        private void OnGUI()
        {
            try
            {
                _alembic = (AlembicStreamPlayer)EditorGUILayout.ObjectField("alembic", _alembic,
                    typeof(AlembicStreamPlayer), true);
                _samplingRate = EditorGUILayout.IntField("samplingRate", _samplingRate);
                _adjustTime = EditorGUILayout.FloatField("adjustTime", _adjustTime);
                _maxTextureWidth = (MaxTextureWidth)EditorGUILayout.EnumPopup("maxTextureWidth", _maxTextureWidth);
                _packNormalsIntoAlpha = EditorGUILayout.Toggle("packNormalsIntoAlpha", _packNormalsIntoAlpha);
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
            catch (FormatException e)
            {
                Debug.LogError(e);
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

            var alembicToVat = new AlembicToVat(_alembic, _maxTextureWidth, _samplingRate, _adjustTime, _packNormalsIntoAlpha);
            var result = alembicToVat.Exec();
            if (result == null) return;

            // create assets
            alembicToVat.SaveAssets(result, _playShader, _folderName);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
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
    }
}