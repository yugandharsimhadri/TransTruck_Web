using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransTrack.Api.Auth;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Api.Controllers;

/// <summary>The company's own users (Owner/CoOwner/Accountant) — separate
/// from EnterpriseController, which only ever reaches across companies for
/// onboarding/reset/renew. This is a company managing its own team.</summary>
[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController(AuthService auth) : ControllerBase
{
    public record UserSummary(Guid Id, string Username, string DisplayName, UserRole Role, bool IsActive, DateTime? LastLoginOn);

    /// <summary>Never the raw User entity here — it carries PasswordHash/
    /// PasswordSalt, which must never reach the client regardless of who's
    /// asking.</summary>
    [HttpGet]
    public async Task<ActionResult<List<UserSummary>>> Get()
    {
        var users = await auth.GetUsersAsync();
        return Ok(users.Select(u => new UserSummary(u.Id, u.Username, u.DisplayName, u.Role, u.IsActive, u.LastLoginOn)));
    }

    public record SaveUserRequest(
        Guid Id, string Username, string DisplayName, UserRole Role, bool IsActive, string? NewPassword);

    /// <summary>Creates a user (NewPassword required), or updates one
    /// (role/name/active, and optionally resets their password — setting
    /// NewPassword also forces MustChangePassword, same as EnterpriseAdmin's
    /// reset).</summary>
    /// <remarks>Deliberately not gated to Owner/CoOwner here. Who may act on
    /// whom is decided by the role hierarchy in AuthService.SaveUserAsync,
    /// which reads the caller's current role from the database — a blanket
    /// policy on this method could only express "may manage users at all",
    /// and previously let a CoOwner create an Owner.</remarks>
    [HttpPost]
    public async Task<IActionResult> Save(SaveUserRequest request)
    {
        await auth.SaveUserAsync(
            new User
            {
                Id = request.Id,
                Username = request.Username,
                DisplayName = request.DisplayName,
                Role = request.Role,
                IsActive = request.IsActive,
            },
            request.NewPassword);
        return Ok();
    }
}
