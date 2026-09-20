#if UNITY_EDITOR
using TimeEcho.Visuals;
using UnityEditor;
using UnityEngine;

namespace TimeEcho.EditorTools
{
    public static class SelectiveSpriteGlowInstaller
    {
        private const string MenuPath =
            "Tools/TimeEcho/Selective Character Glow/Add To Selected Player";

        [MenuItem(MenuPath)]
        private static void AddToSelectedPlayer()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog(
                    "Selective Character Glow",
                    "Select the Player GameObject first.",
                    "OK");
                return;
            }

            SpriteRenderer spriteRenderer = selected.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = selected.GetComponentInChildren<SpriteRenderer>(true);

            if (spriteRenderer == null)
            {
                EditorUtility.DisplayDialog(
                    "Selective Character Glow",
                    "The selected Player has no SpriteRenderer on itself or its children.",
                    "OK");
                return;
            }

            GameObject target = spriteRenderer.gameObject;
            SelectiveSpriteGlow2D glow = target.GetComponent<SelectiveSpriteGlow2D>();
            if (glow == null)
                glow = Undo.AddComponent<SelectiveSpriteGlow2D>(target);

            EditorUtility.SetDirty(target);
            Selection.activeGameObject = target;
            EditorGUIUtility.PingObject(glow);

            Debug.Log(
                "Selective eye glow added. Enter Play Mode, then adjust the glow component if needed.",
                glow);
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateAddToSelectedPlayer()
        {
            return Selection.activeGameObject != null;
        }
    }
}
#endif
