using System;
using System.Collections.Generic;
using System.Linq;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Handbook_Categories
{
    /// <summary>
    /// Provides a category score for resolved grid recipes by looking at tangible data that is exposed by
    /// Vintage Story collectibles (attributes, behaviors, storage flags, light emission, nutrition etc.).
    /// The scorer purposefully ignores recipe and collectible names so that it also works for mod content.
    /// </summary>
    public static class GridRecipeCategorizer
    {
        private static readonly string[] CategoryOrder =
        {
            "Armor",
            "Clothes",
            "Jonas Tech",
            "Slabs",
            "Stairs",
            "Paths",
            "Roofing",
            "Glass",
            "Tools",
            "Weapons",
            "Storage",
            "Consumables",
            "Furniture",
            "Machinery",
            "Crafting Stations",
            "Decorative",
            "Construction",
            "Lighting",
            "Animals",
            "Food"
        };

        private static readonly EnumTool[] WeaponTools =
        {
            EnumTool.Sword,
            EnumTool.Spear,
            EnumTool.Bow,
            EnumTool.Sling,
            EnumTool.Shield,
            EnumTool.Club,
            EnumTool.Mace,
            EnumTool.Warhammer,
            EnumTool.Poleaxe,
            EnumTool.Halberd,
            EnumTool.Polearm,
            EnumTool.Staff,
            EnumTool.Pike,
            EnumTool.Javelin
        };

        private static readonly EnumTool[] UtilityTools =
        {
            EnumTool.Knife,
            EnumTool.Pickaxe,
            EnumTool.Axe,
            EnumTool.Shovel,
            EnumTool.Hammer,
            EnumTool.Sickle,
            EnumTool.Hoe,
            EnumTool.Saw,
            EnumTool.Scythe,
            EnumTool.Shears,
            EnumTool.Chisel,
            EnumTool.Wrench,
            EnumTool.Probe,
            EnumTool.Meter,
            EnumTool.Drill
        };

        /// <summary>
        /// Categorises a resolved grid recipe. The method inspects attributes, behaviors, storage flags, light emission,
        /// nutrition data and crafting hooks to produce a relevance score for every handbook category and returns the
        /// strongest category.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="recipe" /> is null.</exception>

using Vintagestory.API.Common;

namespace Handbook_Categories
{
    public static class GridRecipeCategorizer
    {
        private const string Armor = "Armor";
        private const string Weapons = "Weapons";
        private const string Tools = "Tools";
        private const string JonasTech = "Jonas Tech";
        private const string Lighting = "Lighting";
        private const string Animals = "Animals";
        private const string Clothes = "Clothes";
        private const string Storage = "Storage";
        private const string Furniture = "Furniture";
        private const string Consumables = "Consumables";
        private const string Food = "Food";
        private const string Slabs = "Slabs";
        private const string Stairs = "Stairs";
        private const string Paths = "Paths";
        private const string Roofing = "Roofing";
        private const string Glass = "Glass";
        private const string Machinery = "Machinery";
        private const string CraftingStations = "Crafting Stations";
        private const string Decorative = "Decorative";
        private const string Construction = "Construction";

        private static readonly string[] ArmorPrefixes = { "armor-", "shield-" };
        private static readonly string[] WeaponPrefixes =
        {
            "bow", "arrow", "sling", "spear", "blade", "club", "bomb", "blastingpowder",
            "scrapweaponkit", "hackingspear", "stickslayer", "snowball"
        };
        private static readonly string[] ToolPrefixes =
        {
            "axe", "pickaxe", "hoe", "saw", "shovel", "scythe", "hammer", "knife", "tongs",
            "prospectingpick", "solderingiron", "firestarter", "inkandquill", "plumbandsquare",
            "pan", "rope", "bugnet"
        };
        private static readonly string[] JonasPrefixes =
        {
            "nightvision", "returnbase", "returndeath", "resonator", "riftward", "tobtlocator",
            "glider", "schematic", "basereturnteleporter", "corpsereturnteleporter"
        };
        private static readonly string[] LightingPrefixes = { "lantern", "torch", "oillamp", "torchholder" };
        private static readonly string[] AnimalPrefixes = { "baskettrap", "beenade", "henbox", "skep", "trapcrate", "trough", "hay-normal" };
        private static readonly string[] ClothingPrefixes = { "clothes-", "hide-", "linen-", "cloth-", "sewingkit" };
        private static readonly string[] StoragePrefixes =
        {
            "chest", "crate", "barrel", "basket-", "basket", "backpack", "bookshelf", "displaycase",
            "trunk", "hunterbackpack", "linensack", "miningbag", "stationarybasket", "shelf",
            "doublechest", "labeledchest", "moldrack", "woodbucket", "crock", "toolrack", "scrollrack"
        };
        private static readonly string[] FurniturePrefixes =
        {
            "bed", "chair", "table", "stool", "rushmat", "stonecoffin", "strawbedding", "armorstand"
        };
        private static readonly string[] ConsumablePrefixes =
        {
            "bandage", "poultice", "firewood", "metalbit", "nugget", "paper", "flaxtwine", "solderbar",
            "twine", "parchment"
        };
        private static readonly string[] FoodPrefixes =
        {
            "vegetable-", "dough-", "pemmican", "fruit-", "seeds-", "rawcheese", "waxedcheese"
        };
        private static readonly string[] StairPrefixes =
        {
            "brickstairs", "clayshinglestairs", "cobblestonestairs", "plankstairs", "stonebrickstairs",
            "stonepathstairs"
        };
        private static readonly string[] PathPrefixes = { "stonepath", "woodenpath" };
        private static readonly string[] RoofingPrefixes =
        {
            "slantedroof", "slantedroofing", "clayshingle", "thatch", "roof-", "clayshingleblock"
        };
        private static readonly string[] GlassPrefixes = { "glasspane", "glass-" };
        private static readonly string[] MachineryPrefixes =
        {
            "windmill", "woodenaxle", "angledgears", "clutch", "transmission", "archimedesscrew",
            "helvehammer", "hopper", "chute", "condenser", "crank", "pulverizer", "pounder", "boat",
            "boatseat", "verticalboiler", "oar", "brake", "sail", "woodentoggle", "roller", "anchor",
            "ratlines", "pulverizerframe"
        };
        private static readonly string[] CraftingStationPrefixes =
        {
            "bloomery", "forge", "quern", "churn", "fruitpress", "sieve", "ingot-"
        };
        private static readonly string[] DecorativePrefixes =
        {
            "book", "wildvine", "figurehead", "antlermount", "diamond", "plaque", "sign", "strawdummy",
            "instrument"
        };

        private static readonly string[] BasketDeconstructOutputs = { "cattailtops", "papyrustops" };

        public static string Categorize(GridRecipe recipe)
        {
            if (recipe == null)
            {
                throw new ArgumentNullException(nameof(recipe));
            }


            Dictionary<string, float> scores = InitialiseScores();

            if (recipe.Output?.ResolvedItemstack is ItemStack stack)
            {
                ScoreStack(stack, scores);
            }

            if (recipe.Ingredients != null)
            {
                foreach (CraftingRecipeIngredient ingredient in recipe.Ingredients.Values)
                {
                    if (ingredient?.ResolvedItemstack != null)
                    {
                        ScoreIngredient(ingredient.ResolvedItemstack, scores);
                    }
                    else if (ingredient?.Attributes != null)
                    {
                        ScoreIngredientAttributes(ingredient.Attributes, scores);
                    }
                }
            }

            return scores
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => Array.IndexOf(CategoryOrder, pair.Key))
                .First().Key;
        }

        private static Dictionary<string, float> InitialiseScores()
        {
            var scores = new Dictionary<string, float>(CategoryOrder.Length);
            foreach (string category in CategoryOrder)
            {
                scores[category] = 0f;
            }

            return scores;
        }

        private static void ScoreStack(ItemStack stack, IDictionary<string, float> scores)
        {
            CollectibleObject collectible = stack.Collectible;

            ScoreWearables(collectible, scores);
            ScoreToolsAndWeapons(collectible, scores);
            ScoreStorage(collectible, scores);
            ScoreFoodAndConsumables(collectible, scores);
            ScoreLighting(collectible, stack, scores);
            ScoreAnimals(collectible, scores);
            ScoreMachinery(collectible, scores);
            ScoreCraftingStations(collectible, scores);
            ScoreSpecialBlocks(collectible, scores);
            ScoreFurnitureAndDecor(collectible, scores);

            if (scores["Construction"] == 0f && collectible is Block)
            {
                scores["Construction"] += 1f;
            }
        }

        private static void ScoreIngredient(ItemStack stack, IDictionary<string, float> scores)
        {
            CollectibleObject collectible = stack.Collectible;

            if (collectible is ItemWearable wearable)
            {
                scores[wearable.IsArmor ? "Armor" : "Clothes"] += 0.5f;
            }

            if (collectible.Tool is EnumTool tool && UtilityTools.Contains(tool))
            {
                scores["Tools"] += 0.5f;
            }

            if (collectible.NutritionProps != null)
            {
                scores["Food"] += 0.25f;
            }

            ScoreIngredientAttributes(collectible.Attributes, scores);
        }

        private static void ScoreIngredientAttributes(JsonObject attributes, IDictionary<string, float> scores)
        {
            if (attributes == null)
            {
                return;
            }

            if (attributes["clothescategory"].Exists)
            {
                scores["Clothes"] += 0.4f;
            }

            if (attributes["traptype"].Exists || attributes["creatureContainer"].Exists)
            {
                scores["Animals"] += 0.6f;
            }

            if (attributes["mechanicalPower"].Exists)
            {
                scores["Machinery"] += 0.5f;
            }
        }

        private static void ScoreWearables(CollectibleObject collectible, IDictionary<string, float> scores)
        {
            if (collectible is ItemWearable wearable)
            {
                scores[wearable.IsArmor ? "Armor" : "Clothes"] += 10f;
            }
        }

        private static void ScoreToolsAndWeapons(CollectibleObject collectible, IDictionary<string, float> scores)
        {
            EnumTool? tool = collectible.Tool;
            if (tool == null)
            {
                return;
            }

            if (WeaponTools.Contains(tool.Value) || collectible.AttackPower >= 4f)
            {
                scores["Weapons"] += 7f;
            }

            if (UtilityTools.Contains(tool.Value) || collectible.ToolTier > 0)
            {
                scores["Tools"] += 6.5f;
            }
        }

        private static void ScoreStorage(CollectibleObject collectible, IDictionary<string, float> scores)
        {
            if (LooksLikeStorage(collectible))
            {
                scores["Storage"] += 9f;
            }
        }

        private static bool LooksLikeStorage(CollectibleObject collectible)
        {
            JsonObject attributes = collectible.Attributes;

            bool hasInventoryAttributes = attributes != null && (
                attributes["inventoryClassName"].Exists ||
                attributes["quantitySlots"].Exists ||
                attributes["quantityColumns"].Exists ||
                attributes["storageType"].Exists ||
                attributes["storageFlags"].Exists ||
                attributes["containerSlots"].Exists ||
                attributes["slotRefillIdentifier"].Exists);

            bool hasContainerBehavior = collectible is Block block && block.HasBehavior<BlockBehaviorContainer>();

            bool isBackpack = (collectible.StorageFlags & EnumItemStorageFlags.Backpack) != 0;

            return hasInventoryAttributes || hasContainerBehavior || isBackpack;
        }

        private static void ScoreFoodAndConsumables(CollectibleObject collectible, IDictionary<string, float> scores)
        {
            if (collectible.NutritionProps != null)
            {
                scores["Food"] += 10f;
            }

            bool isConsumable = collectible.CombustibleProps != null
                || (collectible.TransitionableProps != null && collectible.TransitionableProps.Length > 0)
                || collectible.HasBehavior<CollectibleBehaviorGroundStorable>();

            if (isConsumable)
            {
                scores["Consumables"] += collectible.NutritionProps != null ? 2.5f : 6f;
            }
        }

        private static void ScoreLighting(CollectibleObject collectible, ItemStack stack, IDictionary<string, float> scores)
        {
            if (EmitsLight(collectible, stack))
            {
                scores["Lighting"] += 8.5f;
            }
        }

        private static bool EmitsLight(CollectibleObject collectible, ItemStack stack)
        {
            if (collectible.LightHsv[0] != 0 || collectible.LightHsv[1] != 0 || collectible.LightHsv[2] != 0)
            {
                return true;
            }

            if (collectible is Block block && block.LightAbsorption < 15)
            {
                byte[] hsv = block.GetLightHsv(null, null, stack);
                if (hsv != null && hsv.Length == 3)
                {
                    return hsv[0] != 0 || hsv[1] != 0 || hsv[2] != 0;
                }
            }

            return false;
        }

        private static void ScoreAnimals(CollectibleObject collectible, IDictionary<string, float> scores)
        {
            JsonObject attributes = collectible.Attributes;
            bool creatureContainer = attributes != null && (
                attributes["traptype"].Exists ||
                attributes["creatureContainer"].Exists ||
                attributes["animalfeed"].Exists ||
                attributes["petAccessory"].Exists);

            if (collectible is ItemCreature || collectible is ItemCreatureInventory || creatureContainer)
            {
                scores["Animals"] += 7.5f;
            }
        }

        private static void ScoreMachinery(CollectibleObject collectible, IDictionary<string, float> scores)
        {
            JsonObject attributes = collectible.Attributes;
            bool hasMechanicalAttributes = attributes != null && (
                attributes["mechanicalPower"].Exists ||
                attributes["transmission"].Exists ||
                attributes["machinepart"].Exists);

            bool mechanicalBlock = collectible is Block block && (
                block.HasBehavior<BlockBehaviorJonasHydraulicPump>() ||
                block.EntityClass != null && block.EntityClass.IndexOf("Mechanical", StringComparison.OrdinalIgnoreCase) >= 0);

            if (hasMechanicalAttributes || mechanicalBlock)
            {
                scores["Machinery"] += 9.5f;
            }

            if (LooksLikeJonasDevice(collectible))
            {
                scores["Jonas Tech"] += 9.5f;
            }
        }

        private static bool LooksLikeJonasDevice(CollectibleObject collectible)
        {
            Type type = collectible.GetType();
            if (type.Name.IndexOf("Jonas", StringComparison.OrdinalIgnoreCase) >= 0 ||
                type.Name.IndexOf("Resonator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                type.Name.IndexOf("Teleporter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                type.Name.IndexOf("Nightvision", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (collectible is Block block)
            {
                return block is BlockGasifier
                    || block is BlockResonator
                    || block is BlockRiftWard
                    || block is BlockBaseReturnTeleporter
                    || block is BlockCorpseReturnTeleporter;
            }

            JsonObject attributes = collectible.Attributes;
            return attributes != null && (attributes["displaycaseableByType"].Exists || attributes["jonasComponent"].Exists);
        }

        private static void ScoreCraftingStations(CollectibleObject collectible, IDictionary<string, float> scores)
        {
            if (collectible is Block block)
            {
                bool hasStationEntity = !string.IsNullOrEmpty(block.EntityClass) && (
                    block.EntityClass.IndexOf("Forge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    block.EntityClass.IndexOf("Quern", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    block.EntityClass.IndexOf("Churn", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    block.EntityClass.IndexOf("Bloomery", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    block.EntityClass.IndexOf("Anvil", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    block.EntityClass.IndexOf("Station", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    block.EntityClass.IndexOf("Press", StringComparison.OrdinalIgnoreCase) >= 0);

                bool stationBehaviors = block.HasBehavior<BlockBehaviorHeatSource>()
                    || block.BlockEntityBehaviors.Any(b => b.Name.IndexOf("Work", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                            b.Name.IndexOf("Cook", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                            b.Name.IndexOf("Forge", StringComparison.OrdinalIgnoreCase) >= 0);

                if (hasStationEntity || stationBehaviors)
                {
                    scores["Crafting Stations"] += 8.5f;
                }
            }
        }

        private static void ScoreSpecialBlocks(CollectibleObject collectible, IDictionary<string, float> scores)
        {
            if (collectible is not Block block)
            {
                return;
            }

            if (block is BlockSlab || block.HasBehavior<BlockBehaviorSlab>())
            {
                scores["Slabs"] += 9f;
            }

            if (block is BlockStairs)
            {
                scores["Stairs"] += 9f;
            }

            JsonObject attributes = block.Attributes;
            string mapColor = attributes?["mapColorCode"].AsString();
            if (!string.IsNullOrEmpty(mapColor))
            {
                if (mapColor.Equals("road", StringComparison.OrdinalIgnoreCase))
                {
                    scores["Paths"] += 9f;
                }

                if (mapColor.Equals("settlement", StringComparison.OrdinalIgnoreCase) && attributes["humanoidTraversalCost"].AsInt(0) >= 50)
                {
                    scores["Roofing"] += 6f;
                }
            }

            if (block.BlockMaterial == EnumBlockMaterial.Glass || block.DrawType == EnumDrawType.Transparent)
            {
                scores["Glass"] += 8f;
            }
        }

        private static void ScoreFurnitureAndDecor(CollectibleObject collectible, IDictionary<string, float> scores)
        {
            if (collectible is not Block block)
            {
                if (collectible.CreativeInventoryTabs != null && collectible.CreativeInventoryTabs.Any(tab => tab.Equals("decorative", StringComparison.OrdinalIgnoreCase)))
                {
                    scores["Decorative"] += 4f;
                }

                return;
            }

            JsonObject attributes = block.Attributes;
            int traversalCost = attributes?["humanoidTraversalCost"].AsInt(0) ?? 0;
            bool hasDecorTab = block.CreativeInventoryTabs != null && block.CreativeInventoryTabs.Any(tab => tab.Equals("decorative", StringComparison.OrdinalIgnoreCase));

            if (traversalCost >= 75 && block.CollisionBoxes?.Any(box => !IsFullBlock(box)) == true)
            {
                scores["Furniture"] += 8f;
            }

            if (hasDecorTab || attributes?["handbook"].Exists == true)
            {
                scores["Decorative"] += 6f;
            }
        }

        private static bool IsFullBlock(Cuboidf box)
        {
            return Math.Abs(box.X1) < float.Epsilon && Math.Abs(box.Y1) < float.Epsilon && Math.Abs(box.Z1) < float.Epsilon &&
                   Math.Abs(box.X2 - 1f) < float.Epsilon && Math.Abs(box.Y2 - 1f) < float.Epsilon && Math.Abs(box.Z2 - 1f) < float.Epsilon;
            List<string> outputs = CollectOutputs(recipe);
            List<string> ingredients = CollectIngredients(recipe);

            if (AnyStartsWith(outputs, ArmorPrefixes) || AnyStartsWith(ingredients, ArmorPrefixes))
            {
                return Armor;
            }

            if (AnyStartsWith(outputs, WeaponPrefixes))
            {
                return Weapons;
            }

            if (AnyStartsWith(outputs, ToolPrefixes))
            {
                return Tools;
            }

            if (AnyStartsWith(outputs, JonasPrefixes))
            {
                return JonasTech;
            }

            if (AnyStartsWith(outputs, LightingPrefixes))
            {
                return Lighting;
            }

            if (AnyStartsWith(outputs, AnimalPrefixes) || outputs.Any(code => code.StartsWith("hoovedwearables-", StringComparison.Ordinal) && !code.StartsWith("hoovedwearables-blanket", StringComparison.Ordinal)))
            {
                return Animals;
            }

            if (AnyStartsWith(outputs, ClothingPrefixes) || outputs.Any(code => code.StartsWith("hoovedwearables-blanket", StringComparison.Ordinal)) || (AnyStartsWith(outputs, new[] { "gear-" }) && AnyStartsWith(ingredients, new[] { "clothes-" })))
            {
                return Clothes;
            }

            if (AnyStartsWith(outputs, StoragePrefixes))
            {
                if (!outputs.Any(code => code.StartsWith("baskettrap", StringComparison.Ordinal)))
                {
                    return Storage;
                }
            }

            if (AnyEquals(outputs, BasketDeconstructOutputs) && AnyStartsWith(ingredients, new[] { "basket", "stationarybasket" }))
            {
                return Storage;
            }

            if (AnyStartsWith(outputs, FurniturePrefixes) || AnyStartsWith(ingredients, new[] { "bed-" }))
            {
                return Furniture;
            }

            if (AnyStartsWith(outputs, new[] { "metalbit" }) && AnyStartsWith(ingredients, new[] { "plaque-" }))
            {
                return Decorative;
            }

            if (AnyStartsWith(outputs, new[] { "metalbit" }) && AnyStartsWith(ingredients, new[] { "lightningrod" }))
            {
                return Construction;
            }

            if (AnyStartsWith(outputs, ConsumablePrefixes))
            {
                return Consumables;
            }

            if (AnyStartsWith(outputs, FoodPrefixes))
            {
                return Food;
            }

            if (outputs.Any(code => code.Contains("slab", StringComparison.Ordinal) || code.Contains("labs-", StringComparison.Ordinal)))
            {
                return Slabs;
            }

            if (AnyStartsWith(outputs, StairPrefixes))
            {
                return Stairs;
            }

            if (AnyStartsWith(outputs, PathPrefixes))
            {
                return Paths;
            }

            if (AnyStartsWith(outputs, RoofingPrefixes))
            {
                return Roofing;
            }

            if (AnyStartsWith(outputs, GlassPrefixes))
            {
                return Glass;
            }

            if (AnyStartsWith(outputs, MachineryPrefixes) || AnyStartsWith(ingredients, new[] { "helvehammerbase" }))
            {
                return Machinery;
            }

            if (AnyStartsWith(outputs, CraftingStationPrefixes))
            {
                return CraftingStations;
            }

            if (AnyStartsWith(outputs, DecorativePrefixes) || AnyStartsWith(ingredients, new[] { "plaque-" }))
            {
                return Decorative;
            }

            return Construction;
        }

        private static List<string> CollectOutputs(GridRecipe recipe)
        {
            var outputs = new List<string>(capacity: 1);
            string? code = recipe.Output?.Code?.Path;
            if (!string.IsNullOrEmpty(code))
            {
                outputs.Add(code);
            }

            return outputs;
        }

        private static List<string> CollectIngredients(GridRecipe recipe)
        {
            if (recipe.Ingredients == null || recipe.Ingredients.Count == 0)
            {
                return new List<string>(0);
            }

            return recipe.Ingredients.Values
                .Select(ingredient => ingredient?.Code?.Path)
                .Where(code => !string.IsNullOrEmpty(code))
                .Select(code => code!)
                .ToList();
        }

        private static bool AnyStartsWith(IEnumerable<string> values, IEnumerable<string> prefixes)
        {
            foreach (string value in values)
            {
                foreach (string prefix in prefixes)
                {
                    if (value.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool AnyStartsWith(IEnumerable<string> values, string[] prefixes)
        {
            return AnyStartsWith(values, (IEnumerable<string>)prefixes);
        }

        private static bool AnyEquals(IEnumerable<string> values, IEnumerable<string> expected)
        {
            foreach (string value in values)
            {
                foreach (string candidate in expected)
                {
                    if (value.Equals(candidate, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;

        }
    }
}
