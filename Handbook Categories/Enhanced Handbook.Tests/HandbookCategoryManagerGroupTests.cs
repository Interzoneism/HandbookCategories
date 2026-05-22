using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Enhanced_Handbook;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Xunit;

namespace Enhanced_Handbook.Tests;

public sealed class HandbookCategoryManagerGroupTests
{
    [Fact]
    public void BuildEverythingGroupsCategoryPagesDoesNotReplaceSingletonMembersWithGroup()
    {
        Type managerType = typeof(Handbook_CategoriesModSystem).Assembly.GetType("Enhanced_Handbook.HandbookCategoryManager", throwOnError: true)!;
        Type groupType = typeof(Handbook_CategoriesModSystem).Assembly.GetType("Enhanced_Handbook.GroupHandbookPage", throwOnError: true)!;
        MethodInfo method = managerType.GetMethod("BuildEverythingGroupsCategoryPages", BindingFlags.NonPublic | BindingFlags.Static)!;
        var bookPage = new TestHandbookPage("item-lore-book-aged-gray", "stack", "Gray Book");
        object singletonGroup = Activator.CreateInstance(
            groupType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: new object[] { "group-gray-book", "hidden-gray-book", "handbookcategories-everything-groups", "Gray Book", new[] { bookPage } },
            culture: null)!;
        IList groups = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(groupType))!;
        groups.Add(singletonGroup);

        var result = (List<GuiHandbookPage>)method.Invoke(null, new object[] { new List<GuiHandbookPage> { bookPage }, groups })!;

        GuiHandbookPage onlyPage = Assert.Single(result);
        Assert.Same(bookPage, onlyPage);
    }

    [Fact]
    public void ShouldHidePageForCategoryKeepsSingletonMemberWhenEverythingGroupIsSuppressed()
    {
        Type managerType = typeof(Handbook_CategoriesModSystem).Assembly.GetType("Enhanced_Handbook.HandbookCategoryManager", throwOnError: true)!;
        Type groupType = typeof(Handbook_CategoriesModSystem).Assembly.GetType("Enhanced_Handbook.GroupHandbookPage", throwOnError: true)!;
        MethodInfo method = managerType.GetMethod("ShouldHidePageForCategory", BindingFlags.NonPublic | BindingFlags.Static)!;
        FieldInfo groupsByMemberPageField = managerType.GetField("groupsByMemberPage", BindingFlags.NonPublic | BindingFlags.Static)!;
        var barrelPage = new TestHandbookPage("item-barrel", "stack", "Barrel");
        object singletonGroup = Activator.CreateInstance(
            groupType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: new object[] { "group-barrel", "hidden-barrel", "handbookcategories-everything-groups", "Barrel", new[] { barrelPage } },
            culture: null)!;
        IDictionary groupsByMemberPage = (IDictionary)groupsByMemberPageField.GetValue(null)!;
        object groupList = Activator.CreateInstance(typeof(List<>).MakeGenericType(groupType))!;
        ((IList)groupList).Add(singletonGroup);
        groupsByMemberPage[barrelPage] = groupList;

        try
        {
            bool shouldHide = (bool)method.Invoke(null, new object[] { barrelPage, "handbookcategories-everything-groups" })!;

            Assert.False(shouldHide);
        }
        finally
        {
            groupsByMemberPage.Remove(barrelPage);
        }
    }

    [Fact]
    public void BuildEverythingGroupsCategoryPagesKeepsUserConfiguredSingletonGroup()
    {
        Type managerType = typeof(Handbook_CategoriesModSystem).Assembly.GetType("Enhanced_Handbook.HandbookCategoryManager", throwOnError: true)!;
        Type groupType = typeof(Handbook_CategoriesModSystem).Assembly.GetType("Enhanced_Handbook.GroupHandbookPage", throwOnError: true)!;
        Type configEntryType = typeof(Handbook_CategoriesModSystem).Assembly.GetType("Enhanced_Handbook.HandbookGroupConfigEntry", throwOnError: true)!;
        MethodInfo method = managerType.GetMethod("BuildEverythingGroupsCategoryPages", BindingFlags.NonPublic | BindingFlags.Static)!;
        FieldInfo groupConfigEntriesField = managerType.GetField("groupConfigEntriesByHiddenCode", BindingFlags.NonPublic | BindingFlags.Static)!;
        var barrelPage = new TestHandbookPage("item-barrel", "stack", "Barrel");
        object singletonGroup = Activator.CreateInstance(
            groupType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: new object[] { "group-barrel", "hidden-barrel", "handbookcategories-everything-groups", "Barrel", new[] { barrelPage } },
            culture: null)!;
        IList groups = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(groupType))!;
        groups.Add(singletonGroup);
        IDictionary configEntries = (IDictionary)groupConfigEntriesField.GetValue(null)!;
        object configEntry = Activator.CreateInstance(configEntryType)!;
        configEntries["hidden-barrel"] = configEntry;

        try
        {
            var result = (List<GuiHandbookPage>)method.Invoke(null, new object[] { new List<GuiHandbookPage> { barrelPage }, groups })!;

            GuiHandbookPage onlyPage = Assert.Single(result);
            Assert.Same(singletonGroup, onlyPage);
        }
        finally
        {
            configEntries.Remove("hidden-barrel");
        }
    }

    [Fact]
    public void RestoreDialogSearchTextKeepsPrivateSearchStateAfterOverviewReload()
    {
        Type managerType = typeof(Handbook_CategoriesModSystem).Assembly.GetType("Enhanced_Handbook.HandbookCategoryManager", throwOnError: true)!;
        MethodInfo method = managerType.GetMethod("RestoreDialogSearchText", BindingFlags.NonPublic | BindingFlags.Static)!;
        object dialog = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(GuiDialogHandbook));
        FieldInfo currentSearchTextField = typeof(GuiDialogHandbook).GetField("currentSearchText", BindingFlags.Instance | BindingFlags.NonPublic)!;

        method.Invoke(null, new[] { dialog, "resin" });

        Assert.Equal("resin", currentSearchTextField.GetValue(dialog));
    }

    private sealed class TestHandbookPage : GuiHandbookPage
    {
        private readonly string pageCode;
        private readonly string categoryCode;
        private readonly string title;

        internal TestHandbookPage(string pageCode, string categoryCode, string title)
        {
            this.pageCode = pageCode;
            this.categoryCode = categoryCode;
            this.title = title;
        }

        public override string PageCode => pageCode;

        public override string CategoryCode => categoryCode;

        public override bool IsDuplicate => false;

        public override float SearchWeightOffset => 0f;

        public override void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWdith, double cellHeight)
        {
        }

        public override void Dispose()
        {
        }

        public override PageText GetPageText()
        {
            return new PageText { Title = title, Text = title };
        }

        public override void ComposePage(GuiComposer detailViewGui, ElementBounds textBounds, ItemStack[] allstacks, ActionConsumable<string> openDetailPageFor)
        {
        }
    }
}
