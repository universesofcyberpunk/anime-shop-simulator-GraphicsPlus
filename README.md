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
- Optional: Fabianens [ModSettings](https://www.nexusmods.com/animeshopsimulator/mods/40) 1.3.9 or newer, to change the settings in game (F10)

## Install

Needs MelonLoader 0.7.x. Download the ready mod from [Nexus Mods](https://www.nexusmods.com/animeshopsimulator/mods/95) or the [Releases](../../releases) page and extract it
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

## More by Fabianens

| Project | What it does | Links |
|---|---|---|
| **Figure Maker + FigureShop** | turn any 3D model into a shop figure (app) and the mod that sells it | [GitHub](https://github.com/universesofcyberpunk/anime-shop-simulator-figure-maker) · [Nexus](https://www.nexusmods.com/animeshopsimulator/mods/82) |
| **Fabianens Mod Market** | MODS screen on the shop computer; API for other mods to list their items | [GitHub](https://github.com/universesofcyberpunk/anime-shop-simulator-mod-market) · [Nexus](https://www.nexusmods.com/animeshopsimulator/mods/31) |
| **Mod Manager** | build / test / package / publish state of all your mods (tool for modders) | [GitHub](https://github.com/universesofcyberpunk/anime-shop-simulator-mod-manager) |
| **Video TV** | wall TVs that play your own videos | [GitHub](https://github.com/universesofcyberpunk/anime-shop-simulator-video-tv) · [Nexus](https://www.nexusmods.com/animeshopsimulator/mods/90) |
| **Graphics Plus** (this repository) | URP colour, anti-aliasing and SSAO presets, an ENB alternative | [GitHub](https://github.com/universesofcyberpunk/anime-shop-simulator-GraphicsPlus) · [Nexus](https://www.nexusmods.com/animeshopsimulator/mods/95) |
| **Free Placement** | place, stack, resize and tilt items anywhere | [GitHub](https://github.com/universesofcyberpunk/anime-shop-simulator-FreePlacement) · [Nexus](https://www.nexusmods.com/animeshopsimulator/mods/47) |
| **FBS Body** | play as your own VRoid character, with FBS Studio | [GitHub](https://github.com/universesofcyberpunk/anime-shop-simulator-FBSBody) · [Nexus](https://www.nexusmods.com/animeshopsimulator/mods/45) |
| **Mod Settings Manager** | the F10 in-game settings menu used by most of these mods | [Nexus](https://www.nexusmods.com/animeshopsimulator/mods/40) |
| **FTPV - First and Third Person View** | see your own body in first person (needed by FBS Body) | [Nexus](https://www.nexusmods.com/animeshopsimulator/mods/32) |

All mods: [Fabianens on Nexus Mods](https://www.nexusmods.com/profile/Fabianens/mods?gameId=10172)

## Licence

MIT, see `LICENSE`. If you reuse the code, credit **Fabianens**.
