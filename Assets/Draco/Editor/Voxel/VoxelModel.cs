namespace Draco.Editor.Voxel {
internal record Voxel {
    public readonly byte Id;
    public readonly MaterialType MaterialType;

    public Voxel(byte id, MaterialType materialType) {
        Id = id;
        MaterialType = materialType;
    }

    public static readonly Voxel Air = new(0, (MaterialType)(-1));
}

internal class MaterialData {
    public readonly MaterialType MaterialType;
    public readonly float Emission;
    public readonly float Intensity;
    public readonly float Transparency;
    public readonly float Smoothness;
    public readonly float Metallic;

    public MaterialData(MaterialType materialType, float emission, float intensity, float transparency, float smoothness, float metallic) {
        MaterialType = materialType;
        Emission = emission;
        Intensity = intensity;
        Transparency = transparency;
        Smoothness = smoothness;
        Metallic = metallic;
    }
}

internal enum MaterialType {
    Diffuse = 0,
    Metal = 1,
    Glass = 2,
    Emission = 3
}
}