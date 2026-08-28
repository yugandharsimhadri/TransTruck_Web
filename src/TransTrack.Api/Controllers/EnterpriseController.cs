using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransTrack.Api.Auth;
using TransTrack.Data;

namespace TransTrack.Api.Controllers;

/// <summary>
/// EnterpriseAdmin's whole surface area, reached only after an
/// EnterpriseRecovery login (see <see cref="AuthController.Login"/>) —
/// onboarding a company, resetting a forgotten password, and license
/// renewal. Nothing here ever exposes a company's fleet data (vehicles,
/// trips, drivers, ...); every endpoint is one of exactly those four things,
/// matching the product rule that EnterpriseAdmin has no reason to see a
/// customer's own data.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RecoveryToken)]
[Route("api/enterprise")]
public class EnterpriseController(EnterpriseAdminService enterprise) : ControllerBase
{
    public record OnboardRequest(string CompanyName, string OwnerName, string OwnerPhone, int LicenseMonths = 12);

    [HttpPost("companies")]
    public async Task<IActionResult> Onboard(OnboardRequest request)
    {
        var result = await enterprise.OnboardCompanyAsync(
            request.CompanyName, request.OwnerName, request.OwnerPhone, request.LicenseMonths);
        return Ok(result);
    }

    [HttpGet("companies")]
    public async Task<ActionResult<List<EnterpriseAdminService.CompanySummary>>> ListCompanies()
        => Ok(await enterprise.ListCompaniesAsync());

    [HttpGet("companies/{companyId:guid}/users")]
    public async Task<ActionResult<List<EnterpriseAdminService.CompanyUserSummary>>> GetCompanyUsers(Guid companyId)
        => Ok(await enterprise.GetCompanyUsersAsync(companyId));

    public record ResetPasswordRequest(string? TemporaryPassword);

    [HttpPost("companies/{companyId:guid}/users/{userId:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid companyId, Guid userId, ResetPasswordRequest request)
    {
        var username = await enterprise.ResetUserPasswordAsync(companyId, userId, request.TemporaryPassword);
        return Ok(new { message = $"Password for '{username}' has been reset. They must change it on next sign-in." });
    }

    public record ChangeUsernameRequest(string NewPhone);

    /// <summary>Updates a user's login name when their phone number
    /// changes — the username is always the phone number, and never
    /// updates itself, so this is the fix when it does.</summary>
    [HttpPost("companies/{companyId:guid}/users/{userId:guid}/change-username")]
    public async Task<IActionResult> ChangeUsername(Guid companyId, Guid userId, ChangeUsernameRequest request)
    {
        var username = await enterprise.ChangeUsernameAsync(companyId, userId, request.NewPhone);
        return Ok(new { username });
    }

    public record RenewLicenseRequest(int Months);

    [HttpPost("companies/{companyId:guid}/renew-license")]
    public async Task<IActionResult> RenewLicense(Guid companyId, RenewLicenseRequest request)
    {
        var expiresOn = await enterprise.RenewLicenseAsync(companyId, request.Months);
        return Ok(new { licenseExpiresOn = expiresOn });
    }
}
