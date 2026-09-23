using System.Net.Http.Json;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Identity.Api.IntegrationTests;

internal static class ApiResponseAssertions
{
    public static async Task<T> ReadApiDataAsync<T>(this HttpContent content)
    {
        Assert.Equal("application/json", content.Headers.ContentType?.MediaType);
        var response = await content.ReadFromJsonAsync<ApiResponse<T>>();
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.Null(response.Error);
        Assert.False(string.IsNullOrWhiteSpace(response.RequestId));
        Assert.NotNull(response.Data);
        return response.Data;
    }
}
