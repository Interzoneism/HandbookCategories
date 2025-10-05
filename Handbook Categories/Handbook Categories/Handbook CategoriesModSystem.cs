using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Enhanced_Handbook
{
    public class Handbook_CategoriesModSystem : ModSystem
    {
        //Hi Dana! Legend!
        private const string HarmonyId = "handbookcategories.core";

        private Harmony harmony;
        private ICoreClientAPI capi;

        private static readonly char[] QuoteCharacters = { '"', '\'' };

        private readonly struct CommandToken
        {
            internal CommandToken(string value, bool requiresTitleMatch)
            {
                Value = value;
                RequiresTitleMatch = requiresTitleMatch;
            }

            internal string Value { get; }

            internal bool RequiresTitleMatch { get; }
        }

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

            capi.ChatCommands
                .Create("categorymoddefault")
                .WithDescription("Restores the default handbook categories")
                .IgnoreAdditionalArgs()
                .HandleWith(OnCategoryModDefaultCommand);

            harmony = new Harmony(HarmonyId);

            var baseType = typeof(GuiDialogHandbook);
            harmony.Patch(AccessTools.Method(baseType, "LoadPages_Async"),
                postfix: new HarmonyMethod(typeof(HandbookCategoryPatches), nameof(HandbookCategoryPatches.AfterPagesLoaded)));

            harmony.Patch(AccessTools.Method(baseType, "initOverviewGui"),
                postfix: new HarmonyMethod(typeof(HandbookCategoryPatches), nameof(HandbookCategoryPatches.InitOverviewGuiPostfix)));

            harmony.Patch(AccessTools.Method(baseType, "initDetailGui"),
                postfix: new HarmonyMethod(typeof(HandbookCategoryPatches), nameof(HandbookCategoryPatches.InitDetailGuiPostfix)));

            harmony.Patch(AccessTools.Method(baseType, nameof(GuiDialogHandbook.FilterItems)),
                prefix: new HarmonyMethod(typeof(HandbookCategoryPatches), nameof(HandbookCategoryPatches.FilterItemsPrefix)));

            harmony.Patch(AccessTools.Method(baseType, "onLeftClickListElement"),
                prefix: new HarmonyMethod(typeof(HandbookCategoryPatches), nameof(HandbookCategoryPatches.OnLeftClickListElementPrefix)));

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

            List<CommandToken> parsedTokens = TokenizeArguments(rawInput);
            if (parsedTokens.Count == 0)
            {
                return TextCommandResult.Error("Usage: .categorymod [category] [words]");
            }

            string categoryNameInput = parsedTokens[0].Value;
            if (string.IsNullOrWhiteSpace(categoryNameInput))
            {
                return TextCommandResult.Error("You must specify a category name");
            }

            categoryNameInput = categoryNameInput.Trim();

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
                    MatchTitleWords = new List<string>(),
                    ForbiddenWords = new List<string>(),
                    ForbiddenTitleWords = new List<string>()
                };

                config.Categories.Add(category);
                createdCategory = true;
            }

            category.MatchWords ??= new List<string>();
            category.MatchTitleWords ??= new List<string>();
            category.ForbiddenWords ??= new List<string>();
            category.ForbiddenTitleWords ??= new List<string>();

            List<string> addedMatches = new();
            List<string> addedForbidden = new();
            List<string> removedFromMatches = new();
            List<string> removedFromForbidden = new();
            List<string> skipped = new();

            bool nextIsForbidden = false;
            for (int i = 1; i < parsedTokens.Count; i++)
            {
                CommandToken token = parsedTokens[i];
                string tokenValue = token.Value;
                if (string.IsNullOrWhiteSpace(tokenValue))
                {
                    continue;
                }

                string trimmedToken = tokenValue.Trim();
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
                bool requiresTitleMatch = token.RequiresTitleMatch;
                char? inlinePrefix = null;

                if (trimmedToken.StartsWith("-", StringComparison.Ordinal) || trimmedToken.StartsWith("!", StringComparison.Ordinal))
                {
                    isForbidden = true;
                    inlinePrefix = trimmedToken[0];
                    trimmedToken = trimmedToken.Substring(1);
                }

                string word = trimmedToken.Trim();
                if (string.IsNullOrWhiteSpace(word))
                {
                    continue;
                }

                List<string> targetList;
                List<string> conflictingList;
                bool conflictingRequiresTitleMatch;

                if (requiresTitleMatch)
                {
                    targetList = isForbidden ? category.ForbiddenTitleWords : category.MatchTitleWords;
                    conflictingList = isForbidden ? category.MatchTitleWords : category.ForbiddenTitleWords;
                    conflictingRequiresTitleMatch = true;
                }
                else
                {
                    targetList = isForbidden ? category.ForbiddenWords : category.MatchWords;
                    conflictingList = isForbidden ? category.MatchWords : category.ForbiddenWords;
                    conflictingRequiresTitleMatch = false;
                }

                bool removedFromConflicting = RemoveWordCaseInsensitive(conflictingList, word);
                if (removedFromConflicting)
                {
                    string removedWord = FormatWordForMessage(word, conflictingRequiresTitleMatch);
                    if (isForbidden)
                    {
                        if (!removedFromMatches.Any(existing => existing.Equals(removedWord, StringComparison.OrdinalIgnoreCase)))
                        {
                            removedFromMatches.Add(removedWord);
                        }
                    }
                    else
                    {
                        if (!removedFromForbidden.Any(existing => existing.Equals(removedWord, StringComparison.OrdinalIgnoreCase)))
                        {
                            removedFromForbidden.Add(removedWord);
                        }
                    }
                }

                if (targetList.Any(existing => existing != null && existing.Equals(word, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!removedFromConflicting)
                    {
                        string skippedToken = FormatWordForMessage(word, requiresTitleMatch);
                        if (inlinePrefix.HasValue)
                        {
                            skippedToken = inlinePrefix.Value + skippedToken;
                        }

                        skipped.Add(skippedToken);
                    }

                    continue;
                }

                targetList.Add(word);

                string formattedWord = FormatWordForMessage(word, requiresTitleMatch);
                if (isForbidden)
                {
                    addedForbidden.Add(formattedWord);
                }
                else
                {
                    addedMatches.Add(formattedWord);
                }
            }

            bool hasNewWords = addedMatches.Count > 0 || addedForbidden.Count > 0;
            bool hasRemovals = removedFromMatches.Count > 0 || removedFromForbidden.Count > 0;

            if (!hasNewWords)
            {
                List<string> noAdditionParts = new();
                if (removedFromMatches.Count > 0)
                {
                    noAdditionParts.Add($"removed from matches: {string.Join(", ", removedFromMatches)}");
                }

                if (removedFromForbidden.Count > 0)
                {
                    noAdditionParts.Add($"removed from forbidden words: {string.Join(", ", removedFromForbidden)}");
                }

                string removalMessage = noAdditionParts.Count > 0
                    ? $" {string.Join(". ", noAdditionParts)}."
                    : string.Empty;

                if (createdCategory || hasRemovals)
                {
                    capi.StoreModConfig(config, HandbookCategoriesConfig.ConfigFileName);
                    HandbookCategoryManager.ReloadConfiguration();
                    RebuildHandbookTabs();
                }

                string action = createdCategory ? "created" : "updated";
                string message = createdCategory
                    ? $"Created category \"{category.Name}\" with no automatic matches.{removalMessage}"
                    : $"Category \"{category.Name}\" {action}, but no new words were added.{removalMessage}";

                return TextCommandResult.Success(data: message);
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

            if (removedFromMatches.Count > 0)
            {
                parts.Add($"removed from matches: {string.Join(", ", removedFromMatches)}");
            }

            if (removedFromForbidden.Count > 0)
            {
                parts.Add($"removed from forbidden words: {string.Join(", ", removedFromForbidden)}");
            }

            if (skipped.Count > 0)
            {
                parts.Add($"skipped existing: {string.Join(", ", skipped)}");
            }

            parts.Add("Handbook tabs refreshed");

            string summary = string.Join(". ", parts) + ".";
            return TextCommandResult.Success(data: summary);
        }

        private static bool RemoveWordCaseInsensitive(List<string> list, string word)
        {
            if (list == null || string.IsNullOrWhiteSpace(word))
            {
                return false;
            }

            bool removedAny = false;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                string existing = list[i];
                if (HandbookCategoryManager.AreCategoryWordsEquivalent(existing, word))
                {
                    list.RemoveAt(i);
                    removedAny = true;
                }
            }

            return removedAny;
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
                HashSet<string> defaultNames = new(HandbookCategoriesConfig.CreateDefaultCategories()
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

                string deletionSummary = $"Deleted {removed} custom categor{(removed == 1 ? "y" : "ies")}. Handbook tabs refreshed.";
                return TextCommandResult.Success(data: deletionSummary);
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

            string categoryDeletionSummary = $"Deleted category \"{category.Name}\". Handbook tabs refreshed.";
            return TextCommandResult.Success(data: categoryDeletionSummary);
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

            List<CommandToken> tokens = TokenizeArguments(rawInput);
            if (tokens.Count == 0)
            {
                return TextCommandResult.Error("Usage: .categorymodsave [category]");
            }

            string categoryNameInput = tokens[0].Value;
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

            string saveSummary = string.Join(" ", messageParts);
            return TextCommandResult.Success(data: saveSummary);
        }

        private TextCommandResult OnCategoryModDefaultCommand(TextCommandCallingArgs args)
        {
            if (capi == null)
            {
                return TextCommandResult.Error("Client API unavailable");
            }

            HandbookCategoriesConfig config = capi.LoadModConfig<HandbookCategoriesConfig>(HandbookCategoriesConfig.ConfigFileName)
                ?? HandbookCategoriesConfig.CreateDefault();

            List<HandbookCategoryConfigEntry> defaultCategories = HandbookCategoriesConfig.CreateDefaultCategories();

            if (defaultCategories == null || defaultCategories.Count == 0)
            {
                return TextCommandResult.Error("No default categories are available to restore.");
            }

            config.Categories = defaultCategories;
            config.UsesEnglishDefaults = true;

            capi.StoreModConfig(config, HandbookCategoriesConfig.ConfigFileName);
            HandbookCategoryManager.ReloadConfiguration();
            RebuildHandbookTabs();

            string restoreSummary = $"Restored {defaultCategories.Count} default categor{(defaultCategories.Count == 1 ? "y" : "ies")}. Handbook tabs refreshed.";
            return TextCommandResult.Success(data: restoreSummary);
        }

        private static List<CommandToken> TokenizeArguments(string input)
        {
            List<CommandToken> tokens = new();

            if (string.IsNullOrWhiteSpace(input))
            {
                return tokens;
            }

            StringBuilder builder = new();
            bool inQuotes = false;
            char quoteChar = '\0';
            bool currentRequiresTitleMatch = false;

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
                        if (builder.Length == 0)
                        {
                            currentRequiresTitleMatch = false;
                            continue;
                        }

                        inQuotes = false;

                        int endQuoteIndex = i + 1;
                        while (endQuoteIndex < input.Length && input[endQuoteIndex] == quoteChar)
                        {
                            endQuoteIndex++;
                        }

                        if (endQuoteIndex >= input.Length || char.IsWhiteSpace(input[endQuoteIndex]))
                        {
                            i = endQuoteIndex - 1;
                        }

                        continue;
                    }

                    builder.Append(ch);
                }
                else
                {
                    if (IsQuoteCharacter(ch))
                    {
                        if (builder.Length == 0 || (builder.Length == 1 && (builder[0] == '-' || builder[0] == '!')))
                        {
                            inQuotes = true;
                            quoteChar = ch;
                            currentRequiresTitleMatch = false;

                            if (i + 1 < input.Length && input[i + 1] == ch)
                            {
                                currentRequiresTitleMatch = true;
                                i++;
                            }

                            continue;
                        }

                        builder.Append(ch);
                        continue;
                    }

                    if (char.IsWhiteSpace(ch))
                    {
                        AddToken(builder, tokens, ref currentRequiresTitleMatch);
                        continue;
                    }

                    builder.Append(ch);
                }
            }

            AddToken(builder, tokens, ref currentRequiresTitleMatch);

            return tokens;
        }

        private static void AddToken(StringBuilder builder, List<CommandToken> tokens, ref bool requiresTitleMatch)
        {
            if (builder.Length == 0)
            {
                requiresTitleMatch = false;
                return;
            }

            tokens.Add(new CommandToken(builder.ToString(), requiresTitleMatch));
            builder.Clear();
            requiresTitleMatch = false;
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

            if (category?.MatchTitleWords != null)
            {
                foreach (string word in category.MatchTitleWords)
                {
                    string formatted = FormatCommandToken(word, isForbidden: false, requiresTitleMatch: true);
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

            if (category?.ForbiddenTitleWords != null)
            {
                foreach (string word in category.ForbiddenTitleWords)
                {
                    string formatted = FormatCommandToken(word, isForbidden: true, requiresTitleMatch: true);
                    if (!string.IsNullOrWhiteSpace(formatted))
                    {
                        parts.Add(formatted);
                    }
                }
            }

            return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        private static string FormatCommandToken(string value, bool isForbidden, bool requiresTitleMatch = false)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string trimmed = value.Trim();
            string prefix = isForbidden ? "-" : string.Empty;

            if (requiresTitleMatch)
            {
                string escapedTitle = trimmed.Replace("\\", "\\\\").Replace("\"", "\\\"");
                return prefix + "\"\"" + escapedTitle + "\"\"";
            }

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

        private static string FormatWordForMessage(string word, bool requiresTitleMatch)
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                return string.Empty;
            }

            string trimmed = word.Trim();
            return requiresTitleMatch ? $"\"\"{trimmed}\"\"" : trimmed;
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
                    HandbookCategoryManager.MarkCategoriesDirty();
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

            if (HandbookCategoryManager.DragAndDropEnabled)
            {
                HandbookPageDragManager.RegisterOverview(__instance, overviewGui);
            }
        }

        public static void InitDetailGuiPostfix(GuiDialogHandbook __instance)
        {
            if (__instance == null || !HandbookCategoryManager.DragAndDropEnabled)
            {
                return;
            }

            if (DetailGuiField?.GetValue(__instance) is GuiComposer detailGui)
            {
                HandbookPageDragManager.RegisterDetail(__instance, detailGui);
            }
        }

        public static bool OnLeftClickListElementPrefix()
        {
            if (!HandbookCategoryManager.DragAndDropEnabled)
            {
                return true;
            }

            return !HandbookPageDragManager.TryConsumeClickSuppression();
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

            HandbookCategoryManager.UpdateSearchUi(overviewGui, currentSearch);

            bool hasManagedPages = HandbookCategoryManager.TryGetCategoryPages(__instance.currentCatgoryCode, out List<GuiHandbookPage> managedPages);

            if (HandbookCategoryManager.OriginalSearchEnabled && !hasManagedPages)
            {
                return true;
            }

            IEnumerable<GuiHandbookPage> candidatePages = null;
            List<GuiHandbookPage> filteredPages = null;

            if (hasManagedPages)
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
                    filteredPages = allPages
                        .Where(page => page != null && page.CategoryCode == currentCode)
                        .ToList();

                    if (filteredPages.Count == 0)
                    {
                        return true;
                    }

                    candidatePages = filteredPages;
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

            ICoreClientAPI api = HandbookCategoryManager.ClientApi;

            if (api == null)
            {
                return;
            }

            const double fallbackSpacing = 18.0;
            const double fallbackMinWidth = 160.0;
            const double pauseButtonSpacing = 10.0;
            const double toggleSpacing = 10.0;
            const double pauseButtonDefaultWidth = 100.0;
            const double pauseButtonDefaultHeight = 22.0;
            const double pauseButtonDefaultX = 360.0;
            const double pauseButtonDefaultY = 5.0;

            GuiElementToggleButton pauseButton = overviewGui.GetToggleButton("pausegame");
            GuiElementTextInput searchInput = overviewGui.GetTextInput("searchField");

            bool shouldShowOriginalToggle = HandbookCategoryManager.ShouldShowOriginalSearchToggle;
            ElementBounds originalSearchBounds = null;
            ElementBounds recipesOnlyBounds = null;

            GuiElementToggleButton originalSearchToggle = overviewGui.GetToggleButton(HandbookCategoryManager.OriginalSearchToggleKey);
            GuiElementToggleButton recipesToggle = overviewGui.GetToggleButton(HandbookCategoryManager.RecipesOnlyToggleKey);

            if (pauseButton?.Bounds != null)
            {
                double width = pauseButton.Bounds.fixedWidth;
                double height = pauseButton.Bounds.fixedHeight;

                recipesOnlyBounds = pauseButton.Bounds.CopyOffsetedSibling(-(width + pauseButtonSpacing), 0.0);
                recipesOnlyBounds.fixedWidth = width;
                recipesOnlyBounds.fixedHeight = height;

                if (shouldShowOriginalToggle)
                {
                    originalSearchBounds = recipesOnlyBounds.CopyOffsetedSibling(-(width + toggleSpacing), 0.0);
                    originalSearchBounds.fixedWidth = width;
                    originalSearchBounds.fixedHeight = height;
                }
            }

            if (recipesOnlyBounds == null)
            {
                double width = originalSearchToggle?.Bounds?.fixedWidth ?? recipesToggle?.Bounds?.fixedWidth ?? pauseButtonDefaultWidth;
                if (width <= 0.0)
                {
                    width = pauseButtonDefaultWidth;
                }

                double height = originalSearchToggle?.Bounds?.fixedHeight ?? recipesToggle?.Bounds?.fixedHeight ?? pauseButtonDefaultHeight;
                if (height <= 0.0)
                {
                    height = searchInput?.Bounds?.fixedHeight ?? pauseButtonDefaultHeight;
                }

                ElementBounds baseBounds = ElementBounds.Fixed(pauseButtonDefaultX, pauseButtonDefaultY, width, height);
                recipesOnlyBounds = baseBounds;

                if (shouldShowOriginalToggle)
                {
                    originalSearchBounds = baseBounds.CopyOffsetedSibling(-(width + toggleSpacing), 0.0);
                    originalSearchBounds.fixedWidth = width;
                    originalSearchBounds.fixedHeight = height;
                }
            }

            if (recipesOnlyBounds == null && searchInput?.Bounds != null)
            {
                double height = searchInput.Bounds.fixedHeight;
                double width = recipesToggle?.Bounds?.fixedWidth ?? fallbackMinWidth;
                if (width <= 0.0)
                {
                    width = fallbackMinWidth;
                }

                ElementBounds baseBounds = searchInput.Bounds.CopyOffsetedSibling(searchInput.Bounds.fixedWidth + fallbackSpacing, 0.0);
                baseBounds.fixedWidth = width;
                baseBounds.fixedHeight = height;

                if (shouldShowOriginalToggle)
                {
                    originalSearchBounds = baseBounds;
                    recipesOnlyBounds = originalSearchBounds.CopyOffsetedSibling(width + toggleSpacing, 0.0);
                    recipesOnlyBounds.fixedWidth = width;
                    recipesOnlyBounds.fixedHeight = height;
                }
                else
                {
                    recipesOnlyBounds = baseBounds;
                }
            }

            if (recipesOnlyBounds == null)
            {
                return;
            }

            if (shouldShowOriginalToggle && originalSearchBounds == null)
            {
                shouldShowOriginalToggle = false;
            }

            CairoFont font = pauseButton?.Font ?? recipesToggle?.Font ?? CairoFont.WhiteDetailText();

            bool recompose = false;

            if (shouldShowOriginalToggle)
            {
                bool desiredOriginalState = HandbookCategoryManager.OriginalSearchEnabled;
                string originalSearchText = HandbookCategoryManager.GetOriginalSearchToggleText();

                if (originalSearchToggle == null)
                {
                    originalSearchToggle = new GuiElementToggleButton(api, string.Empty, originalSearchText, font, on => OnOriginalSearchToggled(dialog, on), originalSearchBounds, toggleable: true);
                    originalSearchToggle.SetValue(desiredOriginalState);
                    originalSearchToggle.Bounds.CalcWorldBounds();
                    overviewGui.AddInteractiveElement(originalSearchToggle, HandbookCategoryManager.OriginalSearchToggleKey);
                    recompose = true;
                }
                else
                {
                    if (originalSearchBounds != null)
                    {
                        originalSearchToggle.Bounds = originalSearchBounds;
                        originalSearchToggle.Bounds.CalcWorldBounds();
                    }

                    if (originalSearchToggle.On != desiredOriginalState)
                    {
                        originalSearchToggle.SetValue(desiredOriginalState);
                    }
                    if (!string.Equals(originalSearchToggle.Text, originalSearchText, StringComparison.Ordinal))
                    {
                        originalSearchToggle.Text = originalSearchText;
                        recompose = true;
                    }
                    originalSearchToggle.Enabled = true;
                }
            }
            else if (originalSearchToggle != null)
            {
                if (originalSearchToggle.On)
                {
                    originalSearchToggle.SetValue(false);
                }

                if (!string.IsNullOrEmpty(originalSearchToggle.Text))
                {
                    originalSearchToggle.Text = string.Empty;
                    recompose = true;
                }

                originalSearchToggle.Enabled = false;
                originalSearchToggle.Bounds.fixedWidth = 0.0;
                originalSearchToggle.Bounds.fixedHeight = 0.0;
                originalSearchToggle.Bounds.CalcWorldBounds();
            }

            bool desiredRecipesState = HandbookCategoryManager.RecipesOnlyEnabled;
            string recipesOnlyText = HandbookCategoryManager.GetRecipesOnlyToggleText();

            if (recipesToggle == null)
            {
                recipesToggle = new GuiElementToggleButton(api, string.Empty, recipesOnlyText, font, on => OnRecipesOnlyToggled(dialog, on), recipesOnlyBounds, toggleable: true);
                recipesToggle.SetValue(desiredRecipesState);
                recipesToggle.Bounds.CalcWorldBounds();
                overviewGui.AddInteractiveElement(recipesToggle, HandbookCategoryManager.RecipesOnlyToggleKey);
                recompose = true;
            }
            else
            {
                if (recipesOnlyBounds != null)
                {
                    recipesToggle.Bounds = recipesOnlyBounds;
                    recipesToggle.Bounds.CalcWorldBounds();
                }

                if (recipesToggle.On != desiredRecipesState)
                {
                    recipesToggle.SetValue(desiredRecipesState);
                }
                if (!string.Equals(recipesToggle.Text, recipesOnlyText, StringComparison.Ordinal))
                {
                    recipesToggle.Text = recipesOnlyText;
                    recompose = true;
                }
            }

            if (recompose)
            {
                overviewGui.ReCompose();
            }
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

            GuiElementTextButton button = new(api, HandbookCategoryManager.GetCreateCategoryButtonText(), baseFont, hoverFont, () => OnCreateButtonClicked(dialog), buttonBounds, EnumButtonStyle.Normal);
            button.SetOrientation(baseFont.Orientation);
            button.Bounds.CalcWorldBounds();

            overviewGui.AddInteractiveElement(button, HandbookCategoryManager.CreateCategoryButtonKey);
            HandbookCategoryManager.RegisterCreateButton(overviewGui, button);
            overviewGui.ReCompose();
        }

        private static ElementBounds BuildCreateButtonBounds(GuiComposer overviewGui)
        {
            const double minWidth = 60.0;
            const double closeButtonSpacing = 10.0;
            const double defaultSpacing = 160.0;

            if (overviewGui?.LastAddedElement is GuiElementTextButton closeButton && closeButton.Bounds != null && closeButton.Bounds.Alignment == EnumDialogArea.RightFixed)
            {
                double width = Math.Max(minWidth, closeButton.Bounds.fixedWidth);
                ElementBounds bounds = closeButton.Bounds.CopyOffsetedSibling(-(width + closeButtonSpacing), 0.0);
                bounds.fixedWidth = width;
                bounds.fixedHeight = closeButton.Bounds.fixedHeight;
                return bounds;
            }

            GuiElementTextButton backButton = overviewGui?.GetButton("backButton");
            if (backButton != null)
            {
                double width = Math.Max(minWidth, backButton.Bounds.fixedWidth);
                ElementBounds bounds = backButton.Bounds.CopyOffsetedSibling(backButton.Bounds.fixedWidth + defaultSpacing, 0.0);
                bounds.fixedWidth = width;
                bounds.fixedHeight = backButton.Bounds.fixedHeight;
                return bounds;
            }

            GuiElementToggleButton recipesToggle = overviewGui?.GetToggleButton(HandbookCategoryManager.RecipesOnlyToggleKey);
            if (recipesToggle != null && recipesToggle.Bounds != null)
            {
                ElementBounds bounds = recipesToggle.Bounds.CopyOffsetedSibling(recipesToggle.Bounds.fixedWidth + defaultSpacing, 0.0);
                bounds.fixedWidth = Math.Max(minWidth, recipesToggle.Bounds.fixedWidth);
                bounds.fixedHeight = recipesToggle.Bounds.fixedHeight;
                return bounds;
            }

            GuiElementTextInput searchInput = overviewGui?.GetTextInput("searchField");
            if (searchInput != null)
            {
                ElementBounds bounds = searchInput.Bounds.CopyOffsetedSibling(searchInput.Bounds.fixedWidth + defaultSpacing, 0.0);
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

            if (string.IsNullOrWhiteSpace(searchText))
            {
                ShowCreateCategoryPrompt(dialog);
                return true;
            }

            if (!HandbookCategoryManager.TryExecuteCategoryCreateCommand(searchText))
            {
                return false;
            }

            HandbookCategoryManager.ClientApi?.Gui?.PlaySound("menubutton_press");
            RefreshActiveTab(dialog, clearSearch: true);
            return true;
        }

        private static void ShowCreateCategoryPrompt(GuiDialogHandbook dialog)
        {
            ICoreClientAPI api = HandbookCategoryManager.ClientApi;
            if (api?.Gui == null)
            {
                return;
            }

            CreateCategoryPromptDialog prompt = new(api, categoryName =>
            {
                if (string.IsNullOrWhiteSpace(categoryName))
                {
                    return;
                }

                string trimmedName = categoryName.Trim();
                if (string.IsNullOrWhiteSpace(trimmedName))
                {
                    return;
                }

                string unquotedName = trimmedName.Trim('"');
                if (string.IsNullOrWhiteSpace(unquotedName))
                {
                    return;
                }

                string escapedName = unquotedName.Replace("\"", "\\\"");
                string quotedName = $"\"{escapedName}\"";
                string simulatedSearch = $"#{quotedName}";
                if (!HandbookCategoryManager.TryExecuteCategoryCreateCommand(simulatedSearch))
                {
                    return;
                }

                api.Gui.PlaySound("menubutton_press");
                RefreshActiveTab(dialog, clearSearch: true);
            });

            HandbookCategoryManager.SetCreateCategoryPromptOpen(true);
            prompt.OnClosed += () => HandbookCategoryManager.SetCreateCategoryPromptOpen(false);

            if (!prompt.TryOpen())
            {
                HandbookCategoryManager.SetCreateCategoryPromptOpen(false);
            }
        }

        private static void OnRecipesOnlyToggled(GuiDialogHandbook dialog, bool enabled)
        {
            string searchText = CaptureActiveSearchText(dialog);
            bool stateChanged = HandbookCategoryManager.TrySetRecipesOnly(enabled);

            RefreshActiveTab(dialog, clearSearch: false, searchTextToRestore: searchText);

            if (stateChanged)
            {
                HandbookCategoryManager.RequestTabsRebuild();
            }
        }

        private static void OnOriginalSearchToggled(GuiDialogHandbook dialog, bool enabled)
        {
            string searchText = CaptureActiveSearchText(dialog);
            bool stateChanged = HandbookCategoryManager.TrySetOriginalSearch(enabled);

            RefreshActiveTab(dialog, clearSearch: false, searchTextToRestore: searchText);

            if (stateChanged)
            {
                HandbookCategoryManager.ClientApi?.Gui?.PlaySound("menubutton_press");
            }
        }

        private static void RefreshActiveTab(GuiDialogHandbook dialog, bool clearSearch, string searchTextToRestore = null)
        {
            if (dialog == null)
            {
                return;
            }

            string textToRestore = clearSearch ? null : searchTextToRestore;
            bool shouldRestoreSearch = textToRestore != null;

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
            else if (shouldRestoreSearch)
            {
                CurrentSearchTextField?.SetValue(dialog, textToRestore);
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

                    if (!clearSearch && shouldRestoreSearch)
                    {
                        RestoreSearchInputText(dialog, textToRestore);
                    }

                    return;
                }
            }

            dialog.selectTab(dialog.currentCatgoryCode);

            if (!clearSearch && shouldRestoreSearch)
            {
                RestoreSearchInputText(dialog, textToRestore);
            }
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

            string searchText = CaptureActiveSearchText(instance);

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

            if (OverviewGuiField?.GetValue(instance) is not GuiComposer)
            {
                instance.initOverviewGui();
            }

            RefreshActiveTab(instance, clearSearch: false, searchTextToRestore: searchText);
        }

        private static string CaptureActiveSearchText(GuiDialogHandbook dialog)
        {
            if (dialog == null)
            {
                return null;
            }

            string searchText = null;

            if (OverviewGuiField?.GetValue(dialog) is GuiComposer overview)
            {
                GuiElementTextInput searchInput = overview.GetTextInput("searchField");

                if (searchInput != null)
                {
                    searchText = searchInput.GetText();
                }
            }

            if (searchText == null)
            {
                searchText = CurrentSearchTextField?.GetValue(dialog) as string;
            }

            CurrentSearchTextField?.SetValue(dialog, searchText);

            return searchText;
        }

        private static string GetActiveSearchText(GuiDialogHandbook dialog)
        {
            return CaptureActiveSearchText(dialog);
        }

        private static void RestoreSearchInputText(GuiDialogHandbook dialog, string textToRestore)
        {
            if (dialog == null || textToRestore == null)
            {
                return;
            }

            if (OverviewGuiField?.GetValue(dialog) is GuiComposer overview)
            {
                GuiElementTextInput searchInput = overview.GetTextInput("searchField");

                if (searchInput != null)
                {
                    string currentText = searchInput.GetText();

                    if (!string.Equals(currentText, textToRestore, StringComparison.Ordinal))
                    {
                        searchInput.SetValue(textToRestore);
                        currentText = textToRestore;
                    }

                    searchInput.SetCaretPos(currentText?.Length ?? 0);
                    overview.FocusElement(searchInput.TabIndex);
                }
            }
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
                    PaddingTop = tabs.Count == 0 ? 0.0 : 0.0,
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
