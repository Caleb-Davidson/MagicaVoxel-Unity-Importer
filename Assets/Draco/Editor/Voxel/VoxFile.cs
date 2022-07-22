using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/***
 * Vox fileformat parsing based on an implementation found here https://github.com/korobetski/MagicaVoxel-Unity-Importer/blob/main/Assets/Lunatic/Editor/Voxel/VoxImporter.cs
 */
namespace Draco.Editor.Voxel {
/***
 * https://github.com/ephtracy/voxel-model/blob/master/MagicaVoxel-file-format-vox.txt
 * https://github.com/ephtracy/voxel-model/blob/master/MagicaVoxel-file-format-vox-extension.txt
 */
internal class VoxFile {
    public int Version { get; }
    public Texture2D Palette => paletteChunk.Texture;

    private readonly List<MVxModelChunk> modelChunks = new();
    private readonly List<MVxSizeChunk> sizeChunks = new();
    private MVxPaletteChunk paletteChunk;
    private readonly List<MVxMaterialChunk> materialChunks = new();
    private readonly List<MVxSceneNodeChunk> sceneNodes = new();

    private const string FILE_TYPE_HEADER = "VOX ";
    private const int SUPPORTED_FILE_VERSION = 150;

    public static VoxFile Open(string filePath) {
        var fileStream = File.OpenRead(filePath);
        var voxFile = new VoxFile(fileStream);
        fileStream.Close();
        return voxFile;
    }

    public int ModelCount => modelChunks.Count;
    
    public MaterialData GetMaterial(int id) => materialChunks[id]?.AsMaterialData() ?? MVxMaterialChunk.StandardMaterialData;
    
    public Voxel[,,] GetVoxels(int modelIndex) {
        var modelChunk = modelChunks[modelIndex];
        var size = sizeChunks[modelIndex].Size;
        var voxels = new Voxel[size.x, size.y, size.z];
        for (var x = 0; x < size.x; x++) {
            for (var y = 0; y < size.y; y++) {
                for (var z = 0; z < size.z; z++) {
                    var id = modelChunk.Voxels[x, y, z];
                    voxels[x, y, z] = id == 255 ? Voxel.Air : new Voxel(id, materialChunks[id].MaterialType);
                }
            }

        }
        return voxels;
    }

    public class ModelTreeNode {
        public Vector3Int Position;
        public Quaternion Rotation;
        public int ModelId = -1;
    }

    // The first node is actually the center position of the "Scene" in MagicVoxel
    // Subsequent nodes are translations and rotations for individual models
    public List<ModelTreeNode> CalculateModelTree() {
        var modelTree = new List<ModelTreeNode>();
        ProcessSceneNode(modelTree, 0);
        return modelTree;
    }

    private void ProcessSceneNode(List<ModelTreeNode> modelTree, int sceneNodeIndex) {
        var sceneNode = sceneNodes[sceneNodeIndex];
        switch (sceneNode) {
            case MVxTransformChunk transformChunk: {
                if (transformChunk.Translation != null) {
                    modelTree.Last().Position += transformChunk.Translation.Value;
                }
                // TODO: Handle Rotation
                if (transformChunk.Rotation != null) {
                    modelTree.Last().Rotation = transformChunk.Rotation.Value;
                }
                ProcessSceneNode(modelTree, transformChunk.ChildNodeId);
                break;
            }
            case MVxGroupChunk groupChunk: {
                foreach (var childrenId in groupChunk.ChildrenIds) {
                    var node = new ModelTreeNode();
                    modelTree.Add(node);
                    ProcessSceneNode(modelTree, childrenId);
                }
                break;
            }
            case MVxShapeChunk shapeChunk: {
                // I was unable to replicate having multiple models in a shape chunk. Docs say it should be possible
                // but I can't understand the use case or replicate it. So for now having multiple is unsupported.
                if (shapeChunk.ModelAttributes.Count > 1) {
                    Debug.LogWarning("Having multiple models in a shape chunk isn't supported! VOX file may not be imported properly!");
                }
                modelTree.Last().ModelId = shapeChunk.ModelAttributes.First().modelId;
                break;
            }
        }
    }
    
