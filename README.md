<p align="center"><img src="assets/branding/logo-256.png" width="128" alt="Portrait Builder logo: a crowned figure in a gilt ring"></p>

# Portrait Builder (CK2), a modernized fork

> ## This version was coded entirely by AI
> Every change that separates this fork from the code it started from was written by an AI model: **Claude, made by Anthropic**. That covers the new interface, the picture tiles, saving and exporting, the move to current .NET, the logo, and this README.
> The project owner directed the work in conversation and tried the results. They did not write the code.
> It was tested by running the program. **No person has reviewed it line by line.** Please treat it as AI-generated software: useful, but unaudited.
>
> The older code underneath was written by people, and they deserve the credit for it: see [Lineage](#lineage).

A Windows tool that builds **Crusader Kings II** character portraits. Pick a look from pictures instead of dropdowns, then save the image or copy the DNA strings that CK2 character files use.

It draws portraits from **your own CK2 install**. No game art is included here.

## What it does

- **Pick from pictures.** Race, hair, clothes, headgear, background, face features, rank and government are all grids of tiles. Each tile is your current portrait with that one option applied, so you see what a click will change.
- **Roles.** One click sets rank and government for a kind of person (priest, merchant, king, warlord and so on) and rolls a fresh look. Government is what dresses a priest or merchant; a "knight" is only a rank frame.
- **Randomize** the features you tick, with undo and redo. A strip of recent portraits stays under the picture.
- **Keep and Saved.** Keep a portrait and bring it back later. The portrait you closed with is restored on the next start.
- **Export** as PNG at 1x to 8x, with an optional transparent background and no frame. Save 10 random portraits to a folder, or a contact sheet of 20.
- **DNA in and out.** Copy, paste (Ctrl+V) and import the dna and properties strings.
- **DLC and mods.** Reads portraits from the base game, DLC and mod folders, as the earlier versions did.
- **Looks.** Plain Windows, following the system light or dark setting, or an optional parchment-and-gilt "Medieval" style.

## What you need

- Windows 10 or 11 (this fork is Windows only; the Mono, Linux and Mac support of the older versions was not carried over)
- Crusader Kings II installed (Steam is what it was tested with)
- To build it: the [.NET 10 SDK](https://dotnet.microsoft.com/download)

There is no download of a ready-made program. Build it yourself:

```bash
dotnet build src/PortraitBuilder.WinForms -c Release
```

Run `src/PortraitBuilder.WinForms/bin/Release/net10.0-windows/PortraitBuilder.WinForms.exe`. The first time, it asks for your CK2 game file (for example `CK2game.exe` in the Steam folder) and remembers the folder in `gamedir.txt`.

**Keep the folder path short.** Windows refuses to open a program whose full path passes 260 characters; this happened during development.

## Keys

| Key | Action |
|---|---|
| Ctrl+R or F5 | Randomize the ticked features |
| Ctrl+Z, Ctrl+Y | Undo, redo |
| Ctrl+D | Keep this portrait in Saved |
| Ctrl+S | Save the image |
| Ctrl+Shift+C | Copy the image |
| Ctrl+V | Paste dna and properties |
| F1 | All shortcuts |

Less-used things (image size, export options, batches, DNA actions, DLC and mods, style, appearance) are in the **More** menu.

## Files it writes

Next to the program: `gamedir.txt` (your CK2 folder), `settings.txt` (window, options, last portrait), `saved.txt` (Keep), `log.txt`, and a `dlc` folder it creates. None of these are meant for version control (see `.gitignore`).

## Honest limits

- It is **not** reviewed by a person, and has only been run on one Windows 11 machine with one CK2 install (three DLCs).
- The tests are few: five small unit tests. Most of the checking was done by running the program by hand.
- Many face tiles look nearly the same. The game's face sprites differ only slightly at this size.
- Clothing has no labels in the game files, so roles cannot pick armour or robes by themselves.
- Tooltip-style hover cards on the recent-portraits strip were never seen working on screen.
- `src/PortraitBuilder.Online`, `src/PortraitBuilder.ContentPacks` and `src/SpritePackGenerator` come from upstream. I did not build, test or change them, and the Online project did not build with current tools. They may be broken.
- The build is a plain Debug or Release build; there is no installer and no signed release.

## Lineage

This is a fork of a fork of a fork, and the full git history is kept, so every earlier author is still credited on their own commits. From the history of [scorpdx/portraitbuilder](https://github.com/scorpdx/portraitbuilder), at the commit this fork started from (`32a132d`, 4 February 2022):

| Commits | Author name in the history |
|---:|---|
| 198 | J. Zebedee (the `scorpdx` repository: SkiaSharp and newer .NET) |
| 96 | rquinio / romain.quinio (2.x and 3.x; "Romulien" in the changelog) |
| 4 | Measter (PortraitBuilder 1.x, the original) |
| 2 | Stuart Haidon |

The AI-written changes are separate commits on top of that history, so `git log` shows exactly where the human work ends and the AI work begins. See [CREDITS.md](CREDITS.md) for libraries and details. The earlier README is kept in [docs/UPSTREAM-README.md](docs/UPSTREAM-README.md).
## Licence

Free software under the **GNU GPL v3** ([COPYING](COPYING)), with the **GNU LGPL v3** ([COPYING.LESSER](COPYING.LESSER)) as shipped by the upstream project. I have not worked out which files fall under which; both files are kept as found. The AI-written changes are offered under the same terms.

*Crusader Kings* is a trademark of Paradox Interactive. This project is not made, endorsed or supported by Paradox. The portrait art belongs to Paradox, which is also why this README has no screenshots: they would reproduce that art.

## Branding

The logo (a crowned figure inside a gilt ring) is original artwork, also drawn by the AI, using nothing from CK2. Source and generator: `assets/branding/` (`logo.svg`, `make-branding.ps1`, icons, social preview image).

## Contributing

Issues and pull requests are welcome. If you use an AI tool for a change, say so in the pull request. If you review any of the code, please say what you checked.
