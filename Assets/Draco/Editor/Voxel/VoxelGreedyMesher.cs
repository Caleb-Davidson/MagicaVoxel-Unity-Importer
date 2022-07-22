using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

/***
 * Greedy meshing algorithm based on an implementation found here https://gist.github.com/Vercidium/a3002bd083cce2bc854c9ff8f0118d33
 * which was itself base on an implementation found here https://0fps.net/2012/06/30/meshing-in-a-minecraft-game/
 * Principle changes aside from using variable names longer than one letter were to work on voxel faces rather than the voxel itself
 * And adding support for transparent voxels
 */
namespace Draco.Editor.Voxel {
internal class VoxelGreedyMesher {
    private readonly Voxel[,,] voxels;
    private readonly Vector3Int gridSize;

    public VoxelGreedyMesher(Voxel[,,] voxels) {
        this.voxels = voxels;
        this.gridSize = new Vector3Int(voxels.GetLength(0), voxels.GetLength(1), voxels.GetLength(2));
    }

    public MeshData BuildMeshData(float voxelScale, bool greedyMesh, Vector3Int offset, Quaternion rotation) {
        var meshData = new MeshData();
        
        // Go through the model from each direction
        for (var d = 0; d < 6; d++) {
            // We need to keep track of which direction is forward/backward to handle the correct triangle order
            var isForwardDirection = d < 3;
            var plane = d % 3;
            var axis1 = (d + 1) % 3;
            var axis2 = (d + 2) % 3;

            var pivot = new Vector3Int {
                x = gridSize.x / 2,
                y = gridSize.y / 2,
                z = gridSize.z / 2,
            };
            var position = Vector3Int.zero;
            var searchVector = new Vector3Int {
                [plane] = isForwardDirection ? 1 : -1
            };

            // Search includes one voxels outside the grid in every direction to include the outside faces
            for (
                position[plane] = (isForwardDirection ? -1 : gridSize[plane]);
                isForwardDirection ? position[plane] < gridSize[plane] : position[plane] > -1;
                position[plane] += isForwardDirection ? 1 : -1
            ) {
                var faceShouldBeDrawn = new bool[gridSize[axis1], gridSize[axis2]];

                for (position[axis1] = 0; position[axis1] < gridSize[axis1]; position[axis1]++) {
                    for (position[axis2] = 0; position[axis2] < gridSize[axis2]; position[axis2]++) {
                        var currentVoxel = GetVoxel(position);
                        var nextVoxel = GetVoxel(position + searchVector);

                        faceShouldBeDrawn[position[axis1], position[axis2]] = 
                            currentVoxel != Voxel.Air && nextVoxel == Voxel.Air
                            || (currentVoxel!= Voxel.Air && nextVoxel.MaterialType == MaterialType.Glass && currentVoxel != nextVoxel);
                    }
                }
                
                // X & Y for easy following of the algorithm, it doesn't really matter that X & Y won't necessarily match the world X & Y
                // But since the algorithm works on a plane at a time, it's easy to think about traversing that plane in terms of X & Y
                for (var x = 0; x < gridSize[axis1]; x++) {
                    for (var y = 0; y < gridSize[axis2];) {
                        if (!faceShouldBeDrawn[x, y]) {
                            y++;
                            continue;
                        }

                        Vector3Int GetChunkPosition(int axis1Value, int axis2Value) => new() {
                            [plane] = position[plane],
                            [axis1] = axis1Value,
                            [axis2] = axis2Value
                        };
                        var startVoxel = GetVoxel(GetChunkPosition(x, y));

                        var quadSize = Vector2Int.one;
                        if (greedyMesh) {
                            // Compute the width and height of the quad
                            // Voxels will be connected if they both should be drawn and they are the same type
                            for (; y + quadSize.y < gridSize[axis2]; quadSize.y++) {
                                if (!faceShouldBeDrawn[x, y + quadSize.y] || startVoxel != GetVoxel(GetChunkPosition(x, y + quadSize.y)))
                                    break;
                            }

                            for (; x + quadSize.x < gridSize[axis1]; quadSize.x++) {
                                if (quadSize.y == 1) {
                                    if (!faceShouldBeDrawn[x + quadSize.x, y] || startVoxel != GetVoxel(GetChunkPosition(x + quadSize.x, y)))
                                        goto end_search;
                                } else {
                                    for (var k = y; k < y + quadSize.y; k++) {
                                        if (!faceShouldBeDrawn[x + quadSize.x, y] || startVoxel != GetVoxel(GetChunkPosition(x + quadSize.x, k)))
                                            goto end_search;
                                    }
                                }
                            }
                            end_search: ;
                        }

                        var deltaAxis1 = new Vector3 {
                            [axis1] = quadSize.x
                        };
                        var deltaAxis2 = new Vector3 {
                            [axis2] = quadSize.y
                        };

                        var point = new Vector3Int {
                            [plane] = position[plane] + (isForwardDirection ? 1 : 0),
                            [axis1] = x,
                            [axis2] = y
                        };
                        void AddVertexPoint(Vector3 vertexPoint) {
                            meshData.Vertices.Add(((rotation * (vertexPoint - pivot)) + offset) * voxelScale);
                        }
                        AddVertexPoint(isForwardDirection ? point : point + deltaAxis1 + deltaAxis2);
                        AddVertexPoint(point + deltaAxis1);
                        AddVertexPoint(isForwardDirection ? point + deltaAxis1 + deltaAxis2 : point);
                        AddVertexPoint(point + deltaAxis2);

                        List<int> triangles = meshData.GetTrianglesForVoxelSubMesh(startVoxel);
                        triangles.Add(meshData.Vertices.Count - 4);
                        triangles.Add(meshData.Vertices.Count - 3);
                        triangles.Add(meshData.Vertices.Count - 2);

                        triangles.Add(meshData.Vertices.Count - 4);
                        triangles.Add(meshData.Vertices.Count - 2);
                        triangles.Add(meshData.Vertices.Count - 1);

                        var normal = new Vector3 {
                            [plane] = isForwardDirection ? 1 : -1
                        };
                        meshData.Normals.AddRange(new[] { normal, normal, normal, normal });

                        var uv = new Vector2((startVoxel.Id + 0.5f) / 256, 0.5f);
                        meshData.UVs.AddRange(new[] { uv, uv, uv, uv });
                        
                        // Clear this part of the mask, so we don't add duplicate faces
                        for (var wDelta = 0; wDelta < quadSize.x; wDelta++)
                            for (var hDelta = 0; hDelta < quadSize.y; hDelta++)
                                faceShouldBeDrawn[x + wDelta, y + hDelta] = false;
                                
                        // Increment the counter by how many voxels we processed
                        y += quadSize.y;
                    }
                }
            }
        }

        return meshData;
    }

