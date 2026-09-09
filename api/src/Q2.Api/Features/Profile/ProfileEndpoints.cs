using Microsoft.AspNetCore.Http.HttpResults;

using Q2.Api.Features.People;

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

        endpoints.MapPut("/api/profile", UpdateProfile)
            .RequireAuthorization()
            .WithTags("Profile")
            .WithName("UpdateProfile")
            .WithSummary("Changes your display name or your picture. Omitted properties keep their value.")
            .Produces<ProfileResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapGet("/api/people/{personId:guid}", GetPerson)
            .RequireAuthorization()
            .WithTags("Profile")
            .WithName("GetPersonProfile")
            .WithSummary("Returns somebody else's profile, with a balance covering only the goals you share.")
            .Produces<PersonProfileResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Ok<ProfileResponse>> GetProfile(
        ProfileService profile,
        CancellationToken cancellationToken)
    {
        var result = await profile.GetAsync(cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<PersonProfileResponse>> GetPerson(
        ProfileService profile,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var result = await profile.GetPersonAsync(personId, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<ProfileResponse>, ValidationProblem>> UpdateProfile(
        ProfileService profile,
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var errors = UpdateProfileRequestValidator.Validate(request);

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var result = await profile.UpdateAsync(request, cancellationToken);
        return TypedResults.Ok(result);
    }
}
