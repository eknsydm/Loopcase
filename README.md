# Loopcase

A top-down 2D arena brawler built in Unity. You collect swords that spin around your character, then use them to cut down AI opponents before they cut you down. Every blade carves the ground as it passes, so each round leaves its own pattern of scratches across the arena.

## Gameplay

- **Collect blades.** Sword pickups drop around the arena at random times. Walk over one and it joins the ring of blades orbiting your character. More blades means a faster spin.
- **Hit opponents.** A blade that touches another character deals damage and knocks them back. Every character starts with 3 health, and a short invulnerability window stops one hit from counting twice.
- **Clash.** When two blades from different characters meet, both break off. They fly away with sparks and a clang, so a bigger ring doesn't make you untouchable.
- **Loot the fallen.** A defeated character drops a few pickups plus everything they were carrying. The pickups are pulled toward whoever gets close.
- **Carve the arena.** The ground is a scratch-card surface, and every blade cuts a trail through it.
- **Kill cam.** When you die, the camera follows your killer, then whoever kills them, and so on.
- **Identities.** Each fighter is drawn from a roster of countries (Türkiye, USA, Japan, China). They get a flag and a nickname such as `ahmet123`, shown on a nameplate above their health bar.

### Controls

| Action  | Input                                  |
|---------|----------------------------------------|
| Move    | WASD / arrow keys / gamepad left stick |
| Restart | R                                      |

## How it's made

**Engine:** Unity 6 (`6000.5.6f1`) with the Universal Render Pipeline (2D).

**Packages:** Input System, Cinemachine 3, 2D Tilemap + Tilemap Extras, 2D Animation, TextMesh Pro, and the [Scratch Card](https://assetstore.unity.com/packages/slug/228309) asset for the carvable ground.

### Architecture

The code lives in `Assets/Scripts`, grouped by feature:

| Folder          | What it does |
|-----------------|--------------|
| `Arena/`        | `MapGenerator` builds each round procedurally from a seed: a ground tilemap, grass patches placed with Perlin noise, fence walls, a camera bounds collider, and enemy spawns kept clear of the player and of each other. `ArenaScratchCard` stretches the scratch surface over the arena and converts world positions into the plugin's texture space. `ScratchTrail` sweeps a blade's full edge between frames so fast blades don't leave gaps. `KillCam` hands the Cinemachine camera over to the killer when someone dies. |
| `Characters/`   | `PlayerMovement` moves a `Rigidbody2D` with knockback and flips the sprite without mirroring UI. `EnemyAI` re-picks one of four intents every 0.2 s: *seek weapon*, *retreat* (when outgunned), *chase* (when stronger) or *wander*. `CharacterStats` handles health, hits and death events. `CharacterDefinition` and `CharacterRoster` are ScriptableObjects that hold countries, flags and unique nicknames. |
| `Weapons/`      | `WeaponOrbitController` keeps the ring of blades evenly spaced and pools them with `UnityEngine.Pool.ObjectPool`. `OrbitingWeapon` handles damage, knockback and the pop-in animation. `WeaponClash` handles blade-on-blade hits, sparks from one shared particle system, and distance-based volume. |
| `Collectables/` | `CollectableSpawner` places pooled pickups in groups at random intervals inside the arena collider. `CollectableMagnet` makes a pickup flinch away, then shoot toward a nearby collector. `CollectableDropper` scatters pickups in a ring when a character dies. |
| `UI/`           | World-space health bar and flag/nickname nameplate. |
| `Audio/`        | `Earshot` fades sounds with distance from the listener. |

The player and the enemies use the same character prefab. An enemy is that prefab with `EnemyAI` steering `PlayerMovement` in place of player input, so both sides follow the same rules.

### Custom shaders

- `Shaders/ScratchCardInnerShadow.shader` adds an inner shadow along scratched edges, so cuts look recessed into the ground.
- `Shaders/SpriteFlash.shader` flashes a sprite white when the character is hit.

### Editor tooling

Menu items under **Tools → Loopcase** set the scene up automatically:

- **Build Map Generator** connects the generator to the tiles, fence sprites, prefab and camera.
- **Build Arena Scratch Card** creates a scratch card sized to the play area, and bakes a surface texture whose aspect matches the arena.
- **Add Scratch Inner Shadow** / **Rebuild Arena Scratch Surface** are maintenance helpers for the card.

## Running it

1. Install Unity **6000.5.6f1** (or a compatible Unity 6 version) through Unity Hub.
2. Clone the repo and open the folder as a project in Unity Hub.
3. Open `Assets/Scenes/SampleScene.unity` and press Play.
