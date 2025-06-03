# UV Tile Eraser
An NDMF-based Unity tool for VRChat avatars that automatically deletes mesh faces based on UV tile selection during avatar upload.

## Features
- **Automatic face deletion** - Runs during avatar build/upload via NDMF
- **Flexible mesh targeting** - Process multiple meshes from a single component
- **Grid-based selection** - Standard 4x4 or advanced mode (-64 to +64 range)
- **Material filtering** - Target specific materials or exclude others
- **UV channel support** - Works with UV0 through UV7
- **Non-destructive** - Original meshes remain unchanged

## Usage
1. Add component to any GameObject: `Add Component → UV Tile Eraser`
2. Add target SkinnedMeshRenderers to the list
3. Select UV tiles to erase using the grid interface
4. Configure optional material filters
5. Upload avatar - faces are automatically removed

## Requirements
- Unity 2022.3.x
- NDMF (Non-Destructive Modular Framework)
- VRChat SDK

Decent for optimizing avatars by removing hidden geometry under clothing or creating UV-based toggle systems.
Created based on specifications and details from Wosted.
