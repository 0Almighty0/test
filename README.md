FModel Chinese - Unreal Engine Resource Explorer
------------------------------------------

Simplified Chinese fork of FModel, made for Chinese-speaking users who need a friendlier interface for browsing, previewing, and exporting Unreal Engine game assets.

If this project helps you, please consider giving this repository a Star.

中文说明请看 [README_中文.md](README_中文.md)。

### Description:
FModel Chinese is a Simplified Chinese version based on [FModel](https://github.com/4sval/FModel), an archive explorer for [Unreal Engine](https://www.unrealengine.com/) games. It uses [CUE4Parse](https://github.com/FabianFG/CUE4Parse) as its core parsing library and supports browsing, searching, previewing, and exporting UE4 / UE5 game resources.

This fork keeps the upstream project structure while adding Chinese UI text for common workflows.

### Features:
- Simplified Chinese UI for common menus, dialogs, settings, search, backup manager, AES manager, audio player, image merger, and 3D viewer panels.
- UE4 / UE5 archive browsing and resource searching.
- Texture, model, animation, audio, data table, and text preview/export support, depending on game format support.
- AES key and mapping file configuration.
- Clean source release without local game assets, exported files, logs, private settings, or machine-specific configuration.

### Usage:
1. Run `FModel.exe`.
2. Add the target game's `Content\Paks` directory.
3. Select the correct UE version or game profile.
4. Add AES keys or mapping files if required by the game.
5. Browse, preview, search, or export resources.

### Build:
1. Install the .NET SDK required by the project.
2. Clone this repository with submodules.
3. Open `FModel.slnx` or build from the command line:

```powershell
dotnet publish .\FModel\FModel.csproj -c Release -r win-x64 --self-contained false
```

### Upstream:
This project is based on the original FModel project:

https://github.com/4sval/FModel

### License:
FModel is licensed under GPL-3.0. Please keep `LICENSE` and `NOTICE` when redistributing or modifying this project.

This repository does not provide or encourage redistribution of copyrighted game assets.
