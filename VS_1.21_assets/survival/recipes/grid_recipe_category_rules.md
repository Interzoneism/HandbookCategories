# Grid Recipe Categorization Rules

The `GridRecipeCategorizer` groups survival grid recipes into player-facing handbook tabs by inspecting the recipe data itself. The rules below document the concrete attributes that qualify a recipe for each category. Unless otherwise stated the checks look at the recipe output code (e.g. `armor-head-plate-iron`) or ingredient codes (e.g. `plaque-fancy-iron-wall-north`).

- **Armor** – Outputs or ingredients whose codes start with `armor-` or `shield-` (covers crafting and repair recipes).
- **Weapons** – Outputs that start with weapon-focused codes such as `bow`, `arrow`, `spear`, `sling`, `blade`, `club`, `bomb`, `blastingpowder`, `scrapweaponkit`, `hackingspear`, `stickslayer`, or `snowball`.
- **Tools** – Outputs starting with utilitarian codes like `axe`, `pickaxe`, `hoe`, `saw`, `shovel`, `scythe`, `hammer`, `knife`, `tongs`, `prospectingpick`, `solderingiron`, `firestarter`, `inkandquill`, `plumbandsquare`, `pan`, `rope`, or `bugnet`.
- **Jonas Tech** – Outputs with the Jonas technology code patterns (`nightvision`, `returnbase`, `returndeath`, `resonator`, `riftward`, `tobtlocator`, `glider`, `schematic`, `basereturnteleporter`, `corpsereturnteleporter`).
- **Lighting** – Outputs beginning with `lantern`, `torch`, `oillamp`, or `torchholder`.
- **Animals** – Outputs for animal gear and husbandry such as `baskettrap`, `beenade`, `henbox`, `skep`, `trapcrate`, `trough`, `hay-normal`, and any `hoovedwearables-*` code that is not a blanket.
- **Clothes** – Outputs beginning with clothing materials (`clothes-`, `hide-`, `linen-`, `cloth-`, `sewingkit`) or `hoovedwearables-blanket`. Gear salvage recipes are also treated as clothing when the output starts with `gear-` and an ingredient starts with `clothes-`.
- **Storage** – Outputs for storage blocks and containers (`chest`, `crate`, `barrel`, `basket`, `backpack`, `bookshelf`, `displaycase`, `trunk`, `hunterbackpack`, `linensack`, `miningbag`, `stationarybasket`, `shelf`, `doublechest`, `labeledchest`, `moldrack`, `woodbucket`, `crock`, `toolrack`, `scrollrack`). Basket deconstruction recipes fall here when they output `cattailtops`/`papyrustops` and consume basket ingredients.
- **Furniture** – Outputs beginning with `bed`, `chair`, `table`, `stool`, `rushmat`, `stonecoffin`, `strawbedding`, or `armorstand`, plus any recipe whose ingredients include `bed-*` (bed trimming recipes that output `drygrass`).
- **Consumables** – Outputs with consumable codes `bandage`, `poultice`, `firewood`, `metalbit`, `nugget`, `paper`, `flaxtwine`, `solderbar`, `twine`, or `parchment`. Recipes that return `metalbit` from decorative plaques are handled separately under *Decorative*, and those that reclaim metal from `lightningrod` stay in *Construction*.
- **Food** – Outputs beginning with `vegetable-`, `dough-`, `pemmican`, `fruit-`, `seeds-`, `rawcheese`, or `waxedcheese`.
- **Slabs** – Outputs whose codes contain `slab` or the kiln typo `labs-` (e.g. `clayshinglelabs-*`).
- **Stairs** – Outputs starting with the stair families (`brickstairs`, `clayshinglestairs`, `cobblestonestairs`, `plankstairs`, `stonebrickstairs`, `stonepathstairs`).
- **Paths** – Outputs starting with `stonepath` or `woodenpath` that were not already caught by the stair rule.
- **Roofing** – Outputs beginning with roof and shingle codes (`slantedroof`, `slantedroofing`, `clayshingle`, `thatch`, `roof-`, `clayshingleblock`).
- **Glass** – Outputs starting with `glasspane` or `glass-`.
- **Machinery** – Outputs with mechanical codes (`windmill`, `woodenaxle`, `angledgears`, `clutch`, `transmission`, `archimedesscrew`, `helvehammer`, `hopper`, `chute`, `condenser`, `crank`, `pulverizer`, `pounder`, `boat`, `boatseat`, `verticalboiler`, `oar`, `brake`, `sail`, `woodentoggle`, `roller`, `anchor`, `ratlines`, `pulverizerframe`) or recipes whose ingredients include `helvehammerbase`.
- **Crafting Stations** – Outputs starting with `bloomery`, `forge`, `quern`, `churn`, `fruitpress`, `sieve`, or `ingot-`.
- **Decorative** – Outputs with decorative codes (`book`, `wildvine`, `figurehead`, `antlermount`, `diamond`, `plaque`, `sign`, `strawdummy`, `instrument`) or recipes involving `plaque-*` ingredients (e.g., plaque recycling).
- **Construction** – The default fallback for structural materials once every targeted rule above has been evaluated.

These checks only rely on data that ships with each `GridRecipe`: output codes, ingredient codes, and—when needed—variant-aware prefixes. They match all existing 1.21 survival grid recipes; recipes with no entries (e.g. `armorrepair/improvisedwoodarmor.json`) naturally fall through to the fallback.
