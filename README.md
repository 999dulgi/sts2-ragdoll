# Ragdoll

Enemies explode into flying parts when they die.

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
