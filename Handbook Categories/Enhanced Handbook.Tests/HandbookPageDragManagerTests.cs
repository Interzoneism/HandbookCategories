using System;
using System.Reflection;
using Enhanced_Handbook;
using Xunit;

namespace Enhanced_Handbook.Tests;

public sealed class HandbookPageDragManagerTests
{
    [Theory]
    [InlineData(false, false, "None")]
    [InlineData(false, true, "Ctrl")]
    [InlineData(true, false, "Shift")]
    [InlineData(true, true, "ShiftCtrl")]
    public void GetGroupClickModifierActionTreatsShiftCtrlAsDistinctAction(bool shiftHeld, bool ctrlHeld, string expected)
    {
        Type dragManagerType = typeof(Handbook_CategoriesModSystem).Assembly.GetType("Enhanced_Handbook.HandbookPageDragManager", throwOnError: true)!;
        MethodInfo method = dragManagerType.GetMethod("GetGroupClickModifierAction", BindingFlags.NonPublic | BindingFlags.Static)!;

        object result = method.Invoke(null, new object[] { shiftHeld, ctrlHeld })!;

        Assert.Equal(expected, result.ToString());
    }
}
