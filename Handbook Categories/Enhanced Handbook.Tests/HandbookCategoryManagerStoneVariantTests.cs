using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Enhanced_Handbook;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Xunit;

namespace Enhanced_Handbook.Tests;

public sealed class HandbookCategoryManagerStoneVariantTests
{
    [Fact]
    public void TryGetStoneVariantInfoTreatsBoneHandledStoneToolsAsStoneVariant()
    {
        Type managerType = typeof(Handbook_CategoriesModSystem).Assembly.GetType("Enhanced_Handbook.HandbookCategoryManager", throwOnError: true)!;
        SeedKnownStoneVariants(managerType, "granite");

        Item item = CreateItem("knife-generic-bonegranite", new Dictionary<string, string>
        {
            ["type"] = "generic",
            ["material"] = "bonegranite"
        });
        var stack = new ItemStack(item);
        MethodInfo method = managerType.GetMethod("TryGetStoneVariantInfo", BindingFlags.NonPublic | BindingFlags.Static)!;
        object[] args = { stack, null };

        bool result = (bool)method.Invoke(null, args)!;

        Assert.True(result);
        object info = args[1]!;
        Assert.Equal("granite", ReadProperty(info, "Value"));
        Assert.Equal("granite", ReadProperty(info, "NormalizedValue"));
    }

    private static Item CreateItem(string code, Dictionary<string, string> variants)
    {
        var item = new Item
        {
            Code = new AssetLocation("game", code),
            Tool = EnumTool.Knife
        };

        foreach (KeyValuePair<string, string> variant in variants)
        {
            item.VariantStrict[variant.Key] = variant.Value;
        }

        item.Variant = new RelaxedReadOnlyDictionary<string, string>(item.VariantStrict);
        return item;
    }

    private static void SeedKnownStoneVariants(Type managerType, params string[] stoneNames)
    {
        FieldInfo knownField = managerType.GetField("knownStoneVariantNames", BindingFlags.NonPublic | BindingFlags.Static)!;
        FieldInfo displayField = managerType.GetField("stoneVariantDisplayNameByCode", BindingFlags.NonPublic | BindingFlags.Static)!;
        FieldInfo loadedField = managerType.GetField("stoneVariantsLoaded", BindingFlags.NonPublic | BindingFlags.Static)!;

        var known = (ISet<string>)knownField.GetValue(null)!;
        known.Clear();
        foreach (string stoneName in stoneNames)
        {
            known.Add(stoneName);
        }

        var display = (IDictionary)displayField.GetValue(null)!;
        display.Clear();
        display["granite"] = "Granite";

        loadedField.SetValue(null, true);
    }

    private static string ReadProperty(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!;
        return (string)property.GetValue(instance)!;
    }
}
