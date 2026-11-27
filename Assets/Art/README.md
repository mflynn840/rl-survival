# RL Survival 3D art source

The scene is built from Unity 3D meshes and articulated character parts. There are no reference images used as sprites, billboards, or character textures.

## Reference direction

- **Location:** a warm, slightly faded roadside two-story motel with a pitched roof, room galleries, a tall lit sign, parking court, older parked cars, palms, scrub, and a two-lane road.
- **Player silhouette:** an everyday survivor in a dark hoodie/jacket, shirt, jeans, and boots. The current runtime rig is modular so it can be replaced by a skinned character asset later.
- **Raider silhouette:** an understated dark-suited figure with shirt, tie, short dark hair, and a purposeful walking/running gait.
- **Composition:** the motel is the enemy source. A marked crew plot sits across the street, with the road and parking approach between them.

The original reference images are present in the design conversation, but were not supplied as local files in this workspace. This note records their art direction; it does not substitute the reference images or use them in the game.

## Runtime model source

- `Assets/Scripts/World/RoadsideMapBuilder.cs` builds the motel district, road, parking court, landscape, signs, and crew plot from 3D mesh primitives.
- `Assets/Scripts/World/HumanoidModelBuilder.cs` builds and animates volumetric survivor and suited-raider rigs.
- `Assets/Scripts/World/ProceduralArena.cs` assembles the map, crew core, cast, light, and perspective camera.

This keeps the Beta 2 project self-contained while leaving the map and character construction behind replaceable builder classes for future authored FBX/GLTF assets.
