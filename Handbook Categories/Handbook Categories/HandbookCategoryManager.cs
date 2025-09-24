using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Handbook_Categories
{
    internal static class HandbookCategoryManager
    {
        private const string CategoryCodePrefix = "handbookcategories-";
        private const string TranslationPrefix = "handbookcategories:tab-";

        private static readonly Dictionary<string, List<GuiHandbookPage>> pagesByCategory = new();
        private static readonly Dictionary<string, string> displayNameByCategory = new();
        private static readonly Dictionary<string, string> translationKeyByCategory = new();
        private static readonly List<string> orderedCategories = new();

        private static ICoreClientAPI capi;

        internal static bool IsReady => capi?.World?.GridRecipes != null;

        internal static void Initialize(ICoreClientAPI api)
        {
            capi = api;
        }

        internal static void Clear()
        {
            pagesByCategory.Clear();
            displayNameByCategory.Clear();
            translationKeyByCategory.Clear();
            orderedCategories.Clear();
        }

        internal static bool HasCategories => orderedCategories.Count > 0;

        internal static IEnumerable<string> OrderedCategoryCodes => orderedCategories;

        internal static bool IsManagedCategory(string categoryCode)
        {
            return !string.IsNullOrEmpty(categoryCode) && pagesByCategory.ContainsKey(categoryCode);
        }

        internal static string GetTabDisplayName(string categoryCode)
        {
            if (categoryCode == null)
            {
                return string.Empty;
            }

            if (!displayNameByCategory.TryGetValue(categoryCode, out string fallback))
            {
                return categoryCode;
            }

            if (!translationKeyByCategory.TryGetValue(categoryCode, out string translationKey))
            {
                return fallback;
            }

            string translated = Lang.GetMatchingIfExists(translationKey);
            return string.IsNullOrEmpty(translated) ? fallback : translated;
        }

        internal static void RebuildCategories(List<GuiHandbookPage> allPages)
        {
            if (capi?.World?.GridRecipes == null || allPages == null || allPages.Count == 0)
            {
                Clear();
                return;
            }

            var itemPagesByCode = allPages
                .OfType<GuiHandbookItemStackPage>()
                .Where(page => page?.Stack?.Collectible != null)
                .GroupBy(page => page.PageCode)
                .ToDictionary(group => group.Key, group => group.First());

            Dictionary<string, List<GuiHandbookPage>> categorizedPages = new();
            Dictionary<string, HashSet<string>> seenPageCodes = new();
            Dictionary<string, string> displayNames = new();
            Dictionary<string, string> translationKeys = new();

            foreach (GridRecipe recipe in capi.World.GridRecipes)
            {
                if (recipe == null || recipe.Output?.ResolvedItemstack == null || !recipe.ShowInCreatedBy)
                {
                    continue;
                }

                GuiHandbookItemStackPage page = FindPageForRecipe(recipe, itemPagesByCode);
                if (page == null)
                {
                    continue;
                }

                string categoryName = GridRecipeCategorizer.Categorize(recipe);
                string sanitized = Sanitize(categoryName);
                string categoryCode = $"{CategoryCodePrefix}{sanitized}";

                if (!categorizedPages.TryGetValue(categoryCode, out List<GuiHandbookPage> list))
                {
                    list = new List<GuiHandbookPage>();
                    categorizedPages[categoryCode] = list;
                    seenPageCodes[categoryCode] = new HashSet<string>();
                    displayNames[categoryCode] = categoryName;
                    translationKeys[categoryCode] = $"{TranslationPrefix}{sanitized}";
                }

                if (seenPageCodes[categoryCode].Add(page.PageCode))
                {
                    list.Add(page);
                }
            }

            pagesByCategory.Clear();
            displayNameByCategory.Clear();
            translationKeyByCategory.Clear();
            orderedCategories.Clear();

            foreach (string categoryName in GridRecipeCategorizer.AllCategories)
            {
                string sanitized = Sanitize(categoryName);
                string categoryCode = $"{CategoryCodePrefix}{sanitized}";
                if (!categorizedPages.TryGetValue(categoryCode, out List<GuiHandbookPage> list) || list.Count == 0)
                {
                    continue;
                }

                list.Sort((a, b) => a.PageNumber.CompareTo(b.PageNumber));

                pagesByCategory[categoryCode] = list;
                displayNameByCategory[categoryCode] = displayNames[categoryCode];
                translationKeyByCategory[categoryCode] = translationKeys[categoryCode];
                orderedCategories.Add(categoryCode);
            }
        }

        internal static void ApplyCategoryFilter(string categoryCode, List<IFlatListItem> shownPages, GuiComposer overviewGui, string currentSearchText, bool loadingPages, double listHeight)
        {
            if (shownPages == null)
            {
                return;
            }

            shownPages.Clear();

            if (!pagesByCategory.TryGetValue(categoryCode, out List<GuiHandbookPage> pages) || loadingPages)
            {
                UpdateScrollArea(overviewGui, listHeight);
                return;
            }

            string[] searchTerms = PrepareSearchTerms(currentSearchText, out bool requireAllMatches);

            List<WeightedHandbookPage> weightedPages = new();
            foreach (GuiHandbookPage page in pages)
            {
                if (page == null || page.IsDuplicate)
                {
                    continue;
                }

                float weight = 1f;
                bool matches = requireAllMatches;
                for (int i = 0; i < searchTerms.Length; i++)
                {
                    weight = page.GetTextMatchWeight(searchTerms[i]);
                    if (weight > 0f)
                    {
                        if (!requireAllMatches)
                        {
                            matches = true;
                            break;
                        }
                    }
                    else if (requireAllMatches)
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches || searchTerms.Length == 0)
                {
                    weightedPages.Add(new WeightedHandbookPage
                    {
                        Page = page,
                        Weight = weight
                    });
                }
            }

            foreach (WeightedHandbookPage weighted in weightedPages.OrderByDescending(w => w.Weight))
            {
                shownPages.Add(weighted.Page);
            }

            UpdateScrollArea(overviewGui, listHeight);
        }

        private static GuiHandbookItemStackPage FindPageForRecipe(GridRecipe recipe, Dictionary<string, GuiHandbookItemStackPage> itemPagesByCode)
        {
            ItemStack output = recipe.Output?.ResolvedItemstack;
            if (output == null)
            {
                return null;
            }

            string pageCode = GuiHandbookItemStackPage.PageCodeForStack(output);
            if (itemPagesByCode.TryGetValue(pageCode, out GuiHandbookItemStackPage page))
            {
                return page;
            }

            if (output.Collectible != null)
            {
                pageCode = GuiHandbookItemStackPage.PageCodeForStack(new ItemStack(output.Collectible));
                if (itemPagesByCode.TryGetValue(pageCode, out page))
                {
                    return page;
                }
            }

            return null;
        }

        private static string[] PrepareSearchTerms(string currentSearchText, out bool requireAllMatches)
        {
            requireAllMatches = false;

            if (string.IsNullOrEmpty(currentSearchText))
            {
                return Array.Empty<string>();
            }

            string text = currentSearchText.ToLowerInvariant();
            string[] parts;

            if (text.Contains(" or ", StringComparison.Ordinal))
            {
                parts = text.Split(new[] { " or " }, StringSplitOptions.RemoveEmptyEntries)
                    .OrderBy(str => str.Length)
                    .ToArray();
            }
            else if (text.Contains(" and ", StringComparison.Ordinal))
            {
                parts = text.Split(new[] { " and " }, StringSplitOptions.RemoveEmptyEntries)
                    .OrderBy(str => str.Length)
                    .ToArray();
                requireAllMatches = parts.Length > 1;
            }
            else
            {
                parts = new[] { text };
            }

            int emptyCount = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].ToSearchFriendly().Trim();
                if (parts[i].Length == 0)
                {
                    emptyCount++;
                }
            }

            if (emptyCount > 0)
            {
                string[] filtered = new string[parts.Length - emptyCount];
                int index = 0;
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i].Length != 0)
                    {
                        filtered[index++] = parts[i];
                    }
                }

                parts = filtered;
                requireAllMatches = requireAllMatches && parts.Length > 1;
            }

            return parts;
        }

        private static void UpdateScrollArea(GuiComposer overviewGui, double listHeight)
        {
            if (overviewGui == null)
            {
                return;
            }

            GuiElementFlatList flatList = overviewGui.GetFlatList("stacklist");
            if (flatList == null)
            {
                return;
            }

            flatList.CalcTotalHeight();
            overviewGui.GetScrollbar("scrollbar")?.SetHeights((float)listHeight, (float)flatList.insideBounds.fixedHeight);
        }

        private static string Sanitize(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return string.Empty;
            }

            StringBuilder builder = new(categoryName.Length);
            foreach (char ch in categoryName.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(ch);
                }
                else if (char.IsWhiteSpace(ch) || ch == '-' || ch == '_')
                {
                    if (builder.Length == 0 || builder[^1] == '-')
                    {
                        continue;
                    }

                    builder.Append('-');
                }
            }

            if (builder.Length == 0)
            {
                return categoryName.ToLowerInvariant();
            }

            if (builder[^1] == '-')
            {
                builder.Length--;
            }

            return builder.ToString();
        }
    }
}
