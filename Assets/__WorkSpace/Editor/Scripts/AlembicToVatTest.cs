using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Formats.Alembic.Importer;
using AlembicToVAT;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class AlembicToVatTest : ScriptableWizard
{
    // Properties
    public List<AlembicStreamPlayer> alembics = new List<AlembicStreamPlayer>();
    public float posOffset = 5;
    public int samplingRate = 20;
    public float adjustTime = -0.04166667f;
    public MaxTextureWidth maxTextureWidth = MaxTextureWidth.w8192;
    public bool packNormalsIntoAlpha = false;
    public string folderName = "Results";
    public string shaderName = "AlembicToVAT/TextureAnimPlayer";
    private Shader _playShader = null;
    private AlembicStreamPlayer _alembic = null;
    private GameObject _parent = null;


    [MenuItem("Tests/AlembicToVATTest")]
    static void Init()
    {
        DisplayWizard<AlembicToVatTest>("AlembicToVATTest");
    }

    /// <summary>
    /// Createボタンが押された
    /// </summary>
    private void OnWizardCreate()
    {
        _playShader = Shader.Find(shaderName);

        var old = GameObject.Find("VAT Objects");
        if (old != null) DestroyImmediate(old);

        _parent = new GameObject("VAT Objects");
        _parent.transform.position = new Vector3(15, 0, 0);


        for (int i = 0; i < alembics.Count; i++)
        {
            _alembic = alembics[i];
            var alembicToVat =
                new AlembicToVat(_alembic, maxTextureWidth, samplingRate, adjustTime, packNormalsIntoAlpha);
            var result = alembicToVat.Exec();
            if (result == null) return;

            // create assets
            var go = alembicToVat.SaveAssets(result, _playShader,folderName);
            go.transform.SetParent(_parent.transform);
            go.transform.localPosition = new Vector3(0, 0, i * posOffset);
        }
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }
}