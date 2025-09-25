using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json.Linq;

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
            "Tools",
            "Weapons",
            "Storage",
            "Furniture",
            "Consumables"
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

        private static readonly KeywordRule[] KeywordRules =
        {
            new KeywordRule("Armor", 7.5f, new[]
            {
                "armor", "armour", "helmet", "helm", "cuirass", "breastplate", "chestplate", "greaves",
                "sabatons", "vambrace", "gauntlet", "chainmail", "mail", "scale", "shield"
            }),
            new KeywordRule("Clothes", 6f, new[]
            {
                "clothes", "hat", "cap", "hood", "shirt", "jacket", "coat", "cloak", "tunic", "robe",
                "dress", "skirt", "pants", "trousers", "leggings", "gloves", "belt", "apron", "garment"
            }),
            new KeywordRule("Tools", 5.5f, new[]
            {
                "tool", "knife", "knives", "axe", "hatchet", "pickaxe", "pick", "hammer", "mallet",
                "hoe", "shovel", "spade", "sickle", "scythe", "saw", "chisel", "drill", "wrench",
                "trowel", "rake", "plane", "adze"
            }),
            new KeywordRule("Weapons", 6.5f, new[]
            {
                "weapon", "sword", "blade", "longblade", "spear", "halberd", "polearm", "pike",
                "sling", "bow", "arrow", "bolt", "javelin", "mace", "club", "warhammer"
            }),
            new KeywordRule("Storage", 5f, new[]
            {
                "storage", "chest", "basket", "crate", "box", "drawer", "cabinet", "locker", "trunk",
                "cask", "vessel", "jar", "urn", "barrel", "bin", "coffer", "shelf", "wardrobe",
                "larder", "pantry"
            }),
            new KeywordRule("Furniture", 4.5f, new[]
            {
                "furniture", "bed", "chair", "stool", "bench", "table", "desk", "bookshelf", "bookcase",
                "couch", "sofa", "dresser", "cupboard", "wardrobe", "cabinet"
            }),
            new KeywordRule("Consumables", 7f, new[]
            {
                "bandage", "poultice", "salve", "remedy", "balm", "ointment", "medicine", "elixir",
                "potion", "tonic", "liniment"
            })
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
                ScoreStack(recipe, stack, scores);
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

        private static void ScoreStack(GridRecipe recipe, ItemStack stack, IDictionary<string, float> scores)
        {
            CollectibleObject collectible = stack.Collectible;

            ApplyMetadataHeuristics(recipe, stack, collectible, scores);
            ScoreWearables(collectible, scores);
            ScoreToolsAndWeapons(collectible, scores);
            ScoreStorage(collectible, scores);
            ScoreConsumables(collectible, scores);
            ScoreFurniture(collectible, scores);
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
                scores["Armor"] += 6.5f;
                return;
            }

            bool isUtilityTool = UtilityTools.Contains(tool.Value) || collectible.ToolTier > 0;
            bool isWeaponTool = WeaponTools.Contains(tool.Value);

            if (!isUtilityTool && (isWeaponTool || collectible.AttackPower >= 4f))
            {
                scores["Weapons"] += 7f;
            }

            if (isUtilityTool)
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

        private static void ScoreConsumables(CollectibleObject collectible, IDictionary<string, float> scores)
        {
            if (collectible == null)
            {
                return;
            }

            bool healingBehavior = collectible.HasBehavior<BehaviorHealingItem>() || collectible is ItemPoultice;
            if (healingBehavior)
            {
                scores["Consumables"] += 8.5f;
            }
        }

        private static void ScoreFurniture(CollectibleObject collectible, IDictionary<string, float> scores)
        {
            if (collectible is not Block block)
            {
                return;
            }

            if (block is BlockAntlerMount || block is BlockScrollRack || block is BlockBookshelf || block is BlockBed)
            {
                scores["Furniture"] += 8.5f;
            }

            JsonObject attributes = block.Attributes;
            int traversalCost = attributes?["humanoidTraversalCost"].AsInt(0) ?? 0;

            if (traversalCost >= 75 && block.CollisionBoxes?.Any(box => !IsFullBlock(box)) == true)
            {
                scores["Furniture"] += 8f;
            }
        }

        private static void ApplyMetadataHeuristics(GridRecipe recipe, ItemStack stack, CollectibleObject collectible, IDictionary<string, float> scores)
        {
            if (collectible == null)
            {
                return;
            }

            List<string> metadata = CollectMetadataStrings(recipe, stack, collectible);
            if (metadata.Count == 0)
            {
                return;
            }

            HashSet<string> tokens = new HashSet<string>();
            foreach (string entry in metadata)
            {
                foreach (string token in SplitIntoTokens(entry))
                {
                    tokens.Add(token);
                }
            }

            foreach (KeywordRule rule in KeywordRules)
            {
                float weight = 0f;
                foreach (string keyword in rule.Keywords)
                {
                    if (tokens.Contains(keyword) || metadata.Any(value => value.Contains(keyword)))
                    {
                        weight += rule.Weight;
                    }
                }

                if (weight > 0f)
                {
                    scores[rule.Category] += weight;
                }
            }
        }

        private static List<string> CollectMetadataStrings(GridRecipe recipe, ItemStack stack, CollectibleObject collectible)
        {
            var metadata = new List<string>();

            void Add(string value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    metadata.Add(value.Trim().ToLowerInvariant());
                }
            }

            Add(recipe?.Name?.Domain);
            Add(recipe?.Name?.Path);

            AddAttributeMetadata(recipe?.Attributes, Add);

            AssetLocation code = collectible.Code;
            Add(code?.Domain);
            Add(code?.Path);
            Add(code?.ToShortString());

            Add(collectible.GetType().Name);

            if (collectible.CreativeInventoryTabs != null)
            {
                foreach (string tab in collectible.CreativeInventoryTabs)
                {
                    Add(tab);
                }
            }

            AddAttributeMetadata(collectible.Attributes, Add);

            try
            {
                Add(stack?.GetName());
            }
            catch
            {
                // The name lookup can fail if the stack is not fully resolved; ignore and continue.
            }

            return metadata;
        }

        private static void AddAttributeMetadata(JsonObject source, Action<string> add)
        {
            if (source == null || add == null)
            {
                return;
            }

            JToken token = source.Token;
            if (token == null)
            {
                return;
            }

            ProcessAttributeToken(token, add);
        }

        private static void ProcessAttributeToken(JToken token, Action<string> add)
        {
            switch (token)
            {
                case null:
                    return;
                case JValue value when value.Type == JTokenType.String:
                    add(value.ToString());
                    break;
                case JObject obj:
                    foreach (JProperty property in obj.Properties())
                    {
                        add(property.Name);
                        ProcessAttributeToken(property.Value, add);
                    }

                    break;
                case JArray array:
                    foreach (JToken child in array)
                    {
                        ProcessAttributeToken(child, add);
                    }

                    break;
            }
        }

        private static IEnumerable<string> SplitIntoTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            StringBuilder builder = new StringBuilder();
            char? previous = null;

            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (builder.Length > 0 && char.IsUpper(c) && previous.HasValue && char.IsLower(previous.Value))
                    {
                        yield return builder.ToString();
                        builder.Clear();
                    }

                    builder.Append(char.ToLowerInvariant(c));
                    previous = c;
                }
                else
                {
                    if (builder.Length > 0)
                    {
                        yield return builder.ToString();
                        builder.Clear();
                    }

                    previous = null;
                }
            }

            if (builder.Length > 0)
            {
                yield return builder.ToString();
            }
        }

        private static bool IsFullBlock(Cuboidf box)
        {
            return Math.Abs(box.X1) < float.Epsilon && Math.Abs(box.Y1) < float.Epsilon && Math.Abs(box.Z1) < float.Epsilon &&
                   Math.Abs(box.X2 - 1f) < float.Epsilon && Math.Abs(box.Y2 - 1f) < float.Epsilon && Math.Abs(box.Z2 - 1f) < float.Epsilon;
        }

        private readonly struct KeywordRule
        {
            public KeywordRule(string category, float weight, string[] keywords)
            {
                Category = category;
                Weight = weight;
                Keywords = keywords ?? Array.Empty<string>();
            }

            public string Category { get; }

            public float Weight { get; }

            public string[] Keywords { get; }
        }
    }
}
