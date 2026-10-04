using System.Net;
using TNRD.Zeepkist.GTR.Api;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class ApiResponseErrorsTests
{
    [Fact]
    public async Task SuccessfulResponse_DoesNotThrow()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ignored") };
        await ApiResponseErrors.EnsureSuccessWithBodyAsync(response);
    }

    [Fact]
    public async Task FailedResponse_ThrowsWithStatusAndBody()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"Time is below the minimum\"}")
        };

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => ApiResponseErrors.EnsureSuccessWithBodyAsync(response));

        Assert.Equal("400 (Bad Request): {\"error\":\"Time is below the minimum\"}", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n")]
    public void Describe_EmptyBody_SaysSo(string body)
    {
        Assert.Equal("500 (Internal Server Error) with an empty response body",
            ApiResponseErrors.Describe(500, "Internal Server Error", body));
    }

    [Fact]
    public void Describe_LongBody_IsTruncated()
    {
        string body = new('x', ApiResponseErrors.MaxBodyLength + 50);

        string description = ApiResponseErrors.Describe(400, "Bad Request", body);

        Assert.Equal("400 (Bad Request): " + new string('x', ApiResponseErrors.MaxBodyLength) + "…", description);
    }
}
