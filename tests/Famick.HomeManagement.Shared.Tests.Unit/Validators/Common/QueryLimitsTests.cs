using Famick.HomeManagement.Core.Validators.Common;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Validators.Common;

public class QueryLimitsTests
{
    [Theory]
    [InlineData(null, 1)]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    public void A_page_is_at_least_one(int? page, int expected) =>
        QueryLimits.Page(page).Should().Be(expected);

    [Theory]
    [InlineData(null, 25)]
    [InlineData(0, 25)]
    [InlineData(-1, 25)]
    [InlineData(40, 40)]
    [InlineData(100, 100)]
    [InlineData(5000, 100)]
    public void A_page_size_is_capped_or_defaulted(int? size, int expected) =>
        QueryLimits.PageSize(size, 25).Should().Be(expected);

    [Fact]
    public void Skip_does_not_overflow_for_a_huge_page() =>
        QueryLimits.Skip(int.MaxValue, 100).Should().Be(int.MaxValue);

    [Fact]
    public void Skip_treats_a_page_below_one_as_the_first() =>
        QueryLimits.Skip(0, 25).Should().Be(0);

    [Theory]
    [InlineData(null, 50)]
    [InlineData(0, 50)]
    [InlineData(10, 10)]
    [InlineData(100_000, 500)]
    public void A_count_is_capped_or_defaulted(int? requested, int expected) =>
        QueryLimits.Count(requested, 50, QueryLimits.MaxLogEntries).Should().Be(expected);
}
