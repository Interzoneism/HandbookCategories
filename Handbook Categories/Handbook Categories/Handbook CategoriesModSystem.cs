using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
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
                    PaddingTop = tabs.Count == 0 ? 20.0 : 5.0
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
