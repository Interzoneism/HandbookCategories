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
        private static readonly Dictionary<string, double[]> tabBackgroundByCategory = new();

        private static bool onlyGridPages = true;
        private static bool showTutorialTab = true;
        private static bool showBlocksAndItemsTab = true;
        private static bool showGuidesTab = true;

        private sealed class WordCategoryDefinition
        {
            private readonly double[] tabBackgroundColor;

            internal WordCategoryDefinition(string categoryName, string sanitizedName, string[] matchWords, string[] matchPhrases, string[] forbiddenWords, string[] forbiddenPhrases, double[] backgroundColor)
            {
                CategoryName = categoryName ?? string.Empty;
                SanitizedName = sanitizedName ?? string.Empty;
                CategoryCode = $"{CategoryCodePrefix}{SanitizedName}";
                TranslationKey = $"{TranslationPrefix}{SanitizedName}";
                MatchWords = matchWords ?? Array.Empty<string>();
                MatchPhrases = matchPhrases ?? Array.Empty<string>();
                ForbiddenWords = forbiddenWords ?? Array.Empty<string>();
                ForbiddenPhrases = forbiddenPhrases ?? Array.Empty<string>();
                tabBackgroundColor = NormalizeColor(backgroundColor);
            }

            internal string CategoryName { get; }

            internal string SanitizedName { get; }

            internal string CategoryCode { get; }

            internal string TranslationKey { get; }

            internal string[] MatchWords { get; }

            internal string[] MatchPhrases { get; }

            internal string[] ForbiddenWords { get; }

            internal string[] ForbiddenPhrases { get; }

            internal double[] BackgroundColor => (double[])tabBackgroundColor.Clone();

            internal bool MatchesAnyPhrase(string normalizedTitle)
            {
                if (MatchPhrases.Length == 0 || string.IsNullOrEmpty(normalizedTitle))
                {
                    return false;
                }

                for (int i = 0; i < MatchPhrases.Length; i++)
                {
                    if (normalizedTitle.IndexOf(MatchPhrases[i], StringComparison.Ordinal) >= 0)
                    {
                        return true;
                    }
                }

                return false;
            }

            internal bool IsForbidden(string normalizedTitle, HashSet<string> wordsInTitle)
            {
                if (ForbiddenWords.Length > 0 && wordsInTitle != null)
                {
                    foreach (string forbidden in ForbiddenWords)
                    {
                        if (wordsInTitle.Contains(forbidden))
                        {
                            return true;
                        }
                    }
                }

                if (ForbiddenPhrases.Length > 0 && !string.IsNullOrEmpty(normalizedTitle))
                {
                    foreach (string phrase in ForbiddenPhrases)
                    {
                        if (normalizedTitle.IndexOf(phrase, StringComparison.Ordinal) >= 0)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            private static double[] NormalizeColor(double[] color)
            {
                if (color == null || color.Length < 4)
                {
                    return HandbookCategoryColors.GetDefaultBackgroundColor();
                }

                double[] copy = new double[4];
                Array.Copy(color, copy, 4);
                return copy;
            }
        }

        private readonly struct SearchQuery
        {
            internal SearchQuery(SearchTerm[] includeTerms, SearchTerm[] excludeTerms, bool requiresAllMatches)
            {
                IncludeTerms = includeTerms ?? Array.Empty<SearchTerm>();
                ExcludeTerms = excludeTerms ?? Array.Empty<SearchTerm>();
                RequiresAllMatches = requiresAllMatches && IncludeTerms.Length > 0;
            }

            internal SearchTerm[] IncludeTerms { get; }

            internal SearchTerm[] ExcludeTerms { get; }

            internal bool RequiresAllMatches { get; }

            internal bool HasFilters => IncludeTerms.Length > 0 || ExcludeTerms.Length > 0;
        }

        private readonly struct SearchTerm
        {
            internal SearchTerm(string term, bool isExactMatch)
            {
                Term = term;
                IsExactMatch = isExactMatch;
            }

            internal string Term { get; }

            internal bool IsExactMatch { get; }
        }

        private static WordCategoryDefinition[] wordCategories = Array.Empty<WordCategoryDefinition>();
        private static readonly Dictionary<string, List<WordCategoryDefinition>> categoriesByMatchWord = new(StringComparer.OrdinalIgnoreCase);
        private static WordCategoryDefinition[] categoriesWithMatchPhrases = Array.Empty<WordCategoryDefinition>();

        private static ICoreClientAPI capi;

        internal static bool IsReady => capi?.World != null && (capi.World.GridRecipes != null || !onlyGridPages);

        internal static void Initialize(ICoreClientAPI api)
        {
            capi = api;
            ReloadConfiguration();
        }

        internal static void ReloadConfiguration()
        {
            if (capi == null)
            {
                wordCategories = Array.Empty<WordCategoryDefinition>();
                onlyGridPages = true;
                showTutorialTab = true;
                showBlocksAndItemsTab = true;
                showGuidesTab = true;
                return;
            }

            bool shouldStoreConfig = false;
            HandbookCategoriesConfig config = capi.LoadModConfig<HandbookCategoriesConfig>(HandbookCategoriesConfig.ConfigFileName);

            if (config == null)
            {
                config = LoadDefaultConfiguration() ?? HandbookCategoriesConfig.CreateDefault();
                shouldStoreConfig = true;
            }

            wordCategories = BuildWordCategories(config);

            if (wordCategories.Length == 0)
            {
                config = HandbookCategoriesConfig.CreateDefault();
                wordCategories = BuildWordCategories(config);
                shouldStoreConfig = true;
            }

            onlyGridPages = config?.OnlyGridPages ?? true;
            showTutorialTab = !(config?.DisableTutorialTab ?? false);
            showBlocksAndItemsTab = !(config?.DisableBlocksAndItemsTab ?? false);
            showGuidesTab = !(config?.DisableGuidesTab ?? false);

            if (shouldStoreConfig)
            {
                capi.StoreModConfig(config, HandbookCategoriesConfig.ConfigFileName);
            }
        }

        internal static bool ShouldDisplayVanillaTab(string categoryCode)
        {
            if (categoryCode == null)
            {
                return true;
            }

            if (categoryCode.Equals("tutorial", StringComparison.OrdinalIgnoreCase))
            {
                return showTutorialTab;
            }

            if (categoryCode.Equals("blocksitems", StringComparison.OrdinalIgnoreCase) || categoryCode.Equals("stack", StringComparison.OrdinalIgnoreCase))
            {
                return showBlocksAndItemsTab;
            }

            if (categoryCode.Equals("guides", StringComparison.OrdinalIgnoreCase) || categoryCode.Equals("guide", StringComparison.OrdinalIgnoreCase))
            {
                return showGuidesTab;
            }

            return true;
        }

        internal static void Clear()
        {
            pagesByCategory.Clear();
            displayNameByCategory.Clear();
            translationKeyByCategory.Clear();
            orderedCategories.Clear();
            tabBackgroundByCategory.Clear();
        }

        internal static bool HasCategories => orderedCategories.Count > 0;

        internal static IEnumerable<string> OrderedCategoryCodes => orderedCategories;

        internal static bool IsManagedCategory(string categoryCode)
        {
            return !string.IsNullOrEmpty(categoryCode) && pagesByCategory.ContainsKey(categoryCode);
        }

        internal static bool TryGetCategoryPages(string categoryCode, out List<GuiHandbookPage> pages)
        {
            if (!string.IsNullOrEmpty(categoryCode) && pagesByCategory.TryGetValue(categoryCode, out List<GuiHandbookPage> managedPages))
            {
                pages = managedPages;
                return true;
            }

            pages = null;
            return false;
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

        internal static double[] GetTabBackgroundColor(string categoryCode)
        {
            if (!string.IsNullOrEmpty(categoryCode) && tabBackgroundByCategory.TryGetValue(categoryCode, out double[] color) && color != null)
            {
                return (double[])color.Clone();
            }

            return HandbookCategoryColors.GetDefaultBackgroundColor();
        }

        internal static void RebuildCategories(List<GuiHandbookPage> allPages)
        {
            if (capi?.World == null || allPages == null || allPages.Count == 0)
            {
                Clear();
                return;
            }

            if (onlyGridPages && capi.World.GridRecipes == null)
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
            ISet<string> gridRecipePageCodes = onlyGridPages ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : null;

            void AddPageToCategory(WordCategoryDefinition definition, GuiHandbookPage page)
            {
                if (page == null || definition == null || string.IsNullOrWhiteSpace(definition.CategoryCode))
                {
                    return;
                }

                string categoryCode = definition.CategoryCode;

                if (!categorizedPages.TryGetValue(categoryCode, out List<GuiHandbookPage> list))
                {
                    list = new List<GuiHandbookPage>();
                    categorizedPages[categoryCode] = list;
                    seenPageCodes[categoryCode] = new HashSet<string>();
                    displayNames[categoryCode] = definition.CategoryName;
                    translationKeys[categoryCode] = definition.TranslationKey;
                }

                if (seenPageCodes[categoryCode].Add(page.PageCode))
                {
                    list.Add(page);
                }
            }

            if (onlyGridPages)
            {
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

                    if (!string.IsNullOrEmpty(page.PageCode))
                    {
                        gridRecipePageCodes?.Add(page.PageCode);
                    }
                }
            }

            ApplyWordBasedCategories(itemPagesByCode.Values, gridRecipePageCodes, AddPageToCategory);


            pagesByCategory.Clear();
            displayNameByCategory.Clear();
            translationKeyByCategory.Clear();
            orderedCategories.Clear();
            tabBackgroundByCategory.Clear();

            foreach (WordCategoryDefinition definition in wordCategories)
            {
                string categoryCode = definition.CategoryCode;
                if (string.IsNullOrEmpty(categoryCode) || !categorizedPages.TryGetValue(categoryCode, out List<GuiHandbookPage> list) || list.Count == 0)
                {
                    continue;
                }

                list.Sort((a, b) => a.PageNumber.CompareTo(b.PageNumber));

                pagesByCategory[categoryCode] = list;
                displayNameByCategory[categoryCode] = displayNames[categoryCode];
                translationKeyByCategory[categoryCode] = translationKeys[categoryCode];
                orderedCategories.Add(categoryCode);
                tabBackgroundByCategory[categoryCode] = definition.BackgroundColor;
            }
        }

        private static void AddWordFromBuilder(StringBuilder builder, HashSet<string> words)
        {
            if (builder == null || words == null || builder.Length == 0)
            {
                return;
            }

            words.Add(builder.ToString());
            builder.Clear();
        }

        private static void ApplyWordBasedCategories(IEnumerable<GuiHandbookItemStackPage> pages, ISet<string> gridRecipePageCodes, Action<WordCategoryDefinition, GuiHandbookPage> addPageAction)
        {
            if (pages == null || addPageAction == null)
            {
                return;
            }

            if (categoriesByMatchWord.Count == 0 && categoriesWithMatchPhrases.Length == 0)
            {
                return;
            }

            bool requireGridPages = onlyGridPages;
            HashSet<WordCategoryDefinition> matchedDefinitions = new();

            foreach (GuiHandbookItemStackPage page in pages)
            {
                if (page?.Stack?.Collectible == null)
                {
                    continue;
                }

                string pageCode = page.PageCode;
                if (string.IsNullOrEmpty(pageCode))
                {
                    continue;
                }

                if (requireGridPages)
                {
                    if (gridRecipePageCodes == null || !gridRecipePageCodes.Contains(pageCode))
                    {
                        continue;
                    }
                }

                string title = page.Stack.GetName();
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                string normalizedTitle = title.ToLowerInvariant();
                HashSet<string> wordsInTitle = ExtractWords(normalizedTitle);
                matchedDefinitions.Clear();

                foreach (string word in wordsInTitle)
                {
                    if (!categoriesByMatchWord.TryGetValue(word, out List<WordCategoryDefinition> definitions))
                    {
                        continue;
                    }

                    foreach (WordCategoryDefinition definition in definitions)
                    {
                        if (!matchedDefinitions.Add(definition) || definition.IsForbidden(normalizedTitle, wordsInTitle))
                        {
                            continue;
                        }

                        addPageAction(definition, page);
                    }
                }

                for (int i = 0; i < categoriesWithMatchPhrases.Length; i++)
                {
                    WordCategoryDefinition definition = categoriesWithMatchPhrases[i];

                    if (!matchedDefinitions.Contains(definition) && definition.MatchesAnyPhrase(normalizedTitle) && !definition.IsForbidden(normalizedTitle, wordsInTitle))
                    {
                        matchedDefinitions.Add(definition);
                        addPageAction(definition, page);
                    }
                }
            }
        }

        internal static void ApplyCategoryFilter(string categoryCode, IEnumerable<GuiHandbookPage> candidatePages, List<IFlatListItem> shownPages, GuiComposer overviewGui, string currentSearchText, bool loadingPages, double listHeight)
        {
            if (shownPages == null)
            {
                return;
            }

            shownPages.Clear();

            if (loadingPages)
            {
                UpdateScrollArea(overviewGui, listHeight);
                return;
            }

            IEnumerable<GuiHandbookPage> pagesToFilter = candidatePages;

            if (pagesToFilter == null)
            {
                if (!TryGetCategoryPages(categoryCode, out List<GuiHandbookPage> managedPages))
                {
                    UpdateScrollArea(overviewGui, listHeight);
                    return;
                }

                pagesToFilter = managedPages;
            }

            SearchQuery searchQuery = PrepareSearchTerms(currentSearchText);

            List<WeightedHandbookPage> weightedPages = new();
            foreach (GuiHandbookPage page in pagesToFilter)
            {
                if (page == null || page.IsDuplicate)
                {
                    continue;
                }

                if (MatchesSearchQuery(page, searchQuery, out float weight))
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

        private static bool MatchesSearchQuery(GuiHandbookPage page, SearchQuery searchQuery, out float weight)
        {
            weight = 1f;

            if (!searchQuery.HasFilters)
            {
                return true;
            }

            string normalizedTitle = GetNormalizedTitle(page);
            HashSet<string> titleWords = ExtractWords(normalizedTitle);

            float bestWeight = 0f;

            if (searchQuery.IncludeTerms.Length > 0)
            {
                bool requiresAll = searchQuery.RequiresAllMatches;
                bool hasMatch = false;

                for (int i = 0; i < searchQuery.IncludeTerms.Length; i++)
                {
                    SearchTerm term = searchQuery.IncludeTerms[i];
                    if (DoesTitleContainTerm(normalizedTitle, titleWords, term))
                    {
                        hasMatch = true;
                        float termWeight = page.GetTextMatchWeight(term.Term);
                        if (termWeight > bestWeight)
                        {
                            bestWeight = termWeight;
                        }
                    }
                    else if (requiresAll)
                    {
                        return false;
                    }
                }

                if (!hasMatch)
                {
                    return false;
                }
            }

            for (int i = 0; i < searchQuery.ExcludeTerms.Length; i++)
            {
                if (DoesTitleContainTerm(normalizedTitle, titleWords, searchQuery.ExcludeTerms[i]))
                {
                    return false;
                }
            }

            weight = bestWeight > 0f ? bestWeight : 1f;
            return true;
        }

        private static bool DoesTitleContainTerm(string normalizedTitle, HashSet<string> titleWords, SearchTerm term)
        {
            if (string.IsNullOrEmpty(term.Term) || string.IsNullOrEmpty(normalizedTitle))
            {
                return false;
            }

            if (term.IsExactMatch)
            {
                if (term.Term.IndexOf(' ', StringComparison.Ordinal) >= 0)
                {
                    return normalizedTitle.IndexOf(term.Term, StringComparison.Ordinal) >= 0;
                }

                return titleWords != null && titleWords.Contains(term.Term);
            }

            if (normalizedTitle.IndexOf(term.Term, StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            if (term.Term.IndexOf(' ', StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            return titleWords != null && titleWords.Contains(term.Term);
        }

        private static string GetNormalizedTitle(GuiHandbookPage page)
        {
            if (page == null)
            {
                return string.Empty;
            }

            string rawTitle = page switch
            {
                GuiHandbookGroupedItemstackPage groupedPage when !string.IsNullOrWhiteSpace(groupedPage.Name) => groupedPage.Name,
                GuiHandbookItemStackPage itemPage when !string.IsNullOrWhiteSpace(itemPage.TextCacheTitle) => itemPage.TextCacheTitle,
                GuiHandbookItemStackPage itemPage when itemPage.Stack != null => itemPage.Stack.GetName(),
                GuiHandbookCommandPage commandPage => commandPage.TextCacheTitle,
                GuiHandbookMealRecipePage mealPage when !string.IsNullOrWhiteSpace(mealPage.Title) => Lang.Get(mealPage.Title),
                GuiHandbookTextPage textPage when !string.IsNullOrWhiteSpace(textPage.Title) => Lang.Get(textPage.Title),
                _ => page.PageCode
            } ?? string.Empty;

            if (string.IsNullOrWhiteSpace(rawTitle))
            {
                return string.Empty;
            }

            return rawTitle.ToSearchFriendly().ToLowerInvariant().Trim();
        }

        private static SearchQuery PrepareSearchTerms(string currentSearchText)
        {
            if (string.IsNullOrWhiteSpace(currentSearchText))
            {
                return new SearchQuery(Array.Empty<SearchTerm>(), Array.Empty<SearchTerm>(), false);
            }

            string text = currentSearchText.ToLowerInvariant();
            int startIndex = 0;
            while (startIndex < text.Length && char.IsWhiteSpace(text[startIndex]))
            {
                startIndex++;
            }

            bool additiveSearch = startIndex < text.Length && text[startIndex] == '#';
            if (additiveSearch)
            {
                startIndex++;
            }

            if (startIndex > 0)
            {
                text = text.Substring(startIndex);
            }
            List<SearchTerm> includeTerms = new();
            List<SearchTerm> excludeTerms = new();
            StringBuilder builder = new();
            bool inQuotes = false;
            bool excludeNext = false;
            bool buildingExactMatch = false;

            void CommitCurrentToken(bool exactMatch)
            {
                if (builder.Length == 0)
                {
                    return;
                }

                string raw = builder.ToString();
                builder.Clear();

                string term = raw.ToSearchFriendly().Trim();
                if (term.Length == 0)
                {
                    excludeNext = false;
                    buildingExactMatch = false;
                    return;
                }

                if (excludeNext)
                {
                    excludeTerms.Add(new SearchTerm(term, exactMatch));
                }
                else
                {
                    includeTerms.Add(new SearchTerm(term, exactMatch));
                }

                excludeNext = false;
                buildingExactMatch = false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];

                if (ch == '"')
                {
                    if (inQuotes)
                    {
                        CommitCurrentToken(true);
                        inQuotes = false;
                    }
                    else
                    {
                        if (builder.Length > 0)
                        {
                            CommitCurrentToken(false);
                        }

                        inQuotes = true;
                        buildingExactMatch = true;
                    }
                }
                else if (!inQuotes && char.IsWhiteSpace(ch))
                {
                    if (builder.Length > 0)
                    {
                        CommitCurrentToken(false);
                    }
                }
                else if (!inQuotes && ch == '!')
                {
                    if (builder.Length == 0)
                    {
                        excludeNext = true;
                    }
                    else
                    {
                        builder.Append(ch);
                    }
                }
                else
                {
                    builder.Append(ch);
                }
            }

            if (builder.Length > 0)
            {
                CommitCurrentToken(buildingExactMatch);
            }

            bool containsOr = false;
            bool containsAnd = false;
            for (int i = includeTerms.Count - 1; i >= 0; i--)
            {
                SearchTerm term = includeTerms[i];
                if (!term.IsExactMatch && term.Term == "or")
                {
                    includeTerms.RemoveAt(i);
                    containsOr = true;
                }
                else if (!term.IsExactMatch && term.Term == "and")
                {
                    includeTerms.RemoveAt(i);
                    containsAnd = true;
                }
            }

            SearchTerm[] includes = includeTerms.ToArray();
            SearchTerm[] excludes = excludeTerms.ToArray();
            bool requireAllMatches = additiveSearch
                ? false
                : containsOr ? false : includes.Length > 1 || containsAnd;

            return new SearchQuery(includes, excludes, requireAllMatches);
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

        private static HashSet<string> ExtractWords(string text)
        {
            HashSet<string> words = new(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(text))
            {
                return words;
            }

            StringBuilder builder = new();

            foreach (char ch in text)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(ch);
                }
                else
                {
                    AddWordFromBuilder(builder, words);
                }
            }

            AddWordFromBuilder(builder, words);

            return words;
        }

        private static WordCategoryDefinition[] BuildWordCategories(HandbookCategoriesConfig config)
        {
            categoriesByMatchWord.Clear();
            categoriesWithMatchPhrases = Array.Empty<WordCategoryDefinition>();

            if (config?.Categories == null)
            {
                return Array.Empty<WordCategoryDefinition>();
            }

            List<WordCategoryDefinition> definitions = new();

            foreach (HandbookCategoryConfigEntry entry in config.Categories)
            {
                if (entry == null)
                {
                    continue;
                }

                string name = entry.Name?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                string sanitized = Sanitize(name);
                if (string.IsNullOrEmpty(sanitized))
                {
                    continue;
                }

                string[] matchWords = NormalizeWords(entry.MatchWords);
                string[] forbiddenWords = NormalizeWords(entry.ForbiddenWords);

                List<string> matchSinglesList = new(matchWords.Length);
                List<string> matchPhrasesList = new();
                for (int i = 0; i < matchWords.Length; i++)
                {
                    string word = matchWords[i];
                    if (word.IndexOf(' ', StringComparison.Ordinal) >= 0)
                    {
                        matchPhrasesList.Add(word);
                    }
                    else
                    {
                        matchSinglesList.Add(word);
                    }
                }

                List<string> forbiddenSinglesList = new(forbiddenWords.Length);
                List<string> forbiddenPhrasesList = new();
                for (int i = 0; i < forbiddenWords.Length; i++)
                {
                    string word = forbiddenWords[i];
                    if (word.IndexOf(' ', StringComparison.Ordinal) >= 0)
                    {
                        forbiddenPhrasesList.Add(word);
                    }
                    else
                    {
                        forbiddenSinglesList.Add(word);
                    }
                }

                string[] matchSingles = matchSinglesList.ToArray();
                string[] matchPhrases = matchPhrasesList.ToArray();
                string[] forbiddenSingles = forbiddenSinglesList.ToArray();
                string[] forbiddenPhrases = forbiddenPhrasesList.ToArray();

                double[] backgroundColor = HandbookCategoryColors.ResolveBackgroundColor(entry.TabBackgroundColor, out bool usedFallback);
                if (usedFallback && !string.IsNullOrWhiteSpace(entry.TabBackgroundColor))
                {
                    capi?.Logger?.Warning("[HandbookCategories] Unknown tab background color \"{0}\" for category \"{1}\". Using default color.", entry.TabBackgroundColor, name);
                }

                WordCategoryDefinition definition = new(name, sanitized, matchSingles, matchPhrases, forbiddenSingles, forbiddenPhrases, backgroundColor);
                definitions.Add(definition);

                for (int i = 0; i < matchSingles.Length; i++)
                {
                    string word = matchSingles[i];
                    if (!categoriesByMatchWord.TryGetValue(word, out List<WordCategoryDefinition> list))
                    {
                        list = new List<WordCategoryDefinition>();
                        categoriesByMatchWord[word] = list;
                    }

                    list.Add(definition);
                }
            }

            categoriesWithMatchPhrases = definitions.Where(definition => definition.MatchPhrases.Length > 0).ToArray();

            return definitions.ToArray();
        }

        private static HandbookCategoriesConfig LoadDefaultConfiguration()
        {
            try
            {
                var asset = capi.Assets.TryGet(new AssetLocation("handbookcategories", "config/categories.json"));
                return asset?.ToObject<HandbookCategoriesConfig>();
            }
            catch (Exception e)
            {
                capi.Logger?.Warning("Failed to load handbook categories config from assets: {0}", e.Message);
                return null;
            }
        }

        private static string[] NormalizeWords(IEnumerable<string> rawWords)
        {
            if (rawWords == null)
            {
                return Array.Empty<string>();
            }

            return rawWords
                .Select(word => word?.Trim())
                .Where(word => !string.IsNullOrEmpty(word))
                .Select(word => word.ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
