using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransTrack.Api.Auth;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Api.Controllers;

/// <summary>
/// Mirrors the desktop's four-stage <c>LoginViewModel</c> flow
/// (Credentials/ChangePassword/Recovery/RecoveryDone) as JWT-token shapes
/// instead of view stages. See <see cref="TokenTypes"/>.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(
    AuthService auth,
    MasterDataService masters,
    JwtTokenService tokens,
    RegistrationService registration,
    IWebHostEnvironment env) : ControllerBase
{
    public record LoginRequest(string Username, string Password);

    public record RegisterRequest(string CompanyName, string Phone, string? Password);

    public record RegisterResponse(string Username, string CompanyName, string Message);

    /// <summary>Self-service sign-up from the login screen. Deliberately
    /// returns no token: registration is not a sign-in, and the new account
    /// has MustChangePassword set, so the owner goes through the normal login
    /// → forced-password-change path exactly as an onboarded owner does.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest request)
    {
        try
        {
            var result = await registration.RegisterAsync(request.CompanyName, request.Phone, request.Password);
            return Ok(new RegisterResponse(
                result.Username,
                result.CompanyName,
                "Registered. Sign in with your phone number — you'll be asked to set a new password."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    public record LoginResponse(
        string Status, string? Token, string? Message,
        bool MustChangePassword, string? DisplayName, string? Role);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new LoginResponse("Failed", null, "Enter a username and password.", false, null, null));

        var result = await auth.LoginAsync(request.Username, request.Password);

        switch (result.Outcome)
        {
            case LoginOutcome.Failed:
                return Unauthorized(new LoginResponse("Failed", null, result.Message, false, null, null));

            case LoginOutcome.LicenseExpired:
                return StatusCode(StatusCodes.Status402PaymentRequired,
                    new LoginResponse("LicenseExpired", null, result.Message, false, null, null));

            case LoginOutcome.EnterpriseRecovery:
            {
                var token = tokens.CreateRecoveryToken();
                SetCookie(token, TimeSpan.FromMinutes(15));
                return Ok(new LoginResponse("Recovery", token, null, false, null, null));
            }

            default:
            {
                var user = result.User!;

                if (user.MustChangePassword)
                {
                    var restricted = tokens.CreateChangePasswordToken(user.Id);
                    SetCookie(restricted, TimeSpan.FromMinutes(15));
                    return Ok(new LoginResponse("MustChangePassword", restricted, null, true, user.DisplayName, user.Role.ToString()));
                }

                var full = tokens.CreateFullSessionToken(user);
                SetCookie(full, tokens.FullSessionLifetime);
                return Ok(new LoginResponse("Success", full, null, false, user.DisplayName, user.Role.ToString()));
            }
        }
    }

    public record ChangePasswordRequest(string NewPassword, string ConfirmPassword);

    /// <summary>The signed-in user setting their own new password — same
    /// validation as <c>LoginViewModel.SubmitNewPasswordAsync</c>. Issues a
    /// full session token on success so the client doesn't need a second
    /// round trip through /login.</summary>
    [HttpPost("change-password")]
    [Authorize(Policy = Policies.ChangePasswordToken)]
    public async Task<ActionResult<LoginResponse>> ChangePassword(ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest(new LoginResponse("Failed", null, "The new password must be at least 8 characters.", false, null, null));

        if (request.NewPassword != request.ConfirmPassword)
            return BadRequest(new LoginResponse("Failed", null, "The passwords do not match.", false, null, null));

        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var user = await auth.ChangeOwnPasswordAsync(userId, request.NewPassword);

        var full = tokens.CreateFullSessionToken(user);
        SetCookie(full, tokens.FullSessionLifetime);

        return Ok(new LoginResponse("Success", full, null, false, user.DisplayName, user.Role.ToString()));
    }

    // EnterpriseAdmin's password-reset and license-renewal endpoints live in
    // EnterpriseController — company-scoped there (pick a company, then a
    // user within it) rather than the flat cross-company list this used to
    // be in the single-tenant version.

    public record MeResponse(Guid UserId, string Username, string DisplayName, string Role, string CompanyName);

    /// <summary>Who the caller is — the frontend's only way to rehydrate
    /// auth state on page load/refresh, since the session token lives in an
    /// httpOnly cookie that client-side JS can never read directly.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Me()
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var user = (await auth.GetUsersAsync()).FirstOrDefault(u => u.Id == userId);
        if (user is null) return Unauthorized();

        var company = await masters.GetCompanyAsync();
        return Ok(new MeResponse(user.Id, user.Username, user.DisplayName, user.Role.ToString(), company.CompanyName));
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(AuthCookie.Name);
        return Ok();
    }

    private void SetCookie(string token, TimeSpan lifetime)
        => Response.Cookies.Append(AuthCookie.Name, token, AuthCookie.Options(env, DateTimeOffset.UtcNow.Add(lifetime)));
}
