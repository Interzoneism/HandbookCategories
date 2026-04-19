# Enhanced Handbook — Handbook Categories

A [Vintage Story](https://www.vintagestory.at/) client-side mod that replaces the default handbook tabs with fully customizable, colour-coded category tabs so you can organise items and blocks exactly the way you want.

---

> ### 🤖 A heartfelt apology
>
> I am genuinely, profoundly sorry. This mod was built almost entirely with AI assistance, and it shows. The code is a labyrinthine tangle of Harmony patches, reflection hacks, JSON configs, and optimistic guesswork about the internal Vintage Story API. If you have opened any of these files hoping for clarity and found only despair, I completely understand. I am sorry for the naming inconsistencies, the occasional wall of string literals that passes for "configuration", and whatever `HandbookCategoryManager.cs` has become. You are a brave soul for being here, and I respect you enormously.

---

## Features

- **Custom category tabs** — create as many handbook tabs as you like, each with its own name and colour.
- **Keyword-based filtering** — each category matches handbook entries by body text keywords and/or page title keywords, with support for forbidden-word exclusions.
- **Variant categories** — optionally auto-generate sub-categories for item variants (e.g. by material).
- **"Everything grouped"** — optional single group page that aggregates all entries in a category.
- **Drag-and-drop reordering** — drag items between categories or reorder pages within a category.
- **Hide built-in tabs** — individually hide the Tutorial, Blocks & Items, and Guides tabs.
- **Hide the original search button** — keep the UI clean if you prefer the category workflow.
- **In-game setup dialog** — open with `.setupbook` for a point-and-click configuration experience.
- **Chat commands** — full command-line control for power users (see below).
- **Config persistence** — everything is saved to `EnhancedHandbook.json` in your mod config folder.

---

## Installation

1. Download the latest `.zip` release from the [Releases](../../releases) page.
2. Place it in your Vintage Story `Mods/` folder.
3. Launch the game. On first load you will see a chat message welcoming you and pointing you to `.setupbook`.

> **Client-side only.** This mod does not need to be installed on the server.

---

## Getting Started

Type `.setupbook` in the chat (the in-game chat, not the server console) to open the configuration dialog. From there you can:

- Toggle which built-in tabs to hide.
- Enable or disable drag-and-drop.
- Load a set of default categories (Tools, Weapons, Armor, Clothes, Food, Building, and more).

---

## Chat Commands

All commands are prefixed with `.` (dot) in the in-game chat.

| Command | Description |
|---|---|
| `.setupbook` | Opens the setup/configuration dialog. |
| `.categorymod <category> <words…>` | Adds match or forbidden words to a category (creates the category if it doesn't exist). |
| `.categorymoddelete <category>` | Deletes a category, or pass `all` to clear all custom categories. |
| `.categorymodsave <category>` | Copies a ready-to-paste `.categorymod` command for the named category to your clipboard. |
| `.categorymoddefault` | Restores the built-in default categories (Tools, Weapons, Armor, etc.). |

### `.categorymod` syntax

```
.categorymod "My Category" Word1 "Multi Word Phrase" -ForbiddenWord !"TitleOnlyForbidden"
```

- Words are matched against handbook entry descriptions (case-insensitive).
- Wrap multi-word phrases in `"quotes"`.
- Prefix a word with `-` or `!` to add it as a **forbidden** word (entries containing it are excluded from the category).
- Prefix a word with `"` and mark it with `=` for **title-only** matching (matched against the page title, not the body).
- Words that already exist in the opposite list (match ↔ forbidden) are automatically moved.

---

## Configuration File

The config is stored at `<VS data folder>/ModConfig/EnhancedHandbook.json`. You can edit it directly, but using the in-game commands is safer.

Key fields:

| Field | Type | Default | Description |
|---|---|---|---|
| `onlyGridPages` | bool | `false` | Show only grid-style pages. |
| `disableTutorialTab` | bool | `true` | Hide the Tutorial tab. |
| `disableBlocksAndItemsTab` | bool | `true` | Hide the Blocks & Items tab. |
| `disableGuidesTab` | bool | `true` | Hide the Guides tab. |
| `disableOriginalSearchButton` | bool | `true` | Hide the original search button. |
| `disableDragAndDrop` | bool | `false` | Disable drag-and-drop reordering. |
| `createVariantCategories` | bool | `false` | Auto-create variant sub-categories. |
| `createEverythingGrouped` | bool | `true` | Add a grouped "all items" page per category. |
| `categories` | array | `[Bookmarks]` | List of category definitions. |

Each category entry supports `name`, `matchWords`, `matchTitleWords`, `forbiddenWords`, `forbiddenTitleWords`, and `tabBackgroundColor` (hex string).

---

## Building from Source

Requirements: .NET 7 SDK and a copy of Vintage Story 1.21.

```bash
dotnet build -nologo -clp:Summary -warnaserror
```

Tests:

```bash
dotnet test --nologo --verbosity=minimal
```

---

## Compatibility

- **Vintage Story**: 1.21
- **Side**: Client only
- **Dependencies**: [HarmonyLib](https://github.com/pardeike/Harmony) (bundled)

---

## License

See [LICENSE](LICENSE) if one exists. If it doesn't, assume "please be nice".

---

*Built with an unreasonable amount of AI assistance and an equally unreasonable amount of optimism. If something is broken, it is probably the AI's fault. If something works well, that was definitely intentional.*
