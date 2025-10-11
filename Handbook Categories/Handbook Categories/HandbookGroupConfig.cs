using Newtonsoft.Json;
using System.Collections.Generic;

namespace Enhanced_Handbook
{
    internal sealed class HandbookGroupConfig
    {
        internal const string ConfigFileName = "EnhancedHandbookGroups.json";

        [JsonProperty("groups")]
        public List<HandbookGroupConfigEntry> Groups { get; set; } = new();

        internal static HandbookGroupConfig CreateDefault()
        {
            return new HandbookGroupConfig
            {
                Groups = new List<HandbookGroupConfigEntry>()
            };
        }
    }

    internal sealed class HandbookGroupConfigEntry
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("hiddenCategoryCode")]
        public string HiddenCategoryCode { get; set; } = string.Empty;

        [JsonProperty("pageCode")]
        public string PageCode { get; set; } = string.Empty;

        [JsonProperty("displayCategoryCode")]
        public string DisplayCategoryCode { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonProperty("memberPageCodes")]
        public List<string> MemberPageCodes { get; set; } = new();

        [JsonProperty("sortOrderHint")]
        public int SortOrderHint { get; set; } = int.MaxValue;

        [JsonProperty("pageNumber")]
        public int PageNumber { get; set; }

        [JsonProperty("weightSourcePageCode")]
        public string WeightSourcePageCode { get; set; }
    }
}
