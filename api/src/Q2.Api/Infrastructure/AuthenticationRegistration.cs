using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Q2.Api.Features.Accounts;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Infrastructure;

/// <summary>
/// Registers ASP.NET Core Identity and the cookie session it signs in with.
/// </summary>
/// <remarks>
/// Identity's default token providers are registered for one thing so far: the
/// link in a password-reset mail (docs/adr/0026-mail-and-password-reset.md).
/// Email confirmation and two-factor codes will use the same providers when
/// they are built. <c>MapIdentityApi</c> stays deliberately absent: its
/// ready-made routes answer in a shape that is not the Problem Details every
/// other endpoint here answers with, so the reset endpoints are written against
/// <see cref="UserManager{TUser}"/> in <c>AccountEndpoints</c> instead.
///
/// What is used is the part that matters: the password hasher, the security
/// stamp, lockout, the cookie and the tokens. None of that is written by hand
/// (docs/adr/0011-authentication-with-identity.md).
/// </remarks>
public static class AuthenticationRegistration
{
    public static WebApplicationBuilder AddQ2Authentication(this WebApplicationBuilder builder)
    {
        // CurrentPerson reads the principal from here. Per request, not global:
        // the accessor resolves the context of the request being served.
        builder.Services.AddHttpContextAccessor();

        builder.Services
            .AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                // Length, and nothing else — see AccountPolicy for why the
                // composition rules are off rather than forgotten.
                options.Password.RequiredLength = AccountPolicy.MinimumPasswordLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                options.Lockout.MaxFailedAccessAttempts = 10;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                // Confirming an address is not built yet. q2 can send mail now,
                // but requiring a confirmed address without that flow would
                // lock every new account out of the app it just signed up for.
                options.SignIn.RequireConfirmedAccount = false;
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<Q2DbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // A reset link works for an hour. Identity's token provider reads the
        // system clock and takes no TimeProvider — the one place in q2 where
        // time is not injected — so the lifetime is tested as configuration
        // (PasswordResetEndpointTests) rather than by moving a clock.
        builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = AccountPolicy.PasswordResetLinkLifetime);

        // How soon a changed security stamp — a reset — ends the sessions that
        // were opened before it. See AccountPolicy.SessionRecheckInterval.
        builder.Services.AddOptions<SecurityStampValidatorOptions>()
            .Configure<TimeProvider>((options, time) =>
            {
                options.ValidationInterval = AccountPolicy.SessionRecheckInterval;
                options.TimeProvider = time;
            });

        builder.Services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = AccountPolicy.SessionCookieName;

            // The session is never readable from JavaScript, which is what
            // makes it not stealable by an injected script.
            options.Cookie.HttpOnly = true;

            // Lax, not Strict: the frontend is a separate origin from the API
            // (localhost:3000 → localhost:5080, app → api subdomain in
            // Staging), and both are the same *site*, which is what SameSite
            // actually compares. Strict would additionally drop the cookie on
            // a normal top-level navigation into the app.
            options.Cookie.SameSite = SameSiteMode.Lax;

            // Secure over HTTPS, plain over HTTP. Pinning it to Always would
            // make local development over http://localhost impossible; pinning
            // it to None would send it in the clear where TLS exists.
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

            // The session is not something a cookie banner may decline: without
            // it there is no signed-in app to decline anything in.
            options.Cookie.IsEssential = true;

            options.ExpireTimeSpan = AccountPolicy.SessionLifetime;
            options.SlidingExpiration = true;

            // This is an API, not a site with a login page: the default is a
            // 302 to /Account/Login, which a fetch() would follow and then
            // report as a successful HTML response. Answering with the status
            // code is what lets the client tell "signed out" from "broken".
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };

            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        builder.Services.AddAuthorization();

        return builder;
    }
}
