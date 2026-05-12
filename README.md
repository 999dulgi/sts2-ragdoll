# Ragdoll

Add ragdoll physics to enemy!

![manifest](./mod_manifest.webp)

## Settings

Click the **Ragdoll Settings** button on the mod's info screen to open the settings panel.

---

### General

| Setting | Description |
| --- | --- |
| **Zero Gravity** | Disables gravity. Bodies and parts float freely and bounce off screen boundaries instead of falling. |
| **Forced Explosion Mode** | Forces all enemies to use Explosion mode regardless of their individual configuration. |
| **Overkill Force** | Applies extra launch force based on how much overkill damage was dealt. |

---

### Spine Ragdoll

Controls the Spine skeleton ragdoll that plays for most enemies.

| Setting | Description |
| --- | --- |
| **Gravity** | Downward acceleration applied to the body. |
| **Speed** | Base launch speed of the body. |
| **Direction Degree** | Base launch angle in degrees. Uses Godot 2D screen coordinates: 0° = right, 90° = down, 180° = left, 270° = up. |
| **Spread Degree** | Random spread applied around the direction. The final angle is `Direction ± (Spread / 2)`. Set to 0 for a fixed direction. |
| **Angular Speed** | Rotational velocity of the body. Negative values spin in the opposite direction. |

---

### Explosion

Controls the flying sprite parts for enemies configured to use Explosion mode.

| Setting | Description |
| --- | --- |
| **Gravity** | Downward acceleration applied to each part. |
| **Speed** | Base launch speed of each part. |
| **Direction Degree** | Base launch angle in degrees. Same coordinate system as Spine Ragdoll direction. |
| **Spread Degree** | Random spread applied around the direction. Each part picks its own random angle within the spread. |
| **Angular Speed** | Rotational velocity of each part. Negative values spin in the opposite direction. |
| **Exclude Small Part when Explode** | Excludes small sprite parts from the explosion. Reduces visual clutter from tiny fragments. |

## Build

### Prerequisites

- [Godot 4.5.1 .NET](https://godotengine.org/download/)
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- Slay the Spire 2 installed via Steam

### Setup

The project references `sts2.dll` at the default Steam install path:

```text
C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64\sts2.dll
```

If your game is installed elsewhere, update the `HintPath` in [ragdoll.csproj](ragdoll.csproj).

### Building

Open the project in Godot and use **Project → Build** (or press `Alt+B`), or run from the terminal:

```sh
dotnet build ragdoll.csproj
```

After a successful build, the DLL is automatically copied to:

```text
C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\ragdoll\
```

---
