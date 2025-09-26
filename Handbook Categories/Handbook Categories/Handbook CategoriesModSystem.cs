using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
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

            harmony = new Harmony(HarmonyId);

            var baseType = typeof(GuiDialogHandbook);
            harmony.Patch(AccessTools.Method(baseType, "LoadPages_Async"),
                postfix: new HarmonyMethod(typeof(HandbookCategoryPatches), nameof(HandbookCategoryPatches.AfterPagesLoaded)));

            harmony.Patch(AccessTools.Method(baseType, nameof(GuiDialogHandbook.FilterItems)),
                prefix: new HarmonyMethod(typeof(HandbookCategoryPatches), nameof(HandbookCategoryPatches.FilterItemsPrefix)));

            harmony.Patch(AccessTools.Method(typeof(GuiDialogSurvivalHandbook), "genTabs"),
                postfix: new HarmonyMethod(typeof(HandbookCategoryPatches), nameof(HandbookCategoryPatches.GenTabsPostfix)));

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

            CmdArgs rawArgs = args?.RawArgs;
            if (rawArgs == null || rawArgs.Length == 0)
            {
                return TextCommandResult.Error("Usage: .categorymod [category] [words]");
            }

            string categoryNameInput = rawArgs.PopWord();
            if (string.IsNullOrWhiteSpace(categoryNameInput))
            {
                return TextCommandResult.Error("You must specify a category name");
            }

            categoryNameInput = categoryNameInput.Trim();

            List<string> tokens = new();
            while (rawArgs.Length > 0)
            {
                string token = rawArgs.PopWord();
                if (!string.IsNullOrWhiteSpace(token))
                {
                    tokens.Add(token.Trim());
                }
            }

            if (tokens.Count == 0)
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

            foreach (string token in tokens)
            {
                string word = token;
                bool isForbidden = false;

                if (word.StartsWith("-", StringComparison.Ordinal))
                {
                    isForbidden = true;
                    word = word.Substring(1);
                }

                word = word.Trim();
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
            HandbookCategoryPatches.RefreshOpenHandbookDialogs(capi);

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

            return TextCommandResult.Success(string.Join(". ", parts) + ".");
        }
    }

    internal static class HandbookCategoryPatches
    {
        private static readonly System.Reflection.FieldInfo AllPagesField = AccessTools.Field(typeof(GuiDialogHandbook), "allHandbookPages");
        private static readonly System.Reflection.FieldInfo ShownPagesField = AccessTools.Field(typeof(GuiDialogHandbook), "shownHandbookPages");
        private static readonly System.Reflection.FieldInfo OverviewGuiField = AccessTools.Field(typeof(GuiDialogHandbook), "overviewGui");
        private static readonly System.Reflection.FieldInfo CurrentSearchTextField = AccessTools.Field(typeof(GuiDialogHandbook), "currentSearchText");
        private static readonly System.Reflection.FieldInfo LoadingPagesField = AccessTools.Field(typeof(GuiDialogHandbook), "loadingPagesAsync");
        private static readonly System.Reflection.FieldInfo ListHeightField = AccessTools.Field(typeof(GuiDialogHandbook), "listHeight");

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

        public static bool FilterItemsPrefix(GuiDialogHandbook __instance)
        {
            if (!HandbookCategoryManager.IsManagedCategory(__instance.currentCatgoryCode))
            {
                return true;
            }

            if (ShownPagesField?.GetValue(__instance) is not List<IFlatListItem> shownPages)
            {
                return true;
            }

            var overviewGui = OverviewGuiField?.GetValue(__instance) as GuiComposer;
            var currentSearch = CurrentSearchTextField?.GetValue(__instance) as string;
            var loading = LoadingPagesField != null && (bool)LoadingPagesField.GetValue(__instance);
            double listHeight = ListHeightField != null ? (double)ListHeightField.GetValue(__instance) : 500d;

            HandbookCategoryManager.ApplyCategoryFilter(__instance.currentCatgoryCode, shownPages, overviewGui, currentSearch, loading, listHeight);

            return false;
        }

        public static void RefreshOpenHandbookDialogs(ICoreClientAPI capi)
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

            List<GuiHandbookPage> pages = null;

            foreach (GuiDialogSurvivalHandbook dialog in openDialogs)
            {
                if (AllPagesField?.GetValue(dialog) is List<GuiHandbookPage> dialogPages && dialogPages.Count > 0)
                {
                    pages = dialogPages;
                    break;
                }
            }

            if (pages != null)
            {
                HandbookCategoryManager.RebuildCategories(pages);
            }

            foreach (GuiDialogSurvivalHandbook dialog in openDialogs)
            {
                dialog.ReloadPage();
            }
        }

        public static void GenTabsPostfix(GuiDialogSurvivalHandbook __instance, ref GuiTab[] __result, ref int curTab)
        {
            if (!HandbookCategoryManager.HasCategories)
            {
                return;
            }

            var tabs = (__result ?? Array.Empty<GuiTab>()).ToList();
            bool updated = false;
            var existing = new HashSet<string>(tabs.OfType<HandbookTab>().Select(tab => tab.CategoryCode));

            foreach (string categoryCode in HandbookCategoryManager.OrderedCategoryCodes)
            {
                if (!existing.Add(categoryCode))
                {
                    continue;
                }

                var tab = new HandbookTab
                {
                    DataInt = tabs.Count,
                    CategoryCode = categoryCode,
                    Name = HandbookCategoryManager.GetTabDisplayName(categoryCode),
                    PaddingTop = tabs.Count == 0 ? 5.0 : 1.0
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
                __result = tabs.ToArray();
            }
        }
    }
}
