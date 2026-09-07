using Famick.HomeManagement.Core.Configuration;
using FluentAssertions;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.DataPortability;

/// <summary>
/// Guards the names that get joined onto a storage path.
/// </summary>
/// <remarks>
/// These come from the database. Until restore existed that made them trustworthy — every one was
/// generated on upload. An archive is a file somebody hands you, so a FileName of
/// "../../../../etc/passwd" would survive Path.Combine untouched, walk out of the storage root,
/// and be served through a download endpoint its owner is entitled to call.
/// </remarks>
public class StoredFileNameTests
{
    [Theory]
    [InlineData("../../../../etc/passwd")]
    [InlineData("..\\..\\plugins\\evil.dll")]
    [InlineData("/etc/shadow")]
    [InlineData("sub/dir/file.jpg")]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("C:evil.dll")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NamesThatNavigateAreRefused(string? fileName)
        => StoredFileName.IsSafe(fileName).Should().BeFalse();

    [Theory]
    // What upload actually generates, and the shapes a real file takes.
    [InlineData("image_20260907_143022_a1b2c3d4.jpg")]
    [InlineData("doc_20260907_143022_a1b2c3d4.pdf")]
    [InlineData("archive.zip")]
    [InlineData("a file with spaces.png")]
    [InlineData("dotted.name.tar.gz")]
    public void OrdinaryFileNamesArePermitted(string fileName)
        => StoredFileName.IsSafe(fileName).Should().BeTrue();

    [Fact]
    public void RequireThrowsRatherThanQuietlyRepairing()
    {
        // A name that fails this is not a slightly wrong name to tidy up — it is a row that should
        // never have been written. Sanitising it would hide that from whoever has to find out how
        // it got there.
        var act = () => StoredFileName.Require("../../etc/passwd", "product image");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a plain file name*");
    }

    [Fact]
    public void RequireReturnsAGoodNameUnchanged()
        => StoredFileName.Require("image_20260907_143022_a1b2c3d4.jpg", "product image")
            .Should().Be("image_20260907_143022_a1b2c3d4.jpg");
}
