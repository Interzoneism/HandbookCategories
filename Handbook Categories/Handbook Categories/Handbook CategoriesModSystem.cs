using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Handbook_Categories
{
    public class Handbook_CategoriesModSystem : ModSystem
    {
        private const string HarmonyId = "handbookcategories.core";

        private Harmony harmony;
        private ICoreClientAPI capi;

        private static readonly char[] QuoteCharacters = { '"', '\'' };

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api ?? throw new ArgumentNullException(nameof(api));

            HandbookCategoryManager.Initialize(api);

            capi.ChatCommands
                .Create("categorymod")
                .WithDescription("Adds allowed or forbidden words to a handbook category")
                .IgnoreAdditionalArgs()
                .HandleWith(OnCategoryModCommand);

            capi.ChatCommands
                .Create("categorymoddelete")
                .WithDescription("Deletes a handbook category or clears custom categories")
                .IgnoreAdditionalArgs()
                .HandleWith(OnCategoryModDeleteCommand);

            capi.ChatCommands
                .Create("categorymodsave")
                .WithDescription("Copies a .categorymod command for the specified category to the clipboard")
                .IgnoreAdditionalArgs()
                .HandleWith(OnCategoryModSaveCommand);

            harmony = new Harmony(HarmonyId);

            var baseType = typeof(GuiDialogHandbook);
            harmony.Patch(AccessTools.Method(baseType, "LoadPages_Async"),
                postfix: new HarmonyMethod(typeof(HandbookCategoryPatches), nameof(HandbookCategoryPatches.AfterPagesLoaded)));

            harmony.Patch(AccessTools.Method(baseType, "initOverviewGui"),
                postfix: new HarmonyMethod(typeof(HandbookCategoryPatches), nameof(HandbookCategoryPatches.InitOverviewGuiPostfix)));

            harmony.Patch(AccessTools.Method(baseType, nameof(GuiDialogHandbook.FilterItems)),
                prefix: new HarmonyMethod(typeof(HandbookCategoryPatches), nameof(HandbookCategoryPatches.FilterItemsPrefix)));

            harmony.Patch(AccessTools.Method(typeof(GuiDialogSurvivalHandbook), "genTabs"),
                postfix: new HarmonyMethod(typeof(HandbookCategoryPatches), nameof(HandbookCategoryPatches.GenTabsPostfix)));

            harmony.Patch(AccessTools.Method(typeof(GuiElementVerticalTabs), nameof(GuiElementVerticalTabs.ComposeTextElements), new[]
            {
                typeof(Cairo.Context),
                typeof(Cairo.ImageSurface)
            }), prefix: new HarmonyMethod(typeof(HandbookCategoryPatches), nameof(HandbookCategoryPatches.ComposeVerticalTabsPrefix)));

            api.Event.LeaveWorld += OnLeaveWorld;
        }

        public override void Dispose()
        {
            base.Dispose();

            if (capi != null)
            {
                capi.Event.LeaveWorld -= OnLeaveWorld;
            }

            HandbookCategoryManager.Clear();
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            capi = null;
        }

        private void OnLeaveWorld()
        {
            HandbookCategoryManager.Clear();
        }

        private TextCommandResult OnCategoryModCommand(TextCommandCallingArgs args)
        {
            if (capi == null)
            {
                return TextCommandResult.Error("Client API unavailable");
            }

            string rawInput = args?.RawArgs?.PopAll();
            if (string.IsNullOrWhiteSpace(rawInput))
            {
                return TextCommandResult.Error("Usage: .categorymod [category] [words]");
            }

            List<string> parsedTokens = TokenizeArguments(rawInput);
            if (parsedTokens.Count == 0)
            {
                return TextCommandResult.Error("Usage: .categorymod [category] [words]");
            }

            string categoryNameInput = parsedTokens[0];
            if (string.IsNullOrWhiteSpace(categoryNameInput))
            {
                return TextCommandResult.Error("You must specify a category name");
            }

            categoryNameInput = categoryNameInput.Trim();

            if (parsedTokens.Count == 1)
            {
                return TextCommandResult.Error("You must specify at least one word to add");
            }

            HandbookCategoriesConfig config = capi.LoadModConfig<HandbookCategoriesConfig>(HandbookCategoriesConfig.ConfigFileName)
                ?? HandbookCategoriesConfig.CreateDefault();

            if (config.Categories == null)
            {
                config.Categories = new List<HandbookCategoryConfigEntry>();
            }

            HandbookCategoryConfigEntry category = config.Categories
                .FirstOrDefault(entry => entry != null && entry.Name != null && entry.Name.Equals(categoryNameInput, StringComparison.OrdinalIgnoreCase));

            bool createdCategory = false;
            if (category == null)
            {
                category = new HandbookCategoryConfigEntry
                {
                    Name = categoryNameInput,
                    MatchWords = new List<string>(),
                    ForbiddenWords = new List<string>()
                };

                config.Categories.Add(category);
                createdCategory = true;
            }

            category.MatchWords ??= new List<string>();
            category.ForbiddenWords ??= new List<string>();

            List<string> addedMatches = new();
            List<string> addedForbidden = new();
            List<string> skipped = new();

            bool nextIsForbidden = false;
            for (int i = 1; i < parsedTokens.Count; i++)
            {
                string token = parsedTokens[i];
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                string trimmedToken = token.Trim();
                if (trimmedToken.Length == 0)
                {
                    continue;
                }

                if (trimmedToken.Equals("-", StringComparison.Ordinal) || trimmedToken.Equals("!", StringComparison.Ordinal))
                {
                    nextIsForbidden = true;
                    continue;
                }

                bool isForbidden = nextIsForbidden;
                nextIsForbidden = false;

                if (trimmedToken.StartsWith("-", StringComparison.Ordinal) || trimmedToken.StartsWith("!", StringComparison.Ordinal))
                {
                    isForbidden = true;
                    trimmedToken = trimmedToken.Substring(1);
                }

                string word = trimmedToken.Trim();
                if (string.IsNullOrWhiteSpace(word))
                {
                    continue;
                }

                List<string> targetList = isForbidden ? category.ForbiddenWords : category.MatchWords;
                if (targetList.Any(existing => existing != null && existing.Equals(word, StringComparison.OrdinalIgnoreCase)))
                {
                    skipped.Add(token);
                    continue;
                }

                targetList.Add(word);

                if (isForbidden)
                {
                    addedForbidden.Add(word);
                }
                else
                {
                    addedMatches.Add(word);
                }
            }

            if (addedMatches.Count == 0 && addedForbidden.Count == 0)
            {
                return TextCommandResult.Success($"Category \"{category.Name}\" {(createdCategory ? "created" : "updated")}, but no new words were added.");
            }

            capi.StoreModConfig(config, HandbookCategoriesConfig.ConfigFileName);
            HandbookCategoryManager.ReloadConfiguration();
            RebuildHandbookTabs();

            List<string> parts = new();
            parts.Add(createdCategory ? $"Created category \"{category.Name}\"" : $"Updated category \"{category.Name}\"");

            if (addedMatches.Count > 0)
            {
                parts.Add($"added matches: {string.Join(", ", addedMatches)}");
            }

            if (addedForbidden.Count > 0)
            {
                parts.Add($"added forbidden words: {string.Join(", ", addedForbidden)}");
            }

            if (skipped.Count > 0)
            {
                parts.Add($"skipped existing: {string.Join(", ", skipped)}");
            }

            parts.Add("Handbook tabs refreshed");

            return TextCommandResult.Success(string.Join(". ", parts) + ".");
        }

        private TextCommandResult OnCategoryModDeleteCommand(TextCommandCallingArgs args)
        {
            if (capi == null)
            {
                return TextCommandResult.Error("Client API unavailable");
            }

            CmdArgs rawArgs = args?.RawArgs;
            if (rawArgs == null || rawArgs.Length == 0)
            {
                return TextCommandResult.Error("Usage: .categorymoddelete [category]");
            }

            string categoryNameInput = rawArgs.PopWord();
            if (string.IsNullOrWhiteSpace(categoryNameInput))
            {
                return TextCommandResult.Error("You must specify a category name");
            }

            categoryNameInput = categoryNameInput.Trim();

            HandbookCategoriesConfig config = capi.LoadModConfig<HandbookCategoriesConfig>(HandbookCategoriesConfig.ConfigFileName)
                ?? HandbookCategoriesConfig.CreateDefault();

            config.Categories ??= new List<HandbookCategoryConfigEntry>();

            if (categoryNameInput.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                HashSet<string> defaultNames = new(HandbookCategoriesConfig.CreateDefault().Categories
                    .Where(entry => !string.IsNullOrWhiteSpace(entry?.Name))
                    .Select(entry => entry.Name), StringComparer.OrdinalIgnoreCase);

                int removed = config.Categories.RemoveAll(entry => entry != null && !defaultNames.Contains(entry.Name ?? string.Empty));

                if (removed == 0)
                {
                    return TextCommandResult.Error("No custom categories to delete.");
                }

                capi.StoreModConfig(config, HandbookCategoriesConfig.ConfigFileName);
                HandbookCategoryManager.ReloadConfiguration();
                RebuildHandbookTabs();

                return TextCommandResult.Success($"Deleted {removed} custom categor{(removed == 1 ? "y" : "ies")}. Handbook tabs refreshed.");
            }

            HandbookCategoryConfigEntry category = config.Categories
                .FirstOrDefault(entry => entry != null && entry.Name != null && entry.Name.Equals(categoryNameInput, StringComparison.OrdinalIgnoreCase));

            if (category == null)
            {
                return TextCommandResult.Error($"Category \"{categoryNameInput}\" was not found");
            }

            config.Categories.Remove(category);

            capi.StoreModConfig(config, HandbookCategoriesConfig.ConfigFileName);
            HandbookCategoryManager.ReloadConfiguration();
            RebuildHandbookTabs();

            return TextCommandResult.Success($"Deleted category \"{category.Name}\". Handbook tabs refreshed.");
        }

        private TextCommandResult OnCategoryModSaveCommand(TextCommandCallingArgs args)
        {
            if (capi == null)
            {
                return TextCommandResult.Error("Client API unavailable");
            }

            string rawInput = args?.RawArgs?.PopAll();
            if (string.IsNullOrWhiteSpace(rawInput))
            {
                return TextCommandResult.Error("Usage: .categorymodsave [category]");
            }

            List<string> tokens = TokenizeArguments(rawInput);
            if (tokens.Count == 0)
            {
                return TextCommandResult.Error("Usage: .categorymodsave [category]");
            }

            string categoryNameInput = tokens[0];
            if (string.IsNullOrWhiteSpace(categoryNameInput))
            {
                return TextCommandResult.Error("You must specify a category name");
            }

            HandbookCategoriesConfig config = capi.LoadModConfig<HandbookCategoriesConfig>(HandbookCategoriesConfig.ConfigFileName)
                ?? HandbookCategoriesConfig.CreateDefault();

            config.Categories ??= new List<HandbookCategoryConfigEntry>();

            HandbookCategoryConfigEntry category = config.Categories
                .FirstOrDefault(entry => entry != null && entry.Name != null && entry.Name.Equals(categoryNameInput, StringComparison.OrdinalIgnoreCase));

            if (category == null)
            {
                return TextCommandResult.Error($"Category \"{categoryNameInput}\" was not found");
            }

            string command = BuildCategoryCommand(category);
            bool clipboardCopied = false;

            if (!string.IsNullOrWhiteSpace(command))
            {
                try
                {
                    capi.Forms?.SetClipboardText(command);
                    clipboardCopied = true;
                }
                catch (Exception ex)
                {
                    capi.Logger?.Warning("Handbook Categories: Failed to copy category command to clipboard: {0}", ex.Message);
                }
            }

            List<string> messageParts = new()
            {
                $"Category \"{category.Name}\" command: {command}",
                clipboardCopied ? "Copied to clipboard." : "Unable to copy to clipboard."
            };

            return TextCommandResult.Success(string.Join(" ", messageParts));
        }

        private static List<string> TokenizeArguments(string input)
        {
            List<string> tokens = new();

            if (string.IsNullOrWhiteSpace(input))
            {
                return tokens;
            }

            StringBuilder builder = new();
            bool inQuotes = false;
            char quoteChar = '\0';

            for (int i = 0; i < input.Length; i++)
            {
                char ch = input[i];

                if (inQuotes)
                {
                    if (ch == '\\' && i + 1 < input.Length)
                    {
                        i++;
                        builder.Append(input[i]);
                        continue;
                    }

                    if (ch == quoteChar)
                    {
                        inQuotes = false;
                        continue;
                    }

                    builder.Append(ch);
                }
                else
                {
                    if (IsQuoteCharacter(ch))
                    {
                        if (builder.Length == 0 || (builder.Length == 1 && builder[0] == '-'))
                        {
                            inQuotes = true;
                            quoteChar = ch;
                            continue;
                        }

                        builder.Append(ch);
                        continue;
                    }

                    if (char.IsWhiteSpace(ch))
                    {
                        AddToken(builder, tokens);
                        continue;
                    }

                    builder.Append(ch);
                }
            }

            AddToken(builder, tokens);

            return tokens;
        }

        private static void AddToken(StringBuilder builder, List<string> tokens)
        {
            if (builder.Length == 0)
            {
                return;
            }

            tokens.Add(builder.ToString());
            builder.Clear();
        }

        private static bool IsQuoteCharacter(char ch)
        {
            for (int i = 0; i < QuoteCharacters.Length; i++)
            {
                if (QuoteCharacters[i] == ch)
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildCategoryCommand(HandbookCategoryConfigEntry category)
        {
            List<string> parts = new()
            {
                ".categorymod",
                FormatCommandToken(category?.Name, isForbidden: false)
            };

            if (category?.MatchWords != null)
            {
                foreach (string word in category.MatchWords)
                {
                    string formatted = FormatCommandToken(word, isForbidden: false);
                    if (!string.IsNullOrWhiteSpace(formatted))
                    {
                        parts.Add(formatted);
                    }
                }
            }

            if (category?.ForbiddenWords != null)
            {
                foreach (string word in category.ForbiddenWords)
                {
                    string formatted = FormatCommandToken(word, isForbidden: true);
                    if (!string.IsNullOrWhiteSpace(formatted))
                    {
                        parts.Add(formatted);
                    }
                }
            }

            return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        private static string FormatCommandToken(string value, bool isForbidden)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string trimmed = value.Trim();
            string prefix = isForbidden ? "-" : string.Empty;

            bool needsQuotes = trimmed.Any(char.IsWhiteSpace);

            if (!needsQuotes)
            {
                return prefix + trimmed;
            }

            bool containsDouble = trimmed.Contains("\"");
            bool containsSingle = trimmed.Contains("'");

            char quoteChar = containsDouble && !containsSingle ? '\'' : '"';

            string escaped = trimmed.Replace("\\", "\\\\");
            if (quoteChar == '\'')
            {
                escaped = escaped.Replace("'", "\\'");
            }
            else
            {
                escaped = escaped.Replace("\"", "\\\"");
            }

            return prefix + quoteChar + escaped + quoteChar;
        }

        private void RebuildHandbookTabs()
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

                List<GuiDialogSurvivalHandbook> openDialogs = capi.Gui.OpenedGuis
                    .OfType<GuiDialogSurvivalHandbook>()
                    .ToList();

                if (openDialogs.Count == 0)
                {
                    return;
                }

                foreach (GuiDialogSurvivalHandbook dialog in openDialogs)
                {
                    HandbookCategoryPatches.RebuildTabs(dialog);
                }
            }, "handbookcategories-rebuildtabs");
        }
    }

    internal static class HandbookCategoryPatches
    {
        private static readonly System.Reflection.FieldInfo AllPagesField = AccessTools.Field(typeof(GuiDialogHandbook), "allHandbookPages");
        private static readonly System.Reflection.FieldInfo ShownPagesField = AccessTools.Field(typeof(GuiDialogHandbook), "shownHandbookPages");
        private static readonly System.Reflection.FieldInfo OverviewGuiField = AccessTools.Field(typeof(GuiDialogHandbook), "overviewGui");
        private static readonly System.Reflection.FieldInfo DetailGuiField = AccessTools.Field(typeof(GuiDialogHandbook), "detailViewGui");
        private static readonly System.Reflection.FieldInfo CurrentSearchTextField = AccessTools.Field(typeof(GuiDialogHandbook), "currentSearchText");
        private static readonly System.Reflection.FieldInfo LoadingPagesField = AccessTools.Field(typeof(GuiDialogHandbook), "loadingPagesAsync");
        private static readonly System.Reflection.FieldInfo ListHeightField = AccessTools.Field(typeof(GuiDialogHandbook), "listHeight");
        private static readonly System.Reflection.FieldInfo CategoryCodesField = AccessTools.Field(typeof(GuiDialogHandbook), "categoryCodes");
        private static readonly System.Reflection.FieldInfo VerticalTabsField = AccessTools.Field(typeof(GuiElementVerticalTabs), "tabs");

        public static void AfterPagesLoaded(GuiDialogHandbook __instance)
        {
            if (__instance is GuiDialogSurvivalHandbook && HandbookCategoryManager.IsReady)
            {
                if (AllPagesField?.GetValue(__instance) is List<GuiHandbookPage> pages)
                {
                    HandbookCategoryManager.RebuildCategories(pages);
                }
            }
        }

        public static void InitOverviewGuiPostfix(GuiDialogHandbook __instance)
        {
            if (__instance == null)
            {
                return;
            }

            if (OverviewGuiField?.GetValue(__instance) is not GuiComposer overviewGui)
            {
                return;
            }

            EnsureRecipesOnlyToggle(__instance, overviewGui);
            EnsureCreateButton(__instance, overviewGui);
        }

        public static bool FilterItemsPrefix(GuiDialogHandbook __instance)
        {
            if (ShownPagesField?.GetValue(__instance) is not List<IFlatListItem> shownPages)
            {
                return true;
            }

            var overviewGui = OverviewGuiField?.GetValue(__instance) as GuiComposer;
            var currentSearch = CurrentSearchTextField?.GetValue(__instance) as string;
            var loading = LoadingPagesField != null && (bool)LoadingPagesField.GetValue(__instance);
            double listHeight = ListHeightField != null ? (double)ListHeightField.GetValue(__instance) : 500d;

            IEnumerable<GuiHandbookPage> candidatePages = null;

            if (HandbookCategoryManager.TryGetCategoryPages(__instance.currentCatgoryCode, out List<GuiHandbookPage> managedPages))
            {
                candidatePages = managedPages;
            }
            else if (AllPagesField?.GetValue(__instance) is List<GuiHandbookPage> allPages)
            {
                if (__instance.currentCatgoryCode == null)
                {
                    candidatePages = allPages;
                }
                else
                {
                    string currentCode = __instance.currentCatgoryCode;
                    candidatePages = allPages.Where(page => page != null && page.CategoryCode == currentCode);
                }
            }

            if (candidatePages == null)
            {
                return true;
            }

            HandbookCategoryManager.ApplyCategoryFilter(__instance.currentCatgoryCode, candidatePages, shownPages, overviewGui, currentSearch, loading, listHeight);

            return false;
        }

        private static void EnsureRecipesOnlyToggle(GuiDialogHandbook dialog, GuiComposer overviewGui)
        {
            if (overviewGui == null)
            {
                return;
            }

            GuiElementToggleButton existingToggle = overviewGui.GetToggleButton(HandbookCategoryManager.RecipesOnlyToggleKey);
            bool desiredState = HandbookCategoryManager.RecipesOnlyEnabled;

            if (existingToggle != null)
            {
                if (existingToggle.On != desiredState)
                {
                    existingToggle.SetValue(desiredState);
                }

                return;
            }

            ICoreClientAPI api = HandbookCategoryManager.ClientApi;
            GuiElementTextInput searchInput = overviewGui.GetTextInput("searchField");

            if (api == null || searchInput?.Bounds == null)
            {
                return;
            }

            const double spacing = 18.0;
            const double minWidth = 140.0;

            ElementBounds bounds = searchInput.Bounds.CopyOffsetedSibling(searchInput.Bounds.fixedWidth + spacing, 0.0);
            bounds.fixedWidth = minWidth;
            bounds.fixedHeight = searchInput.Bounds.fixedHeight;

            CairoFont font = CairoFont.SmallButtonText(EnumButtonStyle.Normal);

            GuiElementToggleButton toggleButton = new(api, string.Empty, "Recipes Only", font, on => OnRecipesOnlyToggled(dialog, on), bounds, toggleable: true);
            toggleButton.SetValue(desiredState);
            toggleButton.Bounds.CalcWorldBounds();

            overviewGui.AddInteractiveElement(toggleButton, HandbookCategoryManager.RecipesOnlyToggleKey);
            overviewGui.ReCompose();
        }

        private static void EnsureCreateButton(GuiDialogHandbook dialog, GuiComposer overviewGui)
        {
            if (overviewGui == null)
            {
                return;
            }

            GuiElementTextButton existingButton = overviewGui.GetButton(HandbookCategoryManager.CreateCategoryButtonKey);
            if (existingButton != null)
            {
                HandbookCategoryManager.RegisterCreateButton(overviewGui, existingButton);
                return;
            }

            ICoreClientAPI api = HandbookCategoryManager.ClientApi;
            if (api == null)
            {
                return;
            }

            ElementBounds buttonBounds = BuildCreateButtonBounds(overviewGui);
            if (buttonBounds == null)
            {
                return;
            }

            CairoFont baseFont = CairoFont.SmallButtonText(EnumButtonStyle.Normal);
            CairoFont hoverFont = CairoFont.SmallButtonText(EnumButtonStyle.Normal);
            hoverFont.Color = (double[])GuiStyle.ActiveButtonTextColor.Clone();

            GuiElementTextButton button = new(api, "Create Category", baseFont, hoverFont, () => OnCreateButtonClicked(dialog), buttonBounds, EnumButtonStyle.Normal);
            button.SetOrientation(baseFont.Orientation);
            button.Bounds.CalcWorldBounds();

            overviewGui.AddInteractiveElement(button, HandbookCategoryManager.CreateCategoryButtonKey);
            HandbookCategoryManager.RegisterCreateButton(overviewGui, button);
            overviewGui.ReCompose();
        }

        private static ElementBounds BuildCreateButtonBounds(GuiComposer overviewGui)
        {
            const double spacing = 24.0;
            const double minWidth = 120.0;

            if (overviewGui?.LastAddedElement is GuiElementTextButton closeButton && closeButton.Bounds != null && closeButton.Bounds.Alignment == EnumDialogArea.RightFixed)
            {
                double width = Math.Max(minWidth, closeButton.Bounds.fixedWidth);
                ElementBounds bounds = closeButton.Bounds.CopyOffsetedSibling(-(width + spacing), 0.0);
                bounds.fixedWidth = width;
                bounds.fixedHeight = closeButton.Bounds.fixedHeight;
                return bounds;
            }

            GuiElementTextButton backButton = overviewGui?.GetButton("backButton");
            if (backButton != null)
            {
                double width = Math.Max(minWidth, backButton.Bounds.fixedWidth);
                ElementBounds bounds = backButton.Bounds.CopyOffsetedSibling(backButton.Bounds.fixedWidth + spacing, 0.0);
                bounds.fixedWidth = width;
                bounds.fixedHeight = backButton.Bounds.fixedHeight;
                return bounds;
            }

            GuiElementToggleButton recipesToggle = overviewGui?.GetToggleButton(HandbookCategoryManager.RecipesOnlyToggleKey);
            if (recipesToggle != null && recipesToggle.Bounds != null)
            {
                ElementBounds bounds = recipesToggle.Bounds.CopyOffsetedSibling(recipesToggle.Bounds.fixedWidth + spacing, 0.0);
                bounds.fixedWidth = Math.Max(minWidth, recipesToggle.Bounds.fixedWidth);
                bounds.fixedHeight = recipesToggle.Bounds.fixedHeight;
                return bounds;
            }

            GuiElementTextInput searchInput = overviewGui?.GetTextInput("searchField");
            if (searchInput != null)
            {
                ElementBounds bounds = searchInput.Bounds.CopyOffsetedSibling(searchInput.Bounds.fixedWidth + spacing, 0.0);
                bounds.fixedWidth = minWidth;
                bounds.fixedHeight = searchInput.Bounds.fixedHeight;
                return bounds;
            }

            return null;
        }

        private static bool OnCreateButtonClicked(GuiDialogHandbook dialog)
        {
            if (dialog == null)
            {
                return false;
            }

            if (HandbookCategoryManager.TryExecuteCategoryDeleteCommand(dialog))
            {
                HandbookCategoryManager.ClientApi?.Gui?.PlaySound("menubutton_press");
                return true;
            }

            string searchText = CurrentSearchTextField?.GetValue(dialog) as string;
            if (string.IsNullOrWhiteSpace(searchText) && OverviewGuiField?.GetValue(dialog) is GuiComposer overview)
            {
                searchText = overview.GetTextInput("searchField")?.GetText();
            }

            if (!HandbookCategoryManager.TryExecuteCategoryCreateCommand(searchText))
            {
                return false;
            }

            HandbookCategoryManager.ClientApi?.Gui?.PlaySound("menubutton_press");
            RefreshActiveTab(dialog, clearSearch: true);
            return true;
        }

        private static void OnRecipesOnlyToggled(GuiDialogHandbook dialog, bool enabled)
        {
            if (!HandbookCategoryManager.TrySetRecipesOnly(enabled))
            {
                return;
            }

            RefreshActiveTab(dialog, clearSearch: false);
            HandbookCategoryManager.RequestTabsRebuild();
        }

        private static void RefreshActiveTab(GuiDialogHandbook dialog, bool clearSearch)
        {
            if (dialog == null)
            {
                return;
            }

            GuiComposer overview = OverviewGuiField?.GetValue(dialog) as GuiComposer;

            if (clearSearch)
            {
                CurrentSearchTextField?.SetValue(dialog, null);

                if (overview?.GetTextInput("searchField") is GuiElementTextInput searchInput)
                {
                    searchInput.SetValue(string.Empty);
                }
                else
                {
                    dialog.FilterItems();
                }
            }

            if (overview?.GetVerticalTab("verticalTabs") is GuiElementVerticalTabs tabsElement)
            {
                int tabCount = GetTabCount(tabsElement);
                if (tabCount > 0)
                {
                    int activeIndex = tabsElement.ActiveElement;
                    if (activeIndex < 0 || activeIndex >= tabCount)
                    {
                        activeIndex = Math.Clamp(activeIndex, 0, tabCount - 1);
                    }

                    tabsElement.SetValue(activeIndex, true);
                    return;
                }
            }

            dialog.selectTab(dialog.currentCatgoryCode);
        }

        private static int GetTabCount(GuiElementVerticalTabs tabsElement)
        {
            if (VerticalTabsField?.GetValue(tabsElement) is GuiTab[] tabs && tabs != null)
            {
                return tabs.Length;
            }

            return 0;
        }

        public static void RebuildTabs(GuiDialogSurvivalHandbook instance)
        {
            if (instance == null)
            {
                return;
            }

            if (AllPagesField?.GetValue(instance) is List<GuiHandbookPage> pages)
            {
                HandbookCategoryManager.RebuildCategories(pages);
            }

            if (OverviewGuiField?.GetValue(instance) is GuiComposer overview)
            {
                overview.Dispose();
                OverviewGuiField.SetValue(instance, null);
            }

            if (DetailGuiField?.GetValue(instance) is GuiComposer detail)
            {
                detail.Dispose();
                DetailGuiField.SetValue(instance, null);
            }

            instance.ReloadPage();
        }

        public static void GenTabsPostfix(GuiDialogSurvivalHandbook __instance, ref GuiTab[] __result, ref int curTab)
        {
            if (__instance != null && AllPagesField?.GetValue(__instance) is List<GuiHandbookPage> pages && pages.Count > 0)
            {
                HandbookCategoryManager.RebuildCategories(pages);
            }

            if (!HandbookCategoryManager.HasCategories)
            {
                return;
            }

            var tabs = (__result ?? Array.Empty<GuiTab>()).ToList();
            bool updated = false;

            if (!HandbookCategoryManager.ShouldDisplayVanillaTab("tutorial"))
            {
                updated |= RemoveVanillaTab(__instance, tabs, ref curTab, "tutorial");
            }

            if (!HandbookCategoryManager.ShouldDisplayVanillaTab("stack"))
            {
                updated |= RemoveVanillaTab(__instance, tabs, ref curTab, "stack");
                updated |= RemoveVanillaTab(__instance, tabs, ref curTab, "blocksitems");
            }

            if (!HandbookCategoryManager.ShouldDisplayVanillaTab("guide"))
            {
                updated |= RemoveVanillaTab(__instance, tabs, ref curTab, "guide");
                updated |= RemoveVanillaTab(__instance, tabs, ref curTab, "guides");
            }

            var existing = new HashSet<string>(tabs.OfType<HandbookTab>().Select(tab => tab.CategoryCode));

            foreach (string categoryCode in HandbookCategoryManager.OrderedCategoryCodes)
            {
                if (!existing.Add(categoryCode))
                {
                    continue;
                }

                double[] backgroundColor = HandbookCategoryManager.GetTabBackgroundColor(categoryCode);

                var tab = new ColoredHandbookTab
                {
                    DataInt = tabs.Count,
                    CategoryCode = categoryCode,
                    Name = HandbookCategoryManager.GetTabDisplayName(categoryCode),
                    PaddingTop = tabs.Count == 0 ? 5.0 : 1.0,
                    BackgroundColor = backgroundColor

                };

                tabs.Add(tab);
                updated = true;

                if (__instance.currentCatgoryCode == categoryCode)
                {
                    curTab = tabs.Count - 1;
                }
            }

            if (updated)
            {
                ReindexTabs(tabs);
                EnsureValidSelection(__instance, tabs, ref curTab);
                __result = tabs.ToArray();
            }
        }

        public static bool ComposeVerticalTabsPrefix(GuiElementVerticalTabs __instance, Cairo.Context ctxStatic, Cairo.ImageSurface surfaceStatic)
        {
            return !GuiElementVerticalTabsWithBackgrounds.TryCompose(__instance);
        }

        private static bool RemoveVanillaTab(GuiDialogSurvivalHandbook instance, List<GuiTab> tabs, ref int curTab, string categoryCode)
        {
            if (instance == null || tabs == null || tabs.Count == 0 || string.IsNullOrEmpty(categoryCode))
            {
                return false;
            }

            for (int i = 0; i < tabs.Count; i++)
            {
                if (tabs[i] is not HandbookTab tab)
                {
                    continue;
                }

                if (!categoryCode.Equals(tab.CategoryCode, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                tabs.RemoveAt(i);

                if (CategoryCodesField?.GetValue(instance) is List<string> codes && codes.Count > 0)
                {
                    codes.RemoveAll(code => code != null && categoryCode.Equals(code, StringComparison.OrdinalIgnoreCase));
                }

                if (instance.currentCatgoryCode != null && categoryCode.Equals(instance.currentCatgoryCode, StringComparison.OrdinalIgnoreCase))
                {
                    instance.currentCatgoryCode = null;
                }

                if (curTab > i)
                {
                    curTab--;
                }
                else if (curTab >= tabs.Count)
                {
                    curTab = Math.Max(0, tabs.Count - 1);
                }

                return true;
            }

            return false;
        }

        private static void ReindexTabs(List<GuiTab> tabs)
        {
            if (tabs == null)
            {
                return;
            }

            for (int i = 0; i < tabs.Count; i++)
            {
                tabs[i].DataInt = i;
            }
        }

        private static void EnsureValidSelection(GuiDialogSurvivalHandbook instance, List<GuiTab> tabs, ref int curTab)
        {
            if (instance == null)
            {
                return;
            }

            if (tabs == null || tabs.Count == 0)
            {
                curTab = 0;
                instance.currentCatgoryCode = null;
                return;
            }

            if (curTab < 0)
            {
                curTab = 0;
            }
            else if (curTab >= tabs.Count)
            {
                curTab = tabs.Count - 1;
            }

            if (curTab < 0 || curTab >= tabs.Count)
            {
                instance.currentCatgoryCode = null;
                return;
            }

            if (tabs[curTab] is HandbookTab tab)
            {
                instance.currentCatgoryCode = tab.CategoryCode;
            }
        }
    }
}
