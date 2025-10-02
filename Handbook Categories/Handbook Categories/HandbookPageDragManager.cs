using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Enhanced_Handbook
{
    internal static class HandbookPageDragManager
    {
        // Use a very high render order so the dragged icon appears above every GUI layer.
        private const double DragIconRenderOrder = 9999.0;

        // Rendering GUI elements in Vintage Story relies on a large positive Z offset to
        // layer visuals correctly (see GuiElementPassiveItemSlot and HudMouseTools). Use a
        // value that comfortably exceeds the in-hand mouse tooltip depth so our drag icon
        // is never occluded by dialog elements.
        private const double DragIconRenderDepth = 4000.0;

        private sealed class DragIconRenderer : IRenderer
        {
            private readonly ICoreClientAPI api;

            internal DragIconRenderer(ICoreClientAPI api)
            {
                this.api = api;
            }

            public double RenderOrder => DragIconRenderOrder;

            public int RenderRange => 0;

            public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
            {
                if (stage != EnumRenderStage.Ortho)
                {
                    return;
                }

                UpdateDragState();

                if (!isDragging || draggingSlot?.Itemstack == null)
                {
                    return;
                }

                int mouseX = api.Input?.MouseX ?? 0;
                int mouseY = api.Input?.MouseY ?? 0;

                float iconSize = (float)GuiElement.scaled(25.0);
                api.Render.RenderItemstackToGui(draggingSlot, mouseX, mouseY, DragIconRenderDepth, iconSize, -1, true, false, false);
            }

            public void Dispose()
            {
            }
        }

        private sealed class DragState
        {
            internal DragState(GuiDialogHandbook dialog)
            {
                Dialog = dialog;
            }

            internal GuiDialogHandbook Dialog { get; }

            internal GuiComposer Overview { get; set; }

            internal GuiElementFlatList SearchList { get; set; }

            internal GuiElementVerticalTabs TabsElement { get; set; }
        }

        private static readonly Dictionary<GuiDialogHandbook, DragState> trackedDialogs = new();

        private static readonly FieldInfo TabsField = AccessTools.Field(typeof(GuiElementVerticalTabs), "tabs");
        private static readonly FieldInfo TabWidthsField = AccessTools.Field(typeof(GuiElementVerticalTabs), "tabWidths");
        private static readonly FieldInfo TabHeightField = AccessTools.Field(typeof(GuiElementVerticalTabs), "tabHeight");
        private static readonly FieldInfo UnscaledTabSpacingField = AccessTools.Field(typeof(GuiElementVerticalTabs), "unscaledTabSpacing");

        private static ICoreClientAPI capi;
        private static DragIconRenderer renderer;

        private static DragState pendingState;
        private static GuiHandbookPage pendingPage;
        private static DummySlot pendingSlot;
        private static int pendingStartX;
        private static int pendingStartY;

        private static DragState draggingState;
        private static GuiHandbookPage draggingPage;
        private static DummySlot draggingSlot;
        private static bool isDragging;
        private static bool wasLeftDown;
        private static bool suppressListClick;

        internal static void Initialize(ICoreClientAPI api)
        {
            if (api == null)
            {
                Clear();
                return;
            }

            Clear();

            capi = api;
            renderer = new DragIconRenderer(api);
            capi.Event.RegisterRenderer(renderer, EnumRenderStage.Ortho, "handbookpagedrag");
        }

        internal static void Clear()
        {
            var previousApi = capi;

            ResetDrag();
            trackedDialogs.Clear();

            if (renderer != null && previousApi != null)
            {
                previousApi.Event.UnregisterRenderer(renderer, EnumRenderStage.Ortho);
                renderer.Dispose();
            }

            renderer = null;
            capi = null;
        }

        internal static void RegisterOverview(GuiDialogHandbook dialog, GuiComposer overview)
        {
            if (dialog == null || overview == null)
            {
                return;
            }

            if (!trackedDialogs.TryGetValue(dialog, out DragState state))
            {
                state = new DragState(dialog);
                trackedDialogs[dialog] = state;
            }

            state.Overview = overview;
            state.SearchList = overview.GetFlatList("stacklist");
            state.TabsElement = overview.GetVerticalTab("verticalTabs");
        }

        internal static bool TryConsumeClickSuppression()
        {
            if (!suppressListClick)
            {
                return false;
            }

            suppressListClick = false;
            return true;
        }

        private static void UpdateDragState()
        {
            if (capi?.Input == null)
            {
                ResetDrag();
                return;
            }

            RemoveClosedDialogs();

            bool leftDown = capi.Input.MouseButton?.Left == true;
            int mouseX = capi.Input.MouseX;
            int mouseY = capi.Input.MouseY;

            if (!leftDown)
            {
                if (wasLeftDown)
                {
                    OnLeftMouseReleased(mouseX, mouseY);
                }

                wasLeftDown = false;
                return;
            }

            if (!wasLeftDown)
            {
                OnLeftMousePressed(mouseX, mouseY);
            }
            else if (!isDragging)
            {
                EvaluateDragThreshold(mouseX, mouseY);
            }

            wasLeftDown = true;
        }

        private static void RemoveClosedDialogs()
        {
            if (capi?.Gui?.OpenedGuis == null)
            {
                return;
            }

            var openDialogs = new HashSet<GuiDialogHandbook>(capi.Gui.OpenedGuis.OfType<GuiDialogHandbook>());

            foreach (GuiDialogHandbook dialog in trackedDialogs.Keys.ToList())
            {
                if (!openDialogs.Contains(dialog))
                {
                    if (draggingState?.Dialog == dialog)
                    {
                        ResetDrag();
                    }

                    if (pendingState?.Dialog == dialog)
                    {
                        pendingState = null;
                        pendingPage = null;
                        pendingSlot = null;
                    }

                    trackedDialogs.Remove(dialog);
                }
            }
        }

        private static void OnLeftMousePressed(int mouseX, int mouseY)
        {
            ResetPending();

            if (capi?.Gui?.OpenedGuis == null)
            {
                return;
            }

            foreach (GuiDialogHandbook dialog in capi.Gui.OpenedGuis.OfType<GuiDialogHandbook>().Reverse())
            {
                if (!trackedDialogs.TryGetValue(dialog, out DragState state))
                {
                    continue;
                }

                if (TryGetPageUnderMouse(state, mouseX, mouseY, out GuiHandbookPage page, out DummySlot slot))
                {
                    pendingState = state;
                    pendingPage = page;
                    pendingSlot = slot;
                    pendingStartX = mouseX;
                    pendingStartY = mouseY;
                    break;
                }
            }
        }

        private static void EvaluateDragThreshold(int mouseX, int mouseY)
        {
            if (pendingPage == null || pendingSlot?.Itemstack == null)
            {
                return;
            }

            double deltaX = mouseX - pendingStartX;
            double deltaY = mouseY - pendingStartY;
            double threshold = GuiElement.scaled(4.0);
            double thresholdSquared = threshold * threshold;

            if (deltaX * deltaX + deltaY * deltaY < thresholdSquared)
            {
                return;
            }

            draggingState = pendingState;
            draggingPage = pendingPage;
            draggingSlot = pendingSlot;
            isDragging = true;
            suppressListClick = true;

            ResetPending();
        }

        private static void OnLeftMouseReleased(int mouseX, int mouseY)
        {
            if (isDragging && draggingState != null)
            {
                TryHandleDrop(mouseX, mouseY);
            }

            ResetDrag();
        }

        private static void TryHandleDrop(int mouseX, int mouseY)
        {
            if (draggingState == null || draggingPage == null)
            {
                return;
            }

            if (!TryGetCategoryUnderMouse(draggingState, mouseX, mouseY, out HandbookTab tab))
            {
                return;
            }

            string categoryCode = tab?.CategoryCode;
            if (string.IsNullOrEmpty(categoryCode) || !HandbookCategoryManager.IsManagedCategory(categoryCode))
            {
                return;
            }

            string title = HandbookCategoryManager.GetLocalizedPageTitle(draggingPage);
            if (string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            GuiDialogSurvivalHandbook dialog = draggingState.Dialog as GuiDialogSurvivalHandbook;
            bool shouldReselect = dialog != null
                && string.Equals(dialog.currentCatgoryCode, categoryCode, StringComparison.OrdinalIgnoreCase);

            capi?.Event?.EnqueueMainThreadTask(() =>
            {
                if (!HandbookCategoryManager.TryAddTitleMatchToCategory(categoryCode, title))
                {
                    return;
                }

                if (dialog == null)
                {
                    return;
                }

                HandbookCategoryPatches.RebuildTabs(dialog);

                if (shouldReselect)
                {
                    dialog.selectTab(categoryCode);
                }
            }, $"handbookcategories-drop-{Guid.NewGuid():N}");
        }

        private static bool TryGetPageUnderMouse(DragState state, int mouseX, int mouseY, out GuiHandbookPage page, out DummySlot slot)
        {
            page = null;
            slot = null;

            GuiElementFlatList list = state?.SearchList ?? state?.Overview?.GetFlatList("stacklist");
            ElementBounds bounds = list?.Bounds;
            ElementBounds parentBounds = bounds?.ParentBounds;

            if (list == null || bounds == null || parentBounds == null)
            {
                return false;
            }

            if (!parentBounds.PointInside(mouseX, mouseY))
            {
                return false;
            }

            double currentY = list.insideBounds.absY;
            double cellHeight = GuiElement.scaled(list.unscaledCellHeight);
            double paddingY = GuiElement.scaled(list.unscalledYPad);

            foreach (IFlatListItem element in list.Elements)
            {
                if (!element.Visible)
                {
                    continue;
                }

                float rowY = (float)(5.0 + bounds.absY + currentY);
                double minY = rowY - paddingY;
                double maxY = rowY + cellHeight - paddingY;
                double minX = bounds.absX;
                double maxX = bounds.absX + bounds.InnerWidth;

                if (mouseX > minX && mouseX <= maxX && mouseY >= minY && mouseY <= maxY)
                {
                    page = element as GuiHandbookPage;
                    if (page == null)
                    {
                        return false;
                    }

                    slot = GetSlotForPage(page);
                    return slot?.Itemstack != null;
                }

                currentY += GuiElement.scaled(list.unscaledCellHeight + list.unscaledCellSpacing);
            }

            return false;
        }

        private static DummySlot GetSlotForPage(GuiHandbookPage page)
        {
            return page switch
            {
                GuiHandbookItemStackPage itemPage => itemPage.dummySlot,
                GuiHandbookMealRecipePage mealPage => mealPage.dummySlot,
                _ => null
            };
        }

        private static bool TryGetCategoryUnderMouse(DragState state, int mouseX, int mouseY, out HandbookTab tab)
        {
            tab = null;

            GuiElementVerticalTabs tabsElement = state?.TabsElement ?? state?.Overview?.GetVerticalTab("verticalTabs");
            ElementBounds bounds = tabsElement?.Bounds;
            if (tabsElement == null || bounds == null)
            {
                return false;
            }

            if (bounds.RequiresRecalculation)
            {
                bounds.CalcWorldBounds();
            }

            GuiTab[] tabs = TabsField?.GetValue(tabsElement) as GuiTab[];
            int[] tabWidths = TabWidthsField?.GetValue(tabsElement) as int[];
            if (tabs == null || tabWidths == null)
            {
                return false;
            }

            double tabHeight = TabHeightField != null ? (double)TabHeightField.GetValue(tabsElement) : GuiElement.scaled(25.0);
            double spacing = GuiElement.scaled(UnscaledTabSpacingField != null ? (double)UnscaledTabSpacingField.GetValue(tabsElement) : 5.0);
            double totalWidth = bounds.InnerWidth + 1.0;
            int localX = mouseX - (int)bounds.absX;
            int localY = mouseY - (int)bounds.absY;
            double currentY = 0.0;

            for (int i = 0; i < tabs.Length; i++)
            {
                GuiTab candidate = tabs[i];
                if (candidate == null)
                {
                    continue;
                }

                currentY += candidate.PaddingTop;
                bool insideX = localX > totalWidth - tabWidths[i] - 3 && localX < totalWidth;
                bool insideY = localY > currentY && localY < currentY + tabHeight + spacing;

                if (insideX && insideY)
                {
                    tab = candidate as HandbookTab;
                    return tab != null;
                }

                currentY += tabHeight + spacing;
            }

            return false;
        }

        private static void ResetDrag()
        {
            isDragging = false;
            draggingState = null;
            draggingPage = null;
            draggingSlot = null;
            suppressListClick = false;
            wasLeftDown = false;
            ResetPending();
        }

        private static void ResetPending()
        {
            pendingState = null;
            pendingPage = null;
            pendingSlot = null;
        }

    }
}