    private VoxFile(Stream data) {
        var reader = new BinaryReader(data);
        var fileTypeHeader = new string(reader.ReadChars(4));
        if (fileTypeHeader != FILE_TYPE_HEADER)
            throw new ArgumentException("File is not a proper VOX file! Cannot process!");

        Version = reader.ReadInt32();
        if (Version > SUPPORTED_FILE_VERSION) {
            Debug.LogWarning("VOX file version is greater than the supported version, importing may not work!");
        }

        var chunkId = new string(reader.ReadChars(4));
        if (chunkId != MVxMainChunk.CHUNK_ID)
            throw new ArgumentException("Malformed VOX file detected! The first chunk must be the main chunk!");
        var mainChunk = new MVxMainChunk(reader);

        while (!mainChunk.HasReachedEnd(reader)) {
            CreateChunk(reader);
        }
    }

    private void CreateChunk(BinaryReader reader) {
        var chunkId = new string(reader.ReadChars(4));
        switch (chunkId) {
            case MVxSizeChunk.CHUNK_ID:
                sizeChunks.Add(new MVxSizeChunk(reader));
                break;
            case MVxModelChunk.CHUNK_ID:
                modelChunks.Add(new MVxModelChunk(reader, sizeChunks.Last().Size));
                break;
            case MVxPaletteChunk.CHUNK_ID:
                paletteChunk = new MVxPaletteChunk(reader);
                break;
            case MVxMaterialChunk.CHUNK_ID:
                materialChunks.Add(new MVxMaterialChunk(reader));
                break;
            case MVxTransformChunk.CHUNK_ID:
                sceneNodes.Add(new MVxTransformChunk(reader));
                break;
            case MVxGroupChunk.CHUNK_ID:
                sceneNodes.Add(new MVxGroupChunk(reader));
                break;
            case MVxShapeChunk.CHUNK_ID:
                sceneNodes.Add(new MVxShapeChunk(reader));
                break;

            // Not Supported Chunk types, but the need to be processed so that we can continue through the file
            case MVxLayerChunk.CHUNK_ID:
                _ = new MVxLayerChunk(reader);
                break;
            case MVxRenderAttributesChunk.CHUNK_ID:
                _ = new MVxRenderAttributesChunk(reader);
                break;
            case MVxCameraAttributesChunk.CHUNK_ID:
                _ = new MVxCameraAttributesChunk(reader);
                break;
            case MVxPaletteNoteChunk.CHUNK_ID:
                _ = new MVxPaletteNoteChunk(reader);
                break;

            default: throw new ArgumentException($"Encountered unsupported chunk type \"{chunkId}\" while importing vox model");
        }
    }

    private abstract class MVxChunk {
        public int ChunkSize { get; }
        public int ChunkChildrenSize { get; }

        private const int CHUNK_HEADER_LENGTH = 8;
        private long startPosition;

        public MVxChunk(BinaryReader reader) {
            startPosition = reader.BaseStream.Position;

            ChunkSize = reader.ReadInt32();
            ChunkChildrenSize = reader.ReadInt32();
        }

        public bool HasReachedEnd(BinaryReader reader) {
            return reader.BaseStream.Position >= startPosition + CHUNK_HEADER_LENGTH + ChunkSize + ChunkChildrenSize;
        }

        protected static IReadOnlyDictionary<string, string> ReadDictionary(BinaryReader reader) {
            var itemCount = reader.ReadInt32();
            var dictionary = new Dictionary<string, string>();
            for (var i = 0; i < itemCount; i++) {
                var keyLength = reader.ReadInt32();
                var key = Encoding.UTF8.GetString(reader.ReadBytes(keyLength));

                var valueLength = reader.ReadInt32();
                var value = Encoding.UTF8.GetString(reader.ReadBytes(valueLength));

                dictionary[key] = value;
            }
            return dictionary;
        }
    }

