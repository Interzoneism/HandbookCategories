using System;
using System.Collections.Generic;
using System.Linq;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;

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
            "Shields",
            "Transportation",
            "Storage",
            "Consumables",
            "Furniture",
            "Machinery",
            "Crafting Stations",
            "Decorative",
            "Construction",
            "Lighting",
            "Animals",
            "Food",
            "Ores & Ingots"
        };

        public static IEnumerable<string> AllCategories => CategoryOrder;

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
            ScoreTransportation(collectible, scores);
            ScoreStorage(collectible, scores);
            ScoreFoodAndConsumables(collectible, scores);
            ScoreLighting(collectible, stack, scores);
            ScoreAnimals(collectible, scores);
            ScoreMachinery(collectible, scores);
            ScoreCraftingStations(collectible, scores);
            ScoreSpecialBlocks(collectible, scores);
            ScoreFurnitureAndDecor(collectible, scores);
            ScoreOresAndIngots(collectible, scores);

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

            if (tool == EnumTool.Shield || collectible is ItemShield)
            {
                scores["Shields"] += 9.5f;
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

            if (collectible is BlockMPBase || collectible is BlockMPMultiblockPulverizer || collectible is BlockLargeGear3m || collectible is BlockHelveHammer)
            {
                scores["Machinery"] += 8.5f;
            }

            if (collectible is IBlockItemFlow)
            {
                scores["Machinery"] += 7.5f;
            }

            if (attributes != null && attributes["rackable"].AsBool(false) && attributes["toolrackTransform"].Exists)
            {
                scores["Machinery"] += 4f;
            }

            if (HandbookGroupContains(attributes, "solderbar"))
            {
                scores["Machinery"] += 6.5f;
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
            return attributes != null && (attributes["displaycaseableByType"].Exists
                || attributes["jonasComponent"].Exists
                || HandbookGroupContains(attributes, "jonas")
                || HandbookGroupContains(attributes, "nightvision")
                || HandbookGroupContains(attributes, "returnbase")
                || HandbookGroupContains(attributes, "returndeath"));
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
                if (collectible is ItemBook)
                {
                    scores["Decorative"] += 7f;
                }

                if (collectible.Attributes?["writingTool"].AsBool(false) == true)
                {
                    scores["Decorative"] += 5.5f;
                }

                if (collectible.CreativeInventoryTabs != null && collectible.CreativeInventoryTabs.Any(tab => tab.Equals("decorative", StringComparison.OrdinalIgnoreCase)))
                {
                    scores["Decorative"] += 4f;
                }

                return;
            }

            if (block is BlockAntlerMount || block is BlockScrollRack || block is BlockBookshelf || block is BlockBed)
            {
                scores["Furniture"] += 8.5f;
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

        private static void ScoreTransportation(CollectibleObject collectible, IDictionary<string, float> scores)
        {
            if (HasAttachableToEntity(collectible.Attributes))
            {
                scores["Transportation"] += 8.5f;
            }

            if (collectible is ItemGlider || collectible is ItemOar || collectible is ItemFlute)
            {
                scores["Transportation"] += 8.5f;
            }
        }

        private static void ScoreOresAndIngots(CollectibleObject collectible, IDictionary<string, float> scores)
        {
            if (collectible is ItemIngot || collectible is ItemNugget || collectible is ItemOre || collectible is ItemCoal)
            {
                scores["Ores & Ingots"] += 9f;
            }
        }

        private static bool IsFullBlock(Cuboidf box)
        {
            return Math.Abs(box.X1) < float.Epsilon && Math.Abs(box.Y1) < float.Epsilon && Math.Abs(box.Z1) < float.Epsilon &&
                   Math.Abs(box.X2 - 1f) < float.Epsilon && Math.Abs(box.Y2 - 1f) < float.Epsilon && Math.Abs(box.Z2 - 1f) < float.Epsilon;
        }

        private static bool HasAttachableToEntity(JsonObject attributes)
        {
            if (attributes == null || !attributes.Exists)
            {
                return false;
            }

            if (attributes["attachableToEntity"].Exists)
            {
                return true;
            }

            foreach (JsonObject child in attributes)
            {
                if (HasAttachableToEntity(child))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HandbookGroupContains(JsonObject attributes, string value)
        {
            if (attributes == null || !attributes.Exists)
            {
                return false;
            }

            string[] groups = attributes["handbook"]["groupBy"].AsArray<string>();
            if (groups == null)
            {
                return false;
            }

            return groups.Any(group => group != null && group.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0);
        }

    }
}
