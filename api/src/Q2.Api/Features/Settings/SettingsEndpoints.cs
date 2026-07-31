using Microsoft.AspNetCore.Http.HttpResults;

namespace Q2.Api.Features.Settings;

/// <summary>HTTP surface for the signed-in person's preferences.</summary>
public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/settings").WithTags("Settings").RequireAuthorization();

        group.MapGet("/", GetSettings)
            .WithName("GetSettings")
            .WithSummary("Returns the current preferences.")
            .Produces<SettingsResponse>();

        group.MapPut("/", UpdateSettings)
            .WithName("UpdateSettings")
            .WithSummary("Changes preferences. Omitted properties keep their value.")
            .Produces<SettingsResponse>();

        return endpoints;
    }

    private static async Task<Ok<SettingsResponse>> GetSettings(
        SettingsService settings,
        CancellationToken cancellationToken)
    {
        var result = await settings.GetAsync(cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<SettingsResponse>> UpdateSettings(
        SettingsService settings,
        UpdateSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await settings.UpdateAsync(request, cancellationToken);
        return TypedResults.Ok(result);
    }
}
