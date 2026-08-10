namespace TransTrack.Api.Auth;

/// <summary>Where the session token lives for browser clients — httpOnly so
/// JS on the page can never read it, SameSite=Strict since this API and its
/// web client are always same-site in practice.</summary>
public static class AuthCookie
{
    public const string Name = "tt_token";

    public static CookieOptions Options(IWebHostEnvironment env, DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = !env.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Expires = expires
    };
}
