using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Formats.Alembic.Importer;
using AlembicToVAT;
using System.IO;
using System;
using UnityEngine.Serialization;

public class AlembicToVATTest : ScriptableWizard
{
    // Properties
    public List<AlembicStreamPlayer> alembics = new List<AlembicStreamPlayer>();
    public float posOffset = 5;
    public int samplingRate = 20;
    public float adjugstTime = -0.04166667f;
    [FormerlySerializedAs("maxTextureWitdh")] public MaxTextureWidth maxTextureWidth = MaxTextureWidth.w8192;
    public string folderName = "Results";
    public string shaderName = "AlembicToVAT/TextureAnimPlayer";
    private Shader _playShader = null;
    private AlembicStreamPlayer _alembic = null;
    private GameObject _parent = null;


    [MenuItem("Tests/AlembicToVATTest")]
    static void Init()
    {
        DisplayWizard<AlembicToVATTest>("AlembicToVATTest");
    }

    /// <summary>
    /// Createボタンが押された
    /// </summary>
    private void OnWizardCreate()
    {
        _playShader = Shader.Find(shaderName);

        var old = GameObject.Find("VAT Objects");
        if(old != null) DestroyImmediate(old);

        _parent = new GameObject("VAT Objects");
        _parent.transform.position = new Vector3(15, 0, 0);


        for (int i = 0; i < alembics.Count; i++)
        {
            _alembic = alembics[i];
            var alembicToVat = new AlembicToVAT.AlembicToVat(_alembic, maxTextureWidth, samplingRate, adjugstTime);
            var result = alembicToVat.Exec();
            if (result == null) return;

            // create assets
            SaveAssets(result, i);
        }
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

            var subFolder = _alembic.gameObject.name;
            var path = Path.Combine(folderPath, subFolder);
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(folderPath, subFolder);
            return path;
        }

    private void SaveAssets(ConvertResult result, int index)
    {
        var posTex = result.posTex;
        var normTex = result.normTex;
        var mainTex = result.mainTex;
        var mesh = result.mesh;
        var topologyType = result.topologyType;
        var path = InitializeFolder();

        AssetDatabase.CreateAsset(posTex, Path.Combine(path, posTex.name + ".asset"));
        AssetDatabase.CreateAsset(normTex, Path.Combine(path, normTex.name + ".asset"));
        AssetDatabase.CreateAsset(mesh, Path.Combine(path, string.Format("{0}_mesh.asset", _alembic.gameObject.name)));

        var mat = new Material(_playShader);
        mat.SetTexture("_MainTex", mainTex);
        mat.SetTexture("_PosTex", posTex);
        mat.SetTexture("_NmlTex", normTex);
        mat.SetFloat("_Length", _alembic.Duration);
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

        var go = new GameObject(_alembic.gameObject.name);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.transform.SetParent(_parent.transform);
        go.transform.localPosition = new Vector3(0 ,0 ,index * posOffset);

        AssetDatabase.CreateAsset(mat, Path.Combine(path, string.Format("{0}_mat.asset", _alembic.gameObject.name)));
        var prefabObj = PrefabUtility.SaveAsPrefabAssetAndConnect(go, Path.Combine(path, go.name + ".prefab").Replace("\\", "/"), InteractionMode.UserAction);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
    }
}
