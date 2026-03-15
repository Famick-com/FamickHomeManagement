using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Pages;

/// <summary>
/// Tests for equipment list tree-building logic (parent-child hierarchy with expand/collapse).
/// Recreates the display model and tree logic to avoid MAUI project dependency.
/// </summary>
public class EquipmentTreeViewTests
{
    private class TestEquipmentItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid? ParentEquipmentId { get; set; }
        public int ChildCount { get; set; }
        public bool IsExpanded { get; set; }
        public int IndentLevel { get; set; }
        public bool HasChildren => ChildCount > 0;
        public string ExpandIcon => IsExpanded ? "▼" : "▶";
        public int IndentWidth => IndentLevel * 24;
    }

    /// <summary>
    /// Mirrors RebuildTreeView + AddChildrenRecursive from EquipmentListPage.xaml.cs
    /// </summary>
    private static List<TestEquipmentItem> BuildTreeView(
        List<TestEquipmentItem> allItems, HashSet<Guid> expandedIds)
    {
        var result = new List<TestEquipmentItem>();

        var childrenByParent = allItems
            .Where(i => i.ParentEquipmentId.HasValue)
            .GroupBy(i => i.ParentEquipmentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.Name).ToList());

        var topLevel = allItems
            .Where(i => !i.ParentEquipmentId.HasValue)
            .OrderBy(i => i.Name)
            .ToList();

        foreach (var item in topLevel)
        {
            item.IndentLevel = 0;
            item.IsExpanded = expandedIds.Contains(item.Id);
            result.Add(item);

            if (item.IsExpanded)
                AddChildrenRecursive(item.Id, 1, childrenByParent, expandedIds, result);
        }

        return result;
    }

    private static void AddChildrenRecursive(Guid parentId, int indentLevel,
        Dictionary<Guid, List<TestEquipmentItem>> childrenByParent,
        HashSet<Guid> expandedIds, List<TestEquipmentItem> result)
    {
        if (!childrenByParent.TryGetValue(parentId, out var children))
            return;

        foreach (var child in children)
        {
            child.IndentLevel = indentLevel;
            child.IsExpanded = expandedIds.Contains(child.Id);
            result.Add(child);

            if (child.IsExpanded)
                AddChildrenRecursive(child.Id, indentLevel + 1, childrenByParent, expandedIds, result);
        }
    }

    private readonly Guid _lawnMowerId = Guid.NewGuid();
    private readonly Guid _bladeId = Guid.NewGuid();
    private readonly Guid _filterId = Guid.NewGuid();
    private readonly Guid _hvacId = Guid.NewGuid();
    private readonly Guid _thermostatId = Guid.NewGuid();
    private readonly Guid _drillId = Guid.NewGuid();

    private List<TestEquipmentItem> CreateTestItems()
    {
        return new List<TestEquipmentItem>
        {
            new() { Id = _lawnMowerId, Name = "Lawn Mower", ChildCount = 2 },
            new() { Id = _bladeId, Name = "Blade", ParentEquipmentId = _lawnMowerId },
            new() { Id = _filterId, Name = "Air Filter", ParentEquipmentId = _lawnMowerId },
            new() { Id = _hvacId, Name = "HVAC System", ChildCount = 1 },
            new() { Id = _thermostatId, Name = "Thermostat", ParentEquipmentId = _hvacId },
            new() { Id = _drillId, Name = "Drill", ChildCount = 0 },
        };
    }

    [Fact]
    public void AllCollapsed_ShowsOnlyTopLevelItems()
    {
        var items = CreateTestItems();
        var result = BuildTreeView(items, new HashSet<Guid>());

        result.Should().HaveCount(3);
        result.Select(r => r.Name).Should().ContainInOrder("Drill", "HVAC System", "Lawn Mower");
    }

    [Fact]
    public void AllCollapsed_TopLevelItemsHaveIndentZero()
    {
        var items = CreateTestItems();
        var result = BuildTreeView(items, new HashSet<Guid>());

        result.Should().AllSatisfy(i => i.IndentLevel.Should().Be(0));
    }

    [Fact]
    public void ExpandParent_ShowsChildren()
    {
        var items = CreateTestItems();
        var expanded = new HashSet<Guid> { _lawnMowerId };
        var result = BuildTreeView(items, expanded);

        result.Should().HaveCount(5); // 3 top-level + 2 lawn mower children
        result.Select(r => r.Name).Should().ContainInOrder(
            "Drill", "HVAC System", "Lawn Mower", "Air Filter", "Blade");
    }

    [Fact]
    public void ExpandedChildren_HaveCorrectIndentLevel()
    {
        var items = CreateTestItems();
        var expanded = new HashSet<Guid> { _lawnMowerId };
        var result = BuildTreeView(items, expanded);

        var airFilter = result.First(i => i.Id == _filterId);
        var blade = result.First(i => i.Id == _bladeId);

        airFilter.IndentLevel.Should().Be(1);
        blade.IndentLevel.Should().Be(1);
    }

    [Fact]
    public void ChildrenSortedAlphabetically()
    {
        var items = CreateTestItems();
        var expanded = new HashSet<Guid> { _lawnMowerId };
        var result = BuildTreeView(items, expanded);

        var lawnMowerIndex = result.FindIndex(i => i.Id == _lawnMowerId);
        result[lawnMowerIndex + 1].Name.Should().Be("Air Filter");
        result[lawnMowerIndex + 2].Name.Should().Be("Blade");
    }

    [Fact]
    public void ExpandMultipleParents_ShowsAllChildren()
    {
        var items = CreateTestItems();
        var expanded = new HashSet<Guid> { _lawnMowerId, _hvacId };
        var result = BuildTreeView(items, expanded);

        result.Should().HaveCount(6); // all items
    }

    [Fact]
    public void NestedExpand_ShowsGrandchildren()
    {
        var grandchildId = Guid.NewGuid();
        var items = CreateTestItems();
        // Make thermostat have a child
        items.First(i => i.Id == _thermostatId).ChildCount = 1;
        items.Add(new TestEquipmentItem
        {
            Id = grandchildId, Name = "Temperature Sensor",
            ParentEquipmentId = _thermostatId
        });

        var expanded = new HashSet<Guid> { _hvacId, _thermostatId };
        var result = BuildTreeView(items, expanded);

        var sensor = result.First(i => i.Id == grandchildId);
        sensor.IndentLevel.Should().Be(2);
        sensor.IndentWidth.Should().Be(48); // 2 * 24
    }

    [Fact]
    public void CollapsedParentWithChildren_ShowsCollapseIcon()
    {
        var items = CreateTestItems();
        var result = BuildTreeView(items, new HashSet<Guid>());

        var lawnMower = result.First(i => i.Id == _lawnMowerId);
        lawnMower.HasChildren.Should().BeTrue();
        lawnMower.ExpandIcon.Should().Be("▶");
        lawnMower.IsExpanded.Should().BeFalse();
    }

    [Fact]
    public void ExpandedParent_ShowsExpandIcon()
    {
        var items = CreateTestItems();
        var expanded = new HashSet<Guid> { _lawnMowerId };
        var result = BuildTreeView(items, expanded);

        var lawnMower = result.First(i => i.Id == _lawnMowerId);
        lawnMower.ExpandIcon.Should().Be("▼");
        lawnMower.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void ItemWithNoChildren_HasChildrenIsFalse()
    {
        var items = CreateTestItems();
        var result = BuildTreeView(items, new HashSet<Guid>());

        var drill = result.First(i => i.Id == _drillId);
        drill.HasChildren.Should().BeFalse();
    }

    [Fact]
    public void EmptyList_ReturnsEmpty()
    {
        var result = BuildTreeView(new List<TestEquipmentItem>(), new HashSet<Guid>());
        result.Should().BeEmpty();
    }

    [Fact]
    public void ToggleExpandCollapse_ChangesVisibleItems()
    {
        var items = CreateTestItems();

        // First: collapsed
        var result1 = BuildTreeView(items, new HashSet<Guid>());
        result1.Should().HaveCount(3);

        // Expand lawn mower
        var expanded = new HashSet<Guid> { _lawnMowerId };
        var result2 = BuildTreeView(CreateTestItems(), expanded);
        result2.Should().HaveCount(5);

        // Collapse again
        expanded.Remove(_lawnMowerId);
        var result3 = BuildTreeView(CreateTestItems(), expanded);
        result3.Should().HaveCount(3);
    }
}