    private class MVxMainChunk : MVxChunk {
        public const string CHUNK_ID = "MAIN";

        public MVxMainChunk(BinaryReader reader) : base(reader) { }
    }

    private class MVxSizeChunk : MVxChunk {
        public const string CHUNK_ID = "SIZE";
        public Vector3Int Size { get; }

        public MVxSizeChunk(BinaryReader reader) : base(reader) {
            // VoxMagica uses x,z,y format
            var sizeX = reader.ReadInt32();
            var sizeZ = reader.ReadInt32();
            var sizeY = reader.ReadInt32();

            Size = new Vector3Int(sizeX, sizeY, sizeZ);
        }
    }

    private class MVxModelChunk : MVxChunk {
        public const string CHUNK_ID = "XYZI";
        public int VoxelCount { get; }
        public byte[,,] Voxels { get; }

        public MVxModelChunk(BinaryReader reader, Vector3Int size) : base(reader) {
            VoxelCount = reader.ReadInt32();
            Voxels = new byte[size.x, size.y, size.z];
            // MagicaVoxel treats 0 as an empty voxel, but when it exports it's palettes, it exports clear as 255 and shifts everything else down
            // We want to maintain palette compatibility so that we can use one exported palette rather than one palette per model
            // But that means we need to use 255 as empty, even though that isn't how the model file is saved. So initialize the voxel array to 255
            // And subtract 1 from the id of every voxel we read. 0 isn't written to the file since it's **supposed** to be empty space
            // so **hopefully** no underflow issues
            for (var x = 0; x < size.x; x++) {
                for (var y = 0; y < size.y; y++) {
                    for (var z = 0; z < size.z; z++) {
                        Voxels[x, y, z] = 255;
                    }
                }
            }

            for (var i = 0; i < VoxelCount; i++) {
                // x, z, y, color index
                var x = reader.ReadByte();
                var z = reader.ReadByte();
                var y = reader.ReadByte();
                var colorIndex = reader.ReadByte();
                Voxels[x, y, z] = (byte)(colorIndex - 1);
            }
        }
    }

    private class MVxPaletteChunk : MVxChunk {
        public const string CHUNK_ID = "RGBA";
        public Color32[] Palette { get; }
        public Texture2D Texture { get; }

        /***
         * Vox palettes are weird, a palette is 256 colors, yet there is 257 saved to the file
         * and yet even further still the first and last are always saved as clear, #00000000
         * and since the color index on a voxel is a byte, the 257th is inaccessible anyway
         */
        public MVxPaletteChunk(BinaryReader reader) : base(reader) {
            Palette = new Color32[256];

            for (var i = 0; i < 256; i++) {
                var red = reader.ReadByte();
                var green = reader.ReadByte();
                var blue = reader.ReadByte();
                var alpha = reader.ReadByte();
                Palette[i] = new Color32(red, green, blue, alpha);
            }

            Texture = new Texture2D(256, 1, TextureFormat.RGBA32, false) {
                name = "Palette",
                filterMode = FilterMode.Point
            };
            Texture.SetPixels32(Palette);
            Texture.filterMode = FilterMode.Point;
            Texture.Apply();
        }
    }

    private class MVxMaterialChunk : MVxChunk {
        public const string CHUNK_ID = "MATL";
        public int Id { get; }
        public MaterialType MaterialType { get; }

        public float Emission { get; }
        public float Intensity { get; }
        public float Transparency { get; }
        public float Smoothness { get; }
        public float Metallic { get; }

        private IReadOnlyDictionary<string, string> properties { get; }

