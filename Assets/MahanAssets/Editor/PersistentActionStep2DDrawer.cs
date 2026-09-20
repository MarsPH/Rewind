#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TimeEcho.Editor
{
    [CustomPropertyDrawer(typeof(PersistentActionStep2D))]
    public sealed class PersistentActionStep2DDrawer : PropertyDrawer
    {
        private const float Gap = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
            {
                return line;
            }

            float height = line + Gap;
            foreach (FieldSpec field in GetFields(property))
            {
                SerializedProperty child = property.FindPropertyRelative(field.Name);
                if (child == null)
                {
                    continue;
                }

                height += EditorGUI.GetPropertyHeight(child, true) + Gap;
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            Rect row = new Rect(
                position.x,
                position.y,
                position.width,
                EditorGUIUtility.singleLineHeight);

            SerializedProperty action = property.FindPropertyRelative("action");
            string summary = action != null
                ? ((PersistentActionKind)action.enumValueIndex).ToString()
                : label.text;
            SerializedProperty note = property.FindPropertyRelative("label");
            if (note != null && !string.IsNullOrWhiteSpace(note.stringValue))
            {
                summary += " — " + note.stringValue;
            }

            property.isExpanded = EditorGUI.Foldout(
                row,
                property.isExpanded,
                label.text + ": " + summary,
                true);

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;
            row.y += row.height + Gap;

            foreach (FieldSpec field in GetFields(property))
            {
                SerializedProperty child = property.FindPropertyRelative(field.Name);
                if (child == null)
                {
                    continue;
                }

                float height = EditorGUI.GetPropertyHeight(child, true);
                row.height = height;
                EditorGUI.PropertyField(row, child, new GUIContent(field.Label), true);
                row.y += height + Gap;
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        private static IEnumerable<FieldSpec> GetFields(SerializedProperty property)
        {
            yield return new FieldSpec("label", "Note");
            yield return new FieldSpec("delay", "Delay After Previous Step");
            yield return new FieldSpec("action", "Action");

            SerializedProperty actionProperty = property.FindPropertyRelative("action");
            PersistentActionKind action = actionProperty != null
                ? (PersistentActionKind)actionProperty.enumValueIndex
                : PersistentActionKind.SetObjectActive;

            switch (action)
            {
                case PersistentActionKind.SetObjectActive:
                    yield return new FieldSpec("targetObject", "Target Object");
                    yield return new FieldSpec("boolValue", "Active");
                    break;

                case PersistentActionKind.SetBehaviourEnabled:
                    yield return new FieldSpec("targetBehaviour", "Target Behaviour");
                    yield return new FieldSpec("boolValue", "Enabled");
                    break;

                case PersistentActionKind.SetColliderEnabled:
                    yield return new FieldSpec("targetCollider", "Target Collider 2D");
                    yield return new FieldSpec("boolValue", "Enabled");
                    break;

                case PersistentActionKind.SetRendererVisible:
                    yield return new FieldSpec("targetRenderer", "Target Renderer");
                    yield return new FieldSpec("boolValue", "Visible");
                    break;

                case PersistentActionKind.SetAnimatorTrigger:
                    yield return new FieldSpec("targetAnimator", "Target Animator");
                    yield return new FieldSpec("parameterName", "Trigger Parameter");
                    break;

                case PersistentActionKind.SetAnimatorBool:
                    yield return new FieldSpec("targetAnimator", "Target Animator");
                    yield return new FieldSpec("parameterName", "Bool Parameter");
                    yield return new FieldSpec("boolValue", "Value");
                    break;

                case PersistentActionKind.ReleaseRigidbody:
                    yield return new FieldSpec("targetBody", "Target Rigidbody 2D");
                    yield return new FieldSpec("floatValue", "Gravity Scale");
                    yield return new FieldSpec("releaseConstraints", "Constraints After Release");
                    break;

                case PersistentActionKind.FreezeRigidbody:
                    yield return new FieldSpec("targetBody", "Target Rigidbody 2D");
                    break;

                case PersistentActionKind.SetRigidbodySimulated:
                    yield return new FieldSpec("targetBody", "Target Rigidbody 2D");
                    yield return new FieldSpec("boolValue", "Simulated");
                    break;

                case PersistentActionKind.SetGravityScale:
                    yield return new FieldSpec("targetBody", "Target Rigidbody 2D");
                    yield return new FieldSpec("floatValue", "Gravity Scale");
                    break;

                case PersistentActionKind.AddImpulse:
                    yield return new FieldSpec("targetBody", "Target Rigidbody 2D");
                    yield return new FieldSpec("vectorValue", "Impulse");
                    break;

                case PersistentActionKind.SetVelocity:
                    yield return new FieldSpec("targetBody", "Target Rigidbody 2D");
                    yield return new FieldSpec("vectorValue", "Velocity");
                    break;

                case PersistentActionKind.MoveToDestination:
                    yield return new FieldSpec("targetTransform", "Target Transform");
                    yield return new FieldSpec("destination", "Destination");
                    yield return new FieldSpec("boolValue", "Copy Rotation");
                    break;

                case PersistentActionKind.PlayAudioCue:
                    yield return new FieldSpec("audioCue", "Audio Cue");
                    yield return new FieldSpec("targetTransform", "Play At (Optional)");
                    break;

                case PersistentActionKind.InvokeCustomEvent:
                    yield return new FieldSpec("customEvent", "Custom Event");
                    break;
            }
        }

        private readonly struct FieldSpec
        {
            public FieldSpec(string name, string label)
            {
                Name = name;
                Label = label;
            }

            public string Name { get; }
            public string Label { get; }
        }
    }
}
#endif
