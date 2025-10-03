using Newtonsoft.Json;
using System.Collections.Generic;

namespace Enhanced_Handbook
{
    internal sealed class HandbookCategoriesConfig
    {
        internal const string ConfigFileName = "EnhancedHandbook.json";

        [JsonProperty("onlyGridPages")]
        public bool OnlyGridPages { get; set; } = false;

        [JsonProperty("disableTutorialTab")]
        public bool DisableTutorialTab { get; set; }

        [JsonProperty("disableBlocksAndItemsTab")]
        public bool DisableBlocksAndItemsTab { get; set; }

        [JsonProperty("disableGuidesTab")]
        public bool DisableGuidesTab { get; set; }

        [JsonProperty("disableOriginalSearchButton")]
        public bool DisableOriginalSearchButton { get; set; }

        [JsonProperty("disableDragAndDrop")]
        public bool DisableDragAndDrop { get; set; }

        [JsonProperty("usesEnglishDefaults")]
        public bool UsesEnglishDefaults { get; set; }

        [JsonProperty("categories")]
        public List<HandbookCategoryConfigEntry> Categories { get; set; } = new();

        internal static HandbookCategoriesConfig CreateDefault()
        {
            return new HandbookCategoriesConfig
            {
                OnlyGridPages = false,
                DisableTutorialTab = false,
                DisableBlocksAndItemsTab = false,
                DisableGuidesTab = false,
                DisableOriginalSearchButton = true,
                DisableDragAndDrop = false,
                UsesEnglishDefaults = false,
                Categories = new List<HandbookCategoryConfigEntry>()
            };
        }

        internal static HandbookCategoriesConfig CreateWithDefaultCategories()
        {
            HandbookCategoriesConfig config = CreateDefault();
            config.UsesEnglishDefaults = true;
            config.Categories = CreateDefaultCategories();
            return config;
        }

        internal static List<HandbookCategoryConfigEntry> CreateDefaultCategories()
        {
            return new List<HandbookCategoryConfigEntry>
            {
                new HandbookCategoryConfigEntry
                {
                    Name = "Armor",
                    MatchWords = new List<string> { "Armor", "Body", "Lamellar", "Helmet", "Jerkin", "Greaves", "Gambeson", "Leg" },
                    MatchTitleWords = new List<string>(),
                    ForbiddenWords = new List<string> { "Shield", "Stand", "Boiler", "Rack" },
                    ForbiddenTitleWords = new List<string>(),
                    TabBackgroundColor = HandbookCategoryColors.DefaultColorName
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
                    MatchTitleWords = new List<string>(),
                    ForbiddenWords = new List<string>
                    {
                        "Blanket", "Rusty Gear", "Temporal Gear", "Armor", "Rack", "Stand", "Witch", "Mourning"
                    },
                    ForbiddenTitleWords = new List<string> { "mantle" },
                    TabBackgroundColor = HandbookCategoryColors.DefaultColorName
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
                    MatchTitleWords = new List<string>(),
                    ForbiddenWords = new List<string>
                    {
                        "Trap", "Soldering", "Sticks", "Helve", "Mold", "Head", "Pedestal", "ruined", "blade"
                    },
                    ForbiddenTitleWords = new List<string>(),
                    TabBackgroundColor = HandbookCategoryColors.DefaultColorName
                },
                new HandbookCategoryConfigEntry
                {
                    Name = "Storage",
                    MatchWords = new List<string>
                    {
                        "Backpack", "Chest", "Basket", "Shelf", "Shelves", "Rack", "Display", "Trunk", "Barrel",
                        "Sack", "Bag", "Saddlebags", "Crate", "Bookshelf"
                    },
                    MatchTitleWords = new List<string>(),
                    ForbiddenWords = new List<string>
                    {
                        "Trap", "Papyrus", "Cattails", "Beenade", "Bricks", "Planks", "Stone", "ruined", "full of", "coral"
                    },
                    ForbiddenTitleWords = new List<string>(),
                    TabBackgroundColor = HandbookCategoryColors.DefaultColorName
                },
                new HandbookCategoryConfigEntry
                {
                    Name = "Consumables",
                    MatchWords = new List<string> { "Poultice", "Healing", "Bandage", "Potion", "Herb", "Poison" },
                    MatchTitleWords = new List<string>(),
                    ForbiddenWords = new List<string> { "Trap" },
                    ForbiddenTitleWords = new List<string>(),
                    TabBackgroundColor = HandbookCategoryColors.DefaultColorName
                },
                new HandbookCategoryConfigEntry
                {
                    Name = "Machinery",
                    MatchWords = new List<string>
                    {
                        "Helve", "Quern", "Forge", "Sail", "Gear", "Gears", "Pulverizer", "Toggle", "Rotor",
                        "Transmission", "Screw", "Chute", "Axle", "Brake", "Pounder", "Hopper"
                    },
                    MatchTitleWords = new List<string>(),
                    ForbiddenWords = new List<string>
                    {
                        "Mold", "Rusty", "Temporal", "Figurehead", "Bricks", "Planks", "Stone", "shattered", "head", "ruined",
                        "amulet", "elk", "jonas"
                    },
                    ForbiddenTitleWords = new List<string>(),
                    TabBackgroundColor = HandbookCategoryColors.DefaultColorName
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

        [JsonProperty("matchTitleWords")]
        public List<string> MatchTitleWords { get; set; } = new();

        [JsonProperty("forbiddenWords")]
        public List<string> ForbiddenWords { get; set; } = new();

        [JsonProperty("forbiddenTitleWords")]
        public List<string> ForbiddenTitleWords { get; set; } = new();

        [JsonProperty("tabBackgroundColor")]
        public string TabBackgroundColor { get; set; } = HandbookCategoryColors.DefaultColorName;
    }
}
