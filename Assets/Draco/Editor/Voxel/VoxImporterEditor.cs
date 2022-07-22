using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Draco.Editor.Voxel {
[CustomEditor(typeof(VoxImporter))]
public class VoxImporterEditor : ScriptedImporterEditor
{
    public override void OnInspectorGUI() {
        serializedObject.Update();
        var overrideProperty = serializedObject.FindProperty("overrideDefaults");

        if (overrideProperty.boolValue) {
            DrawDefaultInspector();
        } else {
            EditorGUILayout.PropertyField(overrideProperty);
            GUI.enabled = false;
            if (VoxImporterSettings.Instance != null) {
                var settings = new SerializedObject(VoxImporterSettings.Instance);
                EditorGUILayout.PropertyField(settings.FindProperty("optimizeMesh"));
                EditorGUILayout.PropertyField(settings.FindProperty("generatePaletteAlways"));
                EditorGUILayout.PropertyField(settings.FindProperty("paletteOverride"));
                EditorGUILayout.PropertyField(settings.FindProperty("voxelsPerUnit"));
            } else {
                EditorGUILayout.HelpBox("No project import settings found. Use the MagicaVoxel Settings Window to set them.", MessageType.Warning);
            }
            GUI.enabled = true;
        }
        
        serializedObject.ApplyModifiedProperties();
        ApplyRevertGUI();
    }
}
}