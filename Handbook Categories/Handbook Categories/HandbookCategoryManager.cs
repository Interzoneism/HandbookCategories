using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
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
        private const string CreateCategoryPromptTitleTranslationKey = "enhancedhandbook:dialog-create-category-title";
        private const string CreateCategoryPromptMessageTranslationKey = "enhancedhandbook:dialog-create-category-message";
        private const string CreateCategoryPromptPlaceholderTranslationKey = "enhancedhandbook:dialog-create-category-placeholder";
        private const string CreateCategoryPromptOkTranslationKey = "enhancedhandbook:dialog-create-category-ok";
        private const string CreateCategoryPromptCancelTranslationKey = "enhancedhandbook:dialog-create-category-cancel";
        private const string AddSearchResultsToggleTranslationKey = "enhancedhandbook:toggle-add-search-results";
        private const string RenameCategoryButtonTranslationKey = "enhancedhandbook:button-rename-category";
        private const string RenameCategoryPromptTitleTranslationKey = "enhancedhandbook:dialog-rename-category-title";
        internal const int MaxCategoryNameLength = 20;
        private const double CreateButtonMinimumWidth = 60.0;
        private const double CreateButtonCloseSpacing = 10.0;
        private const long RowHighlightDurationMs = 2000L;

        private static readonly int CollapseHighlightColor = ColorUtil.ToRgba(160, 255, 230, 0);
        private static readonly int RestoreHighlightColor = ColorUtil.ToRgba(160, 80, 140, 255);

        private const string GroupCategoryCodePrefix = "handbookcategories-groupcat-";
        private const string GroupPageCodePrefix = "handbookcategories-grouppage-";
        private const string EverythingCategoryKey = "\0";
        private const string DefaultGroupName = "Group";
        private const string WoodGroupHiddenCodePrefix = GroupCategoryCodePrefix + "woodvariant-";
        private const string WoodGroupPageCodePrefix = GroupPageCodePrefix + "woodvariant-";
        private const string WoodGroupDisplayCategoryName = "Wood Variants";
        private static readonly string WoodGroupDisplayCategoryCode = string.Concat(CategoryCodePrefix, Sanitize(WoodGroupDisplayCategoryName));
        private const string StoneGroupHiddenCodePrefix = GroupCategoryCodePrefix + "stonevariant-";
        private const string StoneGroupPageCodePrefix = GroupPageCodePrefix + "stonevariant-";
        private const string StoneGroupDisplayCategoryName = "Stone Variants";
        private static readonly string StoneGroupDisplayCategoryCode = string.Concat(CategoryCodePrefix, Sanitize(StoneGroupDisplayCategoryName));
        private const string CeramicGroupHiddenCodePrefix = GroupCategoryCodePrefix + "ceramicvariant-";
        private const string CeramicGroupPageCodePrefix = GroupPageCodePrefix + "ceramicvariant-";
        private const string CeramicGroupDisplayCategoryName = "Ceramic Variants";
        private static readonly string CeramicGroupDisplayCategoryCode = string.Concat(CategoryCodePrefix, Sanitize(CeramicGroupDisplayCategoryName));
        private const string EverythingGroupsDisplayCategoryName = "Everything (Groups)";
        private static readonly string EverythingGroupsDisplayCategoryCode = string.Concat(CategoryCodePrefix, Sanitize(EverythingGroupsDisplayCategoryName));

        private static readonly Dictionary<string, List<GuiHandbookPage>> pagesByCategory = new();
        private static readonly Dictionary<string, string> displayNameByCategory = new();
        private static readonly Dictionary<string, string> translationKeyByCategory = new();
        private static readonly List<string> orderedCategories = new();
        private static readonly Dictionary<string, double[]> tabBackgroundByCategory = new();
        private static readonly Dictionary<GuiHandbookPage, string> englishNormalizedTitleByPage = new();
        private static readonly Dictionary<GuiHandbookPage, RowHighlight> rowHighlights = new();
        private static readonly List<GroupHandbookPage> activeGroupPages = new();
        private static readonly Dictionary<GuiHandbookPage, List<GroupHandbookPage>> groupsByMemberPage = new();
        private static readonly Dictionary<string, GroupHandbookPage> groupByHiddenCategoryCode = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, List<GroupHandbookPage>> groupPagesByDisplayCategory = new(StringComparer.Ordinal);
        private static readonly Dictionary<GuiDialogHandbook, PendingGroupCreation> pendingGroupCreations = new();
        private static readonly Dictionary<GuiDialogHandbook, Stack<GroupNavigationState>> groupNavigationHistory = new();
        private static readonly Dictionary<string, WoodVariantGroupBuilder> woodVariantGroupsByKey = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> woodVariantGroupAliases = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, StoneVariantGroupBuilder> stoneVariantGroupsByKey = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> stoneVariantGroupAliases = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, CeramicVariantGroupBuilder> ceramicVariantGroupsByKey = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> ceramicVariantGroupAliases = new(StringComparer.OrdinalIgnoreCase);
        private static readonly string[] variantAliasPrefixesToStrip = { "block-", "item-" };
        private static readonly string[] woodVariantIgnoredPrefixes = { "clutter-", "block-clutter-" };
        private static readonly string[] stoneVariantIgnoredPrefixes = { "clutter-", "block-clutter-" };
        private static readonly string[] ceramicVariantIgnoredPrefixes = { "clutter-", "block-clutter-" };
        private static readonly string[] woodVariantTrailingDescriptors = { "wood", "timber" };
        private static readonly string[] stoneVariantTrailingDescriptors = { "stone", "rock" };
        private static readonly string[] ceramicVariantTrailingDescriptors = { "ceramic", "clay" };
        private static readonly FieldInfo ShownPagesField = typeof(GuiDialogHandbook).GetField("shownHandbookPages", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ListHeightField = typeof(GuiDialogHandbook).GetField("listHeight", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo OverviewGuiField = typeof(GuiDialogHandbook).GetField("overviewGui", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo BrowseHistoryField = typeof(GuiDialogHandbook).GetField("browseHistory", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo VerticalTabsField = typeof(GuiElementVerticalTabs).GetField("tabs", BindingFlags.Instance | BindingFlags.NonPublic);
        private static int nextGroupId = 1;
        private static string lastCreatedGroupName;
        private static HandbookGroupConfig groupConfig = HandbookGroupConfig.CreateDefault();
        private static readonly Dictionary<string, HandbookGroupConfigEntry> groupConfigEntriesByHiddenCode = new(StringComparer.Ordinal);

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
        private static bool enableDragAndDrop = true;
        private static bool enableGroupCreationHotkeys = false;
        private static bool createVariantCategories = false;
        private static bool createEverythingGrouped = false;

        private static readonly FieldInfo composerInteractiveElementsField = typeof(GuiComposer).GetField("interactiveElements", BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool categoriesInitialized;
        private static bool categoriesDirty = true;

        private static readonly HashSet<string> gridRecipePageCodes = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, HashSet<string>> vanillaSearchExtrasByPageCode = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> recipesOnlyExemptCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            "tutorial",
            "blocksitems",
            "stack",
            "guide",
            "guides"
        };

        private static readonly AssetLocation WoodWorldPropertyCode = new("worldproperties/block/wood.json");
        private const string WoodVariantReportFileName = "EnhancedHandbookWoodVariants.txt";
        private static readonly HashSet<string> knownWoodVariantNames = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> woodVariantDisplayNameByCode = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<WoodVariantReportKey, WoodPageReportEntry> woodVariantPagesByKey = new();
        private static bool woodVariantsLoaded;

        private static readonly AssetLocation StoneWorldPropertyCode = new("worldproperties/block/rock.json");
        private const string StoneVariantReportFileName = "EnhancedHandbookStoneVariants.txt";
        private static readonly HashSet<string> knownStoneVariantNames = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> stoneVariantDisplayNameByCode = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<StoneVariantReportKey, StonePageReportEntry> stoneVariantPagesByKey = new();
        private static bool stoneVariantsLoaded;
        private static readonly Dictionary<string, string> ceramicVariantDisplayNameByCode = new(StringComparer.OrdinalIgnoreCase);
        private static bool ceramicVariantsLoaded;
        private static readonly Dictionary<string, string> ceramicVariantColorMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["black"] = "black",
            ["brown"] = "brown",
            ["cream"] = "cream",
            ["fire"] = "fireclay",
            ["fireclay"] = "fireclay",
            ["gray"] = "gray",
            ["grey"] = "gray",
            ["orange"] = "orange",
            ["red"] = "red",
            ["tan"] = "tan",
        };

        internal static bool RecipesOnlyEnabled => onlyGridPages;

        internal static bool OriginalSearchEnabled => showOriginalSearchToggle && useOriginalSearch;

        internal static bool ShouldShowOriginalSearchToggle => showOriginalSearchToggle;

        internal static bool DragAndDropEnabled => enableDragAndDrop;

        internal static bool GroupCreationHotkeysEnabled => enableGroupCreationHotkeys;

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

        internal static string GetAddSearchResultsToggleText()
        {
            string text = Lang.GetMatchingIfExists(AddSearchResultsToggleTranslationKey);
            return string.IsNullOrWhiteSpace(text) ? "Add current search results" : text;
        }

        internal static string GetCategoryNameTooLongMessage()
        {
            return $"[Handbook Categories] Category names are limited to {MaxCategoryNameLength} characters.";
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

        private sealed class HiddenPageEntry
        {
            internal HiddenPageEntry(GuiHandbookPage page, int index)
            {
                Page = page;
                Index = index;
            }

            internal GuiHandbookPage Page { get; }

            internal int Index { get; }
        }

        private sealed class RowHighlight
        {
            internal RowHighlight(int color, long expiresAtMs)
            {
                Color = color;
                ExpiresAtMs = expiresAtMs;
            }

            internal int Color { get; }

            internal long ExpiresAtMs { get; }
        }

        private sealed class PendingGroupCreation
        {
            internal PendingGroupCreation(
                GuiDialogHandbook dialog,
                GuiComposer overviewGui,
                GuiElementFlatList searchList,
                List<IFlatListItem> shownPages,
                List<GuiHandbookPage> members,
                int insertIndex,
                string displayCategoryCode,
                GuiHandbookPage selectedPage)
            {
                Dialog = dialog;
                OverviewGui = overviewGui;
                SearchList = searchList;
                ShownPages = shownPages;
                Members = members;
                InsertIndex = insertIndex;
                DisplayCategoryCode = displayCategoryCode;
                SelectedPage = selectedPage;
            }

            internal GuiDialogHandbook Dialog { get; }

            internal GuiComposer OverviewGui { get; }

            internal GuiElementFlatList SearchList { get; }

            internal List<IFlatListItem> ShownPages { get; }

            internal List<GuiHandbookPage> Members { get; }

            internal int InsertIndex { get; }

            internal string DisplayCategoryCode { get; }

            internal GuiHandbookPage SelectedPage { get; }
        }

        private readonly struct WeightedHandbookPage
        {
            internal GuiHandbookPage Page { get; init; }

            internal float Weight { get; init; }

            internal int SortHint { get; init; }
        }

        private sealed class GroupNavigationState
        {
            internal GroupNavigationState(string previousCategoryCode, string hiddenCategoryCode, float scrollPosition)
            {
                PreviousCategoryCode = previousCategoryCode;
                HiddenCategoryCode = hiddenCategoryCode;
                ScrollPosition = scrollPosition;
            }

            internal string PreviousCategoryCode { get; }

            internal string HiddenCategoryCode { get; }

            internal float ScrollPosition { get; }
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
        private static long positionSaveListenerId;
        private static GuiDialogHandbook trackedHandbookDialog;
        private static ModDbCacheManager modDbCacheManager;

        internal static ICoreClientAPI ClientApi => capi;

        /// <summary>
        /// Gets the mod database cache manager instance.
        /// </summary>
        internal static ModDbCacheManager ModDbCache => modDbCacheManager;

        internal static bool IsReady => capi?.World != null && (capi.World.GridRecipes != null || !onlyGridPages);

        internal static void Initialize(ICoreClientAPI api)
        {
            capi = api;
            categoriesInitialized = false;
            categoriesDirty = true;
            ResetWoodVariantCache();
            ResetStoneVariantCache();
            ResetCeramicVariantCache();
            ReloadConfiguration();

            // Initialize the mod database cache manager
            if (modDbCacheManager != null)
            {
                modDbCacheManager.Dispose();
            }

            if (api != null)
            {
                modDbCacheManager = new ModDbCacheManager(api);
            }

            if (capi?.Event != null)
            {
                if (createButtonListenerId != 0)
                {
                    capi.Event.UnregisterGameTickListener(createButtonListenerId);
                    createButtonListenerId = 0;
                }

                if (positionSaveListenerId != 0)
                {
                    capi.Event.UnregisterGameTickListener(positionSaveListenerId);
                    positionSaveListenerId = 0;
                }

                createButtonListenerId = capi.Event.RegisterGameTickListener(MonitorCreateButtonState, 50);
                positionSaveListenerId = capi.Event.RegisterGameTickListener(PeriodicPositionSave, 5000);
            }
        }

        internal static void ReloadConfiguration()
        {
            categoriesDirty = true;
            categoriesInitialized = false;
            englishNormalizedTitleByPage.Clear();
            ResetStoneVariantCache();
            ResetCeramicVariantCache();

            if (capi == null)
            {
                wordCategories = Array.Empty<WordCategoryDefinition>();
                onlyGridPages = true;
                showOriginalSearchToggle = true;
                useOriginalSearch = false;
                showTutorialTab = true;
                showBlocksAndItemsTab = true;
                showGuidesTab = true;
                enableDragAndDrop = false;
                enableGroupCreationHotkeys = false;
                createVariantCategories = false;
                createEverythingGrouped = false;
                usingDefaultEnglishWordCategories = false;
                HandbookPageDragManager.SetEnabled(null, false);
                groupConfig = HandbookGroupConfig.CreateDefault();
                groupConfigEntriesByHiddenCode.Clear();
                ResetNextGroupIdFromConfig();
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
            showTutorialTab = !(config?.DisableTutorialTab ?? false);
            showBlocksAndItemsTab = !(config?.DisableBlocksAndItemsTab ?? false);
            showGuidesTab = !(config?.DisableGuidesTab ?? false);
            showOriginalSearchToggle = !(config?.DisableOriginalSearchButton ?? false);
            enableDragAndDrop = !(config?.DisableDragAndDrop ?? false);
            enableGroupCreationHotkeys = config?.EnableGroupCreationHotkeys ?? false;
            createVariantCategories = config?.CreateVariantCategories ?? false;
            createEverythingGrouped = config?.CreateEverythingGrouped ?? false;

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

            LoadGroupConfiguration();
        }

        private static void LoadGroupConfiguration()
        {
            if (capi == null)
            {
                groupConfig = HandbookGroupConfig.CreateDefault();
                groupConfigEntriesByHiddenCode.Clear();
                ResetNextGroupIdFromConfig();
                return;
            }

            HandbookGroupConfig loaded = capi.LoadModConfig<HandbookGroupConfig>(HandbookGroupConfig.ConfigFileName);
            bool shouldStore = false;

            if (loaded == null)
            {
                loaded = HandbookGroupConfig.CreateDefault();
                shouldStore = true;
            }

            groupConfig = loaded ?? HandbookGroupConfig.CreateDefault();
            groupConfig.Groups ??= new List<HandbookGroupConfigEntry>();

            if (NormalizeGroupConfiguration())
            {
                shouldStore = true;
            }

            if (shouldStore)
            {
                StoreGroupConfig();
            }
            else
            {
                ResetNextGroupIdFromConfig();
            }
        }

        private static bool NormalizeGroupConfiguration()
        {
            if (groupConfig == null)
            {
                groupConfig = HandbookGroupConfig.CreateDefault();
                groupConfigEntriesByHiddenCode.Clear();
                ResetNextGroupIdFromConfig();
                return false;
            }

            groupConfig.Groups ??= new List<HandbookGroupConfigEntry>();

            var normalizedGroups = new List<HandbookGroupConfigEntry>();
            var seenIds = new HashSet<int>();
            var seenHiddenCodes = new HashSet<string>(StringComparer.Ordinal);
            bool changed = false;

            foreach (HandbookGroupConfigEntry entry in groupConfig.Groups)
            {
                if (entry == null)
                {
                    changed = true;
                    continue;
                }

                if (entry.MemberPageCodes == null)
                {
                    entry.MemberPageCodes = new List<string>();
                    changed = true;
                }

                if (NormalizeGroupConfigEntry(entry, seenIds, seenHiddenCodes))
                {
                    changed = true;
                }

                normalizedGroups.Add(entry);
            }

            if (normalizedGroups.Count != groupConfig.Groups.Count)
            {
                groupConfig.Groups = normalizedGroups;
                changed = true;
            }
            else if (!ReferenceEquals(groupConfig.Groups, normalizedGroups))
            {
                groupConfig.Groups = normalizedGroups;
            }

            groupConfigEntriesByHiddenCode.Clear();
            foreach (HandbookGroupConfigEntry entry in groupConfig.Groups)
            {
                if (entry?.HiddenCategoryCode == null)
                {
                    continue;
                }

                groupConfigEntriesByHiddenCode[entry.HiddenCategoryCode] = entry;
            }

            ResetNextGroupIdFromConfig();

            return changed;
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
            ResetWoodVariantCache();
            ResetStoneVariantCache();
            ResetCeramicVariantCache();
            rowHighlights.Clear();
            createVariantCategories = false;
            createEverythingGrouped = false;


            if (createButtonListenerId != 0)
            {
                capi?.Event?.UnregisterGameTickListener(createButtonListenerId);
                createButtonListenerId = 0;
            }

            if (positionSaveListenerId != 0)
            {
                capi?.Event?.UnregisterGameTickListener(positionSaveListenerId);
                positionSaveListenerId = 0;
            }

            trackedCreateButtonComposer = null;
            trackedCreateButton = null;
            trackedCloseButton = null;
            trackedHandbookDialog = null;
            categoriesInitialized = false;
            categoriesDirty = true;

            ClearGroupData();

            HandbookPageDragManager.Clear();

            // Save and dispose the mod database cache
            if (modDbCacheManager != null)
            {
                modDbCacheManager.Dispose();
                modDbCacheManager = null;
            }
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

        internal static bool TryAddPagesToCategory(string categoryCode, IEnumerable<string> pageCodes)
        {
            if (string.IsNullOrWhiteSpace(categoryCode) || pageCodes == null)
            {
                return false;
            }

            bool addedAny = false;
            HashSet<string> seenCodes = new(StringComparer.OrdinalIgnoreCase);

            foreach (string code in pageCodes)
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                string normalized = NormalizePageCode(code);
                if (string.IsNullOrEmpty(normalized) || !seenCodes.Add(normalized))
                {
                    continue;
                }

                if (TryAddPageCodeMatchToCategory(categoryCode, code))
                {
                    addedAny = true;
                }
            }

            return addedAny;
        }

        internal static List<string> CaptureCurrentPageCodes(GuiDialogHandbook dialog)
        {
            List<string> codes = new();

            if (dialog == null)
            {
                return codes;
            }

            if (ShownPagesField?.GetValue(dialog) is not List<IFlatListItem> shownPages || shownPages.Count == 0)
            {
                return codes;
            }

            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

            foreach (IFlatListItem item in shownPages)
            {
                if (item is not GuiHandbookPage page)
                {
                    continue;
                }

                string pageCode = page.PageCode;
                if (string.IsNullOrWhiteSpace(pageCode))
                {
                    continue;
                }

                string normalized = NormalizePageCode(pageCode);
                if (string.IsNullOrEmpty(normalized) || !seen.Add(normalized))
                {
                    continue;
                }

                codes.Add(pageCode);
            }

            return codes;
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

            if (string.IsNullOrEmpty(translationKey))
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

            UpdateWoodVariantPageVisibility(allPages);

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

            LoadGroupPagesFromConfig(allPages);
            RestoreGroupCategories();
            RemoveEverythingGroupCategory();

            if (createEverythingGrouped)
            {
                RemoveWoodGroupCategory();
                RemoveStoneGroupCategory();
                RemoveCeramicGroupCategory();
                CreateEverythingGroupsCategory(allPages);
            }
            else if (createVariantCategories)
            {
                CreateWoodVariantGroups();
                CreateStoneVariantGroups();
                CreateCeramicVariantGroups();
            }
            else
            {
                RemoveWoodGroupCategory();
                RemoveStoneGroupCategory();
                RemoveCeramicGroupCategory();
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

        private readonly struct StoneVariantInfo
        {
            internal StoneVariantInfo(string value, string normalizedValue)
            {
                Value = value;
                NormalizedValue = normalizedValue;
            }

            internal string Value { get; }

            internal string NormalizedValue { get; }

            internal bool HasValue => !string.IsNullOrEmpty(NormalizedValue) || !string.IsNullOrEmpty(Value);
        }

        private readonly struct CeramicVariantInfo
        {
            internal CeramicVariantInfo(string rawValue, string canonicalValue, string normalizedValue, string displayName)
            {
                RawValue = rawValue;
                CanonicalValue = canonicalValue;
                NormalizedValue = normalizedValue;
                DisplayName = displayName;
            }

            internal string RawValue { get; }

            internal string CanonicalValue { get; }

            internal string NormalizedValue { get; }

            internal string DisplayName { get; }

            internal bool HasValue => !string.IsNullOrEmpty(NormalizedValue);
        }

        private readonly struct StonePageReportEntry
        {
            internal StonePageReportEntry(string stoneCode, string stoneDisplayName, string pageTitle, string itemCode, string pageCode)
            {
                StoneCode = stoneCode;
                StoneDisplayName = stoneDisplayName;
                PageTitle = pageTitle;
                ItemCode = itemCode;
                PageCode = pageCode;
            }

            internal string StoneCode { get; }

            internal string StoneDisplayName { get; }

            internal string PageTitle { get; }

            internal string ItemCode { get; }

            internal string PageCode { get; }
        }

        private readonly struct StoneVariantReportKey : IEquatable<StoneVariantReportKey>
        {
            internal StoneVariantReportKey(string stoneCode, string pageCode, string itemCode)
            {
                StoneCode = Normalize(stoneCode);
                PageCode = Normalize(pageCode);
                ItemCode = Normalize(itemCode);
            }

            internal string StoneCode { get; }

            internal string PageCode { get; }

            internal string ItemCode { get; }

            public bool Equals(StoneVariantReportKey other)
            {
                return string.Equals(StoneCode, other.StoneCode, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(PageCode, other.PageCode, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(ItemCode, other.ItemCode, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object obj)
            {
                return obj is StoneVariantReportKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                int hash = 17;
                hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(StoneCode);
                hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(PageCode);
                hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(ItemCode);
                return hash;
            }

            private static string Normalize(string value)
            {
                return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            }
        }

        private readonly struct WoodVariantInfo
        {
            internal WoodVariantInfo(string variantKey, string variantValue, string normalizedValue)
            {
                VariantKey = variantKey;
                VariantValue = variantValue;
                NormalizedValue = normalizedValue;
            }

            internal string VariantKey { get; }

            internal string VariantValue { get; }

            internal string NormalizedValue { get; }

            internal bool HasValue => !string.IsNullOrEmpty(NormalizedValue);
        }

        private readonly struct WoodPageReportEntry
        {
            internal WoodPageReportEntry(string woodCode, string pageTitle, string itemCode, string pageCode)
            {
                WoodCode = woodCode;
                PageTitle = pageTitle;
                ItemCode = itemCode;
                PageCode = pageCode;
            }

            internal string WoodCode { get; }

            internal string PageTitle { get; }

            internal string ItemCode { get; }

            internal string PageCode { get; }
        }

        private readonly struct WoodVariantReportKey : IEquatable<WoodVariantReportKey>
        {
            internal WoodVariantReportKey(string woodCode, string pageCode, string itemCode)
            {
                WoodCode = Normalize(woodCode);
                PageCode = Normalize(pageCode);
                ItemCode = Normalize(itemCode);
            }

            internal string WoodCode { get; }

            internal string PageCode { get; }

            internal string ItemCode { get; }

            public bool Equals(WoodVariantReportKey other)
            {
                return string.Equals(WoodCode, other.WoodCode, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(PageCode, other.PageCode, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(ItemCode, other.ItemCode, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object obj)
            {
                return obj is WoodVariantReportKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                int hash = 17;
                hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(WoodCode);
                hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(PageCode);
                hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(ItemCode);
                return hash;
            }

            private static string Normalize(string value)
            {
                return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            }
        }

        private sealed class WoodVariantGroupBuilder
        {
            private readonly HashSet<GuiHandbookPage> uniqueMembers = new();

            internal WoodVariantGroupBuilder(string displayName, string normalizedName, string sanitizedName)
            {
                DisplayName = displayName;
                NormalizedName = normalizedName;
                SanitizedName = sanitizedName;
            }

            internal string DisplayName { get; private set; }

            internal string NormalizedName { get; }

            internal string SanitizedName { get; private set; }

            internal List<GuiHandbookItemStackPage> Members { get; } = new();

            internal int SortHint { get; private set; } = int.MaxValue;

            internal void UpdateDisplayName(string displayName)
            {
                if (!string.IsNullOrEmpty(displayName))
                {
                    DisplayName = displayName;
                }
            }

            internal void UpdateSanitizedName(string sanitizedName)
            {
                if (!string.IsNullOrEmpty(sanitizedName))
                {
                    SanitizedName = sanitizedName;
                }
            }

            internal bool TryAddMember(GuiHandbookItemStackPage page)
            {
                if (page == null)
                {
                    return false;
                }

                if (!uniqueMembers.Add(page))
                {
                    return false;
                }

                Members.Add(page);

                if (page.PageNumber < SortHint)
                {
                    SortHint = page.PageNumber;
                }

                return true;
            }
        }

        private sealed class StoneVariantGroupBuilder
        {
            private readonly HashSet<GuiHandbookPage> uniqueMembers = new();

            internal StoneVariantGroupBuilder(string displayName, string normalizedName, string sanitizedName)
            {
                DisplayName = displayName;
                NormalizedName = normalizedName;
                SanitizedName = sanitizedName;
            }

            internal string DisplayName { get; private set; }

            internal string NormalizedName { get; }

            internal string SanitizedName { get; private set; }

            internal List<GuiHandbookItemStackPage> Members { get; } = new();

            internal int SortHint { get; private set; } = int.MaxValue;

            internal void UpdateDisplayName(string displayName)
            {
                if (!string.IsNullOrEmpty(displayName))
                {
                    DisplayName = displayName;
                }
            }

            internal void UpdateSanitizedName(string sanitizedName)
            {
                if (!string.IsNullOrEmpty(sanitizedName))
                {
                    SanitizedName = sanitizedName;
                }
            }

            internal bool TryAddMember(GuiHandbookItemStackPage page)
            {
                if (page == null)
                {
                    return false;
                }

                if (!uniqueMembers.Add(page))
                {
                    return false;
                }

                Members.Add(page);

                if (page.PageNumber < SortHint)
                {
                    SortHint = page.PageNumber;
                }

                return true;
            }
        }

        private sealed class CeramicVariantGroupBuilder
        {
            private readonly HashSet<GuiHandbookPage> uniqueMembers = new();

            internal CeramicVariantGroupBuilder(string displayName, string normalizedName, string sanitizedName)
            {
                DisplayName = displayName;
                NormalizedName = normalizedName;
                SanitizedName = sanitizedName;
            }

            internal string DisplayName { get; private set; }

            internal string NormalizedName { get; }

            internal string SanitizedName { get; private set; }

            internal List<GuiHandbookItemStackPage> Members { get; } = new();

            internal int SortHint { get; private set; } = int.MaxValue;

            internal void UpdateDisplayName(string displayName)
            {
                if (!string.IsNullOrEmpty(displayName))
                {
                    DisplayName = displayName;
                }
            }

            internal void UpdateSanitizedName(string sanitizedName)
            {
                if (!string.IsNullOrEmpty(sanitizedName))
                {
                    SanitizedName = sanitizedName;
                }
            }

            internal bool TryAddMember(GuiHandbookItemStackPage page)
            {
                if (page == null)
                {
                    return false;
                }

                if (!uniqueMembers.Add(page))
                {
                    return false;
                }

                Members.Add(page);

                if (page.PageNumber < SortHint)
                {
                    SortHint = page.PageNumber;
                }

                return true;
            }
        }

        private sealed class VariantGroupCreationInfo
        {
            internal VariantGroupCreationInfo(
                string displayName,
                string sanitizedName,
                List<GuiHandbookItemStackPage> members,
                int sortHint,
                string hiddenCodePrefix,
                string pageCodePrefix)
            {
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? DefaultGroupName : displayName;
                SanitizedName = string.IsNullOrWhiteSpace(sanitizedName) ? DefaultGroupName.ToLowerInvariant() : sanitizedName;
                Members = members ?? new List<GuiHandbookItemStackPage>();
                SortHint = sortHint;
                HiddenCodePrefix = hiddenCodePrefix;
                PageCodePrefix = pageCodePrefix;
            }

            internal string DisplayName { get; }

            internal string SanitizedName { get; }

            internal List<GuiHandbookItemStackPage> Members { get; }

            internal int SortHint { get; }

            internal string HiddenCodePrefix { get; }

            internal string PageCodePrefix { get; }
        }

        private static void UpdateWoodVariantPageVisibility(IEnumerable<GuiHandbookPage> pages)
        {
            EnsureWoodVariantsLoaded();
            EnsureStoneVariantsLoaded();
            EnsureCeramicVariantsLoaded();

            woodVariantPagesByKey.Clear();
            woodVariantGroupsByKey.Clear();
            woodVariantGroupAliases.Clear();
            stoneVariantPagesByKey.Clear();
            stoneVariantGroupsByKey.Clear();
            stoneVariantGroupAliases.Clear();
            ceramicVariantGroupsByKey.Clear();
            ceramicVariantGroupAliases.Clear();

            if (pages != null)
            {
                foreach (GuiHandbookPage page in pages)
                {
                    if (page is not GuiHandbookItemStackPage stackPage || page.IsDuplicate)
                    {
                        continue;
                    }

                    if (!IsGridRecipePage(stackPage))
                    {
                        continue;
                    }

                    CollectStoneVariantEntry(stackPage);
                    CollectCeramicVariantEntry(stackPage);

                    if (!TryGetWoodVariantInfo(stackPage.Stack, out WoodVariantInfo info) || !info.HasValue)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(info.NormalizedValue) && !woodVariantDisplayNameByCode.ContainsKey(info.NormalizedValue))
                    {
                        woodVariantDisplayNameByCode[info.NormalizedValue] = BuildWoodVariantDisplayName(info.VariantValue);
                    }

                    string itemCode = GetItemCodeForStack(stackPage.Stack);
                    string pageCode = GetEffectivePageCode(stackPage);
                    if (ShouldIgnoreWoodVariantEntry(itemCode, pageCode))
                    {
                        continue;
                    }

                    string pageTitle = GetWoodVariantReportTitle(stackPage);

                    RegisterWoodVariantGroupCandidate(stackPage, info, pageTitle);

                    WoodVariantReportKey key = new(info.NormalizedValue, pageCode, itemCode);
                    woodVariantPagesByKey[key] = new WoodPageReportEntry(info.NormalizedValue, pageTitle, itemCode, pageCode);
                }
            }

            SaveWoodVariantReport();
            SaveStoneVariantReport();
        }

        private static void CollectStoneVariantEntry(GuiHandbookItemStackPage page)
        {
            if (page?.Stack == null)
            {
                return;
            }

            if (IsToolCollectible(page.Stack))
            {
                return;
            }

            if (!TryGetStoneVariantInfo(page.Stack, out StoneVariantInfo info) || !info.HasValue)
            {
                return;
            }

            string normalized = info.NormalizedValue;
            if (string.IsNullOrEmpty(normalized) && !string.IsNullOrEmpty(info.Value))
            {
                normalized = NormalizeStoneName(info.Value);
            }

            if (string.IsNullOrEmpty(normalized))
            {
                return;
            }

            if (!stoneVariantDisplayNameByCode.ContainsKey(normalized))
            {
                string displayName = BuildStoneVariantDisplayName(info.Value) ?? BuildStoneVariantDisplayName(normalized);
                if (!string.IsNullOrEmpty(displayName))
                {
                    stoneVariantDisplayNameByCode[normalized] = displayName;
                }
            }

            string itemCode = GetItemCodeForStack(page.Stack);
            string pageCode = GetEffectivePageCode(page);
            if (ShouldIgnoreStoneVariantEntry(itemCode, pageCode))
            {
                return;
            }

            string pageTitle = GetWoodVariantReportTitle(page);
            string display = GetStoneVariantDisplayName(normalized);

            RegisterStoneVariantGroupCandidate(page, info, pageTitle, display);

            StoneVariantReportKey key = new(normalized, pageCode, itemCode);
            stoneVariantPagesByKey[key] = new StonePageReportEntry(normalized, display, pageTitle, itemCode, pageCode);
        }

        private static void CollectCeramicVariantEntry(GuiHandbookItemStackPage page)
        {
            if (page?.Stack == null)
            {
                return;
            }

            if (!TryGetCeramicVariantInfo(page.Stack, out CeramicVariantInfo info) || !info.HasValue)
            {
                return;
            }

            string itemCode = GetItemCodeForStack(page.Stack);
            string pageCode = GetEffectivePageCode(page);
            if (ShouldIgnoreCeramicVariantEntry(itemCode, pageCode))
            {
                return;
            }

            string pageTitle = GetWoodVariantReportTitle(page);
            RegisterCeramicVariantGroupCandidate(page, info, pageTitle);
        }

        private static bool ShouldIgnoreWoodVariantEntry(string itemCode, string pageCode)
        {
            if (HasIgnoredWoodVariantPrefix(itemCode))
            {
                return true;
            }

            if (HasIgnoredWoodVariantPrefix(pageCode))
            {
                return true;
            }

            string normalizedPageCode = NormalizePageCode(pageCode);
            if (!string.IsNullOrEmpty(normalizedPageCode))
            {
                string codename = ExtractCodenameFromPageCode(normalizedPageCode);
                if (HasIgnoredWoodVariantPrefix(codename))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasIgnoredWoodVariantPrefix(string value)
        {
            return HasIgnoredVariantPrefix(value, woodVariantIgnoredPrefixes);
        }

        private static bool ShouldIgnoreStoneVariantEntry(string itemCode, string pageCode)
        {
            if (HasIgnoredStoneVariantPrefix(itemCode))
            {
                return true;
            }

            if (HasIgnoredStoneVariantPrefix(pageCode))
            {
                return true;
            }

            string normalizedPageCode = NormalizePageCode(pageCode);
            if (!string.IsNullOrEmpty(normalizedPageCode))
            {
                string codename = ExtractCodenameFromPageCode(normalizedPageCode);
                if (HasIgnoredStoneVariantPrefix(codename))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasIgnoredStoneVariantPrefix(string value)
        {
            return HasIgnoredVariantPrefix(value, stoneVariantIgnoredPrefixes);
        }

        private static bool ShouldIgnoreCeramicVariantEntry(string itemCode, string pageCode)
        {
            if (HasIgnoredCeramicVariantPrefix(itemCode))
            {
                return true;
            }

            if (HasIgnoredCeramicVariantPrefix(pageCode))
            {
                return true;
            }

            string normalizedPageCode = NormalizePageCode(pageCode);
            if (!string.IsNullOrEmpty(normalizedPageCode))
            {
                string codename = ExtractCodenameFromPageCode(normalizedPageCode);
                if (HasIgnoredCeramicVariantPrefix(codename))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasIgnoredCeramicVariantPrefix(string value)
        {
            return HasIgnoredVariantPrefix(value, ceramicVariantIgnoredPrefixes);
        }

        private static bool IsToolCollectible(ItemStack stack)
        {
            CollectibleObject collectible = stack?.Collectible;
            if (collectible == null)
            {
                return false;
            }

            EnumTool? tool = collectible.Tool;
            return tool.HasValue;
        }

        private static bool HasIgnoredVariantPrefix(string value, string[] prefixes)
        {
            if (string.IsNullOrWhiteSpace(value) || prefixes == null || prefixes.Length == 0)
            {
                return false;
            }

            string trimmed = value.Trim();

            if (StartsWithIgnoredPrefix(trimmed, prefixes))
            {
                return true;
            }

            int colonIndex = trimmed.IndexOf(':');
            if (colonIndex >= 0 && colonIndex < trimmed.Length - 1)
            {
                string withoutDomain = trimmed.Substring(colonIndex + 1).TrimStart();
                if (StartsWithIgnoredPrefix(withoutDomain, prefixes))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool StartsWithIgnoredPrefix(string value, string[] prefixes)
        {
            if (string.IsNullOrEmpty(value) || prefixes == null)
            {
                return false;
            }

            foreach (string prefix in prefixes)
            {
                if (!string.IsNullOrEmpty(prefix) && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RegisterWoodVariantGroupCandidate(
            GuiHandbookItemStackPage page,
            WoodVariantInfo info,
            string pageTitle)
        {
            if (page == null || !info.HasValue)
            {
                return;
            }

            string woodDisplayName = null;
            if (!string.IsNullOrEmpty(info.NormalizedValue))
            {
                woodVariantDisplayNameByCode.TryGetValue(info.NormalizedValue, out woodDisplayName);
            }

            woodDisplayName ??= BuildWoodVariantDisplayName(info.VariantValue);

            string baseTitle = ExtractVariantGroupBaseName(pageTitle, woodDisplayName, woodVariantTrailingDescriptors, IsWoodVariantValue);
            if (string.IsNullOrWhiteSpace(baseTitle))
            {
                return;
            }

            string aliasKey = GetVariantGroupAlias(page);
            string normalizedName = NormalizeTitle(baseTitle);

            if (!string.IsNullOrEmpty(aliasKey)
                && woodVariantGroupAliases.TryGetValue(aliasKey, out string existingGroupKey)
                && !string.IsNullOrEmpty(existingGroupKey))
            {
                normalizedName = existingGroupKey;
            }

            if (string.IsNullOrEmpty(normalizedName))
            {
                normalizedName = aliasKey;
            }

            if (string.IsNullOrEmpty(normalizedName))
            {
                return;
            }

            string displayName = FormatWoodGroupDisplayName(baseTitle);
            if (string.IsNullOrEmpty(displayName))
            {
                return;
            }

            string sanitized = Sanitize(displayName);
            if (string.IsNullOrEmpty(sanitized))
            {
                sanitized = Sanitize(baseTitle);
            }

            if (string.IsNullOrEmpty(sanitized))
            {
                sanitized = "woodvariant";
            }

            if (!woodVariantGroupsByKey.TryGetValue(normalizedName, out WoodVariantGroupBuilder builder))
            {
                builder = new WoodVariantGroupBuilder(displayName, normalizedName, sanitized);
                woodVariantGroupsByKey[normalizedName] = builder;
            }
            else
            {
                builder.UpdateDisplayName(displayName);
                builder.UpdateSanitizedName(sanitized);
            }

            builder.TryAddMember(page);

            if (!string.IsNullOrEmpty(aliasKey))
            {
                woodVariantGroupAliases[aliasKey] = builder.NormalizedName;
            }
        }

        private static void RegisterStoneVariantGroupCandidate(
            GuiHandbookItemStackPage page,
            StoneVariantInfo info,
            string pageTitle,
            string stoneDisplayName)
        {
            if (page == null || !info.HasValue)
            {
                return;
            }

            string displayName = stoneDisplayName;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                if (!string.IsNullOrEmpty(info.NormalizedValue))
                {
                    displayName = GetStoneVariantDisplayName(info.NormalizedValue);
                }
                else if (!string.IsNullOrEmpty(info.Value))
                {
                    displayName = BuildStoneVariantDisplayName(info.Value);
                }
            }

            string baseTitle = ExtractVariantGroupBaseName(pageTitle, displayName, stoneVariantTrailingDescriptors, IsStoneVariantValue);
            if (string.IsNullOrWhiteSpace(baseTitle))
            {
                return;
            }

            string aliasKey = GetVariantGroupAlias(page);
            string normalizedName = NormalizeTitle(baseTitle);

            if (!string.IsNullOrEmpty(aliasKey)
                && stoneVariantGroupAliases.TryGetValue(aliasKey, out string existingGroupKey)
                && !string.IsNullOrEmpty(existingGroupKey))
            {
                normalizedName = existingGroupKey;
            }

            if (string.IsNullOrEmpty(normalizedName))
            {
                normalizedName = aliasKey;
            }

            if (string.IsNullOrEmpty(normalizedName))
            {
                return;
            }

            string formattedDisplayName = FormatStoneGroupDisplayName(baseTitle);
            if (string.IsNullOrEmpty(formattedDisplayName))
            {
                return;
            }

            string sanitized = Sanitize(formattedDisplayName);
            if (string.IsNullOrEmpty(sanitized))
            {
                sanitized = Sanitize(baseTitle);
            }

            if (string.IsNullOrEmpty(sanitized))
            {
                sanitized = "stonevariant";
            }

            if (!stoneVariantGroupsByKey.TryGetValue(normalizedName, out StoneVariantGroupBuilder builder))
            {
                builder = new StoneVariantGroupBuilder(formattedDisplayName, normalizedName, sanitized);
                stoneVariantGroupsByKey[normalizedName] = builder;
            }
            else
            {
                builder.UpdateDisplayName(formattedDisplayName);
                builder.UpdateSanitizedName(sanitized);
            }

            builder.TryAddMember(page);

            if (!string.IsNullOrEmpty(aliasKey))
            {
                stoneVariantGroupAliases[aliasKey] = builder.NormalizedName;
            }
        }

        private static void RegisterCeramicVariantGroupCandidate(
            GuiHandbookItemStackPage page,
            CeramicVariantInfo info,
            string pageTitle)
        {
            if (page == null || !info.HasValue)
            {
                return;
            }

            string variantDisplayName = string.IsNullOrWhiteSpace(info.DisplayName)
                ? GetCeramicVariantDisplayName(info.NormalizedValue)
                : info.DisplayName;

            string baseTitle = ExtractVariantGroupBaseName(pageTitle, variantDisplayName, ceramicVariantTrailingDescriptors, IsCeramicVariantValue);
            if (string.IsNullOrWhiteSpace(baseTitle))
            {
                baseTitle = pageTitle;
            }

            if (string.IsNullOrWhiteSpace(baseTitle))
            {
                return;
            }

            string aliasKey = GetVariantGroupAlias(page);
            string normalizedName = NormalizeTitle(baseTitle);

            if (!string.IsNullOrEmpty(aliasKey)
                && ceramicVariantGroupAliases.TryGetValue(aliasKey, out string existingGroupKey)
                && !string.IsNullOrEmpty(existingGroupKey))
            {
                normalizedName = existingGroupKey;
            }

            if (string.IsNullOrEmpty(normalizedName))
            {
                normalizedName = aliasKey;
            }

            if (string.IsNullOrEmpty(normalizedName))
            {
                return;
            }

            string formattedDisplayName = FormatCeramicGroupDisplayName(baseTitle);
            if (string.IsNullOrEmpty(formattedDisplayName))
            {
                return;
            }

            string sanitized = Sanitize(formattedDisplayName);
            if (string.IsNullOrEmpty(sanitized))
            {
                sanitized = Sanitize(baseTitle);
            }

            if (string.IsNullOrEmpty(sanitized))
            {
                sanitized = "ceramicvariant";
            }

            if (!ceramicVariantGroupsByKey.TryGetValue(normalizedName, out CeramicVariantGroupBuilder builder))
            {
                builder = new CeramicVariantGroupBuilder(formattedDisplayName, normalizedName, sanitized);
                ceramicVariantGroupsByKey[normalizedName] = builder;
            }
            else
            {
                builder.UpdateDisplayName(formattedDisplayName);
                builder.UpdateSanitizedName(sanitized);
            }

            builder.TryAddMember(page);

            if (!string.IsNullOrEmpty(aliasKey))
            {
                ceramicVariantGroupAliases[aliasKey] = builder.NormalizedName;
            }
        }

        private static string GetVariantGroupAlias(GuiHandbookItemStackPage page)
        {
            if (page == null)
            {
                return string.Empty;
            }

            string alias = GetVariantGroupAlias(page.Stack);
            if (!string.IsNullOrEmpty(alias))
            {
                return alias;
            }

            string effectivePageCode = GetEffectivePageCode(page);
            string normalizedPageCode = NormalizePageCode(effectivePageCode);
            if (!string.IsNullOrEmpty(normalizedPageCode))
            {
                alias = GetVariantGroupAliasFromCode(normalizedPageCode);
                if (!string.IsNullOrEmpty(alias))
                {
                    return alias;
                }

                string codename = ExtractCodenameFromPageCode(normalizedPageCode);
                alias = GetVariantGroupAliasFromCode(codename);
                if (!string.IsNullOrEmpty(alias))
                {
                    return alias;
                }
            }

            return string.Empty;
        }

        private static string GetVariantGroupAlias(ItemStack stack)
        {
            CollectibleObject collectible = stack?.Collectible;
            AssetLocation code = collectible?.Code;
            if (code == null)
            {
                return string.Empty;
            }

            string path = code.Path;
            if (!string.IsNullOrWhiteSpace(path))
            {
                string alias = NormalizeVariantAlias(path);
                if (!string.IsNullOrEmpty(alias))
                {
                    return alias;
                }
            }

            return GetVariantGroupAliasFromCode(code.ToString());
        }

        private static string GetVariantGroupAliasFromCode(string code)
        {
            string path = ExtractVariantPathComponent(code);
            return NormalizeVariantAlias(path);
        }

        private static string ExtractVariantPathComponent(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            int colonIndex = trimmed.IndexOf(':');
            if (colonIndex >= 0)
            {
                trimmed = colonIndex < trimmed.Length - 1
                    ? trimmed[(colonIndex + 1)..]
                    : string.Empty;
            }

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return string.Empty;
            }

            int attributeIndex = trimmed.IndexOf("-{", StringComparison.Ordinal);
            if (attributeIndex > 0)
            {
                trimmed = trimmed.Substring(0, attributeIndex);
            }

            return trimmed.Trim();
        }

        private static string NormalizeVariantAlias(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return string.Empty;
            }

            string prepared = ExtractVariantPathComponent(alias);
            string valueToUse = string.IsNullOrEmpty(prepared) ? alias : prepared;

            string trimmed = valueToUse.Trim().ToLowerInvariant();
            if (trimmed.Length == 0)
            {
                return string.Empty;
            }

            bool removed;
            do
            {
                removed = false;

                foreach (string prefix in variantAliasPrefixesToStrip)
                {
                    if (string.IsNullOrEmpty(prefix))
                    {
                        continue;
                    }

                    if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        string candidate = trimmed.Substring(prefix.Length).Trim();
                        if (!string.IsNullOrWhiteSpace(candidate))
                        {
                            trimmed = candidate;
                            removed = true;
                        }
                    }
                }
            }
            while (removed);

            return trimmed;
        }

        private static string ExtractVariantGroupBaseName(
            string pageTitle,
            string variantDisplayName,
            string[] trailingDescriptors = null,
            System.Func<string, bool> isVariantValue = null)
        {
            string trimmedTitle = string.IsNullOrWhiteSpace(pageTitle) ? string.Empty : pageTitle.Trim();
            if (string.IsNullOrEmpty(trimmedTitle))
            {
                return string.Empty;
            }

            string trimmedVariant = string.IsNullOrWhiteSpace(variantDisplayName) ? string.Empty : variantDisplayName.Trim();
            if (string.IsNullOrEmpty(trimmedVariant))
            {
                return FinalizeVariantGroupBaseName(trimmedTitle, trailingDescriptors, false, isVariantValue);
            }

            if (trimmedTitle.EndsWith(trimmedVariant, StringComparison.OrdinalIgnoreCase))
            {
                int removalStartIndex = trimmedTitle.Length - trimmedVariant.Length;
                string candidate = RemoveVariantAndConnectedWord(trimmedTitle, removalStartIndex, trimmedVariant.Length);
                string finalized = FinalizeVariantGroupBaseName(candidate, trailingDescriptors, true, isVariantValue);
                if (!string.IsNullOrEmpty(finalized))
                {
                    return finalized;
                }
            }

            int index = trimmedTitle.LastIndexOf(trimmedVariant, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                int endIndex = index + trimmedVariant.Length;
                bool leftSeparator = index == 0
                    || char.IsWhiteSpace(trimmedTitle[index - 1])
                    || trimmedTitle[index - 1] == '-'
                    || trimmedTitle[index - 1] == '('
                    || trimmedTitle[index - 1] == '['
                    || trimmedTitle[index - 1] == '{';
                bool rightSeparator = endIndex >= trimmedTitle.Length
                    || char.IsWhiteSpace(trimmedTitle[endIndex])
                    || trimmedTitle[endIndex] == ')'
                    || trimmedTitle[endIndex] == ']'
                    || trimmedTitle[endIndex] == '}'
                    || trimmedTitle[endIndex] == '-';

                if (leftSeparator && rightSeparator)
                {
                    string candidate = RemoveVariantAndConnectedWord(trimmedTitle, index, trimmedVariant.Length);
                    string finalized = FinalizeVariantGroupBaseName(candidate, trailingDescriptors, true, isVariantValue);
                    if (!string.IsNullOrEmpty(finalized))
                    {
                        return finalized;
                    }
                }
            }

            string normalizedVariant = NormalizeMaterialName(trimmedVariant);
            if (!string.IsNullOrEmpty(normalizedVariant))
            {
                string normalizedTitle = NormalizeMaterialNameWithIndexMap(trimmedTitle, out List<int> normalizedIndexMap);
                if (!string.IsNullOrEmpty(normalizedTitle)
                    && normalizedIndexMap != null
                    && normalizedIndexMap.Count >= normalizedTitle.Length + 1)
                {
                    int normalizedIndex = normalizedTitle.LastIndexOf(normalizedVariant, StringComparison.OrdinalIgnoreCase);
                    if (normalizedIndex >= 0 && normalizedIndex < normalizedIndexMap.Count - 1)
                    {
                        int normalizedEnd = normalizedIndex + normalizedVariant.Length;
                        if (normalizedEnd <= normalizedTitle.Length && normalizedEnd < normalizedIndexMap.Count)
                        {
                            int startIndex = normalizedIndexMap[normalizedIndex];
                            int endIndex = normalizedIndexMap[normalizedEnd];
                            if (startIndex >= 0 && endIndex >= startIndex && endIndex <= trimmedTitle.Length)
                            {
                                string candidate = RemoveVariantAndConnectedWord(trimmedTitle, startIndex, endIndex - startIndex);
                                string finalized = FinalizeVariantGroupBaseName(candidate, trailingDescriptors, true, isVariantValue);
                                if (!string.IsNullOrEmpty(finalized))
                                {
                                    return finalized;
                                }
                            }
                        }
                    }
                }
            }

            return FinalizeVariantGroupBaseName(trimmedTitle, trailingDescriptors, false, isVariantValue);
        }

        private static string FinalizeVariantGroupBaseName(
            string baseName,
            string[] trailingDescriptors,
            bool variantRemoved,
            System.Func<string, bool> isVariantValue)
        {
            string trimmed = string.IsNullOrWhiteSpace(baseName) ? string.Empty : TrimTrailingSeparators(baseName).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return string.Empty;
            }

            string withoutTrailing = RemoveTrailingVariantDescriptors(trimmed, trailingDescriptors, variantRemoved, isVariantValue);
            if (string.IsNullOrWhiteSpace(withoutTrailing))
            {
                withoutTrailing = trimmed;
            }

            return CollapseSpaces(withoutTrailing);
        }

        private static string RemoveTrailingVariantDescriptors(
            string value,
            string[] descriptors,
            bool variantRemoved,
            System.Func<string, bool> isVariantValue)
        {
            if (string.IsNullOrWhiteSpace(value) || descriptors == null || descriptors.Length == 0)
            {
                return value;
            }

            string working = value;
            bool removed;

            do
            {
                removed = false;
                string trimmedWorking = TrimTrailingSeparators(working).TrimEnd();

                foreach (string descriptor in descriptors)
                {
                    if (string.IsNullOrWhiteSpace(descriptor))
                    {
                        continue;
                    }

                    string trimmedDescriptor = descriptor.Trim();
                    if (trimmedDescriptor.Length == 0 || trimmedDescriptor.Length > trimmedWorking.Length)
                    {
                        continue;
                    }

                    if (!trimmedWorking.EndsWith(trimmedDescriptor, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    int descriptorStart = trimmedWorking.Length - trimmedDescriptor.Length;
                    if (descriptorStart <= 0)
                    {
                        continue;
                    }

                    char preceding = trimmedWorking[descriptorStart - 1];
                    if (!char.IsWhiteSpace(preceding) && !IsHyphen(preceding) && !IsOpeningBracket(preceding))
                    {
                        continue;
                    }

                    if (!variantRemoved && isVariantValue != null)
                    {
                        string precedingWord = ExtractTrailingWord(trimmedWorking, descriptorStart);
                        if (string.IsNullOrEmpty(precedingWord) || !isVariantValue(precedingWord))
                        {
                            continue;
                        }
                    }
                    else if (!variantRemoved && isVariantValue == null)
                    {
                        continue;
                    }

                    int removalStart = descriptorStart;
                    while (removalStart > 0 && char.IsWhiteSpace(trimmedWorking[removalStart - 1]))
                    {
                        removalStart--;
                    }

                    if (removalStart > 0 && IsHyphen(trimmedWorking[removalStart - 1]))
                    {
                        removalStart--;
                        while (removalStart > 0 && char.IsWhiteSpace(trimmedWorking[removalStart - 1]))
                        {
                            removalStart--;
                        }
                    }

                    string candidate = removalStart <= 0 ? string.Empty : trimmedWorking.Substring(0, removalStart);
                    string trimmedCandidate = TrimTrailingSeparators(candidate).Trim();
                    if (!string.IsNullOrEmpty(trimmedCandidate))
                    {
                        working = trimmedCandidate;
                        removed = true;
                    }

                    break;
                }

                if (!removed)
                {
                    working = TrimTrailingSeparators(working).Trim();
                }
            }
            while (removed);

            return working;
        }

        private static string ExtractTrailingWord(string value, int endIndex)
        {
            if (string.IsNullOrEmpty(value) || endIndex <= 0)
            {
                return string.Empty;
            }

            int index = endIndex - 1;
            while (index >= 0 && (char.IsWhiteSpace(value[index]) || IsClosingBracket(value[index]) || value[index] == ','))
            {
                index--;
            }

            if (index < 0)
            {
                return string.Empty;
            }

            int wordEnd = index + 1;

            while (index >= 0)
            {
                char ch = value[index];
                if (char.IsWhiteSpace(ch) || IsHyphen(ch) || IsOpeningBracket(ch) || IsClosingBracket(ch) || ch == ',')
                {
                    break;
                }

                index--;
            }

            int wordStart = index + 1;
            if (wordStart >= wordEnd)
            {
                return string.Empty;
            }

            string word = value.Substring(wordStart, wordEnd - wordStart);
            return string.IsNullOrWhiteSpace(word) ? string.Empty : word.Trim();
        }

        private static string TrimTrailingSeparators(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            int end = value.Length;
            while (end > 0)
            {
                char ch = value[end - 1];
                if (!char.IsWhiteSpace(ch) && ch != '-' && ch != '–' && ch != '—' && ch != '(' && ch != '[' && ch != '{')
                {
                    break;
                }

                end--;
            }

            return end <= 0 ? string.Empty : value.Substring(0, end);
        }

        private static string RemoveVariantAndConnectedWord(string source, int startIndex, int length)
        {
            if (string.IsNullOrEmpty(source) || length <= 0)
            {
                return source;
            }

            int originalStart = Math.Max(0, Math.Min(startIndex, source.Length));
            int originalEnd = Math.Max(originalStart, Math.Min(source.Length, startIndex + length));

            int adjustedStart = originalStart;
            int adjustedEnd = originalEnd;

            if (adjustedStart > 0 && IsHyphen(source[adjustedStart - 1]))
            {
                int wordStart = adjustedStart - 1;
                while (wordStart > 0)
                {
                    char preceding = source[wordStart - 1];
                    if (char.IsWhiteSpace(preceding) || IsOpeningBracket(preceding) || IsHyphen(preceding))
                    {
                        break;
                    }

                    wordStart--;
                }

                adjustedStart = wordStart;
            }

            if (adjustedEnd < source.Length && IsHyphen(source[adjustedEnd]))
            {
                int wordEnd = adjustedEnd + 1;
                while (wordEnd < source.Length)
                {
                    char following = source[wordEnd];
                    if (char.IsWhiteSpace(following) || IsClosingBracket(following) || IsHyphen(following))
                    {
                        break;
                    }

                    wordEnd++;
                }

                adjustedEnd = wordEnd;
            }

            if (adjustedEnd <= adjustedStart)
            {
                return source;
            }

            return source.Remove(adjustedStart, adjustedEnd - adjustedStart);
        }

        private static bool IsHyphen(char ch)
        {
            return ch == '-' || ch == '–' || ch == '—';
        }

        private static bool IsOpeningBracket(char ch)
        {
            return ch == '(' || ch == '[' || ch == '{';
        }

        private static bool IsClosingBracket(char ch)
        {
            return ch == ')' || ch == ']' || ch == '}';
        }

        private static string CollapseSpaces(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new(value.Length);
            bool previousWasSpace = false;

            foreach (char ch in value)
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (!previousWasSpace)
                    {
                        builder.Append(' ');
                        previousWasSpace = true;
                    }
                }
                else
                {
                    builder.Append(ch);
                    previousWasSpace = false;
                }
            }

            return builder.ToString().Trim();
        }

        private static string FormatWoodGroupDisplayName(string baseName)
        {
            string collapsed = CollapseSpaces(baseName);
            if (string.IsNullOrEmpty(collapsed))
            {
                return string.Empty;
            }

            if (char.IsLetter(collapsed[0]))
            {
                if (collapsed.Length == 1)
                {
                    return collapsed.ToUpperInvariant();
                }

                char first = char.ToUpper(collapsed[0], CultureInfo.InvariantCulture);
                return first + collapsed.Substring(1);
            }

            return collapsed;
        }

        private static string FormatStoneGroupDisplayName(string baseName)
        {
            string collapsed = CollapseSpaces(baseName);
            if (string.IsNullOrEmpty(collapsed))
            {
                return string.Empty;
            }

            string trimmed = collapsed.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return string.Empty;
            }

            if (trimmed.Length == 1)
            {
                return trimmed.ToUpperInvariant();
            }

            char first = trimmed[0];
            char upper = char.ToUpper(first, CultureInfo.InvariantCulture);
            return upper + trimmed.Substring(1);
        }

        private static string FormatCeramicGroupDisplayName(string baseName)
        {
            string collapsed = CollapseSpaces(baseName);
            if (string.IsNullOrEmpty(collapsed))
            {
                return string.Empty;
            }

            string trimmed = collapsed.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return string.Empty;
            }

            string replaced;
            if (trimmed.StartsWith("Ceramic", StringComparison.OrdinalIgnoreCase))
            {
                replaced = trimmed;
            }
            else if (trimmed.StartsWith("Clay", StringComparison.OrdinalIgnoreCase))
            {
                replaced = string.Concat("Ceramic", trimmed.AsSpan(4));
            }
            else
            {
                replaced = string.Concat("Ceramic ", trimmed);
            }

            string lower = replaced.ToLowerInvariant();
            if (lower.Length == 0)
            {
                return string.Empty;
            }

            if (lower.Length == 1)
            {
                return lower.ToUpperInvariant();
            }

            char first = char.ToUpper(lower[0], CultureInfo.InvariantCulture);
            return first + lower[1..];
        }

        private static bool TryGetWoodVariantInfo(ItemStack stack, out WoodVariantInfo info)
        {
            if (stack?.Collectible == null)
            {
                info = default;
                return false;
            }

            EnsureWoodVariantsLoaded();

            CollectibleObject collectible = stack.Collectible;
            HashSet<string> attributeWoodVariants = LoadWoodVariantGroupNames(collectible);
            RelaxedReadOnlyDictionary<string, string> variants = collectible.Variant;

            if (variants != null)
            {
                string explicitWood = variants["wood"];
                if (!string.IsNullOrEmpty(explicitWood))
                {
                    RegisterKnownWoodName(explicitWood);
                    info = new WoodVariantInfo("wood", explicitWood, NormalizeWoodName(explicitWood));
                    return info.HasValue;
                }

                string typeVariant = variants["type"];
                if (!string.IsNullOrEmpty(typeVariant))
                {
                    string normalizedType = NormalizeWoodName(typeVariant);
                    bool matchesWoodGroup = ContainsWoodVariant(attributeWoodVariants, normalizedType);

                    if (IsWoodVariantValue(typeVariant) || matchesWoodGroup)
                    {
                        RegisterKnownWoodName(typeVariant);
                        info = new WoodVariantInfo("type", typeVariant, normalizedType);
                        return info.HasValue;
                    }
                }

                foreach (KeyValuePair<string, string> entry in variants)
                {
                    string key = entry.Key;
                    string value = entry.Value;
                    if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                    {
                        continue;
                    }

                    string normalized = NormalizeWoodName(value);
                    bool keyIndicatesWood = key.IndexOf("wood", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (keyIndicatesWood || ContainsWoodVariant(attributeWoodVariants, normalized) || IsWoodVariantValue(value))
                    {
                        RegisterKnownWoodName(value);
                        info = new WoodVariantInfo(key, value, normalized);
                        return info.HasValue;
                    }
                }
            }

            string codePath = collectible.Code?.Path;
            string woodFromCode = FindWoodNameInCode(codePath);
            if (!string.IsNullOrEmpty(woodFromCode))
            {
                RegisterKnownWoodName(woodFromCode);
                info = new WoodVariantInfo(null, woodFromCode, NormalizeWoodName(woodFromCode));
                return info.HasValue;
            }

            string pageCode = GuiHandbookItemStackPage.PageCodeForStack(stack);
            woodFromCode = FindWoodNameInCode(pageCode);
            if (!string.IsNullOrEmpty(woodFromCode))
            {
                RegisterKnownWoodName(woodFromCode);
                info = new WoodVariantInfo(null, woodFromCode, NormalizeWoodName(woodFromCode));
                return info.HasValue;
            }

            info = default;
            return false;
        }

        private static HashSet<string> LoadWoodVariantGroupNames(CollectibleObject collectible)
        {
            JsonObject attributes = collectible?.Attributes;
            if (attributes == null)
            {
                return null;
            }

            JsonObject variantGroupsObject;
            try
            {
                variantGroupsObject = attributes["variantGroups"];
            }
            catch
            {
                return null;
            }

            if (variantGroupsObject == null || variantGroupsObject.Token == null || !variantGroupsObject.Token.HasValues)
            {
                return null;
            }

            Dictionary<string, string[]> variantGroups;
            try
            {
                variantGroups = variantGroupsObject.AsObject<Dictionary<string, string[]>>();
            }
            catch
            {
                return null;
            }

            if (variantGroups == null || variantGroups.Count == 0)
            {
                return null;
            }

            HashSet<string> woodVariants = null;

            foreach (KeyValuePair<string, string[]> group in variantGroups)
            {
                if (!IsWoodVariantGroupKey(group.Key))
                {
                    continue;
                }

                string[] values = group.Value;
                if (values == null || values.Length == 0)
                {
                    continue;
                }

                woodVariants ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (string value in values)
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    RegisterKnownWoodName(value);

                    string normalized = NormalizeWoodName(value);
                    if (!string.IsNullOrEmpty(normalized))
                    {
                        woodVariants.Add(normalized);
                    }
                }
            }

            return woodVariants;
        }

        private static bool ContainsWoodVariant(HashSet<string> attributeWoodVariants, string normalizedVariant)
        {
            return attributeWoodVariants != null
                && !string.IsNullOrEmpty(normalizedVariant)
                && attributeWoodVariants.Contains(normalizedVariant);
        }

        private static bool IsWoodVariantGroupKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return key.IndexOf("wood", StringComparison.OrdinalIgnoreCase) >= 0
                || key.IndexOf("timber", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FindWoodNameInCode(string code)
        {
            if (string.IsNullOrEmpty(code) || knownWoodVariantNames.Count == 0)
            {
                return null;
            }

            foreach (string wood in knownWoodVariantNames)
            {
                if (CodeContainsWoodToken(code, wood))
                {
                    return wood;
                }
            }

            return null;
        }

        private static bool CodeContainsWoodToken(string value, string wood)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(wood))
            {
                return false;
            }

            foreach (string token in EnumerateCodeTokens(value))
            {
                if (token.Equals(wood, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> EnumerateCodeTokens(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                yield break;
            }

            int start = -1;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetterOrDigit(c))
                {
                    if (start == -1)
                    {
                        start = i;
                    }

                    continue;
                }

                if (start != -1)
                {
                    yield return value.Substring(start, i - start);
                    start = -1;
                }
            }

            if (start != -1)
            {
                yield return value.Substring(start);
            }
        }

        private static void EnsureWoodVariantsLoaded()
        {
            if (woodVariantsLoaded)
            {
                return;
            }

            woodVariantsLoaded = true;

            if (capi?.Assets == null)
            {
                return;
            }

            try
            {
                IAsset asset = capi.Assets.TryGet(WoodWorldPropertyCode);
                if (asset == null)
                {
                    return;
                }

                StandardWorldProperty property = asset.ToObject<StandardWorldProperty>();
                if (property?.Variants == null)
                {
                    return;
                }

                foreach (WorldPropertyVariant variant in property.Variants)
                {
                    string name = variant?.Code?.Path;
                    if (!string.IsNullOrEmpty(name))
                    {
                        RegisterKnownWoodName(name);
                    }
                }
            }
            catch (Exception ex)
            {
                capi?.Logger?.Warning("[Handbook Categories] Failed to load wood world property {0}: {1}", WoodWorldPropertyCode, ex);
            }
        }

        private static void RegisterKnownWoodName(string woodName)
        {
            string normalized = NormalizeWoodName(woodName);
            if (string.IsNullOrEmpty(normalized))
            {
                return;
            }

            bool added = knownWoodVariantNames.Add(normalized);
            string displayName = BuildWoodVariantDisplayName(woodName);

            if (!string.IsNullOrEmpty(displayName))
            {
                woodVariantDisplayNameByCode[normalized] = displayName;
            }
            else if (added && !woodVariantDisplayNameByCode.ContainsKey(normalized))
            {
                woodVariantDisplayNameByCode[normalized] = BuildWoodVariantDisplayName(normalized);
            }
        }

        private static string NormalizeWoodName(string woodName)
        {
            return NormalizeMaterialName(woodName);
        }

        private static string BuildWoodVariantDisplayName(string woodName)
        {
            if (string.IsNullOrWhiteSpace(woodName))
            {
                return null;
            }

            string trimmed = woodName.Trim();
            string spaced = trimmed.Replace('_', ' ').Replace('-', ' ');
            string lower = spaced.ToLowerInvariant();
            string titleCase = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(lower);

            return string.IsNullOrWhiteSpace(titleCase) ? trimmed : titleCase;
        }

        private static bool IsWoodVariantValue(string value)
        {
            string normalized = NormalizeWoodName(value);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            if (knownWoodVariantNames.Count == 0)
            {
                return true;
            }

            return knownWoodVariantNames.Contains(normalized);
        }

        private static void EnsureStoneVariantsLoaded()
        {
            if (stoneVariantsLoaded)
            {
                return;
            }

            stoneVariantsLoaded = true;

            if (capi?.Assets == null)
            {
                return;
            }

            try
            {
                IAsset asset = capi.Assets.TryGet(StoneWorldPropertyCode);
                if (asset == null)
                {
                    return;
                }

                StandardWorldProperty property = asset.ToObject<StandardWorldProperty>();
                if (property?.Variants == null)
                {
                    return;
                }

                foreach (WorldPropertyVariant variant in property.Variants)
                {
                    string name = variant?.Code?.Path;
                    if (!string.IsNullOrEmpty(name))
                    {
                        RegisterKnownStoneName(name);
                    }
                }
            }
            catch (Exception ex)
            {
                capi?.Logger?.Warning("[Handbook Categories] Failed to load stone world property {0}: {1}", StoneWorldPropertyCode, ex);
            }
        }

        private static void RegisterKnownStoneName(string stoneName)
        {
            string normalized = NormalizeStoneName(stoneName);
            if (string.IsNullOrEmpty(normalized))
            {
                return;
            }

            bool added = knownStoneVariantNames.Add(normalized);
            string displayName = BuildStoneVariantDisplayName(stoneName);

            if (!string.IsNullOrEmpty(displayName))
            {
                stoneVariantDisplayNameByCode[normalized] = displayName;
            }
            else if (added && !stoneVariantDisplayNameByCode.ContainsKey(normalized))
            {
                stoneVariantDisplayNameByCode[normalized] = BuildStoneVariantDisplayName(normalized);
            }
        }

        private static void EnsureCeramicVariantsLoaded()
        {
            if (ceramicVariantsLoaded)
            {
                return;
            }

            ceramicVariantsLoaded = true;

            foreach (string value in ceramicVariantColorMap.Values.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string normalized = NormalizeCeramicName(value);
                if (string.IsNullOrEmpty(normalized))
                {
                    continue;
                }

                if (!ceramicVariantDisplayNameByCode.ContainsKey(normalized))
                {
                    ceramicVariantDisplayNameByCode[normalized] = BuildCeramicVariantDisplayName(value);
                }
            }
        }

        private static string NormalizeStoneName(string stoneName)
        {
            return NormalizeMaterialName(stoneName);
        }

        private static string NormalizeCeramicName(string ceramicName)
        {
            return NormalizeMaterialName(ceramicName);
        }

        private static string NormalizeMaterialName(string materialName)
        {
            return NormalizeMaterialNameWithIndexMap(materialName, out _);
        }

        private static string NormalizeMaterialNameWithIndexMap(string materialName, out List<int> normalizedIndexMap)
        {
            normalizedIndexMap = null;
            if (string.IsNullOrWhiteSpace(materialName))
            {
                return null;
            }

            string trimmed = materialName.Trim();
            if (trimmed.Length == 0)
            {
                normalizedIndexMap = new List<int> { 0 };
                return null;
            }

            string lower = trimmed.ToLowerInvariant();
            StringBuilder builder = new(lower.Length);
            normalizedIndexMap = new List<int>(lower.Length + 1);

            for (int i = 0; i < lower.Length; i++)
            {
                char ch = lower[i];
                if (char.IsWhiteSpace(ch) || ch == '-' || ch == '_')
                {
                    continue;
                }

                builder.Append(ch);
                normalizedIndexMap.Add(i);
            }

            normalizedIndexMap.Add(lower.Length);

            return builder.Length == 0 ? null : builder.ToString();
        }

        private static string BuildStoneVariantDisplayName(string stoneName)
        {
            if (string.IsNullOrWhiteSpace(stoneName))
            {
                return null;
            }

            string trimmed = stoneName.Trim();
            string spaced = trimmed.Replace('_', ' ').Replace('-', ' ');
            string lower = spaced.ToLowerInvariant();
            string titleCase = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(lower);

            return string.IsNullOrWhiteSpace(titleCase) ? trimmed : titleCase;
        }

        private static string BuildCeramicVariantDisplayName(string ceramicName)
        {
            if (string.IsNullOrWhiteSpace(ceramicName))
            {
                return null;
            }

            string trimmed = ceramicName.Trim();
            string spaced = trimmed.Replace('_', ' ').Replace('-', ' ');
            string lower = spaced.ToLowerInvariant();
            string titleCase = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(lower);

            return string.IsNullOrWhiteSpace(titleCase) ? trimmed : titleCase;
        }

        private static string GetStoneVariantDisplayName(string normalized)
        {
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            if (stoneVariantDisplayNameByCode.TryGetValue(normalized, out string display) && !string.IsNullOrEmpty(display))
            {
                return display;
            }

            string fallback = BuildStoneVariantDisplayName(normalized);
            return string.IsNullOrEmpty(fallback) ? normalized : fallback;
        }

        private static string GetCeramicVariantDisplayName(string normalized)
        {
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            if (ceramicVariantDisplayNameByCode.TryGetValue(normalized, out string display) && !string.IsNullOrEmpty(display))
            {
                return display;
            }

            string fallback = BuildCeramicVariantDisplayName(normalized);
            if (!string.IsNullOrEmpty(fallback))
            {
                ceramicVariantDisplayNameByCode[normalized] = fallback;
                return fallback;
            }

            return normalized;
        }

        private static bool IsCeramicVariantValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (TryMapCeramicColor(value, out _))
            {
                return true;
            }

            string normalized = NormalizeCeramicName(value);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            if (ceramicVariantDisplayNameByCode.Count == 0)
            {
                return true;
            }

            return ceramicVariantDisplayNameByCode.ContainsKey(normalized);
        }

        private static bool IsStoneVariantValue(string value)
        {
            string normalized = NormalizeStoneName(value);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            if (knownStoneVariantNames.Count == 0)
            {
                return true;
            }

            return knownStoneVariantNames.Contains(normalized);
        }

        private static void ResetWoodVariantCache()
        {
            knownWoodVariantNames.Clear();
            woodVariantDisplayNameByCode.Clear();
            woodVariantPagesByKey.Clear();
            woodVariantGroupsByKey.Clear();
            woodVariantGroupAliases.Clear();
            woodVariantsLoaded = false;
        }

        private static void ResetStoneVariantCache()
        {
            knownStoneVariantNames.Clear();
            stoneVariantDisplayNameByCode.Clear();
            stoneVariantPagesByKey.Clear();
            stoneVariantGroupsByKey.Clear();
            stoneVariantGroupAliases.Clear();
            stoneVariantsLoaded = false;
        }

        private static void ResetCeramicVariantCache()
        {
            ceramicVariantDisplayNameByCode.Clear();
            ceramicVariantGroupsByKey.Clear();
            ceramicVariantGroupAliases.Clear();
            ceramicVariantsLoaded = false;
        }

        private static void SaveWoodVariantReport()
        {
            if (capi == null)
            {
                return;
            }

            string configDirectory;
            try
            {
                configDirectory = capi.GetOrCreateDataPath("ModConfig");
            }
            catch (Exception ex)
            {
                capi.Logger?.Warning("[Handbook Categories] Failed to access ModConfig directory: {0}", ex);
                return;
            }

            if (string.IsNullOrEmpty(configDirectory))
            {
                return;
            }

            string filePath = System.IO.Path.Combine(configDirectory, WoodVariantReportFileName);

            try
            {
                using StreamWriter writer = new(filePath, false, Encoding.UTF8);

                List<(string Title, string ItemCode, string PageCode)> orderedEntries = woodVariantPagesByKey.Values
                    .Select(CreateWoodVariantReportLine)
                    .OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.ItemCode, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.PageCode, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach ((string Title, string ItemCode, string PageCode) entry in orderedEntries)
                {
                    writer.WriteLine($"{entry.Title} - {entry.ItemCode} - {entry.PageCode}");
                }
            }
            catch (Exception ex)
            {
                capi.Logger?.Warning("[Handbook Categories] Failed to write wood variant report to {0}: {1}", filePath, ex);
            }
        }

        private static void SaveStoneVariantReport()
        {
            if (capi == null)
            {
                return;
            }

            string configDirectory;
            try
            {
                configDirectory = capi.GetOrCreateDataPath("ModConfig");
            }
            catch (Exception ex)
            {
                capi.Logger?.Warning("[Handbook Categories] Failed to access ModConfig directory for stone variants: {0}", ex);
                return;
            }

            if (string.IsNullOrEmpty(configDirectory))
            {
                return;
            }

            string filePath = System.IO.Path.Combine(configDirectory, StoneVariantReportFileName);

            try
            {
                using StreamWriter writer = new(filePath, false, Encoding.UTF8);

                List<(string StoneName, string PageTitle, string ItemCode, string PageCode)> orderedEntries = stoneVariantPagesByKey.Values
                    .Select(CreateStoneVariantReportLine)
                    .OrderBy(entry => entry.StoneName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.PageTitle, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.ItemCode, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.PageCode, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach ((string StoneName, string PageTitle, string ItemCode, string PageCode) entry in orderedEntries)
                {
                    writer.WriteLine($"{entry.StoneName} - {entry.PageTitle} - {entry.ItemCode} - {entry.PageCode}");
                }
            }
            catch (Exception ex)
            {
                capi.Logger?.Warning("[Handbook Categories] Failed to write stone variant report to {0}: {1}", filePath, ex);
            }
        }

        private static (string Title, string ItemCode, string PageCode) CreateWoodVariantReportLine(WoodPageReportEntry entry)
        {
            string title = FormatWoodVariantReportTitle(entry.PageTitle, entry.PageCode);
            string itemCode = FormatWoodVariantReportItemCode(entry.ItemCode);
            string pageCode = FormatWoodVariantReportPageCode(entry.PageCode);

            return (title, itemCode, pageCode);
        }

        private static (string StoneName, string PageTitle, string ItemCode, string PageCode) CreateStoneVariantReportLine(StonePageReportEntry entry)
        {
            string displayName = string.IsNullOrWhiteSpace(entry.StoneDisplayName)
                ? GetStoneVariantDisplayName(entry.StoneCode)
                : entry.StoneDisplayName;

            string title = FormatWoodVariantReportTitle(entry.PageTitle, entry.PageCode);
            string itemCode = FormatWoodVariantReportItemCode(entry.ItemCode);
            string pageCode = FormatWoodVariantReportPageCode(entry.PageCode);

            return (displayName, title, itemCode, pageCode);
        }

        private static bool TryGetStoneVariantInfo(ItemStack stack, out StoneVariantInfo info)
        {
            if (stack?.Collectible == null)
            {
                info = default;
                return false;
            }

            EnsureStoneVariantsLoaded();

            CollectibleObject collectible = stack.Collectible;

            string candidate = TryGetStoneVariantFromVariants(collectible.Variant);
            if (TryCreateStoneVariantInfo(candidate, out info))
            {
                return true;
            }

            if (collectible is Block block)
            {
                candidate = TryGetStoneVariantFromBlock(block, new HashSet<int>());
                if (TryCreateStoneVariantInfo(candidate, out info))
                {
                    return true;
                }
            }

            candidate = FindStoneNameInCode(collectible.Code?.Path);
            if (TryCreateStoneVariantInfo(candidate, out info))
            {
                return true;
            }

            string pageCode = GuiHandbookItemStackPage.PageCodeForStack(stack);
            candidate = FindStoneNameInCode(pageCode);
            if (TryCreateStoneVariantInfo(candidate, out info))
            {
                return true;
            }

            info = default;
            return false;
        }

        private static bool TryGetCeramicVariantInfo(ItemStack stack, out CeramicVariantInfo info)
        {
            info = default;

            if (stack?.Collectible is not Block block)
            {
                return false;
            }

            if (block.BlockMaterial != EnumBlockMaterial.Ceramic)
            {
                return false;
            }

            string candidate = TryGetCeramicVariantFromVariants(block.Variant);
            if (TryCreateCeramicVariantInfo(candidate, out info))
            {
                return true;
            }

            candidate = FindCeramicVariantInCode(block.Code?.Path);
            if (TryCreateCeramicVariantInfo(candidate, out info))
            {
                return true;
            }

            string pageCode = GuiHandbookItemStackPage.PageCodeForStack(stack);
            candidate = FindCeramicVariantInCode(pageCode);
            if (TryCreateCeramicVariantInfo(candidate, out info))
            {
                return true;
            }

            return false;
        }

        private static bool TryCreateStoneVariantInfo(string value, out StoneVariantInfo info)
        {
            if (string.IsNullOrEmpty(value))
            {
                info = default;
                return false;
            }

            RegisterKnownStoneName(value);
            string normalized = NormalizeStoneName(value);
            info = new StoneVariantInfo(value, normalized);
            return info.HasValue;
        }

        private static string TryGetStoneVariantFromVariants(RelaxedReadOnlyDictionary<string, string> variants)
        {
            if (variants == null)
            {
                return null;
            }

            string value = GetVariantValue(variants, "rock");
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            value = GetVariantValue(variants, "rocktype");
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            foreach (KeyValuePair<string, string> entry in variants)
            {
                string key = entry.Key;
                string variantValue = entry.Value;

                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(variantValue))
                {
                    continue;
                }

                if (IsStoneKey(key) || IsStoneVariantValue(variantValue))
                {
                    return variantValue;
                }
            }

            return null;
        }

        private static string TryGetCeramicVariantFromVariants(RelaxedReadOnlyDictionary<string, string> variants)
        {
            if (variants == null)
            {
                return null;
            }

            foreach (KeyValuePair<string, string> entry in variants)
            {
                string variantValue = entry.Value;
                if (TryMapCeramicColor(variantValue, out string canonical))
                {
                    return canonical;
                }
            }

            return null;
        }

        private static string TryGetStoneVariantFromBlock(Block block, HashSet<int> visited)
        {
            if (block == null)
            {
                return null;
            }

            visited ??= new HashSet<int>();
            if (!visited.Add(block.BlockId))
            {
                return null;
            }

            string candidate = TryGetStoneVariantFromVariants(block.Variant);
            if (!string.IsNullOrEmpty(candidate))
            {
                return candidate;
            }

            candidate = FindStoneNameInCode(block.Code?.Path);
            if (!string.IsNullOrEmpty(candidate))
            {
                return candidate;
            }

            return null;
        }

        private static string FindCeramicVariantInCode(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return null;
            }

            foreach (string token in EnumerateCodeTokens(code))
            {
                if (TryMapCeramicColor(token, out string canonical))
                {
                    return canonical;
                }
            }

            return null;
        }

        private static bool TryCreateCeramicVariantInfo(string value, out CeramicVariantInfo info)
        {
            if (!TryMapCeramicColor(value, out string canonical))
            {
                info = default;
                return false;
            }

            string normalized = NormalizeCeramicName(canonical);
            if (string.IsNullOrEmpty(normalized))
            {
                info = default;
                return false;
            }

            string displayName = GetCeramicVariantDisplayName(normalized);
            info = new CeramicVariantInfo(value, canonical, normalized, displayName);
            return info.HasValue;
        }

        private static bool TryMapCeramicColor(string value, out string canonical)
        {
            canonical = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            if (!ceramicVariantColorMap.TryGetValue(trimmed, out canonical))
            {
                return false;
            }

            return !string.IsNullOrEmpty(canonical);
        }

        private static string FindStoneNameInCode(string code, string displayName = null)
        {
            if (string.IsNullOrEmpty(code) || knownStoneVariantNames.Count == 0)
            {
                return null;
            }

            foreach (string stone in knownStoneVariantNames)
            {
                if (CodeContainsStoneToken(code, stone))
                {
                    return stone;
                }
            }

            return null;
        }

        private static bool CodeContainsStoneToken(string value, string stone)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(stone))
            {
                return false;
            }

            List<string> tokens = EnumerateCodeTokens(value)
                .Where(token => !string.IsNullOrEmpty(token))
                .Select(token => token.ToLowerInvariant())
                .ToList();

            if (tokens.Count == 0)
            {
                return false;
            }

            string stoneLower = stone.ToLowerInvariant();

            for (int i = 0; i < tokens.Count; i++)
            {
                string token = tokens[i];
                if (token.Equals(stoneLower, StringComparison.Ordinal))
                {
                    return true;
                }

                if (token.Length >= stoneLower.Length)
                {
                    continue;
                }

                StringBuilder combined = new(token);
                for (int j = i + 1; j < tokens.Count && combined.Length < stoneLower.Length; j++)
                {
                    combined.Append(tokens[j]);
                    int length = combined.Length;

                    if (length == stoneLower.Length)
                    {
                        if (string.Equals(combined.ToString(), stoneLower, StringComparison.Ordinal))
                        {
                            return true;
                        }

                        break;
                    }

                    if (length > stoneLower.Length)
                    {
                        break;
                    }
                }
            }

            return false;
        }

        private static string GetVariantValue(RelaxedReadOnlyDictionary<string, string> variants, string key)
        {
            if (variants == null || string.IsNullOrEmpty(key))
            {
                return null;
            }

            if (variants.TryGetValue(key, out string directValue) && !string.IsNullOrEmpty(directValue))
            {
                return directValue;
            }

            foreach (KeyValuePair<string, string> entry in variants)
            {
                if (!string.IsNullOrEmpty(entry.Key) && entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(entry.Value))
                {
                    return entry.Value;
                }
            }

            return null;
        }

        private static bool IsStoneKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            return key.IndexOf("rock", StringComparison.OrdinalIgnoreCase) >= 0
                || key.IndexOf("stone", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetWoodVariantReportTitle(GuiHandbookPage page)
        {
            string title = GetLocalizedPageTitle(page);
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            return string.IsNullOrWhiteSpace(page?.PageCode) ? string.Empty : page.PageCode.Trim();
        }

        private static string GetItemCodeForStack(ItemStack stack)
        {
            AssetLocation code = stack?.Collectible?.Code;
            if (code == null)
            {
                return string.Empty;
            }

            string shortCode = code.ToShortString();
            if (!string.IsNullOrWhiteSpace(shortCode))
            {
                return shortCode.Trim();
            }

            string fullCode = code.ToString();
            return string.IsNullOrWhiteSpace(fullCode) ? string.Empty : fullCode.Trim();
        }

        private static string GetEffectivePageCode(GuiHandbookItemStackPage page)
        {
            if (page == null)
            {
                return string.Empty;
            }

            string code = page.PageCode;
            if (!string.IsNullOrWhiteSpace(code))
            {
                return code.Trim();
            }

            ItemStack stack = page.Stack;
            if (stack == null)
            {
                return string.Empty;
            }

            string generated = GuiHandbookItemStackPage.PageCodeForStack(stack);
            return string.IsNullOrWhiteSpace(generated) ? string.Empty : generated.Trim();
        }

        private static string FormatWoodVariantReportTitle(string title, string pageCode)
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            return string.IsNullOrWhiteSpace(pageCode) ? "<unnamed page>" : pageCode;
        }

        private static string FormatWoodVariantReportItemCode(string itemCode)
        {
            return string.IsNullOrWhiteSpace(itemCode) ? "<unknown item code>" : itemCode;
        }

        private static string FormatWoodVariantReportPageCode(string pageCode)
        {
            return string.IsNullOrWhiteSpace(pageCode) ? "<unknown page code>" : pageCode;
        }

        private static string BuildUniqueGroupCode(string prefix, string sanitizedName, HashSet<string> usedCodes)
        {
            string baseCode = string.Concat(prefix, sanitizedName);
            string candidate = baseCode;
            int counter = 1;

            while (!usedCodes.Add(candidate))
            {
                candidate = string.Concat(baseCode, "-", counter.ToString("D2", CultureInfo.InvariantCulture));
                counter++;
            }

            return candidate;
        }

        private static List<VariantGroupCreationInfo> CollectWoodVariantGroupInfos()
        {
            var groups = new List<VariantGroupCreationInfo>();

            foreach (WoodVariantGroupBuilder builder in woodVariantGroupsByKey.Values)
            {
                if (builder == null)
                {
                    continue;
                }

                List<GuiHandbookItemStackPage> members = builder.Members
                    .Where(member => member != null && !member.IsDuplicate)
                    .Distinct()
                    .ToList();

                if (members.Count <= 1)
                {
                    continue;
                }

                members.Sort((a, b) => string.Compare(GetLocalizedPageTitle(a), GetLocalizedPageTitle(b), StringComparison.OrdinalIgnoreCase));

                string sanitized = !string.IsNullOrEmpty(builder.SanitizedName)
                    ? builder.SanitizedName
                    : Sanitize(builder.DisplayName);

                if (string.IsNullOrEmpty(sanitized))
                {
                    sanitized = Sanitize(builder.NormalizedName);
                }

                if (string.IsNullOrEmpty(sanitized))
                {
                    sanitized = "woodvariant";
                }

                groups.Add(new VariantGroupCreationInfo(builder.DisplayName, sanitized, members, builder.SortHint, WoodGroupHiddenCodePrefix, WoodGroupPageCodePrefix));
            }

            return groups;
        }

        private static List<VariantGroupCreationInfo> CollectStoneVariantGroupInfos()
        {
            var groups = new List<VariantGroupCreationInfo>();

            foreach (StoneVariantGroupBuilder builder in stoneVariantGroupsByKey.Values)
            {
                if (builder == null)
                {
                    continue;
                }

                List<GuiHandbookItemStackPage> members = builder.Members
                    .Where(member => member != null && !member.IsDuplicate)
                    .Distinct()
                    .ToList();

                if (members.Count <= 1)
                {
                    continue;
                }

                members.Sort((a, b) => string.Compare(GetLocalizedPageTitle(a), GetLocalizedPageTitle(b), StringComparison.OrdinalIgnoreCase));

                string sanitized = !string.IsNullOrEmpty(builder.SanitizedName)
                    ? builder.SanitizedName
                    : Sanitize(builder.DisplayName);

                if (string.IsNullOrEmpty(sanitized))
                {
                    sanitized = Sanitize(builder.NormalizedName);
                }

                if (string.IsNullOrEmpty(sanitized))
                {
                    sanitized = "stonevariant";
                }

                groups.Add(new VariantGroupCreationInfo(builder.DisplayName, sanitized, members, builder.SortHint, StoneGroupHiddenCodePrefix, StoneGroupPageCodePrefix));
            }

            return groups;
        }

        private static List<VariantGroupCreationInfo> CollectCeramicVariantGroupInfos()
        {
            var groups = new List<VariantGroupCreationInfo>();

            foreach (CeramicVariantGroupBuilder builder in ceramicVariantGroupsByKey.Values)
            {
                if (builder == null)
                {
                    continue;
                }

                List<GuiHandbookItemStackPage> members = builder.Members
                    .Where(member => member != null && !member.IsDuplicate)
                    .Distinct()
                    .ToList();

                if (members.Count <= 1)
                {
                    continue;
                }

                members.Sort((a, b) => string.Compare(GetLocalizedPageTitle(a), GetLocalizedPageTitle(b), StringComparison.OrdinalIgnoreCase));

                string sanitized = !string.IsNullOrEmpty(builder.SanitizedName)
                    ? builder.SanitizedName
                    : Sanitize(builder.DisplayName);

                if (string.IsNullOrEmpty(sanitized))
                {
                    sanitized = Sanitize(builder.NormalizedName);
                }

                if (string.IsNullOrEmpty(sanitized))
                {
                    sanitized = "ceramicvariant";
                }

                groups.Add(new VariantGroupCreationInfo(builder.DisplayName, sanitized, members, builder.SortHint, CeramicGroupHiddenCodePrefix, CeramicGroupPageCodePrefix));
            }

            return groups;
        }

        private static List<GuiHandbookPage> EnsureWoodGroupCategoryExists()
        {
            if (!pagesByCategory.TryGetValue(WoodGroupDisplayCategoryCode, out List<GuiHandbookPage> list) || list == null)
            {
                list = new List<GuiHandbookPage>();
                pagesByCategory[WoodGroupDisplayCategoryCode] = list;
            }
            else
            {
                list.Clear();
            }

            displayNameByCategory[WoodGroupDisplayCategoryCode] = WoodGroupDisplayCategoryName;
            translationKeyByCategory[WoodGroupDisplayCategoryCode] = null;
            tabBackgroundByCategory[WoodGroupDisplayCategoryCode] = HandbookCategoryColors.GetDefaultBackgroundColor();

            if (!orderedCategories.Contains(WoodGroupDisplayCategoryCode))
            {
                orderedCategories.Add(WoodGroupDisplayCategoryCode);
            }

            return list;
        }

        private static void RemoveWoodGroupCategory()
        {
            List<GroupHandbookPage> existingWoodGroups = activeGroupPages
                .Where(group => group != null
                    && string.Equals(group.DisplayCategoryCode, WoodGroupDisplayCategoryCode, StringComparison.Ordinal))
                .ToList();

            foreach (GroupHandbookPage group in existingWoodGroups)
            {
                UnregisterGroupPage(group);
            }

            pagesByCategory.Remove(WoodGroupDisplayCategoryCode);
            displayNameByCategory.Remove(WoodGroupDisplayCategoryCode);
            translationKeyByCategory.Remove(WoodGroupDisplayCategoryCode);
            tabBackgroundByCategory.Remove(WoodGroupDisplayCategoryCode);
            orderedCategories.Remove(WoodGroupDisplayCategoryCode);

            string displayKey = GetGroupDisplayKey(WoodGroupDisplayCategoryCode);
            groupPagesByDisplayCategory.Remove(displayKey);
        }

        private static void CreateWoodVariantGroups()
        {
            RemoveWoodGroupCategory();

            List<VariantGroupCreationInfo> groups = CollectWoodVariantGroupInfos();
            if (groups.Count == 0)
            {
                return;
            }

            List<GuiHandbookPage> displayCategoryPages = EnsureWoodGroupCategoryExists();
            RegisterVariantGroups(groups, WoodGroupDisplayCategoryCode, displayCategoryPages);
        }

        private static List<GuiHandbookPage> EnsureStoneGroupCategoryExists()
        {
            if (!pagesByCategory.TryGetValue(StoneGroupDisplayCategoryCode, out List<GuiHandbookPage> list) || list == null)
            {
                list = new List<GuiHandbookPage>();
                pagesByCategory[StoneGroupDisplayCategoryCode] = list;
            }
            else
            {
                list.Clear();
            }

            displayNameByCategory[StoneGroupDisplayCategoryCode] = StoneGroupDisplayCategoryName;
            translationKeyByCategory[StoneGroupDisplayCategoryCode] = null;
            tabBackgroundByCategory[StoneGroupDisplayCategoryCode] = HandbookCategoryColors.GetDefaultBackgroundColor();

            if (!orderedCategories.Contains(StoneGroupDisplayCategoryCode))
            {
                orderedCategories.Add(StoneGroupDisplayCategoryCode);
            }

            return list;
        }

        private static List<GuiHandbookPage> EnsureCeramicGroupCategoryExists()
        {
            if (!pagesByCategory.TryGetValue(CeramicGroupDisplayCategoryCode, out List<GuiHandbookPage> list) || list == null)
            {
                list = new List<GuiHandbookPage>();
                pagesByCategory[CeramicGroupDisplayCategoryCode] = list;
            }
            else
            {
                list.Clear();
            }

            displayNameByCategory[CeramicGroupDisplayCategoryCode] = CeramicGroupDisplayCategoryName;
            translationKeyByCategory[CeramicGroupDisplayCategoryCode] = null;
            tabBackgroundByCategory[CeramicGroupDisplayCategoryCode] = HandbookCategoryColors.GetDefaultBackgroundColor();

            if (!orderedCategories.Contains(CeramicGroupDisplayCategoryCode))
            {
                orderedCategories.Add(CeramicGroupDisplayCategoryCode);
            }

            return list;
        }

        private static void RemoveStoneGroupCategory()
        {
            List<GroupHandbookPage> existingStoneGroups = activeGroupPages
                .Where(group => group != null
                    && string.Equals(group.DisplayCategoryCode, StoneGroupDisplayCategoryCode, StringComparison.Ordinal))
                .ToList();

            foreach (GroupHandbookPage group in existingStoneGroups)
            {
                UnregisterGroupPage(group);
            }

            pagesByCategory.Remove(StoneGroupDisplayCategoryCode);
            displayNameByCategory.Remove(StoneGroupDisplayCategoryCode);
            translationKeyByCategory.Remove(StoneGroupDisplayCategoryCode);
            tabBackgroundByCategory.Remove(StoneGroupDisplayCategoryCode);
            orderedCategories.Remove(StoneGroupDisplayCategoryCode);

            string displayKey = GetGroupDisplayKey(StoneGroupDisplayCategoryCode);
            groupPagesByDisplayCategory.Remove(displayKey);
        }

        private static void RemoveCeramicGroupCategory()
        {
            List<GroupHandbookPage> existingCeramicGroups = activeGroupPages
                .Where(group => group != null
                    && string.Equals(group.DisplayCategoryCode, CeramicGroupDisplayCategoryCode, StringComparison.Ordinal))
                .ToList();

            foreach (GroupHandbookPage group in existingCeramicGroups)
            {
                UnregisterGroupPage(group);
            }

            pagesByCategory.Remove(CeramicGroupDisplayCategoryCode);
            displayNameByCategory.Remove(CeramicGroupDisplayCategoryCode);
            translationKeyByCategory.Remove(CeramicGroupDisplayCategoryCode);
            tabBackgroundByCategory.Remove(CeramicGroupDisplayCategoryCode);
            orderedCategories.Remove(CeramicGroupDisplayCategoryCode);

            string displayKey = GetGroupDisplayKey(CeramicGroupDisplayCategoryCode);
            groupPagesByDisplayCategory.Remove(displayKey);
        }

        private static List<GroupHandbookPage> RegisterVariantGroups(
            IEnumerable<VariantGroupCreationInfo> groupInfos,
            string displayCategoryCode,
            List<GuiHandbookPage> displayCategoryPages)
        {
            var created = new List<GroupHandbookPage>();

            if (groupInfos == null)
            {
                return created;
            }

            var usedHiddenCodes = new HashSet<string>(groupByHiddenCategoryCode.Keys, StringComparer.OrdinalIgnoreCase);
            var usedPageCodes = new HashSet<string>(activeGroupPages
                .Where(page => page != null && !string.IsNullOrWhiteSpace(page.PageCode))
                .Select(page => page.PageCode),
                StringComparer.OrdinalIgnoreCase);

            foreach (VariantGroupCreationInfo info in groupInfos
                .Where(info => info != null && info.Members != null && info.Members.Count > 0)
                .OrderBy(info => info.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                string sanitized = string.IsNullOrEmpty(info.SanitizedName)
                    ? DefaultGroupName.ToLowerInvariant()
                    : info.SanitizedName;

                string hiddenCode = BuildUniqueGroupCode(info.HiddenCodePrefix, sanitized, usedHiddenCodes);
                string pageCode = BuildUniqueGroupCode(info.PageCodePrefix, sanitized, usedPageCodes);

                var groupPage = new GroupHandbookPage(pageCode, hiddenCode, displayCategoryCode, info.DisplayName, info.Members);
                GuiHandbookPage iconSource = info.Members.FirstOrDefault();
                int sortHint = info.SortHint < int.MaxValue ? info.SortHint : iconSource?.PageNumber ?? int.MaxValue;
                groupPage.PageNumber = info.SortHint < int.MaxValue ? info.SortHint : iconSource?.PageNumber ?? 0;
                groupPage.SetSortOrderHint(sortHint);
                groupPage.AdoptAppearanceFrom(iconSource);

                RegisterGroupPage(groupPage);

                if (displayCategoryPages != null && !displayCategoryPages.Contains(groupPage))
                {
                    displayCategoryPages.Add(groupPage);
                }

                created.Add(groupPage);
            }

            return created;
        }
        private static void RemoveEverythingGroupCategory()
        {
            List<GroupHandbookPage> existingGroups = activeGroupPages
                .Where(group => group != null
                    && string.Equals(group.DisplayCategoryCode, EverythingGroupsDisplayCategoryCode, StringComparison.Ordinal))
                .ToList();

            foreach (GroupHandbookPage group in existingGroups)
            {
                UnregisterGroupPage(group);
            }

            pagesByCategory.Remove(EverythingGroupsDisplayCategoryCode);
            displayNameByCategory.Remove(EverythingGroupsDisplayCategoryCode);
            translationKeyByCategory.Remove(EverythingGroupsDisplayCategoryCode);
            tabBackgroundByCategory.Remove(EverythingGroupsDisplayCategoryCode);
            orderedCategories.Remove(EverythingGroupsDisplayCategoryCode);

            string displayKey = GetGroupDisplayKey(EverythingGroupsDisplayCategoryCode);
            groupPagesByDisplayCategory.Remove(displayKey);
        }


        private static void CreateStoneVariantGroups()
        {
            RemoveStoneGroupCategory();

            List<VariantGroupCreationInfo> groups = CollectStoneVariantGroupInfos();
            if (groups.Count == 0)
            {
                return;
            }

            List<GuiHandbookPage> displayCategoryPages = EnsureStoneGroupCategoryExists();
            RegisterVariantGroups(groups, StoneGroupDisplayCategoryCode, displayCategoryPages);
        }

        private static void CreateCeramicVariantGroups()
        {
            RemoveCeramicGroupCategory();

            List<VariantGroupCreationInfo> groups = CollectCeramicVariantGroupInfos();
            if (groups.Count == 0)
            {
                return;
            }

            List<GuiHandbookPage> displayCategoryPages = EnsureCeramicGroupCategoryExists();
            RegisterVariantGroups(groups, CeramicGroupDisplayCategoryCode, displayCategoryPages);
        }
        private static void CreateEverythingGroupsCategory(List<GuiHandbookPage> allPages)
        {
            if (allPages == null || allPages.Count == 0)
            {
                return;
            }

            var createdGroups = new List<GroupHandbookPage>();

            createdGroups.AddRange(RegisterVariantGroups(CollectWoodVariantGroupInfos(), EverythingGroupsDisplayCategoryCode, null));
            createdGroups.AddRange(RegisterVariantGroups(CollectStoneVariantGroupInfos(), EverythingGroupsDisplayCategoryCode, null));
            createdGroups.AddRange(RegisterVariantGroups(CollectCeramicVariantGroupInfos(), EverythingGroupsDisplayCategoryCode, null));

            PopulateEverythingGroupsCategory(allPages, createdGroups);
        }

        private static void PopulateEverythingGroupsCategory(List<GuiHandbookPage> allPages, List<GroupHandbookPage> createdGroups)
        {
            if (createdGroups == null || createdGroups.Count == 0)
            {
                return;
            }

            List<GuiHandbookPage> categoryPages = BuildEverythingGroupsCategoryPages(allPages, createdGroups);
            if (categoryPages == null || categoryPages.Count == 0)
            {
                return;
            }

            pagesByCategory[EverythingGroupsDisplayCategoryCode] = categoryPages;
            displayNameByCategory[EverythingGroupsDisplayCategoryCode] = EverythingGroupsDisplayCategoryName;
            translationKeyByCategory[EverythingGroupsDisplayCategoryCode] = null;
            tabBackgroundByCategory[EverythingGroupsDisplayCategoryCode] = HandbookCategoryColors.GetDefaultBackgroundColor();

            if (!orderedCategories.Contains(EverythingGroupsDisplayCategoryCode))
            {
                orderedCategories.Add(EverythingGroupsDisplayCategoryCode);
            }
        }

        private static List<GuiHandbookPage> BuildEverythingGroupsCategoryPages(List<GuiHandbookPage> allPages, List<GroupHandbookPage> groups)
        {
            var result = new List<GuiHandbookPage>();

            if (allPages == null || allPages.Count == 0)
            {
                return result;
            }

            if (groups == null || groups.Count == 0)
            {
                result.AddRange(allPages.Where(page => page != null));
                return result;
            }

            var groupLookup = new Dictionary<GuiHandbookPage, GroupHandbookPage>();
            foreach (GroupHandbookPage group in groups)
            {
                if (group?.Members == null)
                {
                    continue;
                }

                foreach (GuiHandbookPage member in group.Members)
                {
                    if (member == null)
                    {
                        continue;
                    }

                    if (!groupLookup.ContainsKey(member))
                    {
                        groupLookup[member] = group;
                    }
                }
            }

            var insertedGroups = new HashSet<GroupHandbookPage>();

            foreach (GuiHandbookPage page in allPages)
            {
                if (page == null)
                {
                    continue;
                }

                if (groupLookup.TryGetValue(page, out GroupHandbookPage group) && group != null)
                {
                    if (insertedGroups.Add(group))
                    {
                        result.Add(group);
                    }

                    continue;
                }

                result.Add(page);
            }

            foreach (GroupHandbookPage group in groups)
            {
                if (group == null || insertedGroups.Contains(group))
                {
                    continue;
                }

                result.Add(group);
            }

            return result;
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
                string searchableContent = GetSearchableContent(page, titleData);
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

                if (ShouldHidePageForCategory(page, categoryCode))
                {
                    continue;
                }

                if (MatchesSearchQuery(page, searchQuery, out float weight))
                {
                    weightedPages.Add(new WeightedHandbookPage
                    {
                        Page = page,
                        Weight = weight,
                        SortHint = page?.PageNumber ?? int.MaxValue
                    });
                }
            }

            AppendGroupPages(categoryCode, searchQuery, weightedPages);

            foreach (WeightedHandbookPage weighted in weightedPages
                .OrderByDescending(w => w.Weight)
                .ThenBy(w => w.SortHint))
            {
                shownPages.Add(weighted.Page);
            }

            UpdateScrollArea(overviewGui, listHeight);
        }

        internal static bool TryHandleGroupShiftClick(
            GuiDialogHandbook dialog,
            GuiComposer overviewGui,
            GuiElementFlatList searchList,
            GuiHandbookPage selectedPage)
        {
            if (!enableGroupCreationHotkeys)
            {
                return false;
            }

            return TryHandleGroupClick(
                dialog,
                overviewGui,
                searchList,
                selectedPage,
                GetNormalizedPageCode,
                ExtractOrderedPageCodeWords,
                minimumWordCount: 3);
        }

        internal static bool TryHandleGroupCtrlClick(
            GuiDialogHandbook dialog,
            GuiComposer overviewGui,
            GuiElementFlatList searchList,
            GuiHandbookPage selectedPage)
        {
            if (!enableGroupCreationHotkeys)
            {
                return false;
            }

            return TryHandleGroupClick(
                dialog,
                overviewGui,
                searchList,
                selectedPage,
                GetLocalizedPageTitle,
                ExtractOrderedWordsPreservingCase,
                minimumWordCount: 2);
        }

        private static bool HasGroupInDisplayCategory(GuiHandbookPage page, string displayCategoryCode)
        {
            if (page == null)
            {
                return false;
            }

            if (!groupsByMemberPage.TryGetValue(page, out List<GroupHandbookPage> groups) || groups == null || groups.Count == 0)
            {
                return false;
            }

            string targetKey = GetGroupDisplayKey(displayCategoryCode);

            foreach (GroupHandbookPage group in groups)
            {
                if (group == null)
                {
                    continue;
                }

                string groupKey = GetGroupDisplayKey(group.DisplayCategoryCode);
                if (string.Equals(groupKey, targetKey, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RegisterMemberToGroup(GuiHandbookPage member, GroupHandbookPage group)
        {
            if (member == null || group == null)
            {
                return;
            }

            if (!groupsByMemberPage.TryGetValue(member, out List<GroupHandbookPage> groups) || groups == null)
            {
                groups = new List<GroupHandbookPage>();
                groupsByMemberPage[member] = groups;
            }

            if (!groups.Contains(group))
            {
                groups.Add(group);
            }
        }

        private static void UnregisterMemberFromGroup(GuiHandbookPage member, GroupHandbookPage group)
        {
            if (member == null)
            {
                return;
            }

            if (!groupsByMemberPage.TryGetValue(member, out List<GroupHandbookPage> groups) || groups == null || groups.Count == 0)
            {
                return;
            }

            for (int i = groups.Count - 1; i >= 0; i--)
            {
                GroupHandbookPage existing = groups[i];
                if (existing == null || ReferenceEquals(existing, group))
                {
                    groups.RemoveAt(i);
                }
            }

            if (groups.Count == 0)
            {
                groupsByMemberPage.Remove(member);
            }
        }

        private static bool TryHandleGroupClick(
            GuiDialogHandbook dialog,
            GuiComposer overviewGui,
            GuiElementFlatList searchList,
            GuiHandbookPage selectedPage,
            System.Func<GuiHandbookPage, string> keySelector,
            System.Func<string, List<string>> wordExtractor,
            int minimumWordCount = 0)
        {
            if (dialog == null || selectedPage == null)
            {
                return false;
            }

            string displayCategoryCode = dialog.currentCatgoryCode;

            if (selectedPage is GroupHandbookPage existingGroup)
            {
                return TryRemoveGroupPage(dialog, overviewGui, searchList, existingGroup);
            }

            if (HasGroupInDisplayCategory(selectedPage, displayCategoryCode))
            {
                return false;
            }

            if (ShownPagesField?.GetValue(dialog) is not List<IFlatListItem> shownPages || shownPages.Count == 0)
            {
                return false;
            }

            int selectedIndex = shownPages.IndexOf(selectedPage);
            if (selectedIndex < 0)
            {
                return false;
            }

            List<HiddenPageEntry> candidateEntries = new()
            {
                new HiddenPageEntry(selectedPage, selectedIndex)
            };

            string selectedKey = GetGroupingKey(selectedPage, keySelector);
            List<string> selectedWords = ExtractWordsForGrouping(selectedKey, wordExtractor);
            if (minimumWordCount > 0 && selectedWords.Count > 0 && selectedWords.Count < minimumWordCount)
            {
                return false;
            }
            if (selectedWords.Count == 0 && !string.IsNullOrWhiteSpace(selectedKey))
            {
                selectedWords.Add(selectedKey.Trim());
            }

            bool allowMatches = selectedWords.Count > 0;
            int? allowedDifferingIndex = null;

            foreach (int index in EnumerateGroupSearchOrder(shownPages.Count, selectedIndex))
            {
                if (shownPages[index] is not GuiHandbookPage candidate || ReferenceEquals(candidate, selectedPage))
                {
                    continue;
                }

                if (candidate is GroupHandbookPage || HasGroupInDisplayCategory(candidate, displayCategoryCode))
                {
                    continue;
                }

                string candidateKey = GetGroupingKey(candidate, keySelector);
                if (string.IsNullOrWhiteSpace(candidateKey))
                {
                    continue;
                }

                List<string> candidateWords = ExtractWordsForGrouping(candidateKey, wordExtractor);
                if (minimumWordCount > 0 && candidateWords.Count > 0 && candidateWords.Count < minimumWordCount)
                {
                    continue;
                }
                if (candidateWords.Count == 0)
                {
                    candidateWords.Add(candidateKey.Trim());
                }

                if (!allowMatches)
                {
                    continue;
                }

                if (!TitlesMatchAllowingOneWordDifference(selectedWords, candidateWords, out int differingIndex))
                {
                    continue;
                }

                if (allowedDifferingIndex.HasValue)
                {
                    if (differingIndex >= 0 && allowedDifferingIndex.Value != differingIndex)
                    {
                        continue;
                    }
                }
                else if (differingIndex >= 0)
                {
                    allowedDifferingIndex = differingIndex;
                }

                candidateEntries.Add(new HiddenPageEntry(candidate, index));
            }

            candidateEntries.Sort((a, b) => a.Index.CompareTo(b.Index));

            List<GuiHandbookPage> members = new();
            foreach (HiddenPageEntry entry in candidateEntries)
            {
                if (entry?.Page == null)
                {
                    continue;
                }

                if (!members.Contains(entry.Page))
                {
                    members.Add(entry.Page);
                }
            }

            if (members.Count == 0)
            {
                members.Add(selectedPage);
            }

            int insertIndex = candidateEntries.Count > 0
                ? Math.Max(0, candidateEntries.Min(entry => entry.Index))
                : selectedIndex;

            PendingGroupCreation pending = new(
                dialog,
                overviewGui,
                searchList,
                shownPages,
                members,
                insertIndex,
                dialog.currentCatgoryCode,
                selectedPage);

            pendingGroupCreations[dialog] = pending;

            string defaultName = GetDefaultGroupName(selectedPage);
            ShowGroupNamePrompt(pending, defaultName);

            return true;
        }

        private static IEnumerable<int> EnumerateGroupSearchOrder(int pageCount, int selectedIndex)
        {
            if (pageCount <= 0)
            {
                yield break;
            }

            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }
            else if (selectedIndex >= pageCount)
            {
                selectedIndex = pageCount - 1;
            }

            for (int index = selectedIndex + 1; index < pageCount; index++)
            {
                yield return index;
            }

            for (int index = selectedIndex - 1; index >= 0; index--)
            {
                yield return index;
            }
        }

        internal static bool TryAddPageToGroup(
            GuiDialogHandbook dialog,
            GuiComposer overviewGui,
            GuiElementFlatList searchList,
            GroupHandbookPage groupPage,
            GuiHandbookPage memberPage)
        {
            if (dialog == null || groupPage == null || memberPage == null)
            {
                return false;
            }

            if (ReferenceEquals(groupPage, memberPage) || memberPage is GroupHandbookPage)
            {
                return false;
            }

            if (HasGroupInDisplayCategory(memberPage, groupPage.DisplayCategoryCode))
            {
                return false;
            }

            bool added = groupPage.AddMembers(new[] { memberPage });
            if (!added)
            {
                return false;
            }

            RegisterMemberToGroup(memberPage, groupPage);

            string hiddenCode = groupPage.HiddenCategoryCode;
            if (!string.IsNullOrEmpty(hiddenCode))
            {
                pagesByCategory[hiddenCode] = groupPage.Members
                    .Where(page => page != null)
                    .ToList();
            }

            UpdateConfigEntryMembers(groupPage);

            if (ShownPagesField?.GetValue(dialog) is List<IFlatListItem> shownPages)
            {
                int index = shownPages.IndexOf(memberPage);
                if (index >= 0)
                {
                    shownPages.RemoveAt(index);

                    searchList?.CalcTotalHeight();

                    if (overviewGui != null)
                    {
                        UpdateScrollArea(overviewGui, GetListHeight(dialog));
                    }
                }
            }

            CenterSearchListOnPage(overviewGui, searchList, groupPage);
            AddRowHighlight(groupPage, CollapseHighlightColor, RowHighlightDurationMs);

            return true;
        }

        internal static bool TryRemovePageFromGroup(
            GuiDialogHandbook dialog,
            GuiComposer overviewGui,
            GuiElementFlatList searchList,
            GroupHandbookPage groupPage,
            GuiHandbookPage memberPage)
        {
            if (dialog == null || groupPage == null || memberPage == null)
            {
                return false;
            }

            bool removed = groupPage.RemoveMember(memberPage);
            if (!removed)
            {
                return false;
            }

            UnregisterMemberFromGroup(memberPage, groupPage);

            string hiddenCode = groupPage.HiddenCategoryCode;
            List<GuiHandbookPage> remainingMembers = groupPage.Members
                .Where(page => page != null)
                .ToList();

            if (!string.IsNullOrEmpty(hiddenCode))
            {
                if (remainingMembers.Count > 0)
                {
                    pagesByCategory[hiddenCode] = remainingMembers;
                }
                else
                {
                    pagesByCategory.Remove(hiddenCode);
                }
            }

            List<IFlatListItem> shownPages = null;
            if (ShownPagesField?.GetValue(dialog) is List<IFlatListItem> currentPages && currentPages != null)
            {
                shownPages = currentPages;
                RemovePageFromShownPages(currentPages, memberPage);
            }

            if (remainingMembers.Count == 0)
            {
                if (TryRemoveGroupPage(dialog, overviewGui, searchList, groupPage))
                {
                    return true;
                }

                HandleEmptyGroupRemoval(dialog, overviewGui, searchList, groupPage, shownPages);
                return true;
            }

            UpdateConfigEntryMembers(groupPage);

            searchList ??= overviewGui?.GetFlatList("stacklist");
            searchList?.CalcTotalHeight();

            if (overviewGui != null)
            {
                UpdateScrollArea(overviewGui, GetListHeight(dialog));
            }

            return true;
        }

        private static string GetGroupingKey(GuiHandbookPage page, System.Func<GuiHandbookPage, string> selector)
        {
            if (page == null)
            {
                return string.Empty;
            }

            string key = selector?.Invoke(page);
            if (!string.IsNullOrWhiteSpace(key))
            {
                return key;
            }

            return GetNormalizedPageCode(page);
        }

        private static List<string> ExtractWordsForGrouping(string key, System.Func<string, List<string>> extractor)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return new List<string>();
            }

            List<string> words = extractor?.Invoke(key) ?? new List<string>();

            if (words.Count == 0)
            {
                return new List<string>();
            }

            List<string> filtered = new(words.Count);
            for (int i = 0; i < words.Count; i++)
            {
                string word = words[i];
                if (!string.IsNullOrWhiteSpace(word))
                {
                    filtered.Add(word.Trim());
                }
            }

            return filtered;
        }

        private static string GetDefaultGroupName(GuiHandbookPage selectedPage)
        {
            string title = GetLocalizedPageTitle(selectedPage);
            if (string.IsNullOrWhiteSpace(title))
            {
                return DefaultGroupName;
            }

            string trimmed = TrimCategoryNameToMaximum(title, out _);
            return string.IsNullOrWhiteSpace(trimmed) ? DefaultGroupName : trimmed;
        }

        private static void ShowGroupNamePrompt(PendingGroupCreation pending, string initialName)
        {
            if (pending == null)
            {
                return;
            }

            ICoreClientAPI api = capi;
            if (api == null)
            {
                return;
            }

            string sanitizedInitial = GetSanitizedInitialGroupName(initialName);
            string recentGroupName = GetSanitizedInitialGroupName(lastCreatedGroupName);
            if (!string.IsNullOrWhiteSpace(recentGroupName))
            {
                sanitizedInitial = recentGroupName;
            }

            if (string.IsNullOrWhiteSpace(sanitizedInitial))
            {
                sanitizedInitial = DefaultGroupName;
            }

            sanitizedInitial = TrimCategoryNameToMaximum(sanitizedInitial, out _);
            if (string.IsNullOrWhiteSpace(sanitizedInitial))
            {
                sanitizedInitial = DefaultGroupName;
            }

            var prompt = new CreateCategoryPromptDialog(
                api,
                result => FinalizePendingGroupCreation(pending, result.Name),
                GetCreateCategoryPromptTitle(),
                GetCreateCategoryPromptMessage(),
                GetCreateCategoryPromptPlaceholder(),
                GetCreateCategoryPromptOkText(),
                GetCreateCategoryPromptCancelText(),
                $"handbookcategories-groupprompt-{Guid.NewGuid():N}",
                sanitizedInitial);

            prompt.TryOpen();
        }

        private static string GetSanitizedInitialGroupName(string proposedName)
        {
            if (string.IsNullOrWhiteSpace(proposedName))
            {
                return null;
            }

            string trimmed = TrimCategoryNameToMaximum(proposedName, out _);
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private static void FinalizePendingGroupCreation(PendingGroupCreation pending, string chosenName)
        {
            if (pending == null)
            {
                return;
            }

            pendingGroupCreations.Remove(pending.Dialog);

            List<GuiHandbookPage> members = pending.Members?.Where(page => page != null).ToList();
            if (members == null || members.Count == 0)
            {
                return;
            }

            string displayName = string.IsNullOrWhiteSpace(chosenName) ? DefaultGroupName : chosenName.Trim();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = DefaultGroupName;
            }

            lastCreatedGroupName = displayName;

            string normalizedName = NormalizeGroupName(displayName);
            if (string.IsNullOrEmpty(normalizedName))
            {
                normalizedName = NormalizeGroupName(DefaultGroupName);
            }

            if (TryMergeMembersWithExistingGroup(pending, members, normalizedName))
            {
                return;
            }

            int assignedId = nextGroupId++;
            string uniqueSuffix = $"{assignedId:D4}";
            string sanitized = Sanitize(displayName);
            if (string.IsNullOrEmpty(sanitized))
            {
                sanitized = "group";
            }
            string hiddenCategoryCode = $"{GroupCategoryCodePrefix}{sanitized}-{uniqueSuffix}";
            string pageCode = $"{GroupPageCodePrefix}{sanitized}-{uniqueSuffix}";

            var groupPage = new GroupHandbookPage(pageCode, hiddenCategoryCode, pending.DisplayCategoryCode, displayName, members);
            GuiHandbookPage referencePage = pending.SelectedPage ?? members.FirstOrDefault();
            if (referencePage != null)
            {
                groupPage.PageNumber = referencePage.PageNumber;
            }
            else
            {
                groupPage.PageNumber = pending.SelectedPage?.PageNumber ?? 0;
            }

            int sortHint = referencePage?.PageNumber ?? int.MaxValue;
            groupPage.SetSortOrderHint(sortHint);
            groupPage.AdoptAppearanceFrom(referencePage);

            RegisterGroupPage(groupPage);
            PersistGroupToConfig(groupPage, referencePage, assignedId);
            ReplaceMembersWithGroup(pending, groupPage);
        }

        private static void ReplaceMembersWithGroup(PendingGroupCreation pending, GroupHandbookPage groupPage)
        {
            if (pending == null || groupPage == null)
            {
                return;
            }

            List<IFlatListItem> shownPages = pending.ShownPages;
            if (shownPages == null)
            {
                return;
            }

            foreach (GuiHandbookPage member in groupPage.Members)
            {
                if (member == null)
                {
                    continue;
                }

                shownPages.Remove(member);
            }

            int insertIndex = Math.Clamp(pending.InsertIndex, 0, shownPages.Count);
            int existingIndex = shownPages.IndexOf(groupPage);
            if (existingIndex >= 0)
            {
                shownPages.RemoveAt(existingIndex);
                if (existingIndex < insertIndex)
                {
                    insertIndex = Math.Max(0, insertIndex - 1);
                }
            }

            insertIndex = Math.Clamp(insertIndex, 0, shownPages.Count);
            shownPages.Insert(insertIndex, groupPage);

            if (pending.SearchList != null)
            {
                pending.SearchList.CalcTotalHeight();
            }

            if (pending.OverviewGui != null)
            {
                UpdateScrollArea(pending.OverviewGui, GetListHeight(pending.Dialog));
            }

            if (pending.OverviewGui != null && pending.SearchList != null)
            {
                CenterSearchListOnPage(pending.OverviewGui, pending.SearchList, groupPage);
            }

            AddRowHighlight(groupPage, CollapseHighlightColor, RowHighlightDurationMs);
        }

        private static bool TryMergeMembersWithExistingGroup(
            PendingGroupCreation pending,
            List<GuiHandbookPage> members,
            string normalizedName)
        {
            if (pending == null || members == null || members.Count == 0 || string.IsNullOrEmpty(normalizedName))
            {
                return false;
            }

            if (!TryFindExistingGroupForName(pending, normalizedName, out GroupHandbookPage existingGroup))
            {
                return false;
            }

            List<GuiHandbookPage> filteredMembers = members
                .Where(page => page != null)
                .ToList();

            if (filteredMembers.Count == 0)
            {
                return false;
            }

            HashSet<GuiHandbookPage> existingMembers = existingGroup.Members
                .Where(page => page != null)
                .ToHashSet();

            List<GuiHandbookPage> additions = filteredMembers
                .Where(page => !existingMembers.Contains(page))
                .ToList();

            bool addedMembers = additions.Count > 0 && existingGroup.AddMembers(additions);

            if (addedMembers)
            {
                foreach (GuiHandbookPage page in additions)
                {
                    RegisterMemberToGroup(page, existingGroup);
                }

                string hiddenCode = existingGroup.HiddenCategoryCode;
                if (!string.IsNullOrEmpty(hiddenCode))
                {
                    pagesByCategory[hiddenCode] = existingGroup.Members
                        .Where(page => page != null)
                        .ToList();
                }

                UpdateConfigEntryMembers(existingGroup);
            }

            ReplaceMembersWithGroup(pending, existingGroup);
            return true;
        }

        private static bool TryFindExistingGroupForName(
            PendingGroupCreation pending,
            string normalizedName,
            out GroupHandbookPage groupPage)
        {
            groupPage = null;

            if (pending == null || string.IsNullOrEmpty(normalizedName))
            {
                return false;
            }

            string displayCategory = pending.DisplayCategoryCode;
            string selectedCategory = pending.SelectedPage?.CategoryCode;

            List<string> searchCategories = BuildGroupSearchCategories(displayCategory, selectedCategory);

            foreach (string category in searchCategories)
            {
                if (!TryGetGroupPagesForCategory(category, out List<GroupHandbookPage> groups) || groups == null)
                {
                    continue;
                }

                foreach (GroupHandbookPage group in groups)
                {
                    if (group == null)
                    {
                        continue;
                    }

                    if (!MatchesPendingDisplayCategory(group, displayCategory, selectedCategory))
                    {
                        continue;
                    }

                    string existingNormalized = NormalizeGroupName(group.DisplayName);
                    if (string.Equals(existingNormalized, normalizedName, StringComparison.Ordinal))
                    {
                        groupPage = group;
                        return true;
                    }
                }
            }

            return false;
        }

        private static List<string> BuildGroupSearchCategories(string displayCategory, string selectedCategory)
        {
            var searchCategories = new List<string>();
            var seenCategories = new HashSet<string>();

            void AddCategory(string category)
            {
                if (seenCategories.Add(category))
                {
                    searchCategories.Add(category);
                }
            }

            if (string.IsNullOrEmpty(displayCategory))
            {
                if (!string.IsNullOrEmpty(selectedCategory))
                {
                    AddCategory(selectedCategory);
                }

                AddCategory(displayCategory);
            }
            else
            {
                AddCategory(displayCategory);

                if (!string.IsNullOrEmpty(selectedCategory))
                {
                    AddCategory(selectedCategory);
                }
            }

            AddCategory(null);

            return searchCategories;
        }

        private static bool MatchesPendingDisplayCategory(
            GroupHandbookPage groupPage,
            string pendingDisplayCategory,
            string selectedCategory)
        {
            if (groupPage == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(pendingDisplayCategory))
            {
                return string.Equals(groupPage.DisplayCategoryCode, pendingDisplayCategory, StringComparison.Ordinal);
            }

            if (!string.IsNullOrEmpty(selectedCategory))
            {
                if (groupPage.DisplayCategoryCode == null)
                {
                    return true;
                }

                return string.Equals(groupPage.DisplayCategoryCode, selectedCategory, StringComparison.Ordinal);
            }

            return groupPage.DisplayCategoryCode == null;
        }

        private static void RegisterGroupPage(GroupHandbookPage groupPage)
        {
            if (groupPage == null)
            {
                return;
            }

            if (!activeGroupPages.Contains(groupPage))
            {
                activeGroupPages.Add(groupPage);
            }

            string hiddenCode = groupPage.HiddenCategoryCode;
            if (!string.IsNullOrEmpty(hiddenCode))
            {
                groupByHiddenCategoryCode[hiddenCode] = groupPage;
                pagesByCategory[hiddenCode] = groupPage.Members.Where(page => page != null).ToList();
                displayNameByCategory[hiddenCode] = groupPage.DisplayName;
                translationKeyByCategory[hiddenCode] = null;
                tabBackgroundByCategory[hiddenCode] = HandbookCategoryColors.GetDefaultBackgroundColor();
            }

            foreach (GuiHandbookPage member in groupPage.Members)
            {
                RegisterMemberToGroup(member, groupPage);
            }

            AddGroupToDisplayCategory(groupPage.DisplayCategoryCode, groupPage);
        }

        private static void AddGroupToDisplayCategory(string categoryCode, GroupHandbookPage groupPage)
        {
            if (groupPage == null)
            {
                return;
            }

            string key = GetGroupDisplayKey(categoryCode);
            if (!groupPagesByDisplayCategory.TryGetValue(key, out List<GroupHandbookPage> list) || list == null)
            {
                list = new List<GroupHandbookPage>();
                groupPagesByDisplayCategory[key] = list;
            }

            if (!list.Contains(groupPage))
            {
                list.Add(groupPage);
            }
        }

        private static void RemoveGroupFromDisplayCategory(string categoryCode, GroupHandbookPage groupPage)
        {
            string key = GetGroupDisplayKey(categoryCode);
            if (!groupPagesByDisplayCategory.TryGetValue(key, out List<GroupHandbookPage> list) || list == null)
            {
                return;
            }

            list.Remove(groupPage);

            if (list.Count == 0)
            {
                groupPagesByDisplayCategory.Remove(key);
            }
        }

        private static void RemoveNavigationStatesFor(GuiDialogHandbook dialog, string hiddenCategoryCode)
        {
            if (dialog == null || string.IsNullOrEmpty(hiddenCategoryCode))
            {
                return;
            }

            if (!groupNavigationHistory.TryGetValue(dialog, out Stack<GroupNavigationState> stack) || stack.Count == 0)
            {
                return;
            }

            Stack<GroupNavigationState> retained = new();
            while (stack.Count > 0)
            {
                GroupNavigationState state = stack.Pop();
                if (!string.Equals(state.HiddenCategoryCode, hiddenCategoryCode, StringComparison.Ordinal))
                {
                    retained.Push(state);
                }
            }

            while (retained.Count > 0)
            {
                stack.Push(retained.Pop());
            }

            if (stack.Count == 0)
            {
                groupNavigationHistory.Remove(dialog);
            }
        }

        private static bool TryRemoveGroupPage(
            GuiDialogHandbook dialog,
            GuiComposer overviewGui,
            GuiElementFlatList searchList,
            GroupHandbookPage groupPage)
        {
            if (dialog == null || groupPage == null)
            {
                return false;
            }

            if (ShownPagesField?.GetValue(dialog) is not List<IFlatListItem> shownPages || shownPages.Count == 0)
            {
                return false;
            }

            int index = shownPages.IndexOf(groupPage);
            if (index < 0)
            {
                return false;
            }

            shownPages.RemoveAt(index);

            List<GuiHandbookPage> members = groupPage.Members.Where(page => page != null).ToList();
            int insertIndex = index;
            foreach (GuiHandbookPage member in members)
            {
                shownPages.Insert(insertIndex++, member);
            }

            if (searchList != null)
            {
                searchList.CalcTotalHeight();
            }

            if (overviewGui != null)
            {
                UpdateScrollArea(overviewGui, GetListHeight(dialog));
            }

            if (overviewGui != null && searchList != null && members.Count > 0)
            {
                CenterSearchListOnPage(overviewGui, searchList, members[0]);
            }

            AddRowHighlights(members, RestoreHighlightColor, RowHighlightDurationMs);

            RemoveNavigationStatesFor(dialog, groupPage.HiddenCategoryCode);
            pendingGroupCreations.Remove(dialog);

            UnregisterGroupPage(groupPage);
            RemoveGroupFromConfig(groupPage);
            groupPage.DisposeTexture();

            return true;
        }

        private static void HandleEmptyGroupRemoval(
            GuiDialogHandbook dialog,
            GuiComposer overviewGui,
            GuiElementFlatList searchList,
            GroupHandbookPage groupPage,
            List<IFlatListItem> shownPages)
        {
            if (groupPage == null)
            {
                return;
            }

            if (shownPages != null)
            {
                RemovePageFromShownPages(shownPages, groupPage);
            }

            RemoveNavigationStatesFor(dialog, groupPage.HiddenCategoryCode);
            pendingGroupCreations.Remove(dialog);

            UnregisterGroupPage(groupPage);
            RemoveGroupFromConfig(groupPage);
            groupPage.DisposeTexture();

            searchList ??= overviewGui?.GetFlatList("stacklist");
            searchList?.CalcTotalHeight();

            if (overviewGui != null)
            {
                UpdateScrollArea(overviewGui, GetListHeight(dialog));
            }
        }

        private static void RemovePageFromShownPages(List<IFlatListItem> shownPages, GuiHandbookPage memberPage)
        {
            if (shownPages == null || shownPages.Count == 0 || memberPage == null)
            {
                return;
            }

            string memberCode = memberPage.PageCode;

            for (int i = shownPages.Count - 1; i >= 0; i--)
            {
                if (shownPages[i] is not GuiHandbookPage page)
                {
                    continue;
                }

                if (ReferenceEquals(page, memberPage))
                {
                    shownPages.RemoveAt(i);
                    return;
                }

                if (!string.IsNullOrEmpty(memberCode)
                    && !string.IsNullOrEmpty(page.PageCode)
                    && string.Equals(page.PageCode, memberCode, StringComparison.OrdinalIgnoreCase))
                {
                    shownPages.RemoveAt(i);
                    return;
                }
            }
        }

        private static void UnregisterGroupPage(GroupHandbookPage groupPage)
        {
            if (groupPage == null)
            {
                return;
            }

            activeGroupPages.Remove(groupPage);

            foreach (GuiHandbookPage member in groupPage.Members)
            {
                UnregisterMemberFromGroup(member, groupPage);
            }

            string hiddenCode = groupPage.HiddenCategoryCode;
            if (!string.IsNullOrEmpty(hiddenCode))
            {
                groupByHiddenCategoryCode.Remove(hiddenCode);
                pagesByCategory.Remove(hiddenCode);
                displayNameByCategory.Remove(hiddenCode);
                translationKeyByCategory.Remove(hiddenCode);
                tabBackgroundByCategory.Remove(hiddenCode);
            }

            RemoveGroupFromDisplayCategory(groupPage.DisplayCategoryCode, groupPage);
        }

        private static void PushGroupNavigation(GuiDialogHandbook dialog, string hiddenCategoryCode, string previousCategoryCode, float scrollPosition)
        {
            if (dialog == null || string.IsNullOrEmpty(hiddenCategoryCode))
            {
                return;
            }

            if (!groupNavigationHistory.TryGetValue(dialog, out Stack<GroupNavigationState> stack))
            {
                stack = new Stack<GroupNavigationState>();
                groupNavigationHistory[dialog] = stack;
            }

            stack.Push(new GroupNavigationState(previousCategoryCode, hiddenCategoryCode, scrollPosition));
        }

        internal static bool HasGroupBackNavigation(GuiDialogHandbook dialog)
        {
            return TryGetActiveGroupNavigationState(dialog, out _);
        }

        private static bool TryGetActiveGroupNavigationState(GuiDialogHandbook dialog, out GroupNavigationState state)
        {
            state = null;

            if (dialog == null)
            {
                return false;
            }

            if (!groupNavigationHistory.TryGetValue(dialog, out Stack<GroupNavigationState> stack) || stack == null || stack.Count == 0)
            {
                return false;
            }

            if (!TryGetActiveGroupHiddenCategoryCode(dialog, stack, out string hiddenCategoryCode))
            {
                return false;
            }

            foreach (GroupNavigationState candidate in stack)
            {
                if (candidate != null && string.Equals(candidate.HiddenCategoryCode, hiddenCategoryCode, StringComparison.Ordinal))
                {
                    state = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool HasBrowseHistory(GuiDialogHandbook dialog)
        {
            if (dialog == null || BrowseHistoryField == null)
            {
                return false;
            }

            if (BrowseHistoryField.GetValue(dialog) is ICollection history)
            {
                return history.Count > 0;
            }

            return false;
        }

        internal static bool ShouldEnableBackButton(GuiDialogHandbook dialog, bool baseEnabled)
        {
            if (dialog == null)
            {
                return baseEnabled;
            }

            return baseEnabled || TryGetActiveGroupNavigationState(dialog, out _);
        }

        internal static void UpdateBackButtonState(GuiDialogHandbook dialog)
        {
            if (dialog == null)
            {
                return;
            }

            GuiComposer overviewGui = OverviewGuiField?.GetValue(dialog) as GuiComposer;
            GuiElementTextButton backButton = overviewGui?.GetButton("backButton");
            if (backButton == null)
            {
                return;
            }

            bool hasHistory = HasBrowseHistory(dialog);
            bool isViewingGroupPage = TryGetActiveGroupNavigationState(dialog, out _);
            bool shouldEnable = hasHistory || isViewingGroupPage;
            if (backButton.Enabled != shouldEnable)
            {
                backButton.Enabled = shouldEnable;
            }

            backButton.SetActive(false);
        }

        internal static bool TryHandleGroupPageMouseDown(GuiDialogHandbook dialog, GuiHandbookPage selectedPage)
        {
            if (dialog == null || selectedPage is not GroupHandbookPage groupPage)
            {
                return false;
            }

            if (ShownPagesField?.GetValue(dialog) is not List<IFlatListItem> shownPages)
            {
                return false;
            }

            if (!shownPages.Contains(groupPage))
            {
                return false;
            }

            return TryActivateGroupPage(dialog, groupPage);
        }

        internal static bool TryHandleGroupPageClick(GuiDialogHandbook dialog, int index)
        {
            if (dialog == null)
            {
                return false;
            }

            if (ShownPagesField?.GetValue(dialog) is not List<IFlatListItem> shownPages)
            {
                return false;
            }

            if (index < 0 || index >= shownPages.Count)
            {
                return false;
            }

            if (shownPages[index] is not GroupHandbookPage groupPage)
            {
                return false;
            }

            return TryActivateGroupPage(dialog, groupPage);
        }

        private static bool TryActivateGroupPage(GuiDialogHandbook dialog, GroupHandbookPage groupPage)
        {
            if (dialog == null || groupPage == null)
            {
                return false;
            }

            GuiComposer overviewGui = OverviewGuiField?.GetValue(dialog) as GuiComposer;
            GuiElementScrollbar scrollbar = overviewGui?.GetScrollbar("scrollbar");
            float scrollPosition = scrollbar?.CurrentYPosition ?? 0f;

            string previousCategory = DetermineGroupReturnCategory(groupPage, dialog.currentCatgoryCode);

            PushGroupNavigation(dialog, groupPage.HiddenCategoryCode, previousCategory, scrollPosition);

            dialog.currentCatgoryCode = groupPage.HiddenCategoryCode;
            dialog.FilterItems();

            RestoreOverviewScroll(dialog, 0f);

            UpdateBackButtonState(dialog);

            return true;
        }

        private static string DetermineGroupReturnCategory(GroupHandbookPage groupPage, string fallbackCategoryCode)
        {
            string displayCategory = ResolveGroupDisplayCategoryCode(groupPage);

            if (!string.IsNullOrEmpty(displayCategory))
            {
                return displayCategory;
            }

            return fallbackCategoryCode;
        }

        private static string ResolveGroupDisplayCategoryCode(GroupHandbookPage groupPage)
        {
            if (groupPage == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(groupPage.DisplayCategoryCode))
            {
                return groupPage.DisplayCategoryCode;
            }

            string hiddenCode = groupPage.HiddenCategoryCode;
            if (!string.IsNullOrEmpty(hiddenCode)
                && groupByHiddenCategoryCode.TryGetValue(hiddenCode, out GroupHandbookPage registered)
                && registered != null
                && !string.IsNullOrEmpty(registered.DisplayCategoryCode))
            {
                return registered.DisplayCategoryCode;
            }

            foreach (KeyValuePair<string, List<GroupHandbookPage>> entry in groupPagesByDisplayCategory)
            {
                List<GroupHandbookPage> groups = entry.Value;
                if (groups == null)
                {
                    continue;
                }

                foreach (GroupHandbookPage candidate in groups)
                {
                    if (!ReferenceEquals(candidate, groupPage))
                    {
                        continue;
                    }

                    string categoryCode = ResolveDisplayCategoryFromKey(entry.Key);
                    if (!string.IsNullOrEmpty(categoryCode))
                    {
                        return categoryCode;
                    }
                }
            }

            return null;
        }

        private static string ResolveDisplayCategoryFromKey(string key)
        {
            if (string.IsNullOrEmpty(key) || string.Equals(key, EverythingCategoryKey, StringComparison.Ordinal))
            {
                return null;
            }

            return key;
        }

        internal static bool TryHandleGroupBackNavigation(GuiDialogHandbook dialog)
        {
            if (dialog == null)
            {
                return false;
            }

            if (!groupNavigationHistory.TryGetValue(dialog, out Stack<GroupNavigationState> stack) || stack.Count == 0)
            {
                return false;
            }

            Stack<GroupNavigationState> retained = new();
            GroupNavigationState target = null;

            if (!TryGetActiveGroupHiddenCategoryCode(dialog, stack, out string hiddenCategoryCode))
            {
                return false;
            }

            while (stack.Count > 0)
            {
                GroupNavigationState state = stack.Pop();
                if (target == null && string.Equals(hiddenCategoryCode, state.HiddenCategoryCode, StringComparison.Ordinal))
                {
                    target = state;
                    break;
                }

                retained.Push(state);
            }

            while (retained.Count > 0)
            {
                stack.Push(retained.Pop());
            }

            if (stack.Count == 0)
            {
                groupNavigationHistory.Remove(dialog);
            }

            if (target == null)
            {
                return false;
            }

            string previousCategory = target.PreviousCategoryCode;
            if (!string.IsNullOrEmpty(previousCategory))
            {
                dialog.selectTab(previousCategory);
            }
            else
            {
                dialog.currentCatgoryCode = previousCategory;
                dialog.FilterItems();
            }

            RestoreOverviewScroll(dialog, target.ScrollPosition);

            if (!string.IsNullOrEmpty(previousCategory))
            {
                EnsureActiveTabMatchesCategory(dialog, previousCategory);
            }

            UpdateBackButtonState(dialog);

            return true;
        }

        private static bool TryGetActiveGroupHiddenCategoryCode(
            GuiDialogHandbook dialog,
            Stack<GroupNavigationState> stack,
            out string hiddenCategoryCode)
        {
            hiddenCategoryCode = null;

            if (dialog == null || stack == null || stack.Count == 0)
            {
                return false;
            }

            string currentCode = dialog.currentCatgoryCode;
            if (!string.IsNullOrEmpty(currentCode)
                && stack.Any(state => state != null
                    && string.Equals(state.HiddenCategoryCode, currentCode, StringComparison.Ordinal)))
            {
                hiddenCategoryCode = currentCode;
                return true;
            }

            if (TryGetGroupHiddenCategoryFromBrowseHistory(dialog, out string historyHiddenCode)
                && stack.Any(state => state != null
                    && string.Equals(state.HiddenCategoryCode, historyHiddenCode, StringComparison.Ordinal)))
            {
                hiddenCategoryCode = historyHiddenCode;
                return true;
            }

            return false;
        }

        private static bool TryGetGroupHiddenCategoryFromBrowseHistory(
            GuiDialogHandbook dialog,
            out string hiddenCategoryCode)
        {
            hiddenCategoryCode = null;

            if (dialog == null || BrowseHistoryField == null)
            {
                return false;
            }

            try
            {
                if (BrowseHistoryField.GetValue(dialog) is Stack<BrowseHistoryElement> history && history.Count > 0)
                {
                    BrowseHistoryElement top = history.Peek();
                    if (top?.Page is GroupHandbookPage groupPage)
                    {
                        hiddenCategoryCode = groupPage.HiddenCategoryCode;
                        return !string.IsNullOrEmpty(hiddenCategoryCode);
                    }
                }
            }
            catch
            {
                // Ignore reflection failures and fall back to the default behaviour.
            }

            return false;
        }

        private static void RestoreOverviewScroll(GuiDialogHandbook dialog, float scrollPosition)
        {
            if (dialog == null)
            {
                return;
            }

            GuiComposer overviewGui = OverviewGuiField?.GetValue(dialog) as GuiComposer;
            if (overviewGui == null)
            {
                return;
            }

            UpdateScrollArea(overviewGui, GetListHeight(dialog));

            GuiElementFlatList list = overviewGui.GetFlatList("stacklist");
            GuiElementScrollbar scrollbar = overviewGui.GetScrollbar("scrollbar");
            if (list == null || scrollbar == null)
            {
                return;
            }

            ElementBounds listBounds = list.Bounds;
            if (listBounds != null && listBounds.RequiresRecalculation)
            {
                listBounds.CalcWorldBounds();
            }

            ElementBounds insideBounds = list.insideBounds;
            if (insideBounds != null && insideBounds.RequiresRecalculation)
            {
                insideBounds.CalcWorldBounds();
            }

            double visibleHeight = listBounds?.InnerHeight ?? 0.0;
            double totalHeight = insideBounds?.fixedHeight ?? 0.0;
            float clamped = float.IsNaN(scrollPosition)
                ? 0f
                : Math.Clamp(scrollPosition, 0f, (float)Math.Max(0.0, totalHeight - visibleHeight));

            scrollbar.CurrentYPosition = clamped;
            scrollbar.TriggerChanged();
        }

        private static void EnsureActiveTabMatchesCategory(GuiDialogHandbook dialog, string categoryCode)
        {
            if (dialog == null || string.IsNullOrEmpty(categoryCode))
            {
                return;
            }

            GuiComposer overviewGui = OverviewGuiField?.GetValue(dialog) as GuiComposer;
            if (overviewGui?.GetVerticalTab("verticalTabs") is not GuiElementVerticalTabs tabsElement)
            {
                return;
            }

            if (VerticalTabsField?.GetValue(tabsElement) is not GuiTab[] tabs || tabs.Length == 0)
            {
                return;
            }

            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] is HandbookTab tab && string.Equals(tab.CategoryCode, categoryCode, StringComparison.OrdinalIgnoreCase))
                {
                    tabsElement.SetValue(i, triggerHandler: false);
                    return;
                }
            }
        }

        private static bool ShouldHidePageForCategory(GuiHandbookPage page, string categoryCode)
        {
            if (page == null)
            {
                return false;
            }

            if (!groupsByMemberPage.TryGetValue(page, out List<GroupHandbookPage> groups) || groups == null || groups.Count == 0)
            {
                return false;
            }

            foreach (GroupHandbookPage group in groups)
            {
                if (group == null)
                {
                    continue;
                }

                string hiddenCode = group.HiddenCategoryCode;
                if (!string.IsNullOrEmpty(hiddenCode)
                    && string.Equals(categoryCode, hiddenCode, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            string targetKey = GetGroupDisplayKey(categoryCode);

            foreach (GroupHandbookPage group in groups)
            {
                if (group == null)
                {
                    continue;
                }

                string displayKey = GetGroupDisplayKey(group.DisplayCategoryCode);
                if (string.Equals(displayKey, targetKey, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AppendGroupPages(string categoryCode, SearchQuery searchQuery, List<WeightedHandbookPage> weightedPages)
        {
            if (weightedPages == null)
            {
                return;
            }

            if (!TryGetGroupPagesForCategory(categoryCode, out List<GroupHandbookPage> groups) || groups.Count == 0)
            {
                return;
            }

            var existingPages = new HashSet<GuiHandbookPage>(weightedPages
                .Where(weighted => weighted.Page != null)
                .Select(weighted => weighted.Page));

            foreach (GroupHandbookPage group in groups)
            {
                if (group == null)
                {
                    continue;
                }

                if (!MatchesSearchQuery(group, searchQuery, out float weight))
                {
                    continue;
                }

                if (!existingPages.Add(group))
                {
                    continue;
                }

                weightedPages.Add(new WeightedHandbookPage
                {
                    Page = group,
                    Weight = weight,
                    SortHint = group?.SortOrderHint ?? int.MaxValue
                });
            }
        }

        private static bool TryGetGroupPagesForCategory(string categoryCode, out List<GroupHandbookPage> groups)
        {
            string key = GetGroupDisplayKey(categoryCode);
            if (!groupPagesByDisplayCategory.TryGetValue(key, out List<GroupHandbookPage> existing) || existing == null)
            {
                groups = null;
                return false;
            }

            groups = existing.Where(group => group != null).ToList();
            return groups.Count > 0;
        }

        internal static bool TryGetGroupByHiddenCode(string hiddenCategoryCode, out GroupHandbookPage groupPage)
        {
            if (!string.IsNullOrEmpty(hiddenCategoryCode)
                && groupByHiddenCategoryCode.TryGetValue(hiddenCategoryCode, out GroupHandbookPage existing)
                && existing != null)
            {
                groupPage = existing;
                return true;
            }

            groupPage = null;
            return false;
        }

        internal static bool TryGetActiveGroupContext(
            GuiDialogHandbook dialog,
            string hiddenCategoryCode,
            out GroupHandbookPage groupPage,
            out string displayCategoryCode)
        {
            groupPage = null;
            displayCategoryCode = null;

            if (string.IsNullOrEmpty(hiddenCategoryCode))
            {
                return false;
            }

            string contextCategory = GetActiveGroupDisplayCategory(dialog, hiddenCategoryCode);
            if (!string.IsNullOrEmpty(contextCategory)
                && TryFindGroupForDisplayCategory(contextCategory, hiddenCategoryCode, out GroupHandbookPage contextualGroup))
            {
                groupPage = contextualGroup;
                displayCategoryCode = contextCategory;
                return true;
            }

            if (TryGetGroupByHiddenCode(hiddenCategoryCode, out GroupHandbookPage existingGroup))
            {
                groupPage = existingGroup;
                displayCategoryCode = existingGroup?.DisplayCategoryCode;
                return true;
            }

            return false;
        }

        private static bool TryFindGroupForDisplayCategory(
            string displayCategoryCode,
            string hiddenCategoryCode,
            out GroupHandbookPage groupPage)
        {
            groupPage = null;

            string key = GetGroupDisplayKey(displayCategoryCode);
            if (!groupPagesByDisplayCategory.TryGetValue(key, out List<GroupHandbookPage> groups) || groups == null)
            {
                return false;
            }

            foreach (GroupHandbookPage candidate in groups)
            {
                if (candidate == null)
                {
                    continue;
                }

                if (string.Equals(candidate.HiddenCategoryCode, hiddenCategoryCode, StringComparison.Ordinal))
                {
                    groupPage = candidate;
                    return true;
                }
            }

            return false;
        }

        private static string GetActiveGroupDisplayCategory(GuiDialogHandbook dialog, string hiddenCategoryCode)
        {
            if (dialog == null || string.IsNullOrEmpty(hiddenCategoryCode))
            {
                return null;
            }

            if (!groupNavigationHistory.TryGetValue(dialog, out Stack<GroupNavigationState> stack) || stack == null || stack.Count == 0)
            {
                return null;
            }

            foreach (GroupNavigationState state in stack)
            {
                if (state == null)
                {
                    continue;
                }

                if (string.Equals(state.HiddenCategoryCode, hiddenCategoryCode, StringComparison.Ordinal))
                {
                    return state.PreviousCategoryCode;
                }
            }

            return null;
        }

        private static string GetGroupDisplayKey(string categoryCode)
        {
            return categoryCode ?? EverythingCategoryKey;
        }

        private static void RestoreGroupCategories()
        {
            if (activeGroupPages.Count == 0)
            {
                return;
            }

            foreach (GroupHandbookPage group in activeGroupPages)
            {
                if (group == null)
                {
                    continue;
                }

                string hiddenCode = group.HiddenCategoryCode;
                if (string.IsNullOrEmpty(hiddenCode))
                {
                    continue;
                }

                List<GuiHandbookPage> members = group.Members.Where(page => page != null).ToList();
                pagesByCategory[hiddenCode] = members;
                displayNameByCategory[hiddenCode] = group.DisplayName;
                translationKeyByCategory[hiddenCode] = null;
                tabBackgroundByCategory[hiddenCode] = HandbookCategoryColors.GetDefaultBackgroundColor();

                foreach (GuiHandbookPage member in members)
                {
                    RegisterMemberToGroup(member, group);
                }

                groupByHiddenCategoryCode[hiddenCode] = group;

                AddGroupToDisplayCategory(group.DisplayCategoryCode, group);
            }
        }

        private static void ClearGroupData()
        {
            activeGroupPages.Clear();
            groupsByMemberPage.Clear();
            groupByHiddenCategoryCode.Clear();
            groupPagesByDisplayCategory.Clear();
            pendingGroupCreations.Clear();
            groupNavigationHistory.Clear();
            ResetNextGroupIdFromConfig();
        }

        private static void CenterSearchListOnPage(GuiComposer overviewGui, GuiElementFlatList searchList, GuiHandbookPage selectedPage)
        {
            if (overviewGui == null || searchList == null || selectedPage == null)
            {
                return;
            }

            GuiElementScrollbar scrollbar = overviewGui.GetScrollbar("scrollbar");
            if (scrollbar == null)
            {
                return;
            }

            List<IFlatListItem> elements = searchList.Elements;
            if (elements == null || elements.Count == 0)
            {
                return;
            }

            ElementBounds listBounds = searchList.Bounds;
            if (listBounds != null && listBounds.RequiresRecalculation)
            {
                listBounds.CalcWorldBounds();
            }

            ElementBounds insideBounds = searchList.insideBounds;
            if (insideBounds != null && insideBounds.RequiresRecalculation)
            {
                insideBounds.CalcWorldBounds();
            }

            double visibleHeight = listBounds?.InnerHeight ?? 0.0;
            double totalHeight = insideBounds?.fixedHeight ?? 0.0;
            double maxScroll = Math.Max(0.0, totalHeight - visibleHeight);

            double rowHeight = searchList.unscaledCellHeight + searchList.unscaledCellSpacing;
            if (rowHeight <= 0.0 || visibleHeight <= 0.0)
            {
                return;
            }

            double accumulatedHeight = 0.0;

            foreach (IFlatListItem element in elements)
            {
                if (!element.Visible)
                {
                    continue;
                }

                double rowCenter = accumulatedHeight + (rowHeight * 0.5);

                if (ReferenceEquals(element, selectedPage))
                {
                    double target = rowCenter - (visibleHeight * 0.5);
                    float clamped = (float)Math.Clamp(target, 0.0, maxScroll);
                    scrollbar.CurrentYPosition = clamped;
                    scrollbar.TriggerChanged();
                    return;
                }

                accumulatedHeight += rowHeight;
            }
        }

        private static void AddRowHighlight(GuiHandbookPage page, int color, long durationMs)
        {
            if (page == null || durationMs <= 0)
            {
                return;
            }

            long now = GetCurrentMilliseconds();
            rowHighlights[page] = new RowHighlight(color, now + durationMs);
        }

        private static void AddRowHighlights(IEnumerable<GuiHandbookPage> pages, int color, long durationMs)
        {
            if (pages == null)
            {
                return;
            }

            foreach (GuiHandbookPage page in pages)
            {
                AddRowHighlight(page, color, durationMs);
            }
        }

        internal static void RenderFlatListHighlights(GuiElementFlatList list, ICoreClientAPI api)
        {
            if (list == null || api == null || rowHighlights.Count == 0)
            {
                return;
            }

            ElementBounds bounds = list.Bounds;
            ElementBounds insideBounds = list.insideBounds;
            List<IFlatListItem> elements = list.Elements;

            if (bounds == null || insideBounds == null || elements == null || elements.Count == 0)
            {
                return;
            }

            double width = bounds.InnerWidth;
            if (width <= 0.0)
            {
                return;
            }

            double baseX = bounds.absX;
            double baseY = bounds.absY;
            double rowOffset = insideBounds.absY;
            double rowPadding = GuiElement.scaled(list.unscalledYPad);
            double rowHeight = GuiElement.scaled(list.unscaledCellHeight);
            double rowStep = GuiElement.scaled(list.unscaledCellHeight + list.unscaledCellSpacing);

            long now = GetCurrentMilliseconds();

            foreach (IFlatListItem element in elements)
            {
                if (!element.Visible)
                {
                    continue;
                }

                if (element is GuiHandbookPage page && TryGetHighlightColor(page, now, out int color))
                {
                    float rowCenter = (float)(5.0 + baseY + rowOffset);
                    float top = rowCenter - (float)rowPadding;
                    api.Render.RenderRectangle((float)baseX, top, 500f, (float)width, (float)rowHeight, color);
                }

                rowOffset += rowStep;
            }

            RemoveExpiredHighlights(now);
        }

        private static long GetCurrentMilliseconds()
        {
            return capi?.ElapsedMilliseconds ?? Environment.TickCount64;
        }

        private static bool TryGetHighlightColor(GuiHandbookPage page, long currentTime, out int color)
        {
            color = 0;

            if (page == null)
            {
                return false;
            }

            if (!rowHighlights.TryGetValue(page, out RowHighlight highlight) || highlight == null)
            {
                return false;
            }

            if (highlight.ExpiresAtMs <= currentTime)
            {
                rowHighlights.Remove(page);
                return false;
            }

            color = highlight.Color;
            return true;
        }

        private static void RemoveExpiredHighlights(long currentTime)
        {
            if (rowHighlights.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<GuiHandbookPage, RowHighlight> entry in rowHighlights.ToList())
            {
                RowHighlight highlight = entry.Value;
                if (highlight == null || highlight.ExpiresAtMs <= currentTime)
                {
                    rowHighlights.Remove(entry.Key);
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
            string searchableContent = GetSearchableContent(page, titleData);
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

        private static string GetSearchableContent(GuiHandbookPage page, PageTitleData titleData)
        {
            string content = titleData.SearchableContent;

            if (page is GroupHandbookPage groupPage)
            {
                string groupSearch = groupPage.SearchableText;
                if (!string.IsNullOrWhiteSpace(groupSearch))
                {
                    return string.Concat(content, " ", groupSearch).Trim();
                }
            }

            return content;
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

        internal static string GetLocalizedPageTitle(GuiHandbookPage page)
        {
            if (page == null)
            {
                return string.Empty;
            }

            string title = GetRawTitle(page, allowCachedItemStackTitle: true);
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
                GroupHandbookPage groupPage when !string.IsNullOrWhiteSpace(groupPage.DisplayName) => groupPage.DisplayName,
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
                || config.DisableTutorialTab != defaultConfig.DisableTutorialTab
                || config.DisableBlocksAndItemsTab != defaultConfig.DisableBlocksAndItemsTab
                || config.DisableGuidesTab != defaultConfig.DisableGuidesTab
                || config.DisableOriginalSearchButton != defaultConfig.DisableOriginalSearchButton
                || config.DisableDragAndDrop != defaultConfig.DisableDragAndDrop
                || config.EnableGroupCreationHotkeys != defaultConfig.EnableGroupCreationHotkeys
                || config.CreateVariantCategories != defaultConfig.CreateVariantCategories
                || config.CreateEverythingGrouped != defaultConfig.CreateEverythingGrouped)
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

        internal static string GetCategoryCodeFromDisplayName(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return null;
            }

            string sanitized = Sanitize(categoryName);
            if (string.IsNullOrEmpty(sanitized))
            {
                return null;
            }

            return string.Concat(CategoryCodePrefix, sanitized);
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

        internal static bool IsCreateCategoryPromptOpen()
        {
            return createCategoryPromptOpen;
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

        private static void PeriodicPositionSave(float deltaTime)
        {
            HandbookCategoryPatches.SaveAllOpenHandbookPositions();
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

        private static double GetListHeight(GuiDialogHandbook dialog)
        {
            if (dialog == null || ListHeightField == null)
            {
                return 0.0;
            }

            try
            {
                object value = ListHeightField.GetValue(dialog);
                if (value is double doubleValue)
                {
                    return doubleValue;
                }

                if (value is float floatValue)
                {
                    return floatValue;
                }
            }
            catch
            {
                // Ignore reflection errors and fall back to zero.
            }

            return 0.0;
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

        private static string NormalizeGroupName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            return name.Trim().ToLowerInvariant();
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

        private static List<string> ExtractOrderedWordsPreservingCase(string text)
        {
            List<string> words = new();

            if (string.IsNullOrWhiteSpace(text))
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
                else if (builder.Length > 0)
                {
                    words.Add(builder.ToString());
                    builder.Clear();
                }
            }

            if (builder.Length > 0)
            {
                words.Add(builder.ToString());
            }

            return words;
        }

        private static List<string> ExtractOrderedPageCodeWords(string pageCode)
        {
            List<string> words = new();

            if (string.IsNullOrWhiteSpace(pageCode))
            {
                return words;
            }

            string[] segments = pageCode.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string segment in segments)
            {
                string trimmed = segment.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    words.Add(trimmed);
                }
            }

            return words;
        }

        private static bool TitlesMatchAllowingOneWordDifference(IList<string> selectedWords, IList<string> candidateWords, out int differingIndex)
        {
            differingIndex = -1;

            if (selectedWords == null || candidateWords == null || selectedWords.Count != candidateWords.Count)
            {
                return false;
            }

            for (int i = 0; i < selectedWords.Count; i++)
            {
                string left = selectedWords[i];
                string right = candidateWords[i];

                if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (differingIndex >= 0)
                {
                    differingIndex = -1;
                    return false;
                }

                differingIndex = i;
            }

            return true;
        }

        private static string BuildOverrideTitle(string originalTitle, IList<string> originalWords, int? removalIndex)
        {
            string trimmed = (originalTitle ?? string.Empty).Trim();

            if (removalIndex.HasValue && removalIndex.Value >= 0 && originalWords != null && removalIndex.Value < originalWords.Count)
            {
                IEnumerable<string> filtered = originalWords
                    .Where((word, index) => index != removalIndex.Value && !string.IsNullOrEmpty(word));
                string rebuilt = string.Join(" ", filtered).Trim();
                if (!string.IsNullOrWhiteSpace(rebuilt))
                {
                    trimmed = rebuilt;
                }
            }

            trimmed = CapitalizeFirstLetter(trimmed);

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return "(*)";
            }

            return trimmed + " (*)";
        }

        private static string CapitalizeFirstLetter(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            char first = value[0];
            char capitalized = char.ToUpperInvariant(first);

            if (value.Length == 1)
            {
                return capitalized.ToString();
            }

            if (char.IsUpper(first))
            {
                return value;
            }

            return capitalized + value.Substring(1);
        }

        private static void ApplyPageTitleOverride(GuiHandbookPage page, string newTitle)
        {
            if (page == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(newTitle))
            {
                return;
            }

            ICoreClientAPI api = capi;
            if (api == null)
            {
                return;
            }

            try
            {
                switch (page)
                {
                    case GuiHandbookGroupedItemstackPage groupedPage:
                        groupedPage.Name = newTitle;
                        groupedPage.Texture?.Dispose();
                        groupedPage.Texture = new TextTextureUtil(api).GenTextTexture(newTitle, CairoFont.WhiteSmallText());
                        break;
                    case GuiHandbookItemStackPage itemPage:
                        itemPage.Texture?.Dispose();
                        itemPage.Texture = new TextTextureUtil(api).GenTextTexture(newTitle, CairoFont.WhiteSmallText());
                        break;
                    case GuiHandbookMealRecipePage mealPage:
                        mealPage.Title = newTitle;
                        mealPage.Texture?.Dispose();
                        mealPage.Texture = new TextTextureUtil(api).GenTextTexture(newTitle, CairoFont.WhiteSmallText());
                        break;
                }
            }
            catch
            {
                // Ignore texture regeneration failures to avoid crashing the UI.
            }
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
        private static bool NormalizeGroupConfigEntry(
            HandbookGroupConfigEntry entry,
            HashSet<int> seenIds,
            HashSet<string> seenHiddenCodes)
        {
            bool changed = false;

            entry.DisplayName ??= string.Empty;

            int id = entry.Id;
            if (id <= 0 && !TryParseGroupIdSuffix(entry.HiddenCategoryCode, out id) && !TryParseGroupIdSuffix(entry.PageCode, out id))
            {
                id = GetNextAvailableGroupId(seenIds);
                changed = true;
            }

            if (id <= 0)
            {
                id = GetNextAvailableGroupId(seenIds);
                changed = true;
            }

            entry.Id = id;

            if (!seenIds.Add(entry.Id))
            {
                int newId = GetNextAvailableGroupId(seenIds);
                entry.Id = newId;
                seenIds.Add(newId);
                changed = true;
            }

            string sanitizedName = Sanitize(string.IsNullOrWhiteSpace(entry.DisplayName) ? DefaultGroupName : entry.DisplayName);
            if (string.IsNullOrEmpty(sanitizedName))
            {
                sanitizedName = "group";
            }

            string preferredHiddenCode = string.Concat(GroupCategoryCodePrefix, sanitizedName, "-", entry.Id.ToString("D4"));
            if (string.IsNullOrEmpty(entry.HiddenCategoryCode) || !seenHiddenCodes.Add(entry.HiddenCategoryCode))
            {
                entry.HiddenCategoryCode = preferredHiddenCode;
                seenHiddenCodes.Add(entry.HiddenCategoryCode);
                changed = true;
            }

            if (string.IsNullOrEmpty(entry.PageCode))
            {
                entry.PageCode = string.Concat(GroupPageCodePrefix, sanitizedName, "-", entry.Id.ToString("D4"));
                changed = true;
            }

            List<string> normalizedMembers = entry.MemberPageCodes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!entry.MemberPageCodes.SequenceEqual(normalizedMembers, StringComparer.OrdinalIgnoreCase))
            {
                entry.MemberPageCodes = normalizedMembers;
                changed = true;
            }

            if (!string.IsNullOrEmpty(entry.WeightSourcePageCode))
            {
                string trimmed = entry.WeightSourcePageCode.Trim();
                if (!string.Equals(entry.WeightSourcePageCode, trimmed, StringComparison.Ordinal))
                {
                    entry.WeightSourcePageCode = trimmed;
                    changed = true;
                }
            }

            if (entry.SortOrderHint < 0)
            {
                entry.SortOrderHint = int.MaxValue;
                changed = true;
            }

            if (entry.PageNumber < 0)
            {
                entry.PageNumber = 0;
                changed = true;
            }

            return changed;
        }

        private static int GetNextAvailableGroupId(HashSet<int> seenIds)
        {
            int candidate = 1;

            if (seenIds != null && seenIds.Count > 0)
            {
                candidate = seenIds.Max() + 1;
            }

            if (seenIds == null)
            {
                return candidate;
            }

            while (seenIds.Contains(candidate))
            {
                candidate++;
            }

            return candidate;
        }

        private static void ResetNextGroupIdFromConfig()
        {
            nextGroupId = ComputeNextGroupIdFromConfig();
        }

        private static int ComputeNextGroupIdFromConfig()
        {
            if (groupConfig?.Groups == null || groupConfig.Groups.Count == 0)
            {
                return 1;
            }

            int maxId = 0;
            foreach (HandbookGroupConfigEntry entry in groupConfig.Groups)
            {
                int id = ExtractGroupIdFromEntry(entry);
                if (id > maxId)
                {
                    maxId = id;
                }
            }

            return maxId > 0 ? maxId + 1 : 1;
        }

        private static int ExtractGroupIdFromEntry(HandbookGroupConfigEntry entry)
        {
            if (entry == null)
            {
                return 0;
            }

            if (entry.Id > 0)
            {
                return entry.Id;
            }

            if (TryParseGroupIdSuffix(entry.HiddenCategoryCode, out int hiddenId))
            {
                entry.Id = hiddenId;
                return hiddenId;
            }

            if (TryParseGroupIdSuffix(entry.PageCode, out int pageId))
            {
                entry.Id = pageId;
                return pageId;
            }

            return 0;
        }

        private static bool TryParseGroupIdSuffix(string value, out int id)
        {
            id = 0;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            int dashIndex = value.LastIndexOf('-');
            if (dashIndex < 0 || dashIndex >= value.Length - 1)
            {
                return false;
            }

            string suffix = value.Substring(dashIndex + 1);
            return int.TryParse(suffix, out id);
        }

        private static void LoadGroupPagesFromConfig(List<GuiHandbookPage> allPages)
        {
            activeGroupPages.Clear();
            groupsByMemberPage.Clear();
            groupByHiddenCategoryCode.Clear();
            groupPagesByDisplayCategory.Clear();
            pendingGroupCreations.Clear();
            groupNavigationHistory.Clear();

            if (groupConfig?.Groups == null || groupConfig.Groups.Count == 0 || allPages == null || allPages.Count == 0)
            {
                ResetNextGroupIdFromConfig();
                return;
            }

            Dictionary<string, GuiHandbookPage> pageLookup = allPages
                .Where(page => page != null && !string.IsNullOrEmpty(page.PageCode))
                .GroupBy(page => page.PageCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            foreach (HandbookGroupConfigEntry entry in groupConfig.Groups)
            {
                if (entry == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(entry.HiddenCategoryCode))
                {
                    groupConfigEntriesByHiddenCode[entry.HiddenCategoryCode] = entry;
                }

                var members = new List<GuiHandbookPage>();
                List<string> missingCodes = null;

                foreach (string memberCode in entry.MemberPageCodes)
                {
                    if (string.IsNullOrWhiteSpace(memberCode))
                    {
                        continue;
                    }

                    if (pageLookup.TryGetValue(memberCode, out GuiHandbookPage page))
                    {
                        if (!members.Contains(page))
                        {
                            members.Add(page);
                        }
                    }
                    else
                    {
                        missingCodes ??= new List<string>();
                        missingCodes.Add(memberCode);
                    }
                }

                if (missingCodes != null && missingCodes.Count > 0)
                {
                    LogMissingGroupMembers(entry, missingCodes);
                }

                if (members.Count == 0)
                {
                    continue;
                }

                var groupPage = new GroupHandbookPage(
                    entry.PageCode,
                    entry.HiddenCategoryCode,
                    entry.DisplayCategoryCode,
                    entry.DisplayName,
                    members);

                groupPage.PageNumber = entry.PageNumber;
                groupPage.SetSortOrderHint(entry.SortOrderHint);

                GuiHandbookPage weightSource = null;
                if (!string.IsNullOrEmpty(entry.WeightSourcePageCode))
                {
                    pageLookup.TryGetValue(entry.WeightSourcePageCode, out weightSource);
                    if (weightSource == null)
                    {
                        LogMissingWeightSource(entry);
                    }
                }

                if (weightSource == null)
                {
                    weightSource = members.FirstOrDefault();
                }

                groupPage.AdoptAppearanceFrom(weightSource);
                activeGroupPages.Add(groupPage);
            }

            ResetNextGroupIdFromConfig();
        }

        private static void StoreGroupConfig()
        {
            groupConfig ??= HandbookGroupConfig.CreateDefault();
            groupConfig.Groups ??= new List<HandbookGroupConfigEntry>();

            NormalizeGroupConfiguration();

            if (capi == null)
            {
                ResetNextGroupIdFromConfig();
                return;
            }

            capi.StoreModConfig(groupConfig, HandbookGroupConfig.ConfigFileName);
            ResetNextGroupIdFromConfig();
        }

        private static void PersistGroupToConfig(
            GroupHandbookPage groupPage,
            GuiHandbookPage referencePage,
            int groupId)
        {
            if (groupPage == null)
            {
                return;
            }

            groupConfig ??= HandbookGroupConfig.CreateDefault();
            groupConfig.Groups ??= new List<HandbookGroupConfigEntry>();

            string hiddenCode = groupPage.HiddenCategoryCode;
            if (string.IsNullOrEmpty(hiddenCode))
            {
                return;
            }

            int id = groupId;
            if (id <= 0 && !TryParseGroupIdSuffix(hiddenCode, out id) && !TryParseGroupIdSuffix(groupPage.PageCode, out id))
            {
                id = ComputeNextGroupIdFromConfig();
            }

            List<string> memberCodes = groupPage.Members?
                .Where(page => !string.IsNullOrWhiteSpace(page?.PageCode))
                .Select(page => page.PageCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? new List<string>();

            string weightSourceCode = referencePage?.PageCode;
            if (string.IsNullOrWhiteSpace(weightSourceCode))
            {
                weightSourceCode = groupPage.Members?
                    .FirstOrDefault(page => !string.IsNullOrWhiteSpace(page?.PageCode))?
                    .PageCode;
            }

            HandbookGroupConfigEntry entry = null;
            int entryIndex = -1;
            for (int i = 0; i < groupConfig.Groups.Count; i++)
            {
                HandbookGroupConfigEntry candidate = groupConfig.Groups[i];
                if (candidate != null && string.Equals(candidate.HiddenCategoryCode, hiddenCode, StringComparison.Ordinal))
                {
                    entry = candidate;
                    entryIndex = i;
                    break;
                }
            }

            if (entry == null)
            {
                entry = new HandbookGroupConfigEntry();
                groupConfig.Groups.Add(entry);
                entryIndex = groupConfig.Groups.Count - 1;
            }

            entry.Id = id > 0 ? id : entry.Id;
            entry.HiddenCategoryCode = hiddenCode;
            entry.PageCode = groupPage.PageCode ?? string.Empty;
            entry.DisplayCategoryCode = groupPage.DisplayCategoryCode;
            entry.DisplayName = groupPage.DisplayName ?? string.Empty;
            entry.SortOrderHint = groupPage.SortOrderHint;
            entry.PageNumber = groupPage.PageNumber;
            entry.WeightSourcePageCode = weightSourceCode;
            entry.MemberPageCodes = memberCodes;

            groupConfig.Groups[entryIndex] = entry;
            groupConfigEntriesByHiddenCode[hiddenCode] = entry;

            StoreGroupConfig();
        }

        private static void UpdateConfigEntryMembers(GroupHandbookPage groupPage)
        {
            if (groupPage == null)
            {
                return;
            }

            groupConfig ??= HandbookGroupConfig.CreateDefault();
            groupConfig.Groups ??= new List<HandbookGroupConfigEntry>();

            string hiddenCode = groupPage.HiddenCategoryCode;
            if (string.IsNullOrEmpty(hiddenCode))
            {
                return;
            }

            HandbookGroupConfigEntry entry = null;
            int entryIndex = -1;
            for (int i = 0; i < groupConfig.Groups.Count; i++)
            {
                HandbookGroupConfigEntry candidate = groupConfig.Groups[i];
                if (candidate != null && string.Equals(candidate.HiddenCategoryCode, hiddenCode, StringComparison.Ordinal))
                {
                    entry = candidate;
                    entryIndex = i;
                    break;
                }
            }

            if (entry == null)
            {
                entry = new HandbookGroupConfigEntry
                {
                    HiddenCategoryCode = hiddenCode,
                    PageCode = groupPage.PageCode ?? string.Empty,
                    DisplayCategoryCode = groupPage.DisplayCategoryCode,
                    DisplayName = groupPage.DisplayName ?? string.Empty,
                    SortOrderHint = groupPage.SortOrderHint,
                    PageNumber = groupPage.PageNumber
                };

                if (!TryParseGroupIdSuffix(hiddenCode, out int parsedId) && !TryParseGroupIdSuffix(groupPage.PageCode, out parsedId))
                {
                    parsedId = ComputeNextGroupIdFromConfig();
                }

                entry.Id = parsedId;
                groupConfig.Groups.Add(entry);
                entryIndex = groupConfig.Groups.Count - 1;
            }
            else
            {
                entry.PageCode = groupPage.PageCode ?? entry.PageCode;
                entry.DisplayCategoryCode = groupPage.DisplayCategoryCode;
                entry.DisplayName = groupPage.DisplayName ?? entry.DisplayName;
                entry.SortOrderHint = groupPage.SortOrderHint;
                entry.PageNumber = groupPage.PageNumber;
            }

            entry.MemberPageCodes = groupPage.Members?
                .Where(page => !string.IsNullOrWhiteSpace(page?.PageCode))
                .Select(page => page.PageCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? new List<string>();

            groupConfig.Groups[entryIndex] = entry;
            groupConfigEntriesByHiddenCode[hiddenCode] = entry;

            StoreGroupConfig();
        }

        private static void RemoveGroupFromConfig(GroupHandbookPage groupPage)
        {
            if (groupPage == null)
            {
                return;
            }

            groupConfig ??= HandbookGroupConfig.CreateDefault();
            groupConfig.Groups ??= new List<HandbookGroupConfigEntry>();

            string hiddenCode = groupPage.HiddenCategoryCode;
            if (string.IsNullOrEmpty(hiddenCode))
            {
                return;
            }

            HandbookGroupConfigEntry entry = null;
            int entryIndex = -1;
            for (int i = 0; i < groupConfig.Groups.Count; i++)
            {
                HandbookGroupConfigEntry candidate = groupConfig.Groups[i];
                if (candidate != null && string.Equals(candidate.HiddenCategoryCode, hiddenCode, StringComparison.Ordinal))
                {
                    entry = candidate;
                    entryIndex = i;
                    break;
                }
            }

            groupConfigEntriesByHiddenCode.Remove(hiddenCode);

            if (entryIndex < 0)
            {
                return;
            }

            groupConfig.Groups.RemoveAt(entryIndex);
            StoreGroupConfig();
        }

        private static void LogMissingGroupMembers(HandbookGroupConfigEntry entry, List<string> missingCodes)
        {
            if (entry == null || missingCodes == null || missingCodes.Count == 0)
            {
                return;
            }

            string label = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.HiddenCategoryCode ?? "unknown"
                : entry.DisplayName.Trim();

            string codes = string.Join(", ", missingCodes.Distinct(StringComparer.OrdinalIgnoreCase));
            capi?.Logger?.Debug("[HandbookCategories] Missing handbook pages for group \"{0}\" ({1}): {2}", label, entry.HiddenCategoryCode, codes);
        }

        private static void LogMissingWeightSource(HandbookGroupConfigEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.WeightSourcePageCode))
            {
                return;
            }

            string label = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.HiddenCategoryCode ?? "unknown"
                : entry.DisplayName.Trim();

            capi?.Logger?.Debug("[HandbookCategories] Missing icon source page \"{0}\" for group \"{1}\" ({2}).", entry.WeightSourcePageCode, label, entry.HiddenCategoryCode);
        }
    }
}
