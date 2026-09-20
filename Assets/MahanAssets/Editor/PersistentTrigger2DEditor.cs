#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TimeEcho.Editor
{
    [CustomEditor(typeof(PersistentTrigger2D))]
    public sealed class PersistentTrigger2DEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "This trigger's latch is not rewindable. A target stays changed only when that target has not separately opted into rewind.",
                MessageType.Info);

            DrawDefaultInspector();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "For a two-image lever, assign State Renderer, Inactive Sprite, and Active Sprite above. Add world operations on the Persistent Action Sequence 2D component.",
                    MessageType.None);
                return;
            }

            PersistentTrigger2D trigger = (PersistentTrigger2D)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Runtime State",
                trigger.IsActivated
                    ? "Activated (count " + trigger.ActivationCount + ")"
                    : "Inactive (count " + trigger.ActivationCount + ")");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Activate"))
            {
                trigger.Activate();
            }
            if (GUILayout.Button("Deactivate"))
            {
                trigger.Deactivate();
            }
            if (GUILayout.Button("Reset State"))
            {
                trigger.ResetPersistentState();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
