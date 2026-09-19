#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TimeEcho.Editor
{
    /// <summary>One-time opt-in update for already-serialized GameTuning assets.</summary>
    public static class InstantBoostPreset
    {
        [MenuItem("Tools/Time Echo/Boost/Enable Instant Boost on Game Tuning")]
        public static void EnableInstantBoost()
        {
            GameTuning tuning = Selection.activeObject as GameTuning;
            if (tuning == null)
            {
                string[] matches = AssetDatabase.FindAssets("t:GameTuning");
                if (matches.Length != 1)
                {
                    EditorUtility.DisplayDialog(
                        "Choose Game Tuning",
                        "Select your existing GameTuning asset in the Project window first, then run this menu command. " +
                        "Found " + matches.Length + " tuning assets; none was changed.",
                        "OK");
                    return;
                }

                tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(AssetDatabase.GUIDToAssetPath(matches[0]));
                if (tuning == null)
                {
                    Debug.LogWarning("Instant Boost: unable to load the GameTuning asset.");
                    return;
                }
            }

            Undo.RecordObject(tuning, "Enable instant boost");
            tuning.aim.holdThreshold = 0f;
            tuning.aim.tapCharge = 1f;
            tuning.aim.fullChargeTime = 0.01f;
            tuning.aim.actionCooldown = 0f;
            tuning.Sanitize();
            EditorUtility.SetDirty(tuning);
            AssetDatabase.SaveAssets();
            Selection.activeObject = tuning;
            Debug.Log("Instant Boost enabled on " + AssetDatabase.GetAssetPath(tuning) +
                      ". Boost fires on LMB release with no minimum hold or cooldown.", tuning);
        }
    }
}
#endif
