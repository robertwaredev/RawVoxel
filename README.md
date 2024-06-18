# RawVoxel
A C# voxel world addon built for Godot 4.2.

Made with love by Robert A. Ware.

## Features

- Basic multi-threading support.
- Complete in-editor functionality.
- Adjustable world size and draw distance.
- Optional "center chunk" boolean for odd or even world diameters.
- Inspector control over terrain generation via noise sampling and curve remapping. (Minecraft inspired)
- Basic biome generation support. Functional, but ugly due to lack of any form of biome blending.
- Two different meshing algorithms: Greedy and Naive.

### Godot Resources

The following section lists functional inspector paramters for each resource. Non-functional parameters are not exported, so everything should be safe to use.
Many Resources also contain static methods for generating or manipulating them.

#### Voxel Resource
    
No need to complipcate this.

    - Color

#### Biome Resource
    
Acts a way to define Biomes using a variety of parameters and native Godot resources. Voxel height noise is a Biome-specific noise value which is sampled and handled in a unique way by each child BiomeLayer. Because voxel density (solid vs air) must be sampled continuously across all BiomeLayers, each Biome handles density noise and distribution at a top level before sampling BiomeLayers for height distribution. Temperature and humidty min and max values determine if this Biome is a valid candidate when sampling the WorldSettings Resource's temperature and humidity.

    - Voxel density noise (FastNoiseLite Resource)
    - Voxel density distribution (Curve Resource)
    - Voxel height noise (FastNoiseLite Resource)
    - Temperature and humidity minimums and maximums (floats)
    - Array of BiomeLayer Resources

#### BiomeLayer Resource
    
Acts as a vertical "layer" of voxels along the Y axis. The selected Voxel Resource acts as the fill element, while the height distribution samples the parent Biome's height noise and produces and interpolated range along the Y axis that the specified voxel may fill. Therefore, each BiomeLayer may individually redistribute the height noise sample of its parent Biome to any range of heights, which are specified by the distribution curve minimum and maximum values. This allows for BiomeLayer Resources to have height values that can intersect each other.

    - Voxel Resource
    - Height distribution (Curve Resource)

#### WorldSettings Resource
    
Acts as a "library" for created Voxel and Biome Resources, as well as the temperature and humidity World Attribute Resources needed to sample and select Biome Resources. Currently biomes are selected via temperature and humidity, with temperature controlling the X axis and humidity controlling the Z axis.

    - Array of Voxel Resources
    - Array of Biome Resources
    - WorldAttribute Resource for temperature
    - WorldAttribute Resource for humidity

#### WorldAttribute Resource
    
Acts as a way to remap and interpolate the distrbution of noise samples across one of the X, Y, or Z axes. Distrbution takes into account the diameter of the world in chunk units on a given axis, and remaps any input position along that axis to a [0, 1] range. Range then samples distribution and produces an interpolated range of floats inclusive of any range you'd like via specifying the curve min and max outputs. This allows you to distribute any range of values you'd like across secific axes. These are primarily used in sampling distribution of Biome Resources aross the X and Z axes.

    - Distribution (Curve Resource)
    - Range (Curve Resource)

### Surface Class

A simple class embodiment of the Godot concept of "mesh surfaces". Contains lists of vertices, normals, uvs, and indices.

### Binary Greedy Meshing
    
The most important part of this project, and the current main focus of development.

- Seperate mesh data into six Surfaces, one for each axis sign
- Only pushes geometry into Surfaces for axis signs that are visible to the Focus Node. Can be disabled in the inspector via the "Cull Geometry" boolean.
- Prevents geometry culling in an area round the Focus Node to prevent clipping into a chunk when crosssing into it while facing away from it.

### Standard Naive Meshing

Every voxel gets its own triangles. Includes naive face culling.

- Does what it says, no more, no less.
- Takes AGES compared to binary greedy mesher, but is included as an option for comparing to better algorithms if nothing else.

## TO DO / KNOWN ISSUES

- Fix the Generate button such that it can free chunks before re-meshing if the draw radius is decreased.
- Create texture sampling for the binary greedy mesher, as when using this algorithm there is no visual differentiation between voxels yet, despite having the data structure set up to do so.
- Update the naive mesher to take advantage of axis sign optimizations.
- Proper thread queue for chunk parallelization.
- Fix VoxelPicker class as it relied on a previous data structure.
- Investigate Godot's RenderingDevice for potential memory optimizations for chunk mesh data.
- Biome blending.
- Multiplayer funtionality.

## Usage

Clone it into your Godot project's addons folder like any other addon.
Drag World.cs onto a Node3D and pick a Focus Node via the inspector to generate the world around. There's an included "World.tscn" scene with a simple example.
