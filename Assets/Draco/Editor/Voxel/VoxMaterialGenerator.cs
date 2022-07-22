using System;
using UnityEngine;

namespace Draco.Editor.Voxel {
internal static class VoxMaterialGenerator {
    private static readonly int smoothness = Shader.PropertyToID("_Glossiness");
    private static readonly int specularHighlights = Shader.PropertyToID("_SpecularHighlights");
    private static readonly int metallic = Shader.PropertyToID("_Metallic");
    private static readonly int reflections = Shader.PropertyToID("_GlossyReflections");
    private static readonly int emissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly int emissionTexture = Shader.PropertyToID("_EmissionMap");
    private static readonly int alphaSourceBlend = Shader.PropertyToID("_SrcBlend");
    private static readonly int alphaDestBlend = Shader.PropertyToID("_DstBlend");
    private static readonly int renderingMode = Shader.PropertyToID("_Mode");
    
    public static Material CreateMaterialFor(int id, MaterialData materialData, Texture palette) {
        var type = materialData?.MaterialType ?? MaterialType.Diffuse;
        return type switch {
            MaterialType.Diffuse => CreateDiffuseMaterial(palette),
            MaterialType.Emission => CreateEmissiveMaterial(id, materialData, palette),
            MaterialType.Glass => CreateGlassMaterial(id, materialData, palette),
            MaterialType.Metal => CreateMetalMaterial(id, materialData, palette),
            _ => throw new ArgumentOutOfRangeException($"Encountered unsupported material type \"{type}\" when attempting to generate material.")
        };
    }
    
    private static Material CreateDiffuseMaterial(Texture palette) {
        var mat = new Material(Shader.Find("Standard")) {
            name = "Default Material",
            mainTexture = palette,
            doubleSidedGI = true,
            enableInstancing = true
        };
        mat.SetFloat(smoothness, 0);
        mat.SetFloat(specularHighlights, 0);
        return mat;
    }

    private static Material CreateEmissiveMaterial(int id, MaterialData materialData, Texture palette) {
        var mat = new Material(Shader.Find("Standard")) {
            name = $"Emissive Material ({id})",
            mainTexture = palette,
            enableInstancing = true
        };
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        mat.SetFloat(metallic, 0.7f);
        mat.SetFloat(smoothness, 0);
        mat.SetFloat(reflections, 0);
        mat.SetTexture(emissionTexture, palette);
        mat.SetColor(emissionColor, Color.white * materialData.Intensity);
        return mat;
    }
    
    private static Material CreateGlassMaterial(int id, MaterialData materialData, Texture palette) {
        var mat = new Material(Shader.Find("Standard")) {
            name = $"Glass Material ({id})",
            mainTexture = palette,
            color = new Color(1, 1, 1, 1 - materialData.Transparency),
            doubleSidedGI = true,
            enableInstancing = true
        };
        mat.SetFloat(smoothness, materialData.Smoothness);
        mat.SetInt(alphaSourceBlend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt(alphaDestBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt(renderingMode, 3);
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;
        return mat;
    }
    
    private static Material CreateMetalMaterial(int id, MaterialData materialData, Texture palette) {
        var mat = new Material(Shader.Find("Standard")) {
            name = $"Metal Material ({id})",
            mainTexture = palette,
            doubleSidedGI = true,
            enableInstancing = true
        };
        mat.SetFloat(smoothness, materialData.Smoothness);
        mat.SetFloat(metallic, materialData.Metallic);
        return mat;
    }
}
}