#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TimeEcho.Flow.Editor
{
    [CustomEditor(typeof(TimeEchoFlowConfig))]
    public sealed class TimeEchoFlowConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            TimeEchoFlowConfig config = (TimeEchoFlowConfig)target;
            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("Setup checks", EditorStyles.boldLabel);

            HashSet<string> buildScenes = new HashSet<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled) buildScenes.Add(Path.GetFileNameWithoutExtension(scene.path));
            }

            int missing = CountMissing(config, buildScenes);
            if (missing == 0)
            {
                EditorGUILayout.HelpBox("All configured scenes are enabled in Build Settings.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(missing + " configured scene(s) are missing from Build Settings. Use the validation menu for exact names.", MessageType.Warning);
            }

            if (GUILayout.Button("Run full validation"))
            {
                EditorApplication.ExecuteMenuItem("Tools/Time Echo/Progression/Validate Configuration");
            }
        }

        private static int CountMissing(TimeEchoFlowConfig config, HashSet<string> scenes)
        {
            int count = string.IsNullOrWhiteSpace(config.menuScene) || !scenes.Contains(config.menuScene) ? 1 : 0;
            if (!string.IsNullOrWhiteSpace(config.winScene) && !scenes.Contains(config.winScene)) count++;
            foreach (LevelFlowDefinition level in config.levels)
            {
                if (level == null || string.IsNullOrWhiteSpace(level.sceneName) || !scenes.Contains(level.sceneName)) count++;
            }
            return count;
        }
    }
}
#endif
