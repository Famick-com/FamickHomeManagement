using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Pages;

/// <summary>
/// Tests for todo item display model logic.
/// Recreates display model to avoid MAUI project dependency.
/// </summary>
public class TaskDisplayModelTests
{
    private class TestTodoItem
    {
        public Guid Id { get; set; }
        public string? Description { get; set; }
        public string? Reason { get; set; }
        public string? TaskTypeName { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private static string GetDateDisplay(TestTodoItem item) =>
        item.IsCompleted && item.CompletedAt.HasValue
            ? $"Completed {item.CompletedAt.Value:MMM d}"
            : $"Created {item.CreatedAt:MMM d}";

    private static string GetDisplayDescription(TestTodoItem item) =>
        item.Description ?? item.Reason ?? string.Empty;

    private static bool ShouldStrikethrough(TestTodoItem item) => item.IsCompleted;

    private static string GetTaskTypeColorHex(string? taskTypeName) => taskTypeName switch
    {
        "Inventory" => "#FF9800",
        "Product" => "#2196F3",
        "Equipment" => "#9C27B0",
        _ => "#757575"
    };

    [Fact]
    public void PendingTask_ShowsCreatedDate()
    {
        var item = new TestTodoItem
        {
            IsCompleted = false,
            CreatedAt = new DateTime(2026, 3, 10)
        };

        GetDateDisplay(item).Should().Be("Created Mar 10");
    }

    [Fact]
    public void CompletedTask_ShowsCompletedDate()
    {
        var item = new TestTodoItem
        {
            IsCompleted = true,
            CompletedAt = new DateTime(2026, 3, 12),
            CreatedAt = new DateTime(2026, 3, 10)
        };

        GetDateDisplay(item).Should().Be("Completed Mar 12");
    }

    [Fact]
    public void CompletedTask_HasStrikethrough()
    {
        var item = new TestTodoItem { IsCompleted = true };
        ShouldStrikethrough(item).Should().BeTrue();
    }

    [Fact]
    public void PendingTask_NoStrikethrough()
    {
        var item = new TestTodoItem { IsCompleted = false };
        ShouldStrikethrough(item).Should().BeFalse();
    }

    [Fact]
    public void TaskWithDescription_UsesDescription()
    {
        var item = new TestTodoItem
        {
            Description = "Review milk product",
            Reason = "New product from shopping"
        };

        GetDisplayDescription(item).Should().Be("Review milk product");
    }

    [Fact]
    public void TaskWithoutDescription_FallsBackToReason()
    {
        var item = new TestTodoItem
        {
            Description = null,
            Reason = "Product needs inventory setup"
        };

        GetDisplayDescription(item).Should().Be("Product needs inventory setup");
    }

    [Fact]
    public void InventoryTaskType_HasOrangeColor()
    {
        GetTaskTypeColorHex("Inventory").Should().Be("#FF9800");
    }

    [Fact]
    public void ProductTaskType_HasBlueColor()
    {
        GetTaskTypeColorHex("Product").Should().Be("#2196F3");
    }

    [Fact]
    public void EquipmentTaskType_HasPurpleColor()
    {
        GetTaskTypeColorHex("Equipment").Should().Be("#9C27B0");
    }

    [Fact]
    public void OtherTaskType_HasGrayColor()
    {
        GetTaskTypeColorHex("Other").Should().Be("#757575");
    }

    [Fact]
    public void NullTaskType_HasGrayColor()
    {
        GetTaskTypeColorHex(null).Should().Be("#757575");
    }

    [Fact]
    public void FilterLogic_PendingExcludesCompleted()
    {
        var items = new List<TestTodoItem>
        {
            new() { Id = Guid.NewGuid(), IsCompleted = false },
            new() { Id = Guid.NewGuid(), IsCompleted = true },
            new() { Id = Guid.NewGuid(), IsCompleted = false }
        };

        var pending = items.Where(i => !i.IsCompleted).ToList();
        pending.Should().HaveCount(2);
    }

    [Fact]
    public void SearchFilter_MatchesDescription()
    {
        var items = new List<TestTodoItem>
        {
            new() { Description = "Fix the kitchen sink", Reason = "" },
            new() { Description = "Buy groceries", Reason = "" },
            new() { Description = "Clean the kitchen", Reason = "" }
        };

        var searchTerm = "kitchen";
        var filtered = items.Where(t =>
            t.Description?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false).ToList();

        filtered.Should().HaveCount(2);
    }
}
