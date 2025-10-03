using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
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

            internal GuiComposer Detail { get; set; }

            internal GuiElementFlatList SearchList { get; set; }

            internal GuiElementVerticalTabs TabsElement { get; set; }

            internal GuiElementRichtext DetailRichtext { get; set; }

            internal float? PendingScrollPosition { get; set; }
        }

        private static readonly Dictionary<GuiDialogHandbook, DragState> trackedDialogs = new();

        private static readonly FieldInfo TabsField = AccessTools.Field(typeof(GuiElementVerticalTabs), "tabs");
        private static readonly FieldInfo TabWidthsField = AccessTools.Field(typeof(GuiElementVerticalTabs), "tabWidths");
        private static readonly FieldInfo TabHeightField = AccessTools.Field(typeof(GuiElementVerticalTabs), "tabHeight");
        private static readonly FieldInfo UnscaledTabSpacingField = AccessTools.Field(typeof(GuiElementVerticalTabs), "unscaledTabSpacing");

        private static readonly FieldInfo PageNumberByPageCodeField = AccessTools.Field(typeof(GuiDialogHandbook), "pageNumberByPageCode");
        private static readonly FieldInfo AllPagesField = AccessTools.Field(typeof(GuiDialogHandbook), "allHandbookPages");
        private static readonly FieldInfo BrowseHistoryField = AccessTools.Field(typeof(GuiDialogHandbook), "browseHistory");

        private static readonly FieldInfo ItemstackTextComponentSlotField = AccessTools.Field(typeof(ItemstackTextComponent), "slot");
        private static readonly FieldInfo SlideshowItemstackSlotField = AccessTools.Field(typeof(SlideshowItemstackTextComponent), "slot");
        private static readonly FieldInfo MealstackSlotField = AccessTools.Field(typeof(MealstackTextComponent), "dummySlot");
        private static readonly FieldInfo GridRecipeSizeField = AccessTools.Field(typeof(SlideshowGridRecipeTextComponent), "size");
        private static readonly FieldInfo GridRecipeSecondCounterField = AccessTools.Field(typeof(SlideshowGridRecipeTextComponent), "secondCounter");
        private static readonly FieldInfo GridRecipeVariantSequenceField = AccessTools.Field(typeof(SlideshowGridRecipeTextComponent), "variantDisplaySequence");

        private static ICoreClientAPI capi;
        private static DragIconRenderer renderer;

        private static DragState pendingState;
        private static GuiHandbookPage pendingPage;
        private static DummySlot pendingSlot;
        private static int pendingStartX;
        private static int pendingStartY;
        private static string pendingCategoryCode;

        private static DragState draggingState;
        private static GuiHandbookPage draggingPage;
        private static DummySlot draggingSlot;
        private static string draggingCategoryCode;
        private static bool isDragging;
        private static bool wasLeftDown;
        private static bool suppressListClick;
        private static bool featureEnabled = true;

        internal static void Initialize(ICoreClientAPI api)
        {
            if (!featureEnabled || api == null)
            {
                Clear();
                return;
            }

            if (capi == api && renderer != null)
            {
                return;
            }

            Clear();

            capi = api;
            renderer = new DragIconRenderer(api);
            capi.Event.RegisterRenderer(renderer, EnumRenderStage.Ortho, "handbookpagedrag");
        }

        internal static void SetEnabled(ICoreClientAPI api, bool enabled)
        {
            featureEnabled = enabled;
            Initialize(api);
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
            if (!featureEnabled || dialog == null || overview == null)
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

            TryRestorePendingScroll(state);
        }

        internal static void RegisterDetail(GuiDialogHandbook dialog, GuiComposer detail)
        {
            if (!featureEnabled || dialog == null)
            {
                return;
            }

            if (!trackedDialogs.TryGetValue(dialog, out DragState state))
            {
                state = new DragState(dialog);
                trackedDialogs[dialog] = state;
            }

            state.Detail = detail;
            state.DetailRichtext = detail?.GetRichtext("richtext");
        }

        internal static bool TryConsumeClickSuppression()
        {
            if (!featureEnabled)
            {
                return false;
            }

            if (!suppressListClick)
            {
                return false;
            }

            suppressListClick = false;
            return true;
        }

        private static void UpdateDragState()
        {
            if (!featureEnabled)
            {
                ResetDrag();
                return;
            }

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

            foreach (GuiDialog dialog in Enumerable.Reverse(capi.Gui.OpenedGuis))
            {
                if (dialog is GuiDialogHandbook handbookDialog)
                {
                    if (!trackedDialogs.TryGetValue(handbookDialog, out DragState state))
                    {
                        continue;
                    }

                    if (IsHandbookObscured(state, mouseX, mouseY))
                    {
                        return;
                    }

                    if (TryGetPageUnderMouse(state, mouseX, mouseY, out GuiHandbookPage page, out DummySlot slot))
                    {
                        pendingState = state;
                        pendingPage = page;
                        pendingSlot = slot;
                        pendingCategoryCode = (state.Dialog as GuiDialogSurvivalHandbook)?.currentCatgoryCode;
                        pendingStartX = mouseX;
                        pendingStartY = mouseY;
                        break;
                    }

                    if (TryGetDetailPageUnderMouse(state, mouseX, mouseY, out GuiHandbookPage detailPage, out DummySlot detailSlot))
                    {
                        pendingState = state;
                        pendingPage = detailPage;
                        pendingSlot = detailSlot;
                        pendingCategoryCode = (state.Dialog as GuiDialogSurvivalHandbook)?.currentCatgoryCode;
                        pendingStartX = mouseX;
                        pendingStartY = mouseY;
                        break;
                    }

                    if (IsMouseInsideHandbookGui(state, mouseX, mouseY))
                    {
                        return;
                    }

                    continue;
                }

                if (IsMouseInsideDialog(dialog, mouseX, mouseY))
                {
                    return;
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
            draggingCategoryCode = pendingCategoryCode;
            isDragging = true;
            suppressListClick = true;

            ResetPending();
        }

        private static void OnLeftMouseReleased(int mouseX, int mouseY)
        {
            if (isDragging && draggingState != null)
            {
                bool handledDrop = TrySpawnCreativeStack();
                if (!handledDrop)
                {
                    handledDrop = TryHandleDrop(mouseX, mouseY);
                }

                if (!handledDrop)
                {
                    TryHandleRemoval(mouseX, mouseY);
                }
            }

            ResetDrag();
        }

        private static bool TrySpawnCreativeStack()
        {
            if (capi?.World?.Player == null || draggingSlot?.Itemstack == null)
            {
                return false;
            }

            if (capi.World.Player.WorldData?.CurrentGameMode != EnumGameMode.Creative)
            {
                return false;
            }

            ItemSlot hoveredSlot = capi.World.Player.InventoryManager?.CurrentHoveredSlot;
            if (hoveredSlot == null || hoveredSlot.Inventory == null || hoveredSlot is ItemSlotCreative)
            {
                return false;
            }

            ItemSlot sourceSlotReference = draggingSlot;

            if (!hoveredSlot.CanHold(sourceSlotReference) && !hoveredSlot.CanTakeFrom(sourceSlotReference))
            {
                return false;
            }

            ItemStack sourceStack = draggingSlot.Itemstack.Clone();
            if (sourceStack == null)
            {
                return false;
            }

            var sourceSlot = new DummySlot(sourceStack);
            var op = new ItemStackMoveOperation(capi.World, EnumMouseButton.Left, (EnumModifierKey)0, EnumMergePriority.AutoMerge, sourceStack.StackSize)
            {
                ActingPlayer = capi.World.Player
            };

            object packet = capi.World.Player.InventoryManager?.TryTransferTo(sourceSlot, hoveredSlot, ref op);
            if (op.MovedQuantity <= 0 || packet == null)
            {
                return false;
            }

            capi.Network?.SendPacketClient(packet);
            return true;
        }

        private static bool TryHandleDrop(int mouseX, int mouseY)
        {
            if (draggingState == null || draggingPage == null)
            {
                return false;
            }

            if (!TryGetCategoryUnderMouse(draggingState, mouseX, mouseY, out HandbookTab tab))
            {
                return false;
            }

            string categoryCode = tab?.CategoryCode;
            if (string.IsNullOrEmpty(categoryCode) || !HandbookCategoryManager.IsManagedCategory(categoryCode))
            {
                return false;
            }

            string pageCode = GetEffectivePageCode(draggingPage, draggingSlot);
            if (string.IsNullOrWhiteSpace(pageCode))
            {
                return false;
            }

            DragState state = draggingState;
            float? scrollToRestore = CaptureScrollPosition(state);

            GuiDialogSurvivalHandbook dialog = draggingState.Dialog as GuiDialogSurvivalHandbook;
            bool shouldReselect = dialog != null
                && string.Equals(dialog.currentCatgoryCode, categoryCode, StringComparison.OrdinalIgnoreCase);

            capi?.Event?.EnqueueMainThreadTask(() =>
            {
                if (!HandbookCategoryManager.TryAddPageCodeMatchToCategory(categoryCode, pageCode))
                {
                    return;
                }

                if (state != null)
                {
                    state.PendingScrollPosition = scrollToRestore;
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

            return true;
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

        private static bool TryGetDetailPageUnderMouse(DragState state, int mouseX, int mouseY, out GuiHandbookPage page, out DummySlot slot)
        {
            page = null;
            slot = null;

            GuiComposer detailComposer = state?.Detail;
            GuiElementRichtext richtext = state?.DetailRichtext ?? detailComposer?.GetRichtext("richtext");

            if (detailComposer == null || richtext == null)
            {
                return false;
            }

            ElementBounds detailBounds = detailComposer.Bounds;
            if (detailBounds == null)
            {
                return false;
            }

            if (detailBounds.RequiresRecalculation)
            {
                detailBounds.CalcWorldBounds();
            }

            if (!detailBounds.PointInside(mouseX, mouseY))
            {
                return false;
            }

            ElementBounds richtextBounds = richtext.Bounds;
            if (richtextBounds == null)
            {
                return false;
            }

            if (richtextBounds.RequiresRecalculation)
            {
                richtextBounds.CalcWorldBounds();
            }

            if (!richtextBounds.PointInside(mouseX, mouseY))
            {
                return false;
            }

            GuiHandbookPage currentDetailPage = GetCurrentDetailPage(state.Dialog);
            DummySlot pageSlot = GetSlotForPage(currentDetailPage);
            if (currentDetailPage == null || pageSlot?.Itemstack == null)
            {
                return false;
            }

            RichTextComponentBase iconComponent = GetPrimaryIconComponent(currentDetailPage, richtext.Components);
            if (iconComponent == null)
            {
                return false;
            }

            double localX = mouseX - richtextBounds.absX;
            double localY = mouseY - richtextBounds.absY;

            if (!IsPointInsideComponent(iconComponent, localX, localY))
            {
                return false;
            }

            page = currentDetailPage;
            slot = pageSlot;
            state.DetailRichtext = richtext;
            return true;
        }

        private static RichTextComponentBase GetPrimaryIconComponent(GuiHandbookPage page, RichTextComponentBase[] components)
        {
            if (components == null || components.Length == 0 || page == null)
            {
                return null;
            }

            if (page is GuiHandbookMealRecipePage)
            {
                return components.FirstOrDefault(component => component is MealstackTextComponent);
            }

            if (page is GuiHandbookItemStackPage)
            {
                return components.FirstOrDefault(component => component is ItemstackTextComponent);
            }

            return null;
        }

        private static bool IsPointInsideComponent(RichTextComponentBase component, double localX, double localY)
        {
            if (component == null)
            {
                return false;
            }

            LineRectangled[] bounds = component.BoundsPerLine;
            if (bounds == null || bounds.Length == 0)
            {
                return false;
            }

            double adjustedX = localX;
            double adjustedY = localY;

            if (component is MealstackTextComponent mealComponent)
            {
                Vec3f offset = mealComponent.renderOffset ?? new Vec3f();
                adjustedX += offset.X;
                adjustedY += offset.Y;
            }

            foreach (LineRectangled rect in bounds)
            {
                if (rect != null && rect.PointInside(adjustedX, adjustedY))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveDetailComponent(GuiDialogHandbook dialog, GuiHandbookPage currentDetailPage, RichTextComponentBase component, double localX, double localY, out GuiHandbookPage page, out DummySlot slot)
        {
            page = null;
            slot = null;

            LineRectangled[] bounds = component.BoundsPerLine;
            if (bounds == null || bounds.Length == 0)
            {
                return false;
            }

            double adjustedX = localX;
            double adjustedY = localY;

            if (component is SlideshowItemstackTextComponent offsetSlideshow)
            {
                Vec3f offset = offsetSlideshow.renderOffset ?? new Vec3f();
                adjustedX += offset.X;
                adjustedY += offset.Y;
            }
            else if (component is MealstackTextComponent offsetMeal)
            {
                Vec3f offset = offsetMeal.renderOffset ?? new Vec3f();
                adjustedX += offset.X;
                adjustedY += offset.Y;
            }

            bool inside = false;
            foreach (LineRectangled rect in bounds)
            {
                if (rect != null && rect.PointInside(adjustedX, adjustedY))
                {
                    inside = true;
                    break;
                }
            }

            if (!inside)
            {
                return false;
            }

            ItemStack stack = component switch
            {
                ItemstackTextComponent itemComponent => GetItemstackFromItemComponent(itemComponent),
                SlideshowItemstackTextComponent slideshowComponent => GetItemstackFromSlideshowComponent(slideshowComponent),
                SlideshowGridRecipeTextComponent gridComponent => GetItemstackFromGridComponent(gridComponent, adjustedX, adjustedY),
                MealstackTextComponent mealComponent => GetItemstackFromMealComponent(mealComponent),
                _ => null
            };

            if (stack == null)
            {
                return false;
            }

            GuiHandbookPage resolvedPage = FindPageForStack(dialog, stack);
            if (resolvedPage == null && component is ItemstackTextComponent)
            {
                resolvedPage = currentDetailPage;
            }
            if (resolvedPage == null)
            {
                return false;
            }

            ItemStack clone = stack.Clone();
            if (clone == null)
            {
                return false;
            }

            slot = new DummySlot(clone);
            page = resolvedPage;
            return true;
        }

        private static ItemStack GetItemstackFromItemComponent(ItemstackTextComponent component)
        {
            if (ItemstackTextComponentSlotField?.GetValue(component) is DummySlot slot)
            {
                return slot.Itemstack;
            }

            return null;
        }

        private static ItemStack GetItemstackFromSlideshowComponent(SlideshowItemstackTextComponent component)
        {
            ItemStack stack = null;

            if (SlideshowItemstackSlotField?.GetValue(component) is ItemSlot slot)
            {
                stack = slot?.Itemstack;
            }

            if (stack == null)
            {
                ItemStack[] stacks = component.Itemstacks;
                if (stacks != null && stacks.Length > 0)
                {
                    stack = stacks[0];
                }
            }

            return stack;
        }

        private static ItemStack GetItemstackFromMealComponent(MealstackTextComponent component)
        {
            if (MealstackSlotField?.GetValue(component) is DummySlot slot)
            {
                return slot.Itemstack;
            }

            return null;
        }

        private static ItemStack GetItemstackFromGridComponent(SlideshowGridRecipeTextComponent component, double localX, double localY)
        {
            LineRectangled[] bounds = component.BoundsPerLine;
            if (bounds == null || bounds.Length == 0)
            {
                return null;
            }

            LineRectangled rect = bounds[0];
            if (rect == null)
            {
                return null;
            }

            double relativeX = localX - rect.X;
            double relativeY = localY - rect.Y;
            if (relativeX < 0.0 || relativeY < 0.0)
            {
                return null;
            }

            double size = GridRecipeSizeField != null ? (double)GridRecipeSizeField.GetValue(component) : 0.0;
            if (size <= 0.0)
            {
                return null;
            }

            double spacing = size + 3.0;
            int column = (int)(relativeX / spacing);
            int row = (int)(relativeY / spacing);

            GridRecipeAndUnnamedIngredients visibleRecipe = component.CurrentVisibleRecipe;
            if (visibleRecipe == null)
            {
                GridRecipeAndUnnamedIngredients[] allRecipes = component.GridRecipesAndUnIn;
                if (allRecipes == null || allRecipes.Length == 0)
                {
                    return null;
                }

                visibleRecipe = allRecipes[0];
            }

            GridRecipe recipe = visibleRecipe?.Recipe;
            if (recipe == null)
            {
                return null;
            }

            if (column < 0 || column >= recipe.Width || row < 0 || row >= recipe.Height)
            {
                return null;
            }

            CraftingRecipeIngredient ingredient = recipe.GetElementInGrid(row, column, recipe.resolvedIngredients, recipe.Width);
            if (ingredient == null)
            {
                return null;
            }

            ItemStack stack = null;
            int gridIndex = recipe.GetGridIndex(row, column, recipe.resolvedIngredients, recipe.Width);
            Dictionary<int, ItemStack[]> unnamed = visibleRecipe.unnamedIngredients;

            if (unnamed != null && unnamed.TryGetValue(gridIndex, out ItemStack[] variants) && variants != null && variants.Length > 0)
            {
                int variantIndex = GetGridVariantIndex(component, column, row, variants.Length);
                ItemStack variant = variants[Math.Clamp(variantIndex, 0, variants.Length - 1)];
                stack = variant?.Clone();
            }
            else if (ingredient.ResolvedItemstack != null)
            {
                stack = ingredient.ResolvedItemstack.Clone();
            }

            if (stack != null)
            {
                stack.StackSize = Math.Max(ingredient.Quantity, 1);
            }

            return stack;
        }

        private static int GetGridVariantIndex(SlideshowGridRecipeTextComponent component, int column, int row, int variantCount)
        {
            if (variantCount <= 0)
            {
                return 0;
            }

            int secondCounter = 0;
            if (GridRecipeSecondCounterField != null)
            {
                secondCounter = Math.Abs((int)GridRecipeSecondCounterField.GetValue(component));
            }

            if (GridRecipeVariantSequenceField?.GetValue(null) is int[][,] sequences && sequences.Length > 0)
            {
                int index = secondCounter % sequences.Length;
                int[,] sequence = sequences[index];
                if (sequence != null)
                {
                    int value = sequence[column, row];
                    return Math.Abs(value) % variantCount;
                }
            }

            return secondCounter % variantCount;
        }

        private static GuiHandbookPage FindPageForStack(GuiDialogHandbook dialog, ItemStack stack)
        {
            if (dialog == null || stack == null)
            {
                return null;
            }

            string pageCode = GuiHandbookItemStackPage.PageCodeForStack(stack);
            GuiHandbookPage page = TryGetPageByCode(dialog, pageCode);
            if (page != null)
            {
                return page;
            }

            if (stack.Collectible != null)
            {
                try
                {
                    var baseStack = new ItemStack(stack.Collectible);
                    pageCode = GuiHandbookItemStackPage.PageCodeForStack(baseStack);
                    page = TryGetPageByCode(dialog, pageCode);
                    if (page != null)
                    {
                        return page;
                    }
                }
                catch
                {
                    // Ignore failures when constructing a fallback stack.
                }
            }

            return null;
        }

        private static GuiHandbookPage TryGetPageByCode(GuiDialogHandbook dialog, string pageCode)
        {
            if (dialog == null || string.IsNullOrWhiteSpace(pageCode))
            {
                return null;
            }

            try
            {
                if (PageNumberByPageCodeField?.GetValue(dialog) is Dictionary<string, int> pageLookup
                    && AllPagesField?.GetValue(dialog) is List<GuiHandbookPage> allPages
                    && allPages != null)
                {
                    if (pageLookup.TryGetValue(pageCode, out int index) && index >= 0 && index < allPages.Count)
                    {
                        return allPages[index];
                    }

                    string normalized = pageCode.ToLowerInvariant();
                    if (!string.Equals(pageCode, normalized, StringComparison.Ordinal) && pageLookup.TryGetValue(normalized, out index)
                        && index >= 0 && index < allPages.Count)
                    {
                        return allPages[index];
                    }

                    return allPages.FirstOrDefault(existing => existing != null
                        && !string.IsNullOrEmpty(existing.PageCode)
                        && string.Equals(existing.PageCode, pageCode, StringComparison.OrdinalIgnoreCase));
                }
            }
            catch
            {
                // Ignore reflection issues and fall back to null.
            }

            return null;
        }

        private static GuiHandbookPage GetCurrentDetailPage(GuiDialogHandbook dialog)
        {
            if (dialog == null || BrowseHistoryField == null)
            {
                return null;
            }

            try
            {
                if (BrowseHistoryField.GetValue(dialog) is Stack<BrowseHistoryElement> history && history.Count > 0)
                {
                    return history.Peek()?.Page;
                }
            }
            catch
            {
                // Ignore reflection errors and return null.
            }

            return null;
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
            draggingCategoryCode = null;
            suppressListClick = false;
            wasLeftDown = false;
            ResetPending();
        }

        private static void ResetPending()
        {
            pendingState = null;
            pendingPage = null;
            pendingSlot = null;
            pendingCategoryCode = null;
        }

        private static bool IsHandbookObscured(DragState state, int mouseX, int mouseY)
        {
            GuiDialog dialog = state?.Dialog;
            if (dialog?.Composers == null)
            {
                return false;
            }

            foreach (GuiComposer composer in dialog.Composers.Values)
            {
                if (composer == null || ReferenceEquals(composer, state.Overview))
                {
                    continue;
                }

                if (ReferenceEquals(composer, state.Detail))
                {
                    continue;
                }

                ElementBounds bounds = composer.Bounds;
                if (bounds == null)
                {
                    continue;
                }

                if (bounds.RequiresRecalculation)
                {
                    bounds.CalcWorldBounds();
                }

                if (bounds.PointInside(mouseX, mouseY))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsMouseInsideDialog(GuiDialog dialog, int mouseX, int mouseY)
        {
            if (dialog?.Composers == null)
            {
                return false;
            }

            foreach (GuiComposer composer in dialog.Composers.Values)
            {
                ElementBounds bounds = composer?.Bounds;
                if (bounds == null)
                {
                    continue;
                }

                if (bounds.RequiresRecalculation)
                {
                    bounds.CalcWorldBounds();
                }

                if (bounds.PointInside(mouseX, mouseY))
                {
                    return true;
                }
            }

            GuiComposer singleComposer = dialog.SingleComposer;
            ElementBounds singleBounds = singleComposer?.Bounds;
            if (singleBounds != null)
            {
                if (singleBounds.RequiresRecalculation)
                {
                    singleBounds.CalcWorldBounds();
                }

                if (singleBounds.PointInside(mouseX, mouseY))
                {
                    return true;
                }
            }

            return false;
        }

        private static void TryHandleRemoval(int mouseX, int mouseY)
        {
            if (draggingPage == null || draggingState?.Dialog is not GuiDialogSurvivalHandbook dialog)
            {
                return;
            }

            string categoryCode = draggingCategoryCode;
            if (string.IsNullOrEmpty(categoryCode) || !HandbookCategoryManager.IsManagedCategory(categoryCode))
            {
                return;
            }

            if (!string.Equals(dialog.currentCatgoryCode, categoryCode, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (IsMouseInsideHandbookGui(draggingState, mouseX, mouseY))
            {
                return;
            }

            if (!IsPageInCategory(categoryCode, draggingPage))
            {
                return;
            }

            string pageCode = GetEffectivePageCode(draggingPage, draggingSlot);
            if (string.IsNullOrWhiteSpace(pageCode))
            {
                return;
            }

            DragState state = draggingState;
            float? scrollToRestore = CaptureScrollPosition(state);

            capi?.Event?.EnqueueMainThreadTask(() =>
            {
                if (!HandbookCategoryManager.TryAddForbiddenPageCodeToCategory(categoryCode, pageCode))
                {
                    return;
                }

                if (state != null)
                {
                    state.PendingScrollPosition = scrollToRestore;
                }

                HandbookCategoryPatches.RebuildTabs(dialog);

                if (string.Equals(dialog.currentCatgoryCode, categoryCode, StringComparison.OrdinalIgnoreCase))
                {
                    dialog.selectTab(categoryCode);
                }
            }, $"handbookcategories-remove-{Guid.NewGuid():N}");
        }

        private static string GetEffectivePageCode(GuiHandbookPage page, DummySlot slot)
        {
            string pageCode = page?.PageCode;
            if (!string.IsNullOrWhiteSpace(pageCode))
            {
                return pageCode;
            }

            ItemStack stack = slot?.Itemstack;
            if (stack == null && page is GuiHandbookItemStackPage itemPage)
            {
                stack = itemPage.Stack;
            }

            if (stack == null)
            {
                return string.Empty;
            }

            try
            {
                return GuiHandbookItemStackPage.PageCodeForStack(stack);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static float? CaptureScrollPosition(DragState state)
        {
            GuiComposer overview = state?.Overview;
            GuiElementScrollbar scrollbar = overview?.GetScrollbar("scrollbar");

            if (scrollbar == null)
            {
                return null;
            }

            float position = scrollbar.CurrentYPosition;
            return float.IsNaN(position) ? null : position;
        }

        private static void TryRestorePendingScroll(DragState state)
        {
            if (state?.PendingScrollPosition is not float target)
            {
                return;
            }

            try
            {
                GuiComposer overview = state.Overview;
                GuiElementFlatList list = state.SearchList ?? overview?.GetFlatList("stacklist");
                GuiElementScrollbar scrollbar = overview?.GetScrollbar("scrollbar");

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

                float clamped = float.IsNaN(target)
                    ? 0f
                    : Math.Clamp(target, 0f, (float)Math.Max(0.0, totalHeight - visibleHeight));

                scrollbar.CurrentYPosition = clamped;
                scrollbar.TriggerChanged();
            }
            finally
            {
                state.PendingScrollPosition = null;
            }
        }

        private static bool IsMouseInsideHandbookGui(DragState state, int mouseX, int mouseY)
        {
            bool evaluatedBounds = false;

            if (state?.Dialog?.Composers != null)
            {
                foreach (GuiComposer composer in state.Dialog.Composers.Values)
                {
                    ElementBounds composerBounds = composer?.Bounds;
                    if (composerBounds == null)
                    {
                        continue;
                    }

                    evaluatedBounds = true;

                    if (composerBounds.RequiresRecalculation)
                    {
                        composerBounds.CalcWorldBounds();
                    }

                    if (composerBounds.PointInside(mouseX, mouseY))
                    {
                        return true;
                    }
                }
            }

            ElementBounds overviewBounds = state?.Overview?.Bounds;
            if (overviewBounds != null)
            {
                evaluatedBounds = true;

                if (overviewBounds.RequiresRecalculation)
                {
                    overviewBounds.CalcWorldBounds();
                }

                if (overviewBounds.PointInside(mouseX, mouseY))
                {
                    return true;
                }
            }

            ElementBounds detailBounds = state?.Detail?.Bounds;
            if (detailBounds != null)
            {
                evaluatedBounds = true;

                if (detailBounds.RequiresRecalculation)
                {
                    detailBounds.CalcWorldBounds();
                }

                if (detailBounds.PointInside(mouseX, mouseY))
                {
                    return true;
                }
            }

            return !evaluatedBounds;
        }

        private static bool IsPageInCategory(string categoryCode, GuiHandbookPage page)
        {
            if (string.IsNullOrEmpty(categoryCode) || page == null)
            {
                return false;
            }

            if (!HandbookCategoryManager.TryGetCategoryPages(categoryCode, out List<GuiHandbookPage> pages) || pages == null)
            {
                return false;
            }

            string pageCode = page.PageCode;
            if (string.IsNullOrEmpty(pageCode))
            {
                return pages.Contains(page);
            }

            return pages.Any(existing => existing != null && string.Equals(existing.PageCode, pageCode, StringComparison.OrdinalIgnoreCase));
        }

    }
}
