# MagicaVoxel-Unity-Importer
Add "native" support for importing .vox files in Unity.

Vox files will be automatically imported when added to the project just like other 3d models. There are several options that can be configure for the import process, including:
- Whether to override the palette included in the model
- Whether to have a project wide palette texture instead of importing the embedded texture in every model
- What voxel to Unity world unit scale to use
- Whether to optimize the generated mesh using a GreedyMeshing algorithm, or just generate a full voxel mesh

Generated models are ready to drag and drop in the world! No tinkering necessary!

Multiple models in a single VOX file are supported too! The are imported as a collection of models that can be dragged into the world together or individually.



<img src="https://github.com/Caleb-Davidson/MagicaVoxel-Unity-Importer/blob/main/preview.png"/>


## Credit
- korobetski on github for creating the initial importer that this was based of off
  - https://github.com/korobetski/MagicaVoxel-Unity-Importer
- mikolalysenko for writing a working greedy meshing algrithm that I could shamelessly use
  - https://github.com/mikolalysenko/mikolalysenko.github.com/blob/gh-pages/MinecraftMeshes/js/greedy.js
- Vercidium on github for the a reference C# implimentation of the Greedy Meshing algorithm, it helped when I got stuck trying to decipher the origianl algorithm credited above
  - https://gist.github.com/Vercidium/a3002bd083cce2bc854c9ff8f0118d33
- ephtracy on github for MagicaVoxel and the file format documentation
  - https://github.com/ephtracy/voxel-model
- mchorse on github for figuring out how to decode the model rotations from a bit array to a matrix
  - https://github.com/mchorse/blockbuster/blob/e95fc7c08b662b5f1ca221f73a002ca4720b1826/src/main/java/mchorse/blockbuster/api/formats/vox/VoxReader.java#L178