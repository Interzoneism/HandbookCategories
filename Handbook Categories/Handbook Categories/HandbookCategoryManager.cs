using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
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
        internal const string HideVariantsToggleTranslationKey = "enhancedhandbook:toggle-hide-types";
        private const string CreateCategoryButtonTranslationKey = "enhancedhandbook:button-create-category";
        private const string DeleteCategoryButtonTranslationKey = "enhancedhandbook:button-delete-category";
        private const string CreateCategoryPromptTitleTranslationKey = "enhancedhandbook:dialog-create-category-title";
        private const string CreateCategoryPromptMessageTranslationKey = "enhancedhandbook:dialog-create-category-message";
        private const string CreateCategoryPromptPlaceholderTranslationKey = "enhancedhandbook:dialog-create-category-placeholder";
        private const string CreateCategoryPromptOkTranslationKey = "enhancedhandbook:dialog-create-category-ok";
        private const string CreateCategoryPromptCancelTranslationKey = "enhancedhandbook:dialog-create-category-cancel";
        private const string RenameCategoryButtonTranslationKey = "enhancedhandbook:button-rename-category";
        private const string RenameCategoryPromptTitleTranslationKey = "enhancedhandbook:dialog-rename-category-title";
        internal const int MaxCategoryNameLength = 20;
        private const double CreateButtonMinimumWidth = 60.0;
        private const double CreateButtonCloseSpacing = 10.0;

        private static readonly Dictionary<string, List<GuiHandbookPage>> pagesByCategory = new();
        private static readonly Dictionary<string, string> displayNameByCategory = new();
        private static readonly Dictionary<string, string> translationKeyByCategory = new();
        private static readonly List<string> orderedCategories = new();
        private static readonly Dictionary<string, double[]> tabBackgroundByCategory = new();
        private static readonly Dictionary<GuiHandbookPage, string> englishNormalizedTitleByPage = new();

        private const string EnglishLocaleCode = "en";
        private static bool usingDefaultEnglishWordCategories;

        internal const string CreateCategoryButtonKey = "handbookcategories-create-button";

        internal const string RecipesOnlyToggleKey = "handbookcategories-recipes-toggle";
        internal const string OriginalSearchToggleKey = "handbookcategories-original-search-toggle";
        internal const string HideVariantsToggleKey = "handbookcategories-hide-variants-toggle";
        private static bool onlyGridPages = false;
        private static bool hideVariantTypes = false;
        private static bool useOriginalSearch = false;
        private static bool showOriginalSearchToggle = true;
        private static bool showTutorialTab = true;
        private static bool showBlocksAndItemsTab = true;
        private static bool showGuidesTab = true;
        private static bool enableDragAndDrop = true;

        private static readonly FieldInfo composerInteractiveElementsField = typeof(GuiComposer).GetField("interactiveElements", BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool categoriesInitialized;
        private static bool categoriesDirty = true;

        private static readonly HashSet<string> gridRecipePageCodes = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, HashSet<string>> vanillaSearchExtrasByPageCode = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> variantGroupDisplayNameByKey = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> recipesOnlyExemptCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            "tutorial",
            "blocksitems",
            "stack",
            "guide",
            "guides"
        };
        private static readonly Regex WordRegex = new(@"[\p{L}\p{M}\p{Nd}]+(?:['\-][\p{L}\p{M}\p{Nd}]+)*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        internal static bool RecipesOnlyEnabled => onlyGridPages;

        internal static bool HideVariantTypesEnabled => hideVariantTypes;

        internal static bool OriginalSearchEnabled => showOriginalSearchToggle && useOriginalSearch;

        internal static bool ShouldShowOriginalSearchToggle => showOriginalSearchToggle;

        internal static bool DragAndDropEnabled => enableDragAndDrop;

        internal static string GetCreateCategoryButtonText()
        {
            return Lang.Get(CreateCategoryButtonTranslationKey);
        }

        internal static string GetDeleteCategoryButtonText()
        {
            return Lang.Get(DeleteCategoryButtonTranslationKey);
        }

        internal static string GetRenameCategoryButtonText()
        {
            return Lang.Get(RenameCategoryButtonTranslationKey);
        }

        internal static string GetCreateCategoryPromptTitle()
        {
            return Lang.Get(CreateCategoryPromptTitleTranslationKey);
        }

        internal static string GetRenameCategoryPromptTitle()
        {
            return Lang.Get(RenameCategoryPromptTitleTranslationKey);
        }

        internal static string GetCreateCategoryPromptMessage()
        {
            return Lang.Get(CreateCategoryPromptMessageTranslationKey);
        }

        internal static string GetCreateCategoryPromptPlaceholder()
        {
            return Lang.Get(CreateCategoryPromptPlaceholderTranslationKey);
        }

        internal static string GetCreateCategoryPromptOkText()
        {
            return Lang.Get(CreateCategoryPromptOkTranslationKey);
        }

        internal static string GetCreateCategoryPromptCancelText()
        {
            return Lang.Get(CreateCategoryPromptCancelTranslationKey);
        }

        internal static string GetCategoryNameTooLongMessage()
        {
            return $"[Handbook Categories] Category names are limited to {MaxCategoryNameLength} characters.";
        }

        internal static string GetRecipesOnlyToggleText()
        {
            return Lang.Get(RecipesOnlyToggleTranslationKey);
        }

        internal static string GetHideVariantsToggleText()
        {
            return Lang.Get(HideVariantsToggleTranslationKey);
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

        internal static bool TrySetHideVariantTypes(bool enabled)
        {
            if (hideVariantTypes == enabled)
            {
                return false;
            }

            hideVariantTypes = enabled;
            StoreHideVariantTypesSetting();
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

        private static void StoreHideVariantTypesSetting()
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

            if (config == null || config.HideVariantTypes == hideVariantTypes)
            {
                return;
            }

            config.HideVariantTypes = hideVariantTypes;
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

                        bool requiresCodeMatch = false;
                        bool requiresExactCodeMatch = false;

                        if (trimmed.Length > 0)
                        {
                            if (trimmed[0] == '=')
                            {
                                requiresCodeMatch = true;
                                requiresExactCodeMatch = true;
                                trimmed = trimmed.Substring(1);
                            }
                            else if (trimmed[0] == '%')
                            {
                                requiresCodeMatch = true;
                                trimmed = trimmed.Substring(1);

                                if (trimmed.Length > 0 && trimmed[0] == '%')
                                {
                                    requiresExactCodeMatch = true;
                                    trimmed = trimmed.Substring(1);
                                }
                            }
                        }

                        string normalized = requiresCodeMatch
                            ? NormalizePageCode(trimmed)
                            : NormalizeSearchTerm(trimmed);

                        if (requiresCodeMatch)
                        {
                            if (normalized.Length == 0)
                            {
                                continue;
                            }
                        }

                        if (normalized.Length == 0)
                        {
                            continue;
                        }

                        string cacheKey = requiresCodeMatch
                            ? requiresExactCodeMatch ? $"code-exact:{normalized}" : $"code:{normalized}"
                            : requiresTitleMatch ? $"title:{normalized}" : $"term:{normalized}";
                        if (isRequired)
                        {
                            cacheKey = $"required:{cacheKey}";
                        }
                        if (!seenCache.Add(cacheKey))
                        {
                            continue;
                        }

                        bool isExactMatch = !requiresCodeMatch || requiresExactCodeMatch;
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
            internal SearchTerm(string term, bool isExactMatch, bool requiresTitleMatch, bool requiresPageCodeMatch, bool isRequired = false, bool usesVanillaSearch = false, bool requireWholeWordVanillaMatch = false)
            {
                Term = term;
                IsExactMatch = isExactMatch;
                RequiresTitleMatch = requiresTitleMatch;
                RequiresPageCodeMatch = requiresPageCodeMatch;
                IsRequired = isRequired;
                UsesVanillaSearch = usesVanillaSearch;
                RequiresWholeWordVanillaMatch = requireWholeWordVanillaMatch;
            }

            internal string Term { get; }

            internal bool IsExactMatch { get; }

            internal bool RequiresTitleMatch { get; }

            internal bool RequiresPageCodeMatch { get; }

            internal bool IsRequired { get; }

            internal bool UsesVanillaSearch { get; }

            internal bool RequiresWholeWordVanillaMatch { get; }
        }

        private static WordCategoryDefinition[] wordCategories = Array.Empty<WordCategoryDefinition>();

        private static ICoreClientAPI capi;
        private static GuiComposer trackedCreateButtonComposer;
        private static GuiElementTextButton trackedCreateButton;
        private static bool createCategoryPromptOpen;
        private static GuiElementTextButton trackedCloseButton;
        private static long createButtonListenerId;
        private static GuiDialogHandbook trackedHandbookDialog;

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
            englishNormalizedTitleByPage.Clear();

            if (capi == null)
            {
                wordCategories = Array.Empty<WordCategoryDefinition>();
                onlyGridPages = true;
                hideVariantTypes = false;
                showOriginalSearchToggle = true;
                useOriginalSearch = false;
                showTutorialTab = true;
                showBlocksAndItemsTab = true;
                showGuidesTab = true;
                enableDragAndDrop = false;
                usingDefaultEnglishWordCategories = false;
                HandbookPageDragManager.SetEnabled(null, false);
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
                if (config?.UsesEnglishDefaults == true)
                {
                    config.Categories = HandbookCategoriesConfig.CreateDefaultCategories();
                    wordCategories = BuildWordCategories(config);
                    shouldStoreConfig = true;
                    usingDefaultCategories = true;
                }
                else
                {
                    usingDefaultCategories = false;
                }
            }

            onlyGridPages = config?.OnlyGridPages ?? false;
            hideVariantTypes = config?.HideVariantTypes ?? false;
            showTutorialTab = !(config?.DisableTutorialTab ?? false);
            showBlocksAndItemsTab = !(config?.DisableBlocksAndItemsTab ?? false);
            showGuidesTab = !(config?.DisableGuidesTab ?? false);
            showOriginalSearchToggle = !(config?.DisableOriginalSearchButton ?? false);
            enableDragAndDrop = !(config?.DisableDragAndDrop ?? false);

            if (!showOriginalSearchToggle)
            {
                useOriginalSearch = false;
            }

            HandbookPageDragManager.SetEnabled(capi, enableDragAndDrop);

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
            englishNormalizedTitleByPage.Clear();

            gridRecipePageCodes.Clear();
            vanillaSearchExtrasByPageCode.Clear();
            variantGroupDisplayNameByKey.Clear();


            if (createButtonListenerId != 0)
            {
                capi?.Event?.UnregisterGameTickListener(createButtonListenerId);
                createButtonListenerId = 0;
            }

            trackedCreateButtonComposer = null;
            trackedCreateButton = null;
            trackedCloseButton = null;
            trackedHandbookDialog = null;
            categoriesInitialized = false;
            categoriesDirty = true;

            HandbookPageDragManager.Clear();

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

        internal static bool TryAddTitleMatchToCategory(string categoryCode, string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return false;
            }

            if (!TryGetCategoryConfig(categoryCode, out HandbookCategoriesConfig config, out HandbookCategoryConfigEntry category))
            {
                return false;
            }

            string trimmedTitle = title.Trim();
            if (trimmedTitle.Length == 0)
            {
                return false;
            }

            category.MatchTitleWords ??= new List<string>();
            category.ForbiddenTitleWords ??= new List<string>();

            bool changed = RemoveWordCaseInsensitive(category.ForbiddenTitleWords, trimmedTitle);

            if (!category.MatchTitleWords.Any(existing => existing != null && existing.Equals(trimmedTitle, StringComparison.OrdinalIgnoreCase)))
            {
                category.MatchTitleWords.Add(trimmedTitle);
                changed = true;
            }

            if (!changed)
            {
                return false;
            }

            capi.StoreModConfig(config, HandbookCategoriesConfig.ConfigFileName);
            ReloadConfiguration();
            return true;
        }

        internal static bool TryAddPageCodeMatchToCategory(string categoryCode, string pageCode)
        {
            return TryUpdatePageCodeEntry(categoryCode, pageCode, addToForbidden: false, requireExactCodeMatch: true);
        }

        internal static bool TryAddForbiddenPageCodeToCategory(string categoryCode, string pageCode)
        {
            return TryUpdatePageCodeEntry(categoryCode, pageCode, addToForbidden: true, requireExactCodeMatch: true);
        }

        private static bool RemoveWordCaseInsensitive(List<string> list, string word)
        {
            if (list == null || string.IsNullOrWhiteSpace(word))
            {
                return false;
            }

            bool isExactWord = IsExactCodeWord(word);

            bool removed = false;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                string existing = list[i];
                if (existing == null)
                {
                    continue;
                }

                if (!isExactWord && IsExactCodeWord(existing))
                {
                    continue;
                }

                if (AreCategoryWordsEquivalent(existing, word))
                {
                    list.RemoveAt(i);
                    removed = true;
                }
            }

            return removed;
        }

        internal static bool AreCategoryWordsEquivalent(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return false;
            }

            bool leftExact = IsExactCodeWord(left);
            bool rightExact = IsExactCodeWord(right);

            if (leftExact && rightExact)
            {
                string leftCode = NormalizeExactCodeWord(left);
                string rightCode = NormalizeExactCodeWord(right);
                if (string.IsNullOrEmpty(leftCode) || string.IsNullOrEmpty(rightCode))
                {
                    return false;
                }

                return string.Equals(leftCode, rightCode, StringComparison.OrdinalIgnoreCase);
            }

            if (leftExact != rightExact)
            {
                return false;
            }

            return left.Equals(right, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExactCodeWord(string word)
        {
            return IsModernExactCodeWord(word) || IsLegacyExactCodeWord(word);
        }

        private static bool IsModernExactCodeWord(string word)
        {
            return !string.IsNullOrEmpty(word) && word.Length > 1 && word[0] == '=';
        }

        private static bool IsLegacyExactCodeWord(string word)
        {
            return !string.IsNullOrEmpty(word) && word.Length > 2 && word[0] == '%' && word[1] == '%';
        }

        private static string NormalizeExactCodeWord(string word)
        {
            if (string.IsNullOrEmpty(word))
            {
                return string.Empty;
            }

            if (word[0] == '=')
            {
                return word.Substring(1);
            }

            if (IsLegacyExactCodeWord(word))
            {
                return word.Substring(2);
            }

            return word;
        }

        private static bool RemoveExactCodeWordEntries(List<string> list, string code)
        {
            if (list == null || string.IsNullOrEmpty(code))
            {
                return false;
            }

            bool removed = false;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                string existing = list[i];
                if (IsExactCodeWord(existing)
                    && string.Equals(NormalizeExactCodeWord(existing), code, StringComparison.OrdinalIgnoreCase))
                {
                    list.RemoveAt(i);
                    removed = true;
                }
            }

            return removed;
        }

        private static bool TryGetCategoryConfig(string categoryCode, out HandbookCategoriesConfig config, out HandbookCategoryConfigEntry category)
        {
            config = null;
            category = null;

            if (capi == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(categoryCode) || !categoryCode.StartsWith(CategoryCodePrefix, StringComparison.Ordinal))
            {
                return false;
            }

            string sanitizedCode = categoryCode.Substring(CategoryCodePrefix.Length);
            if (string.IsNullOrEmpty(sanitizedCode))
            {
                return false;
            }

            config = capi.LoadModConfig<HandbookCategoriesConfig>(HandbookCategoriesConfig.ConfigFileName)
                ?? LoadDefaultConfiguration()
                ?? HandbookCategoriesConfig.CreateDefault();

            if (config?.Categories == null)
            {
                return false;
            }

            foreach (HandbookCategoryConfigEntry entry in config.Categories)
            {
                if (entry?.Name == null)
                {
                    continue;
                }

                string sanitizedName = Sanitize(entry.Name);
                if (string.Equals(sanitizedName, sanitizedCode, StringComparison.Ordinal))
                {
                    category = entry;
                    break;
                }
            }

            return category != null;
        }

        private static bool TryUpdatePageCodeEntry(string categoryCode, string pageCode, bool addToForbidden, bool requireExactCodeMatch = false)
        {
            if (!TryGetCategoryConfig(categoryCode, out HandbookCategoriesConfig config, out HandbookCategoryConfigEntry category))
            {
                return false;
            }

            string normalizedCode = NormalizePageCode(pageCode);
            if (string.IsNullOrEmpty(normalizedCode))
            {
                return false;
            }

            category.MatchWords ??= new List<string>();
            category.ForbiddenWords ??= new List<string>();

            List<string> targetList = addToForbidden ? category.ForbiddenWords : category.MatchWords;
            List<string> opposingList = addToForbidden ? category.MatchWords : category.ForbiddenWords;

            bool changed = false;

            List<(string value, string code)> valuesToAdd = new();

            List<string> exactCodesToRemove = null;

            if (requireExactCodeMatch)
            {
                AddValue("=", normalizedCode);

                string codename = ExtractCodenameFromPageCode(normalizedCode);
                if (!string.IsNullOrEmpty(codename)
                    && !string.Equals(codename, normalizedCode, StringComparison.Ordinal))
                {
                    exactCodesToRemove = new List<string> { codename };
                }
            }
            else
            {
                AddValue("%", normalizedCode);
            }

            if (exactCodesToRemove != null)
            {
                foreach (string codeToRemove in exactCodesToRemove)
                {
                    if (RemoveExactCodeWordEntries(targetList, codeToRemove))
                    {
                        changed = true;
                    }

                    if (RemoveExactCodeWordEntries(opposingList, codeToRemove))
                    {
                        changed = true;
                    }
                }
            }

            foreach ((string value, string code) in valuesToAdd)
            {
                if (RemoveWordCaseInsensitive(opposingList, value))
                {
                    changed = true;
                }

                if (requireExactCodeMatch && !string.IsNullOrEmpty(code)
                    && RemoveExactCodeWordEntries(targetList, code))
                {
                    changed = true;
                }

                if (!targetList.Any(existing => AreCategoryWordsEquivalent(existing, value)))
                {
                    targetList.Add(value);
                    changed = true;
                }
            }

            if (!changed)
            {
                return false;
            }

            capi.StoreModConfig(config, HandbookCategoriesConfig.ConfigFileName);
            ReloadConfiguration();
            return true;

            void AddValue(string prefix, string code)
            {
                if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(code))
                {
                    return;
                }

                string value = prefix + code;
                for (int i = 0; i < valuesToAdd.Count; i++)
                {
                    if (AreCategoryWordsEquivalent(valuesToAdd[i].value, value))
                    {
                        return;
                    }
                }

                valuesToAdd.Add((value, code));
            }
        }

        private static string NormalizePageCode(string pageCode)
        {
            if (string.IsNullOrWhiteSpace(pageCode))
            {
                return string.Empty;
            }

            return pageCode.Trim().ToLowerInvariant();
        }

        private static string ExtractCodenameFromPageCode(string normalizedCode)
        {
            if (string.IsNullOrEmpty(normalizedCode))
            {
                return string.Empty;
            }

            int firstDashIndex = normalizedCode.IndexOf('-');
            if (firstDashIndex < 0 || firstDashIndex == normalizedCode.Length - 1)
            {
                return normalizedCode;
            }

            int attributesIndex = normalizedCode.IndexOf("-{", firstDashIndex + 1, StringComparison.Ordinal);
            if (attributesIndex >= 0)
            {
                return normalizedCode.Substring(firstDashIndex + 1, attributesIndex - (firstDashIndex + 1));
            }

            return normalizedCode.Substring(firstDashIndex + 1);
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

            if (ShouldUseEnglishFallbackForDefaultCategories())
            {
                PopulateEnglishTitleCache(allPages);
            }
            else
            {
                englishNormalizedTitleByPage.Clear();
            }

            ComputeVariantGroupDisplayNames(allPages);

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
            vanillaSearchExtrasByPageCode.Clear();

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
                        if (!string.IsNullOrEmpty(recipe.RequiresTrait))
                        {
                            AddTraitSearchExtras(page.PageCode, recipe.RequiresTrait);
                        }
                    }
                }
            }

            void EnsureCategoryMetadata(WordCategoryDefinition definition)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.CategoryCode))
                {
                    return;
                }

                string categoryCode = definition.CategoryCode;

                if (!categorizedPages.ContainsKey(categoryCode))
                {
                    categorizedPages[categoryCode] = new List<GuiHandbookPage>();
                }

                if (!seenPageCodes.ContainsKey(categoryCode))
                {
                    seenPageCodes[categoryCode] = new HashSet<string>();
                }

                if (!displayNames.ContainsKey(categoryCode))
                {
                    displayNames[categoryCode] = definition.CategoryName;
                }

                if (!translationKeys.ContainsKey(categoryCode))
                {
                    translationKeys[categoryCode] = definition.TranslationKey;
                }
            }

            void AddPageToCategory(WordCategoryDefinition definition, GuiHandbookPage page)
            {
                if (page == null || definition == null || string.IsNullOrWhiteSpace(definition.CategoryCode))
                {
                    return;
                }

                string categoryCode = definition.CategoryCode;

                EnsureCategoryMetadata(definition);

                List<GuiHandbookPage> list = categorizedPages[categoryCode];

                if (seenPageCodes[categoryCode].Add(page.PageCode))
                {
                    list.Add(page);
                }
            }

            if (wordCategories != null)
            {
                foreach (WordCategoryDefinition definition in wordCategories)
                {
                    EnsureCategoryMetadata(definition);
                }
            }

            ApplyWordBasedCategories(allPages, onlyGridPages ? gridRecipePageCodes : null, AddPageToCategory);


            pagesByCategory.Clear();
            displayNameByCategory.Clear();
            translationKeyByCategory.Clear();
            orderedCategories.Clear();
            tabBackgroundByCategory.Clear();

            foreach (WordCategoryDefinition definition in wordCategories)
            {
                if (definition == null)
                {
                    continue;
                }

                string categoryCode = definition.CategoryCode;
                if (string.IsNullOrEmpty(categoryCode))
                {
                    continue;
                }

                if (!categorizedPages.TryGetValue(categoryCode, out List<GuiHandbookPage> list) || list == null)
                {
                    list = new List<GuiHandbookPage>();
                }

                if (list.Count > 1)
                {
                    list.Sort((a, b) => a.PageNumber.CompareTo(b.PageNumber));
                }

                pagesByCategory[categoryCode] = list;
                displayNameByCategory[categoryCode] = displayNames.TryGetValue(categoryCode, out string displayName)
                    ? displayName
                    : definition.CategoryName;
                translationKeyByCategory[categoryCode] = translationKeys.TryGetValue(categoryCode, out string translationKey)
                    ? translationKey
                    : definition.TranslationKey;
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

        private static void ApplyWordBasedCategories(IEnumerable<GuiHandbookPage> pages, ISet<string> gridRecipeCodes, Action<WordCategoryDefinition, GuiHandbookPage> addPageAction)
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

            foreach (GuiHandbookPage page in pages)
            {
                if (page == null || page.IsDuplicate)
                {
                    continue;
                }

                if (page is GuiHandbookItemStackPage stackPage && stackPage.Stack?.Collectible == null)
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

        internal static void UpdateSearchUi(GuiComposer overviewGui, string currentSearchText, GuiDialogHandbook dialog)
        {
            if (overviewGui == null)
            {
                return;
            }

            SearchQuery searchQuery = PrepareSearchTerms(currentSearchText);
            UpdateCreateButton(overviewGui, searchQuery, dialog);
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

            HashSet<string> seenVariantGroups = hideVariantTypes
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : null;

            foreach (WeightedHandbookPage weighted in weightedPages.OrderByDescending(w => w.Weight))
            {
                GuiHandbookPage page = weighted.Page;
                if (hideVariantTypes && seenVariantGroups != null)
                {
                    string variantKey = GetVariantGroupKey(page);
                    if (!string.IsNullOrEmpty(variantKey) && !seenVariantGroups.Add(variantKey))
                    {
                        continue;
                    }
                }

                PreparePageForDisplay(page);
                shownPages.Add(page);
            }

            UpdateScrollArea(overviewGui, listHeight);
        }

        private static void PreparePageForDisplay(GuiHandbookPage page)
        {
            if (page is not GuiHandbookItemStackPage itemPage)
            {
                return;
            }

            itemPage.Texture?.Dispose();
            itemPage.Texture = null;
        }

        private static string GetVariantGroupKey(GuiHandbookPage page)
        {
            if (page == null)
            {
                return null;
            }

            if (page is GuiHandbookGroupedItemstackPage groupedPage)
            {
                if (groupedPage.Stacks != null)
                {
                    foreach (ItemStack stack in groupedPage.Stacks)
                    {
                        string key = GetVariantGroupKeyForStack(stack, groupedPage);
                        if (!string.IsNullOrEmpty(key))
                        {
                            return key;
                        }
                    }
                }

                return GetVariantGroupKeyForStack(groupedPage.Stack, groupedPage);
            }

            if (page is GuiHandbookItemStackPage itemPage)
            {
                return GetVariantGroupKeyForStack(itemPage.Stack, page);
            }

            return null;
        }

        private static void ComputeVariantGroupDisplayNames(IEnumerable<GuiHandbookPage> pages)
        {
            variantGroupDisplayNameByKey.Clear();

            if (pages == null)
            {
                return;
            }

            Dictionary<string, List<string>> titlesByGroup = new(StringComparer.OrdinalIgnoreCase);

            foreach (GuiHandbookPage page in pages)
            {
                if (page == null)
                {
                    continue;
                }

                string key = GetVariantGroupKey(page);
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                string rawTitle = GetRawTitle(page, allowCachedItemStackTitle: false);
                if (string.IsNullOrWhiteSpace(rawTitle))
                {
                    continue;
                }

                if (!titlesByGroup.TryGetValue(key, out List<string> titles))
                {
                    titles = new List<string>();
                    titlesByGroup[key] = titles;
                }

                string trimmed = rawTitle.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                bool exists = false;
                for (int i = 0; i < titles.Count; i++)
                {
                    if (string.Equals(titles[i], trimmed, StringComparison.Ordinal))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    titles.Add(trimmed);
                }
            }

            foreach (KeyValuePair<string, List<string>> entry in titlesByGroup)
            {
                string displayName = DeriveVariantDisplayName(entry.Value);
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    variantGroupDisplayNameByKey[entry.Key] = displayName.Trim();
                }
            }
        }

        private static string DeriveVariantDisplayName(List<string> titles)
        {
            if (titles == null || titles.Count == 0)
            {
                return string.Empty;
            }

            List<string> distinct = titles
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .Select(title => title.Trim())
                .Where(title => title.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (distinct.Count == 0)
            {
                return string.Empty;
            }

            if (distinct.Count == 1)
            {
                return RemoveTrailingParenthetical(distinct[0]);
            }

            List<string[]> tokenized = distinct
                .Select(TokenizeTitle)
                .Where(tokens => tokens.Length > 0)
                .ToList();

            if (tokenized.Count > 0)
            {
                string[] firstTokens = tokenized[0];
                List<string> commonTokens = new();

                foreach (string token in firstTokens)
                {
                    string normalized = NormalizeToken(token);
                    if (normalized.Length == 0)
                    {
                        continue;
                    }

                    bool presentInAll = true;

                    for (int i = 1; i < tokenized.Count && presentInAll; i++)
                    {
                        if (!tokenized[i].Any(other => string.Equals(NormalizeToken(other), normalized, StringComparison.Ordinal)))
                        {
                            presentInAll = false;
                        }
                    }

                    if (presentInAll && !commonTokens.Any(existing => string.Equals(NormalizeToken(existing), normalized, StringComparison.Ordinal)))
                    {
                        commonTokens.Add(token);
                    }
                }

                string candidate = string.Join(" ", commonTokens).Trim();
                if (candidate.Length > 0)
                {
                    return candidate;
                }
            }

            string commonSubstring = FindCommonSubstring(distinct);
            if (!string.IsNullOrWhiteSpace(commonSubstring))
            {
                return commonSubstring.Trim();
            }

            return RemoveTrailingParenthetical(distinct[0]);
        }

        private static string[] TokenizeTitle(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            MatchCollection matches = WordRegex.Matches(text);
            if (matches.Count == 0)
            {
                return Array.Empty<string>();
            }

            string[] tokens = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++)
            {
                tokens[i] = matches[i].Value;
            }

            return tokens;
        }

        private static string NormalizeToken(string token)
        {
            return string.IsNullOrWhiteSpace(token) ? string.Empty : token.Trim().ToLowerInvariant();
        }

        private static string FindCommonSubstring(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return string.Empty;
            }

            string reference = values[0];
            if (string.IsNullOrWhiteSpace(reference))
            {
                return string.Empty;
            }

            string trimmedReference = reference.Trim();
            int referenceLength = trimmedReference.Length;

            for (int length = referenceLength; length >= 3; length--)
            {
                for (int start = 0; start <= referenceLength - length; start++)
                {
                    string candidate = trimmedReference.Substring(start, length);
                    if (string.IsNullOrWhiteSpace(candidate))
                    {
                        continue;
                    }

                    bool presentInAll = true;
                    for (int i = 1; i < values.Count; i++)
                    {
                        if (values[i].IndexOf(candidate, StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            presentInAll = false;
                            break;
                        }
                    }

                    if (presentInAll)
                    {
                        return candidate;
                    }
                }
            }

            return string.Empty;
        }

        private static string RemoveTrailingParenthetical(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string trimmed = text.Trim();
            int openIndex = trimmed.LastIndexOf('(');
            if (openIndex < 0)
            {
                return trimmed;
            }

            int closeIndex = trimmed.IndexOf(')', openIndex);
            if (closeIndex != trimmed.Length - 1)
            {
                return trimmed;
            }

            string withoutParenthetical = trimmed.Substring(0, openIndex).TrimEnd();
            return withoutParenthetical.Length > 0 ? withoutParenthetical : trimmed;
        }

        private static string GetVariantGroupKeyForStack(ItemStack stack, GuiHandbookPage page)
        {
            if (stack == null)
            {
                return null;
            }

            CollectibleObject collectible = stack.Collectible;
            if (collectible == null)
            {
                return null;
            }

            string groupByKey = GetGroupByKey(collectible);
            if (!string.IsNullOrEmpty(groupByKey))
            {
                return groupByKey;
            }

            string variantKey = BuildVariantPlaceholderKey(collectible);
            if (string.IsNullOrEmpty(variantKey))
            {
                variantKey = collectible.Code?.ToString();
            }

            if (string.IsNullOrEmpty(variantKey))
            {
                variantKey = GetNormalizedPageCode(page);
            }

            string attributeKey = GetAttributeStructureKey(stack);
            if (!string.IsNullOrEmpty(attributeKey))
            {
                return string.Concat(variantKey, "|attrs:", attributeKey);
            }

            return variantKey;
        }

        private static string GetGroupByKey(CollectibleObject collectible)
        {
            if (collectible == null)
            {
                return null;
            }

            JsonObject handbook = collectible.Attributes?["handbook"];
            JsonObject groupBy = handbook?["groupBy"];
            if (groupBy == null || !groupBy.Exists)
            {
                return null;
            }

            string[] patterns = groupBy.AsArray<string>();
            if (patterns == null || patterns.Length == 0)
            {
                return null;
            }

            string domain = collectible.Code?.Domain ?? string.Empty;
            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < patterns.Length; i++)
            {
                string pattern = patterns[i] ?? string.Empty;
                if (pattern.IndexOf(':') < 0 && !string.IsNullOrEmpty(domain))
                {
                    pattern = string.Concat(domain, ":", pattern);
                }

                if (i > 0)
                {
                    builder.Append('|');
                }

                builder.Append(pattern);
            }

            return builder.ToString();
        }

        private static string BuildVariantPlaceholderKey(CollectibleObject collectible)
        {
            AssetLocation code = collectible?.Code;
            if (code == null)
            {
                return null;
            }

            string path = code.Path ?? string.Empty;
            if (path.Length == 0)
            {
                return code.ToString();
            }

            int variantCount = collectible.VariantStrict?.Count ?? 0;
            if (variantCount <= 0)
            {
                return code.ToString();
            }

            string[] parts = path.Split('-');
            int baseSegmentCount = parts.Length - variantCount;
            if (baseSegmentCount < 1)
            {
                baseSegmentCount = 1;
            }

            StringBuilder builder = new StringBuilder(parts[0]);
            for (int i = 1; i < baseSegmentCount && i < parts.Length; i++)
            {
                builder.Append('-').Append(parts[i]);
            }

            int index = 0;
            foreach (KeyValuePair<string, string> variant in collectible.VariantStrict)
            {
                builder.Append('-');
                string key = variant.Key;
                if (string.IsNullOrEmpty(key))
                {
                    key = string.Concat("variant", index.ToString(CultureInfo.InvariantCulture));
                }
                builder.Append('{').Append(key).Append('}');
                index++;
            }

            if (parts.Length > baseSegmentCount + variantCount)
            {
                for (int i = baseSegmentCount + variantCount; i < parts.Length; i++)
                {
                    builder.Append('-').Append(parts[i]);
                }
            }

            return string.Concat(code.Domain, ":", builder.ToString());
        }

        private static string GetAttributeStructureKey(ItemStack stack)
        {
            ITreeAttribute attributes = stack?.Attributes;
            if (attributes == null || attributes.Count == 0)
            {
                return null;
            }

            ITreeAttribute clone = attributes.Clone();
            if (clone == null)
            {
                return null;
            }

            string[] ignored = GlobalConstants.IgnoredStackAttributes;
            if (ignored != null)
            {
                for (int i = 0; i < ignored.Length; i++)
                {
                    clone.RemoveAttribute(ignored[i]);
                }
            }

            clone.RemoveAttribute("durability");

            if (clone.Count == 0)
            {
                return null;
            }

            List<string> parts = new List<string>();
            CollectAttributeStructureKeys(clone, parts, string.Empty);

            if (parts.Count == 0)
            {
                return null;
            }

            parts.Sort(StringComparer.Ordinal);
            return string.Join("|", parts);
        }

        private static void CollectAttributeStructureKeys(ITreeAttribute tree, List<string> parts, string prefix)
        {
            if (tree == null || parts == null)
            {
                return;
            }

            foreach (KeyValuePair<string, IAttribute> entry in tree)
            {
                string key = entry.Key;
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                string path = string.IsNullOrEmpty(prefix) ? key : string.Concat(prefix, "/", key);
                IAttribute attribute = entry.Value;

                if (attribute is ITreeAttribute childTree)
                {
                    parts.Add(string.Concat(path, "/tree"));
                    CollectAttributeStructureKeys(childTree, parts, path);
                }
                else
                {
                    int attributeId = attribute?.GetAttributeId() ?? -1;
                    parts.Add(string.Concat(path, ":", attributeId.ToString(CultureInfo.InvariantCulture)));
                }
            }
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
                        string normalizedCodename = ExtractCodenameFromPageCode(normalizedPageCode);
                        if (!string.Equals(normalizedCodename, term.Term, StringComparison.Ordinal))
                        {
                            return false;
                        }
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

            if (term.UsesVanillaSearch)
            {
                float vanillaWeight = GetVanillaMatchWeight(page, term.Term, term.RequiresWholeWordVanillaMatch);
                if (vanillaWeight <= 0f)
                {
                    return false;
                }

                weight = vanillaWeight;
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

        private static float GetVanillaMatchWeight(GuiHandbookPage page, string term, bool requireWholeWordMatch)
        {
            if (page == null || string.IsNullOrEmpty(term))
            {
                return 0f;
            }

            float baseWeight = page.GetTextMatchWeight(term);
            bool baseHasWholeWord = !requireWholeWordMatch || MatchesWholeWordInPageText(page, term);

            float extrasWeight = GetVanillaSearchExtrasWeight(page, term, requireWholeWordMatch, out bool extrasHaveWholeWord);

            if (requireWholeWordMatch && !baseHasWholeWord && !extrasHaveWholeWord)
            {
                return 0f;
            }

            return extrasWeight > baseWeight ? extrasWeight : baseWeight;
        }

        private static float GetVanillaSearchExtrasWeight(GuiHandbookPage page, string term, bool requireWholeWordMatch, out bool hasWholeWordMatch)
        {
            hasWholeWordMatch = false;

            if (page == null || string.IsNullOrEmpty(term))
            {
                return 0f;
            }

            string pageCode = page.PageCode;
            if (string.IsNullOrEmpty(pageCode))
            {
                return 0f;
            }

            if (!vanillaSearchExtrasByPageCode.TryGetValue(pageCode, out HashSet<string> extras) || extras == null || extras.Count == 0)
            {
                return 0f;
            }

            foreach (string extra in extras)
            {
                if (string.IsNullOrEmpty(extra))
                {
                    continue;
                }

                if (extra.IndexOf(term, StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                if (requireWholeWordMatch && !ContainsWholeWord(extra, term))
                {
                    continue;
                }

                hasWholeWordMatch = true;

                float weight = 1f;

                if (page is GuiHandbookItemStackPage itemPage)
                {
                    weight += itemPage.searchWeightOffset;
                }

                return weight;
            }

            return 0f;
        }

        private static bool MatchesWholeWordInPageText(GuiHandbookPage page, string term)
        {
            if (page == null || string.IsNullOrEmpty(term))
            {
                return false;
            }

            foreach (string candidate in EnumerateVanillaSearchTexts(page))
            {
                string normalized = NormalizeWholeWordCandidate(candidate);
                if (normalized.Length == 0)
                {
                    continue;
                }

                if (ContainsWholeWord(normalized, term))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> EnumerateVanillaSearchTexts(GuiHandbookPage page)
        {
            if (page == null)
            {
                yield break;
            }

            switch (page)
            {
                case GuiHandbookItemStackPage itemPage:
                    yield return itemPage.TextCacheAll;
                    yield return itemPage.TextCacheTitle;
                    break;
                case GuiHandbookCommandPage commandPage:
                    yield return commandPage.TextCacheAll;
                    yield return commandPage.TextCacheTitle;
                    break;
                case GuiHandbookTextPage textPage:
                    yield return textPage.Text;
                    if (!string.IsNullOrWhiteSpace(textPage.Title))
                    {
                        yield return Lang.Get(textPage.Title);
                    }
                    break;
                case GuiHandbookMealRecipePage mealPage:
                    yield return GetMealRecipeTitle(mealPage);
                    yield return GetMealRecipeSearchKeywords(mealPage);
                    break;
            }
        }

        private static string GetMealRecipeTitle(GuiHandbookMealRecipePage page)
        {
            string title = page?.Title;
            return string.IsNullOrWhiteSpace(title) ? string.Empty : Lang.Get(title);
        }

        private static string GetMealRecipeSearchKeywords(GuiHandbookMealRecipePage page)
        {
            if (page == null)
            {
                return string.Empty;
            }

            string pageCode = page.PageCode ?? string.Empty;
            bool isPie = pageCode.EndsWith("-pie", StringComparison.Ordinal);
            string key = string.Concat("handbook-mealrecipe-", isPie ? "pie" : "meal", "searchkeywords");
            return Lang.Get(key);
        }

        private static string NormalizeWholeWordCandidate(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? string.Empty : text.ToSearchFriendly().Trim();
        }

        private static bool ContainsWholeWord(string source, string term)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(term))
            {
                return false;
            }

            int index = 0;
            while (index <= source.Length - term.Length)
            {
                index = source.IndexOf(term, index, StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }

                bool atStart = index == 0 || source[index - 1] == ' ';
                int afterIndex = index + term.Length;
                bool atEnd = afterIndex >= source.Length || source[afterIndex] == ' ';

                if (atStart && atEnd)
                {
                    return true;
                }

                index = afterIndex;
            }

            return false;
        }

        private static void AddTraitSearchExtras(string pageCode, string traitCode)
        {
            if (string.IsNullOrWhiteSpace(pageCode) || string.IsNullOrWhiteSpace(traitCode))
            {
                return;
            }

            string traitTranslationKey = string.Concat("traitname-", traitCode);
            string traitName = Lang.GetMatchingIfExists(traitTranslationKey);

            if (string.IsNullOrWhiteSpace(traitName))
            {
                traitName = Lang.Get(traitTranslationKey);
            }

            AddVanillaSearchText(pageCode, traitName);
            AddVanillaSearchText(pageCode, traitCode);

            string requiresTraitText = null;

            if (!string.IsNullOrWhiteSpace(traitName))
            {
                requiresTraitText = Lang.GetMatchingIfExists("gridrecipe-requirestrait", traitName);
            }

            if (string.IsNullOrWhiteSpace(requiresTraitText))
            {
                requiresTraitText = Lang.Get("gridrecipe-requirestrait", !string.IsNullOrWhiteSpace(traitName) ? traitName : traitCode);
            }

            AddVanillaSearchText(pageCode, requiresTraitText);
        }

        private static void AddVanillaSearchText(string pageCode, string text)
        {
            if (string.IsNullOrWhiteSpace(pageCode) || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string normalized = text.ToSearchFriendly().Trim();
            if (normalized.Length == 0)
            {
                return;
            }

            if (!vanillaSearchExtrasByPageCode.TryGetValue(pageCode, out HashSet<string> extras))
            {
                extras = new HashSet<string>(StringComparer.Ordinal);
                vanillaSearchExtrasByPageCode[pageCode] = extras;
            }

            extras.Add(normalized);
        }

        private static string GetSearchableContent(PageTitleData titleData)
        {
            return titleData.SearchableContent;
        }

        private static PageTitleData GetPageTitleData(GuiHandbookPage page)
        {
            string localizedTitle = GetNormalizedTitle(page);
            if (englishNormalizedTitleByPage.Count == 0)
            {
                return new PageTitleData(localizedTitle, localizedTitle);
            }

            if (!englishNormalizedTitleByPage.TryGetValue(page, out string englishTitle) || string.IsNullOrEmpty(englishTitle))
            {
                englishTitle = localizedTitle;
            }

            return new PageTitleData(englishTitle, localizedTitle);
        }

        internal static bool TryGetVariantDisplayText(GuiHandbookPage page, out string displayText)
        {
            displayText = null;

            if (page == null || !hideVariantTypes)
            {
                return false;
            }

            string rawTitle = GetRawTitle(page, allowCachedItemStackTitle: false);
            if (string.IsNullOrWhiteSpace(rawTitle))
            {
                return false;
            }

            string variantKey = GetVariantGroupKey(page);
            if (!string.IsNullOrEmpty(variantKey)
                && variantGroupDisplayNameByKey.TryGetValue(variantKey, out string derivedTitle)
                && !string.IsNullOrWhiteSpace(derivedTitle))
            {
                displayText = derivedTitle;
                return true;
            }

            string trimmed = RemoveTrailingParenthetical(rawTitle);
            if (!string.Equals(trimmed, rawTitle.Trim(), StringComparison.Ordinal))
            {
                displayText = trimmed;
                return true;
            }

            return false;
        }

        internal static string GetLocalizedPageTitle(GuiHandbookPage page)
        {
            if (page == null)
            {
                return string.Empty;
            }

            string title = GetRawTitle(page, allowCachedItemStackTitle: true);
            if (hideVariantTypes && TryGetVariantDisplayText(page, out string variantTitle) && !string.IsNullOrWhiteSpace(variantTitle))
            {
                title = variantTitle;
            }
            return string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim();
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

            return NormalizeTitle(rawTitle);
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

        private static string NormalizeTitle(string rawTitle)
        {
            if (string.IsNullOrWhiteSpace(rawTitle))
            {
                return string.Empty;
            }

            return rawTitle.ToSearchFriendly().ToLowerInvariant().Trim();
        }

        private static void PopulateEnglishTitleCache(IEnumerable<GuiHandbookPage> pages)
        {
            englishNormalizedTitleByPage.Clear();

            if (pages == null)
            {
                return;
            }

            string originalLocale = Lang.CurrentLocale;
            bool restoreLocale = !string.IsNullOrEmpty(originalLocale);
            bool alreadyEnglish = IsEnglishLocale(originalLocale);

            if (!alreadyEnglish)
            {
                Lang.ChangeLanguage(EnglishLocaleCode);
            }

            try
            {
                foreach (GuiHandbookPage page in pages)
                {
                    if (page == null)
                    {
                        continue;
                    }

                    string normalized = NormalizeTitle(GetRawTitle(page, allowCachedItemStackTitle: false));
                    if (!string.IsNullOrEmpty(normalized))
                    {
                        englishNormalizedTitleByPage[page] = normalized;
                    }
                }
            }
            finally
            {
                if (!alreadyEnglish)
                {
                    if (restoreLocale)
                    {
                        Lang.ChangeLanguage(originalLocale);
                    }
                    else
                    {
                        Lang.ChangeLanguage(EnglishLocaleCode);
                    }
                }
            }
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
                || config.HideVariantTypes != defaultConfig.HideVariantTypes
                || config.DisableTutorialTab != defaultConfig.DisableTutorialTab
                || config.DisableBlocksAndItemsTab != defaultConfig.DisableBlocksAndItemsTab
                || config.DisableGuidesTab != defaultConfig.DisableGuidesTab
                || config.DisableOriginalSearchButton != defaultConfig.DisableOriginalSearchButton
                || config.DisableDragAndDrop != defaultConfig.DisableDragAndDrop)
            {
                return false;
            }

            List<HandbookCategoryConfigEntry> categories = config.Categories ?? new List<HandbookCategoryConfigEntry>();
            List<HandbookCategoryConfigEntry> defaultCategories = HandbookCategoriesConfig.CreateDefaultCategories();

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
                bool usesVanillaSearch = false;

                while (!string.IsNullOrEmpty(raw))
                {
                    char prefix = raw[0];
                    if (prefix == '+')
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
                        continue;
                    }

                    if (prefix == '=')
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
                        exactMatch = true;
                        requiresTitleMatch = false;
                        usesVanillaSearch = false;
                        continue;
                    }

                    if (prefix == '%')
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
                        usesVanillaSearch = false;

                        if (!string.IsNullOrEmpty(raw) && raw[0] == '%')
                        {
                            raw = raw.Substring(1);
                            exactMatch = true;
                        }
                        continue;
                    }

                    if (prefix == '?')
                    {
                        if (raw.Length == 1)
                        {
                            excludeNext = false;
                            buildingExactMatch = false;
                            currentTokenRequiresTitleMatch = false;
                            return;
                        }

                        raw = raw.Substring(1);
                        usesVanillaSearch = true;
                        continue;
                    }

                    break;
                }

                if (requiresCodeMatch)
                {
                    usesVanillaSearch = false;
                }

                if (usesVanillaSearch)
                {
                    requiresTitleMatch = false;
                }

                string term = requiresCodeMatch
                    ? NormalizePageCode(raw)
                    : raw.ToSearchFriendly().Trim();
                if (term.Length == 0)
                {
                    excludeNext = false;
                    buildingExactMatch = false;
                    currentTokenRequiresTitleMatch = false;
                    return;
                }

                if (excludeNext)
                {
                    excludeTerms.Add(new SearchTerm(term, exactMatch, requiresTitleMatch, requiresCodeMatch, usesVanillaSearch: usesVanillaSearch));
                }
                else
                {
                    includeTerms.Add(new SearchTerm(term, exactMatch, requiresTitleMatch, requiresCodeMatch, isRequired, usesVanillaSearch));
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
                    bool isExactMatch = term.RequiresPageCodeMatch ? term.IsExactMatch : true;
                    bool useVanillaSearch = term.UsesVanillaSearch;
                    bool requireWholeWord = term.RequiresWholeWordVanillaMatch || useVanillaSearch;
                    copy[i] = new SearchTerm(term.Term, isExactMatch, term.RequiresTitleMatch, term.RequiresPageCodeMatch, term.IsRequired, useVanillaSearch, requireWholeWord);
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

            if (categoryName.Length > MaxCategoryNameLength)
            {
                capi.ShowChatMessage(GetCategoryNameTooLongMessage());
                return false;
            }

            string remainder = CombineCategorySegments(beforeHash, afterCategory);
            string trimmedRemainder = remainder?.Trim();
            bool hasKeywords = !string.IsNullOrEmpty(trimmedRemainder);

            string formattedCategoryName = FormatCategoryNameForCommand(categoryName);
            if (string.IsNullOrWhiteSpace(formattedCategoryName))
            {
                capi.ShowChatMessage("[Handbook Categories] Unable to format the category name. Please try again.");
                return false;
            }

            string command = hasKeywords
                ? $".categorymod {formattedCategoryName} {trimmedRemainder}"
                : $".categorymod {formattedCategoryName}";

            capi.TriggerChatMessage(command);
            return true;
        }

        private static void UpdateCreateButton(GuiComposer overviewGui, SearchQuery searchQuery, GuiDialogHandbook dialog)
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

            RegisterCreateButton(overviewGui, createButton, dialog);
        }

        internal static void RegisterCreateButton(GuiComposer overviewGui, GuiElementTextButton button, GuiDialogHandbook dialog = null)
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
            if (dialog != null)
            {
                trackedHandbookDialog = dialog;
            }

            EnsureCreateButtonLayout(overviewGui, button);
            UpdateCreateButtonTextInternal(overviewGui, button);
            ApplyCreateButtonEnabledState(button);
        }

        internal static void SetCreateCategoryPromptOpen(bool isOpen)
        {
            if (createCategoryPromptOpen == isOpen)
            {
                return;
            }

            createCategoryPromptOpen = isOpen;
            ApplyCreateButtonEnabledState(trackedCreateButton);
        }

        internal static bool TryExecuteCategoryDeleteCommand(GuiDialogHandbook dialog)
        {
            if (dialog == null || capi == null)
            {
                return false;
            }

            if (!IsControlKeyHeld() || IsShiftKeyHeld())
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

            string commandArgument = FormatChatArgument(tabName);
            string command = $".categorymoddelete {commandArgument}";
            capi.TriggerChatMessage(command);
            return true;
        }

        internal static bool ShouldHandleRename(GuiDialogHandbook dialog)
        {
            if (dialog == null)
            {
                return false;
            }

            if (!IsShiftKeyHeld() || IsControlKeyHeld())
            {
                return false;
            }

            return IsModCategoryCode(dialog.currentCatgoryCode);
        }

        internal static bool TryRenameCategory(string categoryCode, string newDisplayName, out string newCategoryCode)
        {
            newCategoryCode = null;

            if (capi == null)
            {
                return false;
            }

            if (!IsModCategoryCode(categoryCode))
            {
                return false;
            }

            string normalizedName = NormalizeCategoryName(newDisplayName);
            if (normalizedName == null)
            {
                capi.ShowChatMessage("[Handbook Categories] Please enter a valid category name.");
                return false;
            }

            if (normalizedName.Length > MaxCategoryNameLength)
            {
                capi.ShowChatMessage(GetCategoryNameTooLongMessage());
                return false;
            }

            if (!TryGetCategoryConfig(categoryCode, out HandbookCategoriesConfig config, out HandbookCategoryConfigEntry category))
            {
                capi.ShowChatMessage("[Handbook Categories] Unable to locate the selected category.");
                return false;
            }

            string sanitizedNew = Sanitize(normalizedName);
            if (string.IsNullOrEmpty(sanitizedNew))
            {
                capi.ShowChatMessage("[Handbook Categories] Please choose a different category name.");
                return false;
            }

            string sanitizedExisting = Sanitize(category.Name);

            if (config?.Categories != null)
            {
                foreach (HandbookCategoryConfigEntry entry in config.Categories)
                {
                    if (entry == null || ReferenceEquals(entry, category))
                    {
                        continue;
                    }

                    string otherSanitized = Sanitize(entry.Name);
                    if (!string.IsNullOrEmpty(otherSanitized) && string.Equals(otherSanitized, sanitizedNew, StringComparison.Ordinal))
                    {
                        capi.ShowChatMessage($"[Handbook Categories] A category named \"{normalizedName}\" already exists.");
                        return false;
                    }
                }
            }

            bool nameChanged = !string.Equals(category.Name, normalizedName, StringComparison.Ordinal);
            bool codeChanged = !string.Equals(sanitizedExisting, sanitizedNew, StringComparison.Ordinal);

            if (!nameChanged && !codeChanged)
            {
                return false;
            }

            category.Name = normalizedName;
            capi.StoreModConfig(config, HandbookCategoriesConfig.ConfigFileName);
            ReloadConfiguration();
            RequestTabsRebuild();

            newCategoryCode = codeChanged ? $"{CategoryCodePrefix}{sanitizedNew}" : categoryCode;
            return true;
        }

        private static string FormatChatArgument(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return $"\"{escaped}\"";
        }

        private static void MonitorCreateButtonState(float deltaTime)
        {
            GuiComposer composer = trackedCreateButtonComposer;
            if (composer == null || !composer.Composed)
            {
                trackedCreateButtonComposer = null;
                trackedCreateButton = null;
                trackedCloseButton = null;
                trackedHandbookDialog = null;
                return;
            }

            GuiElementTextButton button = composer.GetButton(CreateCategoryButtonKey);
            if (button == null)
            {
                trackedCreateButtonComposer = null;
                trackedCreateButton = null;
                trackedCloseButton = null;
                trackedHandbookDialog = null;
                return;
            }

            trackedCreateButton = button;


            if (!TryEnsureButtonBounds(composer, button))
            {
                trackedCreateButtonComposer = null;
                trackedCreateButton = null;
                trackedCloseButton = null;
                trackedHandbookDialog = null;

                return;
            }

            UpdateCreateButtonTextInternal(composer, button);
            ApplyCreateButtonEnabledState(button);
        }

        private static bool TryEnsureButtonBounds(GuiComposer composer, GuiElementTextButton button)
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

            if (bounds.RequiresRecalculation)
            {
                bounds.CalcWorldBounds();
            }

            return EnsureCreateButtonLayout(composer, button);
        }

        private static void UpdateCreateButtonTextInternal(GuiComposer composer, GuiElementTextButton button)
        {
            if (composer == null || button == null || !composer.Composed)
            {
                return;
            }

            string desiredText;
            if (ShouldShowDeleteText(button))
            {
                desiredText = GetDeleteCategoryButtonText();
            }
            else if (ShouldShowRenameText(button))
            {
                desiredText = GetRenameCategoryButtonText();
            }
            else
            {
                desiredText = GetCreateCategoryButtonText();
            }

            if (!string.Equals(button.Text, desiredText, StringComparison.Ordinal))
            {
                button.Text = desiredText;
                RecomposeTextButton(button);

                EnsureCreateButtonLayout(composer, button);

            }
        }

        private static void ApplyCreateButtonEnabledState(GuiElementTextButton button)
        {
            if (button == null)
            {
                return;
            }

            bool shouldEnable = !createCategoryPromptOpen;
            if (button.Enabled != shouldEnable)
            {
                button.Enabled = shouldEnable;
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

        private static bool ShouldShowDeleteText(GuiElementTextButton _)
        {
            if (!IsControlKeyHeld() || IsShiftKeyHeld())
            {
                return false;
            }

            string categoryCode = trackedHandbookDialog?.currentCatgoryCode;
            return IsModCategoryCode(categoryCode);
        }

        private static bool ShouldShowRenameText(GuiElementTextButton _)
        {
            if (!IsShiftKeyHeld() || IsControlKeyHeld())
            {
                return false;
            }

            string categoryCode = trackedHandbookDialog?.currentCatgoryCode;
            return IsModCategoryCode(categoryCode);
        }

        private static bool IsMouseOverButton(GuiElementTextButton button)
        {
            if (button?.Bounds == null || capi?.Input == null)
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

        private static bool IsShiftKeyHeld()
        {
            bool[] keys = capi?.Input?.KeyboardKeyState;
            if (keys == null)
            {
                return false;
            }

            int leftIndex = (int)GlKeys.ShiftLeft;
            int rightIndex = (int)GlKeys.ShiftRight;

            bool leftDown = leftIndex >= 0 && leftIndex < keys.Length && keys[leftIndex];
            bool rightDown = rightIndex >= 0 && rightIndex < keys.Length && keys[rightIndex];

            return leftDown || rightDown;
        }

        private static bool IsModCategoryCode(string categoryCode)
        {
            return !string.IsNullOrEmpty(categoryCode)
                && categoryCode.StartsWith(CategoryCodePrefix, StringComparison.Ordinal);
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
            if (trimmed.Length == 0)
            {
                return null;
            }

            bool wasQuoted = false;
            if (trimmed.Length >= 2)
            {
                char first = trimmed[0];
                char last = trimmed[^1];
                if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                {
                    trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
                    wasQuoted = true;
                }
            }

            if (trimmed.Length == 0)
            {
                return null;
            }

            if (wasQuoted)
            {
                trimmed = UnescapeQuotedCategoryName(trimmed);
            }

            return trimmed.Length == 0 ? null : trimmed;
        }

        private static string UnescapeQuotedCategoryName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            StringBuilder builder = new(value.Length);
            bool escaping = false;

            foreach (char ch in value)
            {
                if (escaping)
                {
                    builder.Append(ch);
                    escaping = false;
                }
                else if (ch == '\\')
                {
                    escaping = true;
                }
                else
                {
                    builder.Append(ch);
                }
            }

            if (escaping)
            {
                builder.Append('\\');
            }

            return builder.ToString();
        }

        private static string TrimCategoryNameToMaximum(string categoryName, out bool wasTrimmed)
        {
            wasTrimmed = false;
            if (string.IsNullOrEmpty(categoryName))
            {
                return categoryName;
            }

            string trimmed = categoryName.Trim();
            if (trimmed.Length <= MaxCategoryNameLength)
            {
                return trimmed;
            }

            wasTrimmed = true;
            return trimmed.Substring(0, MaxCategoryNameLength);
        }

        private static string FormatCategoryNameForCommand(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return null;
            }

            string trimmed = categoryName.Trim();
            if (trimmed.Length == 0)
            {
                return null;
            }

            string escaped = trimmed.Replace("\\", "\\\\");
            bool needsQuotes = trimmed.Any(char.IsWhiteSpace);

            if (!needsQuotes)
            {
                return escaped;
            }

            bool containsDoubleQuote = trimmed.Contains('\"');
            bool containsSingleQuote = trimmed.Contains('\'');
            char quoteChar = containsDoubleQuote && !containsSingleQuote ? '\'' : '\"';

            if (quoteChar == '\'')
            {
                escaped = escaped.Replace("'", "\\'");
            }
            else
            {
                escaped = escaped.Replace("\"", "\\\"");
            }

            return string.Concat(quoteChar, escaped, quoteChar);
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

            if (closeBounds.ParentBounds != null && !ReferenceEquals(bounds.ParentBounds, closeBounds.ParentBounds))
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

            double targetWidth = closeBounds.fixedWidth > 0.0 ? closeBounds.fixedWidth : bounds.fixedWidth;
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
            double createPadding = Math.Max(0.0, bounds.fixedPaddingX);
            double closePadding = Math.Max(0.0, closeBounds.fixedPaddingX);
            double spacing = CreateButtonCloseSpacing + createPadding + closePadding;
            bounds.FixedLeftOf(closeBounds, spacing);
            if (ValuesDiffer(previousX, bounds.fixedX))
            {
                changed = true;
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

                string originalName = name;
                name = TrimCategoryNameToMaximum(name, out bool wasTrimmedForLength);
                if (wasTrimmedForLength)
                {
                    capi?.Logger?.Warning("[HandbookCategories] Category name \"{0}\" exceeds {1} characters and was truncated to \"{2}\".", originalName, MaxCategoryNameLength, name);
                    entry.Name = name;
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

            string normalized = builder.ToString();

            if (normalized.Length > 2 && normalized[0] == '%' && normalized[1] == '%')
            {
                return "=" + normalized.Substring(2);
            }

            return normalized;
        }
    }
}
