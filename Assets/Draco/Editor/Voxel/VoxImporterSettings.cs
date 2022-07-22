using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Draco.Editor.Voxel {
public class VoxImporterSettings : ScriptableObject {
    public const bool DEFAULT_OPTIMIZE_MESH = true;
    public const bool DEFAULT_GENERATE_PALETTE_ALWAYS = false;
    public const int DEFAULT_VOXELS_PER_UNIT = 10;

    public static bool OptimizeMesh => Instance?.optimizeMesh ?? DEFAULT_OPTIMIZE_MESH;
    public static bool GeneratePaletteAlways => Instance?.generatePaletteAlways ?? DEFAULT_GENERATE_PALETTE_ALWAYS;
    public static Texture2D PaletteOverride => Instance?.paletteOverride;
    public static int VoxelsPerUnit => Instance?.voxelsPerUnit ?? DEFAULT_VOXELS_PER_UNIT;
    
    [SerializeField, Tooltip("The default setting for whether models should be generated with full voxel geometry or an optimized one.")]
    private bool optimizeMesh = DEFAULT_OPTIMIZE_MESH;
    [SerializeField, Tooltip("Whether to still generate the palette texture for models when the palette override texture has been provided.")]
    private bool generatePaletteAlways = DEFAULT_GENERATE_PALETTE_ALWAYS;
    [SerializeField, Tooltip("A texture to use as the palette for all imported models instead of the built in palette.")]
    private Texture2D paletteOverride;
    [SerializeField, Tooltip("The default voxels per Unity world unit to import all new models at.")]
    private int voxelsPerUnit = DEFAULT_VOXELS_PER_UNIT;

    public static VoxImporterSettings Instance {
        get {
            var assetGuid = AssetDatabase.FindAssets("t:VoxImporterSettings").FirstOrDefault();
            return string.IsNullOrWhiteSpace(assetGuid) ? null : AssetDatabase.LoadAssetAtPath<VoxImporterSettings>(AssetDatabase.GUIDToAssetPath(assetGuid));
        }
    }
}
}