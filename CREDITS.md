# Credits

## Who wrote what

**The AI-written part.** Every change that separates this fork from the code it started from was written by an AI model, Claude, made by Anthropic, working in conversation with the project owner. That includes the interface rework, picture tiles, roles, saving, export, theming, the upgrade to current .NET and current SkiaSharp, the unit test added, the logo and branding files, the README and this file. The owner gave instructions and tried the results but did not write the code. No person has reviewed it line by line.

**The human-written part.** Everything older was written by people. Counts are commits in the history of [scorpdx/portraitbuilder](https://github.com/scorpdx/portraitbuilder) at the commit this fork started from (`32a132d`):

| Author name in the history | Commits | What the files and changelog show |
|---|---:|---|
| **J. Zebedee** | 198 | The fork this one started from: SkiaSharp rendering, newer .NET, the online API and sprite pack tools |
| **rquinio** / **romain.quinio** (changelog signed "Romulien") | 96 | PortraitBuilder 2.x and 3.x: DLC and mod loading, characteristic handling, government frames, Mono support |
| **Measter** | 4 | PortraitBuilder 1.x, the original tool |
| **Stuart Haidon** | 2 | Contributions in the history |

If you wrote part of it and your name is wrong or missing, please open an issue.
## Libraries

- SkiaSharp (rendering)
- Pfim (reading .dds images)
- Hime (parsing CK2 files; the Hime.Redist package)
- log4net (logging)

Each has its own licence; check them before redistributing builds.

## Art

The portrait art that the tool draws belongs to **Paradox Interactive**. It is read from the user's own installed copy of the game and is not included, converted or redistributed by this project. The logo and icons in `assets/branding/` are original and unrelated to it.