    private Voxel GetVoxel(Vector3Int position) {
        return IsInBounds(position) ? voxels[position.x, position.y, position.z] : Voxel.Air;
    }
    
    private bool IsInBounds(Vector3Int position) {
        return position.x >= 0 && position.x < voxels.GetLength(0)
         && position.y >= 0 && position.y < voxels.GetLength(1)
         && position.z >= 0 && position.z < voxels.GetLength(2);
    }
}

internal class MeshData {
    public readonly List<Vector3> Vertices = new();
    public readonly List<Vector3> Normals = new();
    public readonly List<Vector2> UVs = new();
    // one sub-mesh for all the diffuse voxels (id: 0 since that is an impossible color index)
    // one sub-mesh for each other material
    private readonly List<KeyValuePair<int, List<int>>> trianglesBySubMesh = new() { new KeyValuePair<int, List<int>>(0, new List<int>()) };

    public List<int> GetTrianglesForVoxelSubMesh(Voxel voxel) {
        if (voxel.MaterialType == MaterialType.Diffuse) {
            return trianglesBySubMesh[0].Value;
        } else {
            if (trianglesBySubMesh.All(kvp => kvp.Key != voxel.Id))
                trianglesBySubMesh.Add(new KeyValuePair<int, List<int>>(voxel.Id, new List<int>()));
            return trianglesBySubMesh.First(kvp => kvp.Key == voxel.Id).Value;
        }
    }

    public IReadOnlyList<int> GetMaterialIds() {
        return trianglesBySubMesh.Select(kvp => kvp.Key).ToList();
    }
    
    public Mesh BuildMesh() {
        var mesh = new Mesh {
            name = "Mesh",
            indexFormat = Vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16,
            vertices = Vertices.ToArray(),
            subMeshCount = trianglesBySubMesh.Count,
            normals = Normals.ToArray(),
            uv = UVs.ToArray(),
        };

        for (var subMeshIndex = 0; subMeshIndex < trianglesBySubMesh.Count; subMeshIndex++) {
            mesh.SetTriangles(trianglesBySubMesh[subMeshIndex].Value.ToArray(), subMeshIndex);
        }
        
        mesh.Optimize();
        return mesh;
    }
}
}