using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Draco.Editor.Voxel {
[ScriptedImporter(150, "vox")]
public class VoxImporter : ScriptedImporter {
    [SerializeField, Tooltip("Override the default settings for this model and use these instead.")]
    private bool overrideDefaults = false;
    [SerializeField, Tooltip("The default setting for whether models should be generated with full voxel geometry or an optimized one.")]
    private bool optimizeMesh = VoxImporterSettings.DEFAULT_OPTIMIZE_MESH;
    [SerializeField, Tooltip("Whether to still generate the palette texture for models when the palette override texture has been provided.")]
    private bool generatePaletteAlways = VoxImporterSettings.DEFAULT_GENERATE_PALETTE_ALWAYS;
    [SerializeField, Tooltip("A texture to use as the palette for all imported models instead of the built in palette.")] 
    private Texture2D paletteOverride;
    [SerializeField, Tooltip("The default voxels per Unity world unit to import all new models at")]
    private int voxelsPerUnit = VoxImporterSettings.DEFAULT_VOXELS_PER_UNIT;

    private float voxelScale => 1f / (overrideDefaults ? voxelsPerUnit : VoxImporterSettings.VoxelsPerUnit); 
    
    public override void OnImportAsset(AssetImportContext ctx) {
        var voxFile = VoxFile.Open(ctx.assetPath);

        if ((overrideDefaults ? generatePaletteAlways : VoxImporterSettings.GeneratePaletteAlways)
         || (overrideDefaults ? paletteOverride : VoxImporterSettings.PaletteOverride) == null) {
            ctx.AddObjectToAsset(voxFile.Palette.name, voxFile.Palette);
        }

        var modelTree = voxFile.CalculateModelTree();
        // We only want to offset for the Y position. We want X / Z to be centered on the gameobject
        // But we need to move Y so that the default Y positioning matches what it was in MagicaVoxel
        var rootPosition = new Vector3Int {
            y = modelTree[0].Position.y
        };
        if (voxFile.ModelCount > 1) {
            var modelParent = new GameObject(Path.GetFileNameWithoutExtension(ctx.assetPath));
            foreach (var node in modelTree.Skip(1)) {
                var modelPrefab = CreateModelAssets(ctx, voxFile, node.ModelId, true, rootPosition, node.Rotation);
                modelPrefab.transform.SetParent(modelParent.transform, false);
                modelPrefab.transform.localPosition = (Vector3)node.Position * voxelScale;
            }
            ctx.AddObjectToAsset(modelParent.name, modelParent);
            ctx.SetMainObject(modelParent);
        } else {
            var modelPrefab = CreateModelAssets(ctx, voxFile, 0, false, rootPosition, modelTree[0].Rotation);
            ctx.SetMainObject(modelPrefab);            
        }
        
        materialsCache.Clear();
    }
    
    private GameObject CreateModelAssets(AssetImportContext ctx, VoxFile voxFile, int modelId, bool isMultiModel, Vector3Int rootPosition, Quaternion rotation) {
        var meshData = new VoxelGreedyMesher(voxFile.GetVoxels(modelId)).BuildMeshData(voxelScale, overrideDefaults ? optimizeMesh : VoxImporterSettings.OptimizeMesh, rootPosition, rotation);
        var mesh = meshData.BuildMesh();
        if (isMultiModel) 
            mesh.name += $" ({modelId})";
        ctx.AddObjectToAsset(mesh.name, mesh);

        var materials = meshData.GetMaterialIds().Select(materialId => GetOrCreateMaterial(materialId, voxFile, ctx)).ToArray();

        var modelPrefab = CreateModelPrefab(mesh, materials);
        modelPrefab.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
        if (isMultiModel) 
            modelPrefab.name += $" ({modelId})";
        ctx.AddObjectToAsset(modelPrefab.name, modelPrefab);
        return modelPrefab;
    }

    private readonly Dictionary<int, Material> materialsCache = new();
    private Material GetOrCreateMaterial(int materialId, VoxFile voxFile, AssetImportContext ctx) {
        if (materialsCache.ContainsKey(materialId))
            return materialsCache[materialId];
        
        var palette = overrideDefaults
            ? (paletteOverride != null ? paletteOverride : voxFile.Palette)
            : (VoxImporterSettings.PaletteOverride != null ? VoxImporterSettings.PaletteOverride : voxFile.Palette);
        var material = VoxMaterialGenerator.CreateMaterialFor(materialId, voxFile.GetMaterial(materialId), palette);
        ctx.AddObjectToAsset(material.name, material);
        materialsCache[materialId] = material;
        return material;
    }

    private static GameObject CreateModelPrefab(Mesh mesh, Material[] materials) {
        var model = new GameObject();
        var meshFilter = model.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        var meshRenderer = model.AddComponent<MeshRenderer>();
        meshRenderer.materials = materials;
        return model;
    }
}
}