        public MVxMaterialChunk(BinaryReader reader) : base(reader) {
            Id = reader.ReadInt32();
            properties = ReadDictionary(reader);

            if (properties.ContainsKey("_type")) {
                MaterialType = properties["_type"] switch {
                    "_diffuse" => MaterialType.Diffuse,
                    "_metal" => MaterialType.Metal,
                    "_glass" => MaterialType.Glass,
                    "_emit" => MaterialType.Emission,
                    _ => MaterialType.Diffuse
                };
            } else {
                MaterialType = MaterialType.Diffuse;
            }

            switch (MaterialType) {
                case MaterialType.Emission: {
                    Emission = properties.TryGetValue("_emit", out var emit) ? float.Parse(emit) : 0;
                    Intensity = properties.TryGetValue("_flux", out var flux) ? int.Parse(flux) / 2f : 0;
                    break;
                }
                case MaterialType.Glass: {
                    Transparency = properties.TryGetValue("_alpha", out var alpha) ? float.Parse(alpha) : 0;
                    Smoothness = properties.TryGetValue("_rough", out var roughness) ? 1 - float.Parse(roughness) : 1;
                    break;
                }
                case MaterialType.Metal: {
                    Smoothness = properties.TryGetValue("_rough", out var roughness) ? 1 - float.Parse(roughness) : 1;
                    Metallic = properties.TryGetValue("_metal", out var metal) ? float.Parse(metal) : 0;
                    break;
                }
                case MaterialType.Diffuse: break;
                default:
                    break;
            }
        }

        public MaterialData AsMaterialData() {
            return new MaterialData(MaterialType, Emission, Intensity, Transparency, Smoothness, Metallic);
        }

        public static MaterialData StandardMaterialData = new(MaterialType.Diffuse, 0, 0, 0, 0, 0);
    }

    private abstract class MVxSceneNodeChunk : MVxChunk {
        public int Id { get; }
        public IReadOnlyDictionary<string, string> Attributes { get; }

        public MVxSceneNodeChunk(BinaryReader reader) : base(reader) {
            Id = reader.ReadInt32();
            Attributes = ReadDictionary(reader);
        }
    }

    private class MVxTransformChunk : MVxSceneNodeChunk {
        public const string CHUNK_ID = "nTRN";
        public int ChildNodeId { get; }
        public int LayerId { get; }
        public IReadOnlyDictionary<string, string>[] Frames { get; }

        public Vector3Int? Translation { get; }
        public Quaternion? Rotation { get; }

        public MVxTransformChunk(BinaryReader reader) : base(reader) {
            ChildNodeId = reader.ReadInt32();
            reader.ReadInt32(); // There is a reserved slot here for future expansion, but it is not used yet.
            LayerId = reader.ReadInt32();
            var frameCount = reader.ReadInt32();

            Frames = new IReadOnlyDictionary<string, string>[frameCount];
            for (var i = 0; i < frameCount; i++) {
                Frames[i] = ReadDictionary(reader);
            }

            if (Frames.Length > 0 && Frames[0].ContainsKey("_t")) {
                // MagicaVoxel uses X Z Y
                var positions = Frames[0]["_t"].Split(" ").Select(int.Parse).ToArray();
                Translation = new Vector3Int(positions[0], positions[2], positions[1]);
            }
            
            if (Frames.Length > 0 && Frames[0].ContainsKey("_r")) {
                Rotation = DecodeRotation(int.Parse(Frames[0]["_r"]));
            }
        }
        
