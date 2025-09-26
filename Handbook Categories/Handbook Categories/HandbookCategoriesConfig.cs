using Newtonsoft.Json;
using System.Collections.Generic;

namespace Handbook_Categories
{
    internal sealed class HandbookCategoriesConfig
    {
        internal const string ConfigFileName = "HandbookCategories.json";

        [JsonProperty("categories")]
        public List<HandbookCategoryConfigEntry> Categories { get; set; } = new();

        internal static HandbookCategoriesConfig CreateDefault()
        {
            return new HandbookCategoriesConfig
            {
                Categories = new List<HandbookCategoryConfigEntry>
                {
                    new HandbookCategoryConfigEntry
                    {
                        Name = "Armor",
                        MatchWords = new List<string> { "Armor", "Body", "Lamellar", "Helmet", "Jerkin", "Greaves", "Gambeson", "Leg" },
                        ForbiddenWords = new List<string> { "Shield", "Stand", "Boiler" }
                    },
                    new HandbookCategoryConfigEntry
                    {
                        Name = "Clothes",
                        MatchWords = new List<string>
                        {
                            "Clothes", "Shirt", "Pants", "Boots", "Belt", "Hat", "Blouse", "Coat", "Amulet", "Necklace",
                            "Gloves", "Fur", "Trousers", "Cape", "Capelet", "Apron", "Vest", "Sash", "Tunic", "Bracelet",
                            "Jacket", "Shoes", "Sandals", "Coif", "Scarf", "Breeches", "Leggings", "Pendant", "Gorget",
                            "Skirt", "Cloak", "Mantle"
                        },
                        ForbiddenWords = new List<string> { "Blanket", "Rusty Gear", "Temporal Gear", "Armor" }
                    },
                    new HandbookCategoryConfigEntry
                    {
                        Name = "Tools",
                        MatchWords = new List<string>
                        {
                            "Shovel", "Cleaver", "Tongs", "Shears", "Saw", "Scythe", "Wrench", "Axe", "Hoe", "Knife",
                            "Hammer", "Pickaxe", "Prospecting", "Spear", "Sword", "Club", "Bomb", "Arrow", "Bow",
                            "Shortsword", "Falx"
                        },
                        ForbiddenWords = new List<string> { "Trap", "Soldering", "Sticks", "Helve", "Mold", "Head" }
                    },
                    new HandbookCategoryConfigEntry
                    {
                        Name = "Storage",
                        MatchWords = new List<string> { "Backpack", "Chest", "Basket", "Shelf", "Shelves", "Rack", "Display", "Trunk", "Barrel", "Sack", "Bag", "Saddlebags", "Crate" },
                        ForbiddenWords = new List<string> { "Trap", "Papyrus", "Cattails", "Beenade", "Bookshelf" }
                    },
                    new HandbookCategoryConfigEntry
                    {
                        Name = "Consumables",
                        MatchWords = new List<string> { "Poultice", "Healing", "Bandage", "Potion", "Herb", "Poison" },
                        ForbiddenWords = new List<string> { "Trap" }
                    },
                    new HandbookCategoryConfigEntry
                    {
                        Name = "Machinery",
                        MatchWords = new List<string>
                        {
                            "Helve", "Quern", "Forge", "Sail", "Gear", "Gears", "Pulverizer", "Toggle", "Rotor",
                            "Transmission", "Screw", "Chute", "Axle", "Brake", "Pounder", "Hopper"
                        },
                        ForbiddenWords = new List<string> { "Mold", "Rusty", "Temporal" }
                    }
                }
            };
        }
    }

    internal sealed class HandbookCategoryConfigEntry
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("matchWords")]
        public List<string> MatchWords { get; set; } = new();

        [JsonProperty("forbiddenWords")]
        public List<string> ForbiddenWords { get; set; } = new();
    }
}
