# Grid recipe categorization signals

The `GridRecipeCategorizer` no longer relies on recipe or asset names.  Instead it reads the data that Vintage Story exposes on
resolved collectibles (attributes, behaviors, storage flags, nutrition descriptors, light emission etc.) and scores each
category.  The strongest score wins, which keeps the logic mod-friendly and forward compatible.

Below is a summary of the primary signals that feed into every category score:

| Category | Primary signals |
| --- | --- |
| **Armor** | Output resolves to an `ItemWearable` with `IsArmor == true`. |
| **Clothes** | Wearables that are not armour or ingredients carrying a `clothescategory` attribute. |
| **Jonas Tech** | Collectible type names referencing temporal tech (night vision, resonators, teleporters, Jonas gear) or blocks using the specialised Jonas block behaviours.  Also triggered when attributes expose `displaycaseableByType`/`jonasComponent`. |
| **Slabs** | Output block inherits from `BlockSlab` or has the slab block-behaviour. |
| **Stairs** | Output block inherits from `BlockStairs`. |
| **Paths** | Block attributes carry the road map colour (`mapColorCode == "road"`). |
| **Roofing** | Roofing blocks advertise a high humanoid traversal cost and the settlement colour map. |
| **Glass** | Block material is `EnumBlockMaterial.Glass` or the block renders through the glass draw type. |
| **Tools** | Collectible exposes a non-combat `EnumTool` value or a positive tool tier. |
| **Weapons** | Collectible exposes a combat-oriented `EnumTool` value or a high base attack power. |
| **Storage** | Storage flags, inventory metadata (`quantitySlots`, `inventoryClassName`, `storageType`, …) or container behaviours on the output. |
| **Consumables** | Combustible/transitionable items and anything that registers as ground storable without providing nutrition. |
| **Furniture** | Non-storage blocks with high humanoid traversal cost and partial collision boxes (chairs, beds, tables). |
| **Machinery** | Items and blocks that declare `mechanicalPower` (or similar machine attributes) or use the Jonas hydraulic behaviour / mechanical block entities. |
| **Crafting Stations** | Blocks whose entity class names reference stations (forge, bloomery, quern, press, churn…) or expose heat/work crafting behaviours. |
| **Decorative** | Collectibles offered on the decorative creative tab or with handbook metadata but no stronger category match. |
| **Construction** | Fallback when none of the above signals rise above ambient scores; biased toward structural blocks. |
| **Lighting** | Collectibles that emit light through `LightHsv` or block light emitters. |
| **Animals** | Traps, troughs, creature containers and captured creature items (attributes such as `traptype`, `creatureContainer`, `animalfeed`). |
| **Food** | Nutrition descriptors on the collectible (`NutritionProps`), even when the recipe transforms or combines food. |

Ingredient stacks contribute low-weight hints for the same signals (e.g. a fabric ingredient nudges the *Clothes* score) so that
recolouring or repair recipes still land in the expected tabs even when the output itself is neutral.

Because every signal looks at behaviour, attributes or other typed data, modded content that follows the same conventions
automatically participates in the scoring system without requiring manual string lists.
