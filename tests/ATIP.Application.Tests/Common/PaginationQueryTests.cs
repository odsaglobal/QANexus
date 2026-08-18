using ATIP.Application.Common.Models;
using FluentAssertions;
using Xunit;

namespace ATIP.Application.Tests.Common;

public class PaginationQueryTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public void Page_is_clamped_to_at_least_one(int input, int expected)
    {
        var query = new PaginationQuery { Page = input };
        query.Page.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(500, 200)]
    [InlineData(50, 50)]
    public void PageSize_is_clamped_to_bounds(int input, int expected)
    {
        var query = new PaginationQuery { PageSize = input };
        query.PageSize.Should().Be(expected);
    }

    [Fact]
    public void PagedResult_computes_paging_metadata()
    {
        var result = PagedResult<int>.Create([1, 2, 3], page: 2, pageSize: 3, totalCount: 10);

        result.TotalPages.Should().Be(4);
        result.HasPrevious.Should().BeTrue();
        result.HasNext.Should().BeTrue();
    }
}
