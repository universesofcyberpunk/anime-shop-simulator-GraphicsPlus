# Graphics Plus for Anime Shop Simulator

A MelonLoader mod that improves the picture with the game's own post-processing, as a lighter alternative to ENB or
ReShade. The game uses URP (Unity 2022.3, URP 14). Made by **Fabianens**.

- **Own global Volume** (high priority): exposure, contrast, saturation, temperature, bloom, vignette, tone mapping.
  Values are added on top of the game's own; a setting at 0 leaves the game's value alone.
- **Anti-aliasing** on the main camera: FXAA, SMAA or TAA (with sharpening).
- **SSAO** (ambient occlusion): the URP ScreenSpaceAmbientOcclusion feature is added to the renderer once, then only
  switched on and off.
- **Presets** (e.g. Natural); moving any setting switches to Custom.
- Everything is applied only when a setting changes or a scene loads, never every frame, so it adds no stutter
  (the game is CPU bound).

## Requirements

- MelonLoader 0.7.x
- Optional: Fabianens ModSettings 1.3.9 or newer, to change the settings in game (F10)

## Install

Needs MelonLoader 0.7.x. Download the ready mod from Nexus Mods (or the [Releases](../../releases) page) and extract it
into the game folder, so that `Mods\GraphicsPlus.dll` ends up in the game's `Mods` folder.

Settings are in `UserData\MelonPreferences.cfg` under `[GraphicsPlus]`, or in the F10 menu with ModSettings.

## Build from source

1. Install the [.NET 6 SDK or newer](https://dotnet.microsoft.com/download) and run the game once with MelonLoader 0.7.x
   (that generates `MelonLoader\Il2CppAssemblies`, which the project references).
2. Build:

```bat
dotnet build -c Release
:: game not in the default Steam folder?
dotnet build -c Release -p:GameDir="D:\Games\Anime Shop Simulator"
```

The DLL is copied to the game's `Mods` folder (close the game first). The game's own DLLs are not in this repository.

| File | What it is |
|---|---|
| `GraphicsPlus.cs` | the whole mod: volume overrides, presets, anti-aliasing, SSAO feature |

## Licence

MIT, see `LICENSE`. If you reuse the code, credit **Fabianens**.
