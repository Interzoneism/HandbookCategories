using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Enhanced_Handbook
{
    internal static class HandbookCategoryManager
    {
        private const string CategoryCodePrefix = "handbookcategories-";
        private const string TranslationPrefix = "handbookcategories:tab-";

        internal const string RecipesOnlyToggleTranslationKey = "enhancedhandbook:toggle-recipes-only";
        internal const string OriginalSearchToggleTranslationKey = "enhancedhandbook:toggle-original-search";
        private const string CreateCategoryButtonTranslationKey = "enhancedhandbook:button-create-category";
        private const string DeleteCategoryButtonTranslationKey = "enhancedhandbook:button-delete-category";
        private const double CreateButtonMinimumWidth = 60.0;
        private const double CreateButtonCloseSpacing = 10.0;

        private static readonly Dictionary<string, List<GuiHandbookPage>> pagesByCategory = new();
        private static readonly Dictionary<string, string> displayNameByCategory = new();
        private static readonly Dictionary<string, string> translationKeyByCategory = new();
        private static readonly List<string> orderedCategories = new();
        private static readonly Dictionary<string, double[]> tabBackgroundByCategory = new();

        private const string EnglishLocaleCode = "en";
        private static bool usingDefaultEnglishWordCategories;

        internal const string CreateCategoryButtonKey = "handbookcategories-create-button";

        internal const string RecipesOnlyToggleKey = "handbookcategories-recipes-toggle";
        internal const string OriginalSearchToggleKey = "handbookcategories-original-search-toggle";
        private static bool onlyGridPages = false;
        private static bool useOriginalSearch = false;
        private static bool showOriginalSearchToggle = true;
        private static bool showTutorialTab = true;
        private static bool showBlocksAndItemsTab = true;
        private static bool showGuidesTab = true;

        private static readonly FieldInfo composerInteractiveElementsField = typeof(GuiComposer).GetField("interactiveElements", BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool categoriesInitialized;
        private static bool categoriesDirty = true;

        private static readonly HashSet<string> gridRecipePageCodes = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> recipesOnlyExemptCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            "tutorial",
            "blocksitems",
            "stack",
            "guide",
            "guides"
        };

        internal static bool RecipesOnlyEnabled => onlyGridPages;

        internal static bool OriginalSearchEnabled => showOriginalSearchToggle && useOriginalSearch;

        internal static bool ShouldShowOriginalSearchToggle => showOriginalSearchToggle;

        internal static string GetCreateCategoryButtonText()
        {
            return Lang.Get(CreateCategoryButtonTranslationKey);
        }

        internal static string GetDeleteCategoryButtonText()
        {
            return Lang.Get(DeleteCategoryButtonTranslationKey);
        }

        internal static string GetRecipesOnlyToggleText()
        {
            return Lang.Get(RecipesOnlyToggleTranslationKey);
        }

        internal static string GetOriginalSearchToggleText()
        {
            return Lang.Get(OriginalSearchToggleTranslationKey);
        }

        internal static bool TrySetRecipesOnly(bool enabled)
        {
            if (onlyGridPages == enabled)
            {
                return false;
            }

            onlyGridPages = enabled;
            StoreRecipesOnlySetting();
            MarkCategoriesDirty();
            return true;
        }

        internal static bool TrySetOriginalSearch(bool enabled)
        {
            if (!showOriginalSearchToggle)
            {
                return false;
            }

            if (useOriginalSearch == enabled)
            {
                return false;
            }

            useOriginalSearch = enabled;
            return true;
        }

        internal static void MarkCategoriesDirty()
        {
            categoriesDirty = true;
        }

        private static void StoreRecipesOnlySetting()
        {
            if (capi == null)
            {
                return;
            }

            HandbookCategoriesConfig config = capi.LoadModConfig<HandbookCategoriesConfig>(HandbookCategoriesConfig.ConfigFileName);

            if (config == null)
            {
                config = LoadDefaultConfiguration() ?? HandbookCategoriesConfig.CreateDefault();
            }

            if (config == null || config.OnlyGridPages == onlyGridPages)
            {
                return;
            }

            config.OnlyGridPages = onlyGridPages;
            capi.StoreModConfig(config, HandbookCategoriesConfig.ConfigFileName);
        }

        internal static void RequestTabsRebuild()
        {
            if (capi?.Gui == null)
            {
                return;
            }

            capi.Event.EnqueueMainThreadTask(() =>
            {
                if (capi?.Gui?.OpenedGuis == null)
                {
                    return;
                }

                foreach (GuiDialogSurvivalHandbook dialog in capi.Gui.OpenedGuis.OfType<GuiDialogSurvivalHandbook>())
                {
                    HandbookCategoryPatches.RebuildTabs(dialog);
                }
            }, "handbookcategories-rebuildtabs-toggle");
        }

        private sealed class WordCategoryDefinition
        {
            private readonly double[] tabBackgroundColor;
            private readonly SearchTerm[] includeTerms;
            private readonly SearchTerm[] excludeTerms;

            internal WordCategoryDefinition(
                string categoryName,
                string sanitizedName,
                string[] matchWords,
                string[] matchPhrases,
                string[] matchTitleWords,
                string[] forbiddenWords,
                string[] forbiddenPhrases,
                string[] forbiddenTitleWords,
                double[] backgroundColor)
            {
                CategoryName = categoryName ?? string.Empty;
                SanitizedName = sanitizedName ?? string.Empty;
                CategoryCode = $"{CategoryCodePrefix}{SanitizedName}";
                TranslationKey = $"{TranslationPrefix}{SanitizedName}";
                MatchWords = matchWords ?? Array.Empty<string>();
                MatchPhrases = matchPhrases ?? Array.Empty<string>();
                MatchTitleWords = matchTitleWords ?? Array.Empty<string>();
                ForbiddenWords = forbiddenWords ?? Array.Empty<string>();
                ForbiddenPhrases = forbiddenPhrases ?? Array.Empty<string>();
                ForbiddenTitleWords = forbiddenTitleWords ?? Array.Empty<string>();
                tabBackgroundColor = NormalizeColor(backgroundColor);

                includeTerms = BuildSearchTerms(MatchWords, MatchPhrases, MatchTitleWords);
                excludeTerms = BuildSearchTerms(ForbiddenWords, ForbiddenPhrases, ForbiddenTitleWords);
            }

            internal string CategoryName { get; }

            internal string SanitizedName { get; }

            internal string CategoryCode { get; }

            internal string TranslationKey { get; }

            internal string[] MatchWords { get; }

            internal string[] MatchPhrases { get; }

            internal string[] MatchTitleWords { get; }

            internal string[] ForbiddenWords { get; }

            internal string[] ForbiddenPhrases { get; }

            internal string[] ForbiddenTitleWords { get; }

            internal double[] BackgroundColor => (double[])tabBackgroundColor.Clone();

            internal bool HasSearchTerms
            {
                get
                {
                    if (includeTerms == null || includeTerms.Length == 0)
                    {
                        return false;
                    }

                    for (int i = 0; i < includeTerms.Length; i++)
                    {
                        if (!includeTerms[i].IsRequired)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }

            internal bool MatchesPage(GuiHandbookPage page, string normalizedTitle, string searchableContent, HashSet<string> searchableWords)
            {
                if (page == null || includeTerms.Length == 0)
                {
                    return false;
                }

                for (int i = 0; i < excludeTerms.Length; i++)
                {
                    if (MatchesTerm(page, normalizedTitle, excludeTerms[i], searchableContent, searchableWords, out _))
                    {
                        return false;
                    }
                }

                bool hasOptionalTerms = false;
                bool optionalMatchFound = false;

                for (int i = 0; i < includeTerms.Length; i++)
                {
                    SearchTerm term = includeTerms[i];
                    if (term.IsRequired)
                    {
                        if (!MatchesTerm(page, normalizedTitle, term, searchableContent, searchableWords, out _))
                        {
                            return false;
                        }

                        continue;
                    }

                    hasOptionalTerms = true;
                    if (MatchesTerm(page, normalizedTitle, term, searchableContent, searchableWords, out _))
                    {
                        optionalMatchFound = true;
                    }
                }

                if (!hasOptionalTerms)
                {
                    return false;
                }

                return optionalMatchFound;
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

            private static SearchTerm[] BuildSearchTerms(string[] words, string[] phrases, string[] titleMatchWords)
            {
                if ((words == null || words.Length == 0)
                    && (phrases == null || phrases.Length == 0)
                    && (titleMatchWords == null || titleMatchWords.Length == 0))
                {
                    return Array.Empty<SearchTerm>();
                }

                List<SearchTerm> terms = new();
                HashSet<string> seen = new(StringComparer.Ordinal);

                static void AddTerms(IEnumerable<string> source, List<SearchTerm> target, HashSet<string> seenCache, bool requiresTitleMatch)
                {
                    if (source == null)
                    {
                        return;
                    }

                    foreach (string raw in source)
                    {
                        if (string.IsNullOrWhiteSpace(raw))
                        {
                            continue;
                        }

                        string trimmed = raw.Trim();
                        bool isRequired = false;
                        if (trimmed.Length > 0 && trimmed[0] == '+')
                        {
                            isRequired = true;
                            trimmed = trimmed.Substring(1);
                        }

                        string normalized = NormalizeSearchTerm(trimmed);
                        bool requiresCodeMatch = false;

                        if (normalized.Length > 0 && normalized[0] == '%')
                        {
                            if (normalized.Length == 1)
                            {
                                continue;
                            }

                            normalized = normalized.Substring(1);
                            requiresCodeMatch = true;
                        }

                        if (normalized.Length == 0)
                        {
                            continue;
                        }

                        string cacheKey = requiresCodeMatch
                            ? $"code:{normalized}"
                            : requiresTitleMatch ? $"title:{normalized}" : $"term:{normalized}";
                        if (isRequired)
                        {
                            cacheKey = $"required:{cacheKey}";
                        }
                        if (!seenCache.Add(cacheKey))
                        {
                            continue;
                        }

                        bool isExactMatch = !requiresCodeMatch;
                        target.Add(new SearchTerm(normalized, isExactMatch, requiresTitleMatch, requiresCodeMatch, isRequired));
                    }
                }

                AddTerms(words, terms, seen, requiresTitleMatch: false);
                AddTerms(phrases, terms, seen, requiresTitleMatch: false);
                AddTerms(titleMatchWords, terms, seen, requiresTitleMatch: true);

                return terms.ToArray();
            }

            private static string NormalizeSearchTerm(string term)
            {
                if (string.IsNullOrWhiteSpace(term))
                {
                    return string.Empty;
                }

                return term.ToSearchFriendly().ToLowerInvariant().Trim();
            }
        }

        private readonly struct SearchQuery
        {
            internal SearchQuery(SearchTerm[] includeTerms, SearchTerm[] excludeTerms, bool requiresAllMatches, string categoryName)
            {
                IncludeTerms = includeTerms ?? Array.Empty<SearchTerm>();
                ExcludeTerms = excludeTerms ?? Array.Empty<SearchTerm>();

                int optionalCount = 0;
                if (IncludeTerms.Length > 0)
                {
                    for (int i = 0; i < IncludeTerms.Length; i++)
                    {
                        if (!IncludeTerms[i].IsRequired)
                        {
                            optionalCount++;
                        }
                    }
                }

                OptionalTermCount = optionalCount;
                RequiresAllMatches = requiresAllMatches && OptionalTermCount > 0;
                CategoryName = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName;
            }

            internal SearchTerm[] IncludeTerms { get; }

            internal SearchTerm[] ExcludeTerms { get; }

            internal bool RequiresAllMatches { get; }

            internal int OptionalTermCount { get; }

            internal string CategoryName { get; }

            internal bool HasCategoryName => !string.IsNullOrWhiteSpace(CategoryName);

            internal bool HasFilters => IncludeTerms.Length > 0 || ExcludeTerms.Length > 0;

            internal bool HasOptionalTerms => OptionalTermCount > 0;
        }

        private readonly struct SearchTerm
        {
            internal SearchTerm(string term, bool isExactMatch, bool requiresTitleMatch, bool requiresPageCodeMatch, bool isRequired = false)
            {
                Term = term;
                IsExactMatch = isExactMatch;
                RequiresTitleMatch = requiresTitleMatch;
                RequiresPageCodeMatch = requiresPageCodeMatch;
                IsRequired = isRequired;
            }

            internal string Term { get; }

            internal bool IsExactMatch { get; }

            internal bool RequiresTitleMatch { get; }

            internal bool RequiresPageCodeMatch { get; }

            internal bool IsRequired { get; }
        }

        private static WordCategoryDefinition[] wordCategories = Array.Empty<WordCategoryDefinition>();

        private static ICoreClientAPI capi;
        private static GuiComposer trackedCreateButtonComposer;
        private static GuiElementTextButton trackedCreateButton;
        private static GuiElementTextButton trackedCloseButton;
        private static long createButtonListenerId;

        internal static ICoreClientAPI ClientApi => capi;

        internal static bool IsReady => capi?.World != null && (capi.World.GridRecipes != null || !onlyGridPages);

        internal static void Initialize(ICoreClientAPI api)
        {
            capi = api;
            categoriesInitialized = false;
            categoriesDirty = true;
            ReloadConfiguration();

            if (capi?.Event != null)
            {
                if (createButtonListenerId != 0)
                {
                    capi.Event.UnregisterGameTickListener(createButtonListenerId);
                    createButtonListenerId = 0;
                }

                createButtonListenerId = capi.Event.RegisterGameTickListener(MonitorCreateButtonState, 50);
            }
        }

        internal static void ReloadConfiguration()
        {
            categoriesDirty = true;
            categoriesInitialized = false;

            if (capi == null)
            {
                wordCategories = Array.Empty<WordCategoryDefinition>();
                onlyGridPages = true;
                showOriginalSearchToggle = true;
                useOriginalSearch = false;
                showTutorialTab = true;
                showBlocksAndItemsTab = true;
                showGuidesTab = true;
                usingDefaultEnglishWordCategories = false;
                return;
            }

            bool shouldStoreConfig = false;
            HandbookCategoriesConfig config = capi.LoadModConfig<HandbookCategoriesConfig>(HandbookCategoriesConfig.ConfigFileName);

            if (config == null)
            {
                config = LoadDefaultConfiguration();
                shouldStoreConfig = true;
            }

            if (config == null)
            {
                config = HandbookCategoriesConfig.CreateDefault();
                shouldStoreConfig = true;
            }

            bool usingDefaultCategories = DetermineIfEnglishDefault(config, ref shouldStoreConfig);

            wordCategories = BuildWordCategories(config);

            if (wordCategories.Length == 0)
            {
                config = HandbookCategoriesConfig.CreateDefault();
                wordCategories = BuildWordCategories(config);
                shouldStoreConfig = true;
                usingDefaultCategories = true;
            }

            onlyGridPages = config?.OnlyGridPages ?? false;
            showTutorialTab = !(config?.DisableTutorialTab ?? false);
            showBlocksAndItemsTab = !(config?.DisableBlocksAndItemsTab ?? false);
            showGuidesTab = !(config?.DisableGuidesTab ?? false);
            showOriginalSearchToggle = !(config?.DisableOriginalSearchButton ?? false);

            if (!showOriginalSearchToggle)
            {
                useOriginalSearch = false;
            }

            usingDefaultEnglishWordCategories = usingDefaultCategories;

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

            gridRecipePageCodes.Clear();


            if (createButtonListenerId != 0)
            {
                capi?.Event?.UnregisterGameTickListener(createButtonListenerId);
                createButtonListenerId = 0;
            }

            trackedCreateButtonComposer = null;
            categoriesInitialized = false;
            categoriesDirty = true;

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
            if (!categoriesDirty && categoriesInitialized)
            {
                return;
            }

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

            gridRecipePageCodes.Clear();

            if (capi.World.GridRecipes != null)
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
                        gridRecipePageCodes.Add(page.PageCode);
                    }
                }
            }

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

            ApplyWordBasedCategories(itemPagesByCode.Values, onlyGridPages ? gridRecipePageCodes : null, AddPageToCategory);


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

            categoriesInitialized = true;
            categoriesDirty = false;
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

        private static void ApplyWordBasedCategories(IEnumerable<GuiHandbookItemStackPage> pages, ISet<string> gridRecipeCodes, Action<WordCategoryDefinition, GuiHandbookPage> addPageAction)
        {
            if (pages == null || addPageAction == null)
            {
                return;
            }

            if (wordCategories == null || wordCategories.Length == 0)
            {
                return;
            }

            bool requireGridPages = gridRecipeCodes != null;

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
                    if (gridRecipeCodes == null || !gridRecipeCodes.Contains(pageCode))
                    {
                        continue;
                    }
                }

                PageTitleData titleData = GetPageTitleData(page);
                string normalizedTitle = titleData.NormalizedPrimaryTitle;
                string searchableContent = GetSearchableContent(titleData);
                HashSet<string> searchableWords = ExtractWords(searchableContent);

                for (int i = 0; i < wordCategories.Length; i++)
                {
                    WordCategoryDefinition definition = wordCategories[i];
                    if (definition == null || !definition.HasSearchTerms)
                    {
                        continue;
                    }

                    if (definition.MatchesPage(page, normalizedTitle, searchableContent, searchableWords))
                    {
                        addPageAction(definition, page);
                    }
                }
            }
        }

        private static bool ShouldRestrictToGridRecipes(string categoryCode)
        {
            if (!onlyGridPages)
            {
                return false;
            }

            if (string.IsNullOrEmpty(categoryCode))
            {
                return true;
            }

            return !recipesOnlyExemptCategories.Contains(categoryCode);
        }

        private static bool IsGridRecipePage(GuiHandbookPage page)
        {
            if (page is GuiHandbookItemStackPage itemPage)
            {
                string pageCode = itemPage.PageCode;
                if (!string.IsNullOrEmpty(pageCode))
                {
                    return gridRecipePageCodes.Contains(pageCode);
                }
            }

            return false;
        }

        internal static void UpdateSearchUi(GuiComposer overviewGui, string currentSearchText)
        {
            if (overviewGui == null)
            {
                return;
            }

            SearchQuery searchQuery = PrepareSearchTerms(currentSearchText);
            UpdateCreateButton(overviewGui, searchQuery);
        }

        internal static void ApplyCategoryFilter(string categoryCode, IEnumerable<GuiHandbookPage> candidatePages, List<IFlatListItem> shownPages, GuiComposer overviewGui, string currentSearchText, bool loadingPages, double listHeight)
        {
            SearchQuery searchQuery = PrepareSearchTerms(currentSearchText);

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

            bool restrictToRecipes = ShouldRestrictToGridRecipes(categoryCode);

            List<WeightedHandbookPage> weightedPages = new();
            foreach (GuiHandbookPage page in pagesToFilter)
            {
                if (page == null || page.IsDuplicate)
                {
                    continue;
                }

                if (restrictToRecipes && !IsGridRecipePage(page))
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

            PageTitleData titleData = GetPageTitleData(page);
            string normalizedTitle = titleData.NormalizedPrimaryTitle;
            string searchableContent = GetSearchableContent(titleData);
            HashSet<string> searchableWords = ExtractWords(searchableContent);

            float bestWeight = 0f;

            if (searchQuery.IncludeTerms.Length > 0)
            {
                bool requiresAll = searchQuery.RequiresAllMatches;
                bool hasOptionalMatch = false;
                bool hasOptionalTerms = searchQuery.HasOptionalTerms;

                for (int i = 0; i < searchQuery.IncludeTerms.Length; i++)
                {
                    SearchTerm term = searchQuery.IncludeTerms[i];
                    bool matches = MatchesTerm(page, normalizedTitle, term, searchableContent, searchableWords, out float termWeight);

                    if (term.IsRequired)
                    {
                        if (!matches)
                        {
                            return false;
                        }

                        if (termWeight > bestWeight)
                        {
                            bestWeight = termWeight;
                        }

                        continue;
                    }

                    if (matches)
                    {
                        hasOptionalMatch = true;
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

                if (!hasOptionalTerms || !hasOptionalMatch)
                {
                    return false;
                }
            }

            for (int i = 0; i < searchQuery.ExcludeTerms.Length; i++)
            {
                if (MatchesTerm(page, normalizedTitle, searchQuery.ExcludeTerms[i], searchableContent, searchableWords, out _))
                {
                    return false;
                }
            }

            weight = bestWeight > 0f ? bestWeight : 1f;
            return true;
        }

        private static bool MatchesTerm(GuiHandbookPage page, string normalizedTitle, SearchTerm term, string searchableContent, HashSet<string> searchableWords, out float weight)
        {
            weight = 0f;

            if (page == null || string.IsNullOrEmpty(term.Term))
            {
                return false;
            }

            if (term.RequiresTitleMatch)
            {
                if (!string.Equals(normalizedTitle, term.Term, StringComparison.Ordinal))
                {
                    return false;
                }

                weight = float.MaxValue;
                return true;
            }

            if (term.RequiresPageCodeMatch)
            {
                string normalizedPageCode = GetNormalizedPageCode(page);
                if (string.IsNullOrEmpty(normalizedPageCode))
                {
                    return false;
                }

                if (term.IsExactMatch)
                {
                    if (!string.Equals(normalizedPageCode, term.Term, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    weight = float.MaxValue;
                    return true;
                }

                float codeWeight = GetPageCodeMatchWeight(normalizedPageCode, term.Term);
                if (codeWeight <= 0f)
                {
                    return false;
                }

                weight = codeWeight;
                return true;
            }

            float matchWeight = GetTitleMatchWeight(normalizedTitle, term.Term);
            if (matchWeight <= 0f)
            {
                return false;
            }

            if (term.IsExactMatch && !MatchesExactTerm(searchableContent, searchableWords, term.Term))
            {
                return false;
            }

            weight = matchWeight;
            return true;
        }

        private static bool MatchesExactTerm(string searchableContent, HashSet<string> searchableWords, string term)
        {
            if (string.IsNullOrEmpty(searchableContent) || string.IsNullOrEmpty(term))
            {
                return false;
            }

            if (term.IndexOf(' ', StringComparison.Ordinal) >= 0)
            {
                return searchableContent.IndexOf(term, StringComparison.Ordinal) >= 0;
            }

            return searchableWords != null && searchableWords.Contains(term);
        }

        private static float GetTitleMatchWeight(string normalizedTitle, string term)
        {
            if (string.IsNullOrWhiteSpace(normalizedTitle) || string.IsNullOrWhiteSpace(term))
            {
                return 0f;
            }

            if (string.Equals(normalizedTitle, term, StringComparison.Ordinal))
            {
                return 4f;
            }

            if (normalizedTitle.StartsWith(term + " ", StringComparison.Ordinal))
            {
                return 3.75f;
            }

            if (normalizedTitle.StartsWith(term, StringComparison.Ordinal))
            {
                return 3.5f;
            }

            if (normalizedTitle.IndexOf(term, StringComparison.Ordinal) >= 0)
            {
                return 3f;
            }

            return 0f;
        }

        private static float GetPageCodeMatchWeight(string normalizedPageCode, string term)
        {
            if (string.IsNullOrWhiteSpace(normalizedPageCode) || string.IsNullOrWhiteSpace(term))
            {
                return 0f;
            }

            if (string.Equals(normalizedPageCode, term, StringComparison.Ordinal))
            {
                return 4f;
            }

            if (normalizedPageCode.StartsWith(term + "-", StringComparison.Ordinal))
            {
                return 3.75f;
            }

            if (normalizedPageCode.StartsWith(term, StringComparison.Ordinal))
            {
                return 3.5f;
            }

            if (normalizedPageCode.IndexOf(term, StringComparison.Ordinal) >= 0)
            {
                return 3f;
            }

            return 0f;
        }

        private static string GetSearchableContent(PageTitleData titleData)
        {
            return titleData.SearchableContent;
        }

        private static PageTitleData GetPageTitleData(GuiHandbookPage page)
        {
            string localizedTitle = GetNormalizedTitle(page);

            if (!ShouldUseEnglishFallbackForDefaultCategories())
            {
                return new PageTitleData(localizedTitle, localizedTitle);
            }

            string englishTitle = GetNormalizedTitle(page, EnglishLocaleCode);

            if (string.IsNullOrEmpty(englishTitle))
            {
                englishTitle = localizedTitle;
            }

            return new PageTitleData(englishTitle, localizedTitle);
        }

        private static string GetNormalizedTitle(GuiHandbookPage page)
        {
            return GetNormalizedTitle(page, null);
        }

        private static string GetNormalizedTitle(GuiHandbookPage page, string localeOverride)
        {
            if (page == null)
            {
                return string.Empty;
            }

            bool allowCachedItemStackTitle = string.IsNullOrEmpty(localeOverride);
            string rawTitle = allowCachedItemStackTitle
                ? GetRawTitle(page, allowCachedItemStackTitle)
                : RunWithLocale(localeOverride, () => GetRawTitle(page, allowCachedItemStackTitle: false)) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(rawTitle))
            {
                return string.Empty;
            }

            return rawTitle.ToSearchFriendly().ToLowerInvariant().Trim();
        }

        private static string GetRawTitle(GuiHandbookPage page, bool allowCachedItemStackTitle)
        {
            return page switch
            {
                GuiHandbookGroupedItemstackPage groupedPage when !string.IsNullOrWhiteSpace(groupedPage.Name) => groupedPage.Name,
                GuiHandbookItemStackPage itemPage when allowCachedItemStackTitle && !string.IsNullOrWhiteSpace(itemPage.TextCacheTitle) => itemPage.TextCacheTitle,
                GuiHandbookItemStackPage itemPage when itemPage.Stack != null => itemPage.Stack.GetName(),
                GuiHandbookCommandPage commandPage => commandPage.TextCacheTitle,
                GuiHandbookMealRecipePage mealPage when !string.IsNullOrWhiteSpace(mealPage.Title) => Lang.Get(mealPage.Title),
                GuiHandbookTextPage textPage when !string.IsNullOrWhiteSpace(textPage.Title) => Lang.Get(textPage.Title),
                _ => page.PageCode
            } ?? string.Empty;
        }

        private static T RunWithLocale<T>(string localeCode, Func<T> action)
        {
            if (string.IsNullOrEmpty(localeCode) || action == null)
            {
                return action != null ? action() : default;
            }

            string originalLocale = Lang.CurrentLocale;
            if (string.Equals(originalLocale, localeCode, StringComparison.OrdinalIgnoreCase))
            {
                return action();
            }

            Lang.ChangeLanguage(localeCode);

            try
            {
                return action();
            }
            finally
            {
                if (string.IsNullOrEmpty(originalLocale))
                {
                    Lang.ChangeLanguage(EnglishLocaleCode);
                }
                else
                {
                    Lang.ChangeLanguage(originalLocale);
                }
            }
        }

        private static bool ShouldUseEnglishFallbackForDefaultCategories()
        {
            return usingDefaultEnglishWordCategories && !IsEnglishLocale(Lang.CurrentLocale);
        }

        private static bool IsEnglishLocale(string locale)
        {
            if (string.IsNullOrEmpty(locale))
            {
                return true;
            }

            return string.Equals(locale, EnglishLocaleCode, StringComparison.OrdinalIgnoreCase);
        }

        private readonly struct PageTitleData
        {
            internal PageTitleData(string normalizedPrimaryTitle, string localizedTitle)
            {
                NormalizedPrimaryTitle = normalizedPrimaryTitle ?? string.Empty;
                LocalizedTitle = localizedTitle ?? string.Empty;
            }

            internal string NormalizedPrimaryTitle { get; }

            internal string LocalizedTitle { get; }

            internal string SearchableContent
            {
                get
                {
                    if (string.IsNullOrEmpty(LocalizedTitle) || string.Equals(LocalizedTitle, NormalizedPrimaryTitle, StringComparison.Ordinal))
                    {
                        return NormalizedPrimaryTitle;
                    }

                    if (string.IsNullOrEmpty(NormalizedPrimaryTitle))
                    {
                        return LocalizedTitle;
                    }

                    return string.Concat(NormalizedPrimaryTitle, " ", LocalizedTitle);
                }
            }
        }

        private static bool DetermineIfEnglishDefault(HandbookCategoriesConfig config, ref bool shouldStoreConfig)
        {
            if (config == null)
            {
                return false;
            }

            if (config.UsesEnglishDefaults)
            {
                return true;
            }

            if (!LooksLikeDefaultEnglishConfig(config))
            {
                return false;
            }

            config.UsesEnglishDefaults = true;
            shouldStoreConfig = true;
            return true;
        }

        private static bool LooksLikeDefaultEnglishConfig(HandbookCategoriesConfig config)
        {
            if (config == null)
            {
                return false;
            }

            HandbookCategoriesConfig defaultConfig = HandbookCategoriesConfig.CreateDefault();

            if (config.OnlyGridPages != defaultConfig.OnlyGridPages
                || config.DisableTutorialTab != defaultConfig.DisableTutorialTab
                || config.DisableBlocksAndItemsTab != defaultConfig.DisableBlocksAndItemsTab
                || config.DisableGuidesTab != defaultConfig.DisableGuidesTab
                || config.DisableOriginalSearchButton != defaultConfig.DisableOriginalSearchButton)
            {
                return false;
            }

            List<HandbookCategoryConfigEntry> categories = config.Categories ?? new List<HandbookCategoryConfigEntry>();
            List<HandbookCategoryConfigEntry> defaultCategories = defaultConfig.Categories ?? new List<HandbookCategoryConfigEntry>();

            if (categories.Count != defaultCategories.Count)
            {
                return false;
            }

            for (int i = 0; i < categories.Count; i++)
            {
                if (!CategoryEntryEquals(categories[i], defaultCategories[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool CategoryEntryEquals(HandbookCategoryConfigEntry left, HandbookCategoryConfigEntry right)
        {
            if (left == null || right == null)
            {
                return left == null && right == null;
            }

            if (!string.Equals(left.Name, right.Name, StringComparison.Ordinal))
            {
                return false;
            }

            if (!ListsEquivalent(left.MatchWords, right.MatchWords)
                || !ListsEquivalent(left.MatchTitleWords, right.MatchTitleWords)
                || !ListsEquivalent(left.ForbiddenWords, right.ForbiddenWords)
                || !ListsEquivalent(left.ForbiddenTitleWords, right.ForbiddenTitleWords))
            {
                return false;
            }

            string leftColor = left.TabBackgroundColor ?? string.Empty;
            string rightColor = right.TabBackgroundColor ?? string.Empty;
            return string.Equals(leftColor, rightColor, StringComparison.Ordinal);
        }

        private static bool ListsEquivalent(IList<string> left, IList<string> right)
        {
            int leftCount = left?.Count ?? 0;
            int rightCount = right?.Count ?? 0;

            if (leftCount != rightCount)
            {
                return false;
            }

            for (int i = 0; i < leftCount; i++)
            {
                string leftValue = left[i] ?? string.Empty;
                string rightValue = right[i] ?? string.Empty;
                if (!string.Equals(leftValue, rightValue, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static string GetNormalizedPageCode(GuiHandbookPage page)
        {
            string pageCode = page?.PageCode;
            if (string.IsNullOrWhiteSpace(pageCode))
            {
                return string.Empty;
            }

            return pageCode.ToLowerInvariant();
        }

        private static SearchQuery PrepareSearchTerms(string currentSearchText)
        {
            if (string.IsNullOrWhiteSpace(currentSearchText))
            {
                return new SearchQuery(Array.Empty<SearchTerm>(), Array.Empty<SearchTerm>(), false, null);
            }

            bool hashFound = TryExtractCategorySegments(currentSearchText, out string rawCategoryName, out string beforeHash, out string afterCategory);
            string categoryName = NormalizeCategoryName(rawCategoryName);
            string searchPortion = CombineCategorySegments(beforeHash, afterCategory);

            if (string.IsNullOrWhiteSpace(searchPortion))
            {
                return new SearchQuery(Array.Empty<SearchTerm>(), Array.Empty<SearchTerm>(), false, categoryName);
            }

            string text = searchPortion.ToLowerInvariant();

            int startIndex = 0;
            while (startIndex < text.Length && char.IsWhiteSpace(text[startIndex]))
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
            bool currentTokenRequiresTitleMatch = false;

            void CommitCurrentToken(bool exactMatch, bool requiresTitleMatch)
            {
                if (builder.Length == 0)
                {
                    excludeNext = false;
                    buildingExactMatch = false;
                    currentTokenRequiresTitleMatch = false;
                    return;
                }

                string raw = builder.ToString();
                builder.Clear();

                bool requiresCodeMatch = false;
                bool isRequired = false;

                if (!string.IsNullOrEmpty(raw) && raw[0] == '+')
                {
                    if (raw.Length == 1)
                    {
                        excludeNext = false;
                        buildingExactMatch = false;
                        currentTokenRequiresTitleMatch = false;
                        return;
                    }

                    raw = raw.Substring(1);
                    isRequired = true;
                }

                if (!string.IsNullOrEmpty(raw) && raw[0] == '%')
                {
                    if (raw.Length == 1)
                    {
                        excludeNext = false;
                        buildingExactMatch = false;
                        currentTokenRequiresTitleMatch = false;
                        return;
                    }

                    raw = raw.Substring(1);
                    requiresCodeMatch = true;
                    requiresTitleMatch = false;
                }

                string term = raw.ToSearchFriendly().Trim();
                if (term.Length == 0)
                {
                    excludeNext = false;
                    buildingExactMatch = false;
                    currentTokenRequiresTitleMatch = false;
                    return;
                }

                if (excludeNext)
                {
                    excludeTerms.Add(new SearchTerm(term, exactMatch, requiresTitleMatch, requiresCodeMatch));
                }
                else
                {
                    includeTerms.Add(new SearchTerm(term, exactMatch, requiresTitleMatch, requiresCodeMatch, isRequired));
                }

                excludeNext = false;
                buildingExactMatch = false;
                currentTokenRequiresTitleMatch = false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];

                if (ch == '"')
                {
                    if (inQuotes)
                    {
                        if (currentTokenRequiresTitleMatch && i + 1 < text.Length && text[i + 1] == '"')
                        {
                            CommitCurrentToken(true, true);
                            inQuotes = false;
                            i++;
                        }
                        else
                        {
                            CommitCurrentToken(true, currentTokenRequiresTitleMatch);
                            inQuotes = false;
                        }
                        currentTokenRequiresTitleMatch = false;
                    }
                    else
                    {
                        if (builder.Length > 0)
                        {
                            CommitCurrentToken(false, currentTokenRequiresTitleMatch);
                        }

                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            inQuotes = true;
                            buildingExactMatch = true;
                            currentTokenRequiresTitleMatch = true;
                            i++;
                        }
                        else
                        {
                            inQuotes = true;
                            buildingExactMatch = true;
                            currentTokenRequiresTitleMatch = false;
                        }
                    }
                }
                else if (!inQuotes && char.IsWhiteSpace(ch))
                {
                    if (builder.Length > 0)
                    {
                        CommitCurrentToken(false, currentTokenRequiresTitleMatch);
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
                CommitCurrentToken(buildingExactMatch, currentTokenRequiresTitleMatch);
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

            int optionalCount = 0;
            for (int i = 0; i < includes.Length; i++)
            {
                if (!includes[i].IsRequired)
                {
                    optionalCount++;
                }
            }

            bool categoryBuilderMode = hashFound && string.IsNullOrWhiteSpace(beforeHash);
            if (categoryBuilderMode)
            {
                includes = ForceExactTerms(includes);
                excludes = ForceExactTerms(excludes);
            }

            bool requireAllMatches = false;
            if (!hashFound && !containsOr)
            {
                if (optionalCount > 1 || (containsAnd && optionalCount > 0))
                {
                    requireAllMatches = true;
                }
            }

            return new SearchQuery(includes, excludes, requireAllMatches, categoryName);

            static SearchTerm[] ForceExactTerms(SearchTerm[] terms)
            {
                if (terms == null || terms.Length == 0)
                {
                    return terms ?? Array.Empty<SearchTerm>();
                }

                SearchTerm[] copy = new SearchTerm[terms.Length];
                for (int i = 0; i < terms.Length; i++)
                {
                    SearchTerm term = terms[i];
                    if (term.IsExactMatch || term.RequiresPageCodeMatch)
                    {
                        copy[i] = term;
                        continue;
                    }

                    copy[i] = new SearchTerm(term.Term, true, term.RequiresTitleMatch, term.RequiresPageCodeMatch, term.IsRequired);
                }

                return copy;
            }
        }

        internal static bool TryExecuteCategoryCreateCommand(string searchText)
        {
            if (capi == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(searchText))
            {
                capi.ShowChatMessage("[Handbook Categories] Type some keywords and include #CategoryName before clicking Create Category.");
                return false;
            }

            bool hashFound = TryExtractCategorySegments(searchText, out string rawCategoryName, out string beforeHash, out string afterCategory);
            string categoryName = NormalizeCategoryName(rawCategoryName);
            if (!hashFound)
            {
                capi.ShowChatMessage("[Handbook Categories] Add #CategoryName to your search to choose the category you want to create.");
                return false;
            }

            if (categoryName == null)
            {
                capi.ShowChatMessage("[Handbook Categories] Unable to read the category name after #. Please try again.");
                return false;
            }

            string remainder = CombineCategorySegments(beforeHash, afterCategory);
            string trimmedRemainder = remainder?.Trim();
            if (string.IsNullOrEmpty(trimmedRemainder))
            {
                capi.ShowChatMessage($"[Handbook Categories] Add at least one word before or after #{categoryName} to include in the new category.");
                return false;
            }

            string command = $".categorymod {categoryName} {trimmedRemainder}";
            capi.ShowChatMessage($"[Handbook Categories] Creating category '{categoryName}' with keywords: {trimmedRemainder}.");
            capi.TriggerChatMessage(command);
            return true;
        }

        private static void UpdateCreateButton(GuiComposer overviewGui, SearchQuery searchQuery)
        {
            if (overviewGui == null)
            {
                return;
            }

            GuiElementTextButton createButton = overviewGui.GetButton(CreateCategoryButtonKey);
            if (createButton == null)
            {
                return;
            }

            RegisterCreateButton(overviewGui, createButton);
        }

        internal static void RegisterCreateButton(GuiComposer overviewGui, GuiElementTextButton button)
        {
            if (overviewGui == null || button == null)
            {
                return;
            }

            if (!overviewGui.Composed)
            {
                return;
            }

            trackedCreateButtonComposer = overviewGui;
            trackedCreateButton = button;

            EnsureCreateButtonLayout(overviewGui, button);
            UpdateCreateButtonTextInternal(overviewGui, button);
        }

        internal static bool TryExecuteCategoryDeleteCommand(GuiDialogHandbook dialog)
        {
            if (dialog == null || capi == null)
            {
                return false;
            }

            if (!IsControlKeyHeld())
            {
                return false;
            }

            string categoryCode = dialog.currentCatgoryCode;
            string tabName = GetTabDisplayName(categoryCode)?.Trim();
            if (string.IsNullOrWhiteSpace(tabName))
            {
                tabName = categoryCode?.Trim();
            }

            if (string.IsNullOrWhiteSpace(tabName))
            {
                return false;
            }

            string command = $".categorymoddelete {tabName}";
            capi.TriggerChatMessage(command);
            return true;
        }

        private static void MonitorCreateButtonState(float deltaTime)
        {
            GuiComposer composer = trackedCreateButtonComposer;
            if (composer == null || !composer.Composed)
            {
                trackedCreateButtonComposer = null;
                trackedCreateButton = null;
                trackedCloseButton = null;
                return;
            }

            GuiElementTextButton button = composer.GetButton(CreateCategoryButtonKey);
            if (button == null)
            {
                trackedCreateButtonComposer = null;
                trackedCreateButton = null;
                trackedCloseButton = null;
                return;
            }

            trackedCreateButton = button;


            if (!TryEnsureButtonBounds(composer, button))
            {
                trackedCreateButtonComposer = null;
                trackedCreateButton = null;
                trackedCloseButton = null;

                return;
            }

            UpdateCreateButtonTextInternal(composer, button);
        }

        private static void UpdateCreateButtonTextInternal(GuiComposer composer, GuiElementTextButton button)
        {
            if (composer == null || button == null || !composer.Composed)
            {
                return;
            }

            string desiredText = ShouldShowDeleteText(button) ? GetDeleteCategoryButtonText() : GetCreateCategoryButtonText();
            if (!string.Equals(button.Text, desiredText, StringComparison.Ordinal))
            {
                button.Text = desiredText;
                RecomposeTextButton(button);

                EnsureCreateButtonLayout(composer, button);

            }
        }

        private static void RecomposeTextButton(GuiElementTextButton button)
        {
            if (button == null)
            {
                return;
            }

            using var surface = new ImageSurface(Format.Argb32, 1, 1);
            using var ctx = new Context(surface);

            button.BeforeCalcBounds();
            button.ComposeElements(ctx, surface);

            ElementBounds bounds = button.Bounds;
            if (bounds?.ParentBounds != null)
            {
                bounds.MarkDirtyRecursive();

                if (bounds.RequiresRecalculation)
                {
                    bounds.CalcWorldBounds();
                }
            }
        }

        private static bool ShouldShowDeleteText(GuiElementTextButton button)
        {
            if (button?.Bounds == null || capi?.Input == null)
            {
                return false;
            }

            if (!IsControlKeyHeld())
            {
                return false;
            }

            ElementBounds bounds = button.Bounds;
            if (bounds.ParentBounds == null)
            {
                return false;
            }

            if (bounds.RequiresRecalculation)
            {
                bounds.CalcWorldBounds();
            }

            return bounds.PointInside(capi.Input.MouseX, capi.Input.MouseY);
        }

        private static bool IsControlKeyHeld()
        {
            bool[] keys = capi?.Input?.KeyboardKeyState;
            if (keys == null)
            {
                return false;
            }

            int leftIndex = (int)GlKeys.ControlLeft;
            int rightIndex = (int)GlKeys.ControlRight;

            bool leftDown = leftIndex >= 0 && leftIndex < keys.Length && keys[leftIndex];
            bool rightDown = rightIndex >= 0 && rightIndex < keys.Length && keys[rightIndex];

            return leftDown || rightDown;
        }

        private static bool TryExtractCategorySegments(string searchText, out string categoryName, out string beforeHash, out string afterCategory)
        {
            categoryName = null;
            beforeHash = string.Empty;
            afterCategory = string.Empty;

            if (string.IsNullOrEmpty(searchText))
            {
                return false;
            }

            int hashIndex = searchText.IndexOf('#');
            if (hashIndex < 0)
            {
                beforeHash = searchText;
                return false;
            }

            beforeHash = searchText.Substring(0, hashIndex);

            int categoryStart = hashIndex + 1;
            if (categoryStart >= searchText.Length)
            {
                afterCategory = string.Empty;
                return true;
            }

            int categoryEnd = categoryStart;
            while (categoryEnd < searchText.Length && !char.IsWhiteSpace(searchText[categoryEnd]))
            {
                categoryEnd++;
            }

            categoryName = searchText.Substring(categoryStart, categoryEnd - categoryStart);
            afterCategory = categoryEnd < searchText.Length ? searchText.Substring(categoryEnd) : string.Empty;
            return true;
        }

        private static string CombineCategorySegments(string beforeHash, string afterCategory)
        {
            string first = string.IsNullOrWhiteSpace(beforeHash) ? string.Empty : beforeHash.Trim();
            string second = string.IsNullOrWhiteSpace(afterCategory) ? string.Empty : afterCategory.Trim();

            if (first.Length == 0)
            {
                return second;
            }

            if (second.Length == 0)
            {
                return first;
            }

            return string.Concat(first, " ", second);
        }

        private static string NormalizeCategoryName(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return null;
            }

            string trimmed = categoryName.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        private static bool EnsureCreateButtonLayout(GuiComposer composer, GuiElementTextButton button)
        {
            if (composer == null || button == null)
            {
                return false;
            }

            ElementBounds bounds = button.Bounds;
            if (bounds == null)
            {
                return false;
            }

            GuiElementTextButton closeButton = GetCloseButton(composer);
            ElementBounds closeBounds = closeButton?.Bounds;
            if (closeBounds == null)
            {
                return false;
            }

            if (closeBounds.RequiresRecalculation)
            {
                closeBounds.CalcWorldBounds();
            }


            bool changed = false;

            if (!ReferenceEquals(bounds.ParentBounds, closeBounds.ParentBounds) && closeBounds.ParentBounds != null)
            {
                bounds.ParentBounds = closeBounds.ParentBounds;
                changed = true;
            }

            if (bounds.Alignment != closeBounds.Alignment)
            {
                bounds.Alignment = closeBounds.Alignment;
                changed = true;
            }

            if (ValuesDiffer(bounds.fixedOffsetX, closeBounds.fixedOffsetX))
            {
                bounds.fixedOffsetX = closeBounds.fixedOffsetX;
                changed = true;
            }

            if (ValuesDiffer(bounds.fixedOffsetY, closeBounds.fixedOffsetY))
            {
                bounds.fixedOffsetY = closeBounds.fixedOffsetY;
                changed = true;
            }

            if (ValuesDiffer(bounds.fixedY, closeBounds.fixedY))
            {
                bounds.fixedY = closeBounds.fixedY;
                changed = true;
            }

            if (ValuesDiffer(bounds.fixedPaddingX, closeBounds.fixedPaddingX))
            {
                bounds.fixedPaddingX = closeBounds.fixedPaddingX;
                changed = true;
            }

            if (ValuesDiffer(bounds.fixedPaddingY, closeBounds.fixedPaddingY))
            {
                bounds.fixedPaddingY = closeBounds.fixedPaddingY;
                changed = true;
            }

            double targetWidth = bounds.fixedWidth;
            if (closeBounds.fixedWidth > 0.0)
            {
                targetWidth = closeBounds.fixedWidth;
            }

            if (targetWidth < CreateButtonMinimumWidth)
            {
                targetWidth = CreateButtonMinimumWidth;
            }

            if (ValuesDiffer(bounds.fixedWidth, targetWidth))
            {
                bounds.fixedWidth = targetWidth;
                changed = true;
            }

            if (closeBounds.fixedHeight > 0.0 && ValuesDiffer(bounds.fixedHeight, closeBounds.fixedHeight))
            {
                bounds.fixedHeight = closeBounds.fixedHeight;
                changed = true;
            }

            double previousX = bounds.fixedX;
            bounds.FixedLeftOf(closeBounds, CreateButtonCloseSpacing);
            if (ValuesDiffer(previousX, bounds.fixedX))
            {
                changed = true;

            GuiElementTextButton closeButton = GetCloseButton(composer);
            bool changed = false;

            if (closeButton?.Bounds != null)
            {
                ElementBounds closeBounds = closeButton.Bounds;

                if (closeBounds.RequiresRecalculation)
                {
                    closeBounds.CalcWorldBounds();
                }

                double previousWidth = bounds.fixedWidth;
                if (previousWidth < CreateButtonMinimumWidth)
                {
                    bounds.fixedWidth = CreateButtonMinimumWidth;
                }

                double previousHeight = bounds.fixedHeight;
                if (closeBounds.fixedHeight > 0.0)
                {
                    bounds.fixedHeight = closeBounds.fixedHeight;
                }

                double previousX = bounds.fixedX;
                bounds.FixedLeftOf(closeBounds, CreateButtonCloseSpacing);

                changed = ValuesDiffer(previousWidth, bounds.fixedWidth) || ValuesDiffer(previousHeight, bounds.fixedHeight) || ValuesDiffer(previousX, bounds.fixedX);

            }

            if (bounds.RequiresRecalculation || changed)
            {
                bounds.MarkDirtyRecursive();
                bounds.CalcWorldBounds();
            }

            return true;
        }

        private static bool ValuesDiffer(double left, double right)
        {
            return Math.Abs(left - right) > 0.001;
        }

        private static GuiElementTextButton GetCloseButton(GuiComposer composer)
        {
            if (composer == null)
            {
                return null;
            }

            if (trackedCloseButton?.Bounds?.ParentBounds == composer.Bounds)
            {
                return trackedCloseButton;
            }

            GuiElementTextButton closeButton = TryFindCloseButton(composer);
            if (closeButton != null)
            {
                trackedCloseButton = closeButton;
                return closeButton;
            }

            return trackedCloseButton;
        }

        private static GuiElementTextButton TryFindCloseButton(GuiComposer composer)
        {
            if (composer == null)
            {
                return null;
            }

            string closeText = Lang.Get("Close Handbook")?.Trim();
            GuiElementTextButton candidate = composer.LastAddedElement as GuiElementTextButton;
            if (IsCloseButtonCandidate(candidate, closeText))
            {
                return candidate;
            }

            if (composerInteractiveElementsField?.GetValue(composer) is Dictionary<string, GuiElement> interactiveElements)
            {
                GuiElementTextButton fallback = null;
                double fallbackX = double.MinValue;

                foreach (GuiElement element in interactiveElements.Values)
                {
                    if (element is GuiElementTextButton button)
                    {
                        if (button == trackedCreateButton)
                        {
                            continue;
                        }

                        if (IsCloseButtonCandidate(button, closeText))
                        {
                            return button;
                        }

                        ElementBounds elementBounds = button.Bounds;
                        if (elementBounds?.Alignment == EnumDialogArea.RightFixed)
                        {
                            double candidateX = elementBounds.fixedX;
                            if (candidateX > fallbackX)
                            {
                                fallbackX = candidateX;
                                fallback = button;
                            }
                        }
                    }
                }

                if (fallback != null)
                {
                    return fallback;
                }
            }

            return null;
        }

        private static bool IsCloseButtonCandidate(GuiElementTextButton button, string closeText)
        {
            if (button == null || button == trackedCreateButton)
            {
                return false;
            }

            ElementBounds bounds = button.Bounds;
            if (bounds == null || bounds.Alignment != EnumDialogArea.RightFixed)
            {
                return false;
            }

            string text = button.Text?.Trim();
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(closeText))
            {
                return false;
            }

            return string.Equals(text, closeText, StringComparison.Ordinal);
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
                string[] matchTitleWords = NormalizeWords(entry.MatchTitleWords);
                string[] forbiddenWords = NormalizeWords(entry.ForbiddenWords);
                string[] forbiddenTitleWords = NormalizeWords(entry.ForbiddenTitleWords);

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

                WordCategoryDefinition definition = new(
                    name,
                    sanitized,
                    matchSingles,
                    matchPhrases,
                    matchTitleWords,
                    forbiddenSingles,
                    forbiddenPhrases,
                    forbiddenTitleWords,
                    backgroundColor);
                definitions.Add(definition);

            }

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
                .Select(NormalizePhrase)
                .Where(word => !string.IsNullOrEmpty(word))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static string NormalizePhrase(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            StringBuilder builder = new(text.Length);
            bool previousWasWhitespace = false;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];

                if (char.IsWhiteSpace(ch))
                {
                    if (!previousWasWhitespace && builder.Length > 0)
                    {
                        builder.Append(' ');
                        previousWasWhitespace = true;
                    }

                    continue;
                }

                builder.Append(char.ToLowerInvariant(ch));
                previousWasWhitespace = false;
            }

            if (builder.Length > 0 && builder[^1] == ' ')
            {
                builder.Length--;
            }

            return builder.ToString();
        }
    }
}