        // Credit to mchorse https://github.com/mchorse/blockbuster/blob/e95fc7c08b662b5f1ca221f73a002ca4720b1826/src/main/java/mchorse/blockbuster/api/formats/vox/VoxReader.java#L178
        // I would not have been able to write this function without this as a reference
        /***
         * The 3x3 rotation matrix is converted to a bit array and saved to the file as an int. The conversion process is as follows:
         *  bit | value 
         *  0-1 | index of the non-zero entry in the first row 
         *  2-3 | index of the non-zero entry in the second row 
         *  4   | the sign in the first row (0 : positive; 1 : negative) 
         *  5   | the sign in the second row (0 : positive; 1 : negative) 
         *  6   | the sign in the third row (0 : positive; 1 : negative)
         *
         * As an example,
         *  0 -1 0
         *  1  0 0
         *  0  0 1
         * is packed into 0b0010001 which is then written into the file as the string "17", i.e. the bytes 0x31 0x37.
         */
        private static Quaternion DecodeRotation(int rotation) {
            var firstIndex  = (rotation & 0b0011);
            var secondIndex = (rotation & 0b1100) >> 2;
            var thirdIndex = 3 - secondIndex - firstIndex;

            // Get the value of the bit (0 || 1) then convert (0 to 1) and (1 to -1);
            var firstSign  = ((rotation & 0b0010000) >> 4) == 0 ? 1 : -1;
            var secondSign = ((rotation & 0b0100000) >> 5) == 0 ? 1 : -1 ;
            var thirdSign  = ((rotation & 0b1000000) >> 6) == 0 ? 1 : -1 ;

            var vectors = new Vector3[] { new(), new(), new() };
            vectors[0][firstIndex] = firstSign;
            vectors[1][secondIndex] = secondSign;
            vectors[2][thirdIndex] = thirdSign;

            var matrix = new Matrix4x4(vectors[0], vectors[1], vectors[2], Vector4.zero);
            var lookRotation = Quaternion.LookRotation(matrix.GetColumn(2), matrix.GetColumn(1));
            // Remap Z / Y because MagicaVoxel uses XZY format
            return Quaternion.Euler(lookRotation.eulerAngles.x, lookRotation.eulerAngles.z, lookRotation.eulerAngles.y);
        }
    }

    private class MVxGroupChunk : MVxSceneNodeChunk {
        public const string CHUNK_ID = "nGRP";
        public int[] ChildrenIds { get; }

        public MVxGroupChunk(BinaryReader reader) : base(reader) {
            var numChildren = reader.ReadInt32();
            ChildrenIds = new int[numChildren];
            for (var i = 0; i < numChildren; i++) {
                ChildrenIds[i] = reader.ReadInt32();
            }
        }
    }

    private class MVxShapeChunk : MVxSceneNodeChunk {
        public const string CHUNK_ID = "nSHP";
        public IReadOnlyList<(int modelId, IReadOnlyDictionary<string, string> attributes)> ModelAttributes { get; }

        public MVxShapeChunk(BinaryReader reader) : base(reader) {
            var numModels = reader.ReadInt32();
            var modelAttributes = new List<(int modelId, IReadOnlyDictionary<string, string> attributes)>();
            for (var i = 0; i < numModels; i++) {
                var id = reader.ReadInt32();
                var attributes = ReadDictionary(reader);
                modelAttributes.Add((id, attributes));
            }
            ModelAttributes = modelAttributes;
        }
    }

    private class MVxLayerChunk : MVxSceneNodeChunk {
        public const string CHUNK_ID = "LAYR";

        public MVxLayerChunk(BinaryReader reader) : base(reader) {
            reader.ReadInt32(); // There is a reserved slot here for future expansion, but it is not used yet.
        }
    }

    private class MVxRenderAttributesChunk : MVxChunk {
        public const string CHUNK_ID = "rOBJ";
        public IReadOnlyDictionary<string, string> Attributes { get; }

        public MVxRenderAttributesChunk(BinaryReader reader) : base(reader) {
            Attributes = ReadDictionary(reader);
        }
    }

    private class MVxCameraAttributesChunk : MVxChunk {
        public const string CHUNK_ID = "rCAM";
        public int CameraId { get; }
        public IReadOnlyDictionary<string, string> Attribtues { get; }

        public MVxCameraAttributesChunk(BinaryReader reader) : base(reader) {
            CameraId = reader.ReadInt32();
            Attribtues = ReadDictionary(reader);
        }
    }

    private class MVxPaletteNoteChunk : MVxChunk {
        public const string CHUNK_ID = "NOTE";

        public MVxPaletteNoteChunk(BinaryReader reader) : base(reader) {
            reader.ReadBytes(ChunkSize);
        }
    }
}
}