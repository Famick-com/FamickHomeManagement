using Famick.HomeManagement.Web.Shared.Http;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Famick.HomeManagement.Tests.Unit.Http;

/// <summary>
/// The file-download endpoints previously returned no cache validators at all, so
/// clients refetched every file on every render and each refetch cost a DB lookup
/// (issue #80). These cover the headers that stop that.
/// </summary>
public class StoredFileCacheTests
{
    private static HttpResponse NewResponse() => new DefaultHttpContext().Response;

    [Fact]
    public void Apply_marks_the_response_cacheable_for_this_client_only()
    {
        var response = NewResponse();

        StoredFileCache.Apply(response, "abc-123.jpg");

        // "private" matters: these files are tenant-scoped and must never be held
        // by a shared cache where another tenant could be served them.
        response.Headers.CacheControl.ToString()
            .Should().Be($"private, max-age={StoredFileCache.MaxAgeSeconds}, immutable");
    }

    [Fact]
    public void Apply_returns_an_etag_so_a_stale_client_can_revalidate_instead_of_refetching()
    {
        var etag = StoredFileCache.Apply(NewResponse(), "abc-123.jpg");

        etag.Should().NotBeNull();
        etag.Tag.Value.Should().StartWith("\"").And.EndWith("\"");
        etag.IsWeak.Should().BeFalse();
    }

    [Fact]
    public void The_same_stored_file_always_gets_the_same_etag()
    {
        // Otherwise every response would look like new content and the 304 path
        // would never engage.
        StoredFileCache.ComputeETag("abc-123.jpg")
            .Should().BeEquivalentTo(StoredFileCache.ComputeETag("abc-123.jpg"));
    }

    [Fact]
    public void Replacing_the_file_changes_the_etag()
    {
        StoredFileCache.ComputeETag("abc-123.jpg").Tag.Value
            .Should().NotBe(StoredFileCache.ComputeETag("def-456.jpg").Tag.Value);
    }

    [Fact]
    public void A_one_year_lifetime_is_only_safe_because_stored_files_are_write_once()
    {
        // Guards the assumption the long max-age rests on: uploads mint a fresh
        // unique filename and replacing an image adds a row with a new id, so a
        // given download URL never changes its bytes. If that ever stops holding,
        // this lifetime has to come down with it.
        StoredFileCache.MaxAgeSeconds.Should().Be(31_536_000);
    }
}
