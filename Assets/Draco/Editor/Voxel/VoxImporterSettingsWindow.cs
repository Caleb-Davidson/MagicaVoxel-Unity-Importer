using System.IO;
using UnityEditor;

namespace Draco.Editor.Voxel {
public class VoxImporterSettingsWindow : EditorWindow {
    
    [MenuItem ("Window/MagicaVoxel Import Settings")]
    private static void Open() => GetWindow(typeof (VoxImporterSettingsWindow), false, "MagicaVoxel Import Settings");

    private UnityEditor.Editor inspector;
    private void OnGUI () {
        var settings = VoxImporterSettings.Instance;
        if (settings == null) {
            settings = CreateInstance<VoxImporterSettings>();

            var path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(settings)));
            path = Path.Combine(path, "Default MagicaVoxel Import Settings.asset"); 
            AssetDatabase.CreateAsset(settings, path);
        }

        if (inspector != null)
            DestroyImmediate(inspector);

        inspector = UnityEditor.Editor.CreateEditor(settings);
        inspector.OnInspectorGUI();
    }
    
    private void OnDisable() {
        DestroyImmediate(inspector);
        inspector = null;
    }
    
}
}