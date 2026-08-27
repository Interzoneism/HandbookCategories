using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Enhanced_Handbook;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Xunit;

namespace Enhanced_Handbook.Tests;

public sealed class HandbookCategoryManagerGroupTests
{
    [Fact]
    public void DefaultHandbookGroupsFollowConfiguredGroupByWildcard()
    {
        IList groups = CollectDefaultHandbookGroups(
            CreateItemPage("book-normal-brickred", "Brick red book", "book-*", 0),
            CreateItemPage("book-normal-cherryred", "Cherry red book", "book-*", 1),
            CreateItemPage("book-aged-gray", "Gray book", "book-*", 2));

        object group = Assert.Single(groups.Cast<object>());
        Assert.Equal("Book", ReadProperty<string>(group, "DisplayName"));
        Assert.Equal(3, ReadProperty<IList>(group, "Members").Count);
    }

    [Fact]
    public void DefaultHandbookGroupsKeepStoneAndMetalToolsTogether()
    {
        IList groups = CollectDefaultHandbookGroups(
            CreateItemPage("knife-generic-chert", "Chert knife", "knife-*", 0),
            CreateItemPage("knife-generic-bonechert", "Chert knife", "knife-*", 1),
            CreateItemPage("knife-generic-copper", "Copper knife", "knife-*", 2),
            CreateItemPage("knife-generic-steel", "Steel knife", "knife-*", 3));

        object group = Assert.Single(groups.Cast<object>());
        Assert.Equal("Knife", ReadProperty<string>(group, "DisplayName"));
        Assert.Equal(4, ReadProperty<IList>(group, "Members").Count);
    }

    [Fact]
    public void DiscoveringExplicitWoodVariantDoesNotTeachUnrelatedVariantValuesAsWood()
    {
        Type managerType = GetManagerType();
        FieldInfo knownField = managerType.GetField("knownWoodVariantNames", BindingFlags.NonPublic | BindingFlags.Static)!;
        FieldInfo displayField = managerType.GetField("woodVariantDisplayNameByCode", BindingFlags.NonPublic | BindingFlags.Static)!;
        FieldInfo loadedField = managerType.GetField("woodVariantsLoaded", BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo method = managerType.GetMethod("TryGetWoodVariantInfo", BindingFlags.NonPublic | BindingFlags.Static)!;
        var known = (ISet<string>)knownField.GetValue(null)!;
        var display = (IDictionary)displayField.GetValue(null)!;
        string[] originalKnown = known.ToArray();
        var originalDisplay = display.Cast<DictionaryEntry>().ToDictionary(entry => entry.Key, entry => entry.Value);
        bool originalLoaded = (bool)loadedField.GetValue(null)!;

        try
        {
            known.Clear();
            known.Add("oak");
            display.Clear();
            loadedField.SetValue(null, true);

            Item explicitWoodItem = CreateItem("modded-board-chert", null, new Dictionary<string, string> { ["wood"] = "chert" });
            Item unrelatedItem = CreateItem("knife-generic-chert", null, new Dictionary<string, string> { ["material"] = "chert" });

            Assert.True(InvokeTryGetVariantInfo(method, new ItemStack(explicitWoodItem)));
            Assert.False(InvokeTryGetVariantInfo(method, new ItemStack(unrelatedItem)));
            Assert.DoesNotContain("chert", known);
        }
        finally
        {
            known.Clear();
            foreach (string value in originalKnown)
            {
                known.Add(value);
            }

            display.Clear();
            foreach (KeyValuePair<object, object> entry in originalDisplay)
            {
                display[entry.Key] = entry.Value;
            }

            loadedField.SetValue(null, originalLoaded);
        }
    }

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

    private static IList CollectDefaultHandbookGroups(params GuiHandbookItemStackPage[] pages)
    {
        MethodInfo method = GetManagerType().GetMethod("CollectDefaultHandbookGroupInfos", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (IList)method.Invoke(null, new object[] { pages.Cast<GuiHandbookPage>().ToList() })!;
    }

    private static GuiHandbookItemStackPage CreateItemPage(string code, string title, string groupBy, int pageNumber)
    {
        Item item = CreateItem(code, groupBy, null);
        var page = (GuiHandbookItemStackPage)RuntimeHelpers.GetUninitializedObject(typeof(GuiHandbookItemStackPage));
        page.Stack = new ItemStack(item);
        page.TextCacheTitle = title;
        page.TextCacheAll = title;
        page.PageNumber = pageNumber;
        return page;
    }

    private static Item CreateItem(string code, string groupBy, Dictionary<string, string> variants)
    {
        var item = new Item
        {
            Code = new AssetLocation("game", code)
        };

        if (!string.IsNullOrWhiteSpace(groupBy))
        {
            item.Attributes = JsonObject.FromJson($"{{\"handbook\":{{\"groupBy\":[\"{groupBy}\"]}}}}");
        }

        if (variants != null)
        {
            foreach (KeyValuePair<string, string> variant in variants)
            {
                item.VariantStrict[variant.Key] = variant.Value;
            }

            item.Variant = new RelaxedReadOnlyDictionary<string, string>(item.VariantStrict);
        }

        return item;
    }

    private static bool InvokeTryGetVariantInfo(MethodInfo method, ItemStack stack)
    {
        object[] args = { stack, null };
        return (bool)method.Invoke(null, args)!;
    }

    private static Type GetManagerType()
    {
        return typeof(Handbook_CategoriesModSystem).Assembly.GetType("Enhanced_Handbook.HandbookCategoryManager", throwOnError: true)!;
    }

    private static T ReadProperty<T>(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!;
        return (T)property.GetValue(instance)!;
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
