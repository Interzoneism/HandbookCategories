using System;
using System.Collections.Generic;
using System.Linq;
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
