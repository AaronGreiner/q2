using Microsoft.AspNetCore.Http.HttpResults;

namespace Q2.Api.Features.Profile;

/// <summary>HTTP surface for the signed-in person's own profile.</summary>
public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/profile", GetProfile)
            .RequireAuthorization()
            .WithTags("Profile")
            .WithName("GetProfile")
            .WithSummary("Returns who you are, your streak, your badges and your recent activity.")
            .Produces<ProfileResponse>();

        return endpoints;
    }

    private static async Task<Ok<ProfileResponse>> GetProfile(
        ProfileService profile,
        CancellationToken cancellationToken)
    {
        var result = await profile.GetAsync(cancellationToken);
        return TypedResults.Ok(result);
    }
}
