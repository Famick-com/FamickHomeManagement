using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Pages;

/// <summary>
/// Tests for equipment parent picker logic.
/// Mirrors the parent selection logic from EquipmentEditPage.xaml.cs.
/// </summary>
public class EquipmentParentPickerTests
{
    [Fact]
    public void ParentOptions_ExcludesSelf()
    {
        var currentId = Guid.NewGuid();
        var items = new List<TestEquipment>
        {
            new() { Id = Guid.NewGuid(), Name = "Equipment A" },
            new() { Id = currentId, Name = "Current Equipment" },
            new() { Id = Guid.NewGuid(), Name = "Equipment B" },
        };

        var options = items.Where(e => e.Id != currentId).ToList();

        options.Should().HaveCount(2);
        options.Select(e => e.Name).Should().NotContain("Current Equipment");
    }

    [Fact]
    public void ParentOptions_WhenNewEquipment_IncludesAll()
    {
        var currentId = Guid.Empty; // No current ID for new equipment
        var items = new List<TestEquipment>
        {
            new() { Id = Guid.NewGuid(), Name = "Equipment A" },
            new() { Id = Guid.NewGuid(), Name = "Equipment B" },
        };

        var options = items.Where(e => e.Id != currentId).ToList();
        options.Should().HaveCount(2);
    }

    [Fact]
    public void PickerIndex_NoneSelected_ReturnsNullParentId()
    {
        var items = new List<TestEquipment>
        {
            new() { Id = Guid.NewGuid(), Name = "Equipment A" },
        };

        // Index 0 = "(None)"
        var selectedIndex = 0;
        Guid? parentId = selectedIndex > 0 ? items[selectedIndex - 1].Id : null;

        parentId.Should().BeNull();
    }

    [Fact]
    public void PickerIndex_ItemSelected_ReturnsCorrectParentId()
    {
        var expectedId = Guid.NewGuid();
        var items = new List<TestEquipment>
        {
            new() { Id = expectedId, Name = "Equipment A" },
            new() { Id = Guid.NewGuid(), Name = "Equipment B" },
        };

        // Index 1 = first real item (items[0])
        var selectedIndex = 1;
        Guid? parentId = selectedIndex > 0 ? items[selectedIndex - 1].Id : null;

        parentId.Should().Be(expectedId);
    }

    [Fact]
    public void PopulateForm_SetsCorrectPickerIndex()
    {
        var parentId = Guid.NewGuid();
        var items = new List<TestEquipment>
        {
            new() { Id = Guid.NewGuid(), Name = "Equipment A" },
            new() { Id = parentId, Name = "Parent Equipment" },
            new() { Id = Guid.NewGuid(), Name = "Equipment C" },
        };

        var parentIndex = items.FindIndex(e => e.Id == parentId);
        var pickerIndex = parentIndex >= 0 ? parentIndex + 1 : 0; // +1 for "(None)"

        pickerIndex.Should().Be(2); // "(None)" + "Equipment A" + "Parent Equipment"
    }

    [Fact]
    public void PopulateForm_NoParent_SelectsNone()
    {
        Guid? parentId = null;
        var items = new List<TestEquipment>
        {
            new() { Id = Guid.NewGuid(), Name = "Equipment A" },
        };

        var pickerIndex = parentId.HasValue
            ? items.FindIndex(e => e.Id == parentId.Value) + 1
            : 0;

        pickerIndex.Should().Be(0);
    }

    private class TestEquipment
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
    }
}
