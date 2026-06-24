#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SceneLogicConfig))]
public class SceneLogicConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var config = (SceneLogicConfig)target;

        GUILayout.Space(10f);
        EditorGUILayout.LabelField("Scene Synchronization", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Pull From Scene"))
            {
                config.PullFromScene();
            }

            if (GUILayout.Button("Apply To Scene"))
            {
                config.ApplyToScene();
            }
        }
    }
}
#endif
