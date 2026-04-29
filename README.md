# Ragdoll

Enemies explode into flying parts when they die.

Each body part is extracted from the enemy's Spine atlas and launched in a random direction with gravity, bounce, and rotation. Parts decelerate over time and fade out once they stop.

## Features

- Works on all enemies with Spine animations
- Parts spawn from the correct bone positions
- Manual physics simulation (gravity, floor bounce, air damping)
- Original enemy body fades out as parts fly away

## Build

```bash
dotnet build
```

The DLL is automatically copied to the STS2 mods folder after building. Close the game before building.
