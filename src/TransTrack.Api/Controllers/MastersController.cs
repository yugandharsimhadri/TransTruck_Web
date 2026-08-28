using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransTrack.Api.Auth;
using TransTrack.Core;
using TransTrack.Data;

namespace TransTrack.Api.Controllers;

/// <summary>Thin CRUD over the small lookup tables — one controller since
/// each is a handful of near-identical endpoints. Deletion is always soft,
/// same as <see cref="MasterDataService"/> itself.</summary>
[ApiController]
[Authorize]
[Route("api/masters")]
public class MastersController(MasterDataService masters) : ControllerBase
{
    // ── States ────────────────────────────────────────────────────────────

    [HttpGet("states")]
    public async Task<ActionResult<List<State>>> GetStates() => Ok(await masters.GetStatesAsync());

    [HttpPost("states")]
    public async Task<ActionResult<Guid>> SaveState(State state) => Ok(await masters.SaveStateAsync(state));

    [HttpDelete("states/{id:guid}")]
    public async Task<IActionResult> DeleteState(Guid id) { await masters.DeleteStateAsync(id); return NoContent(); }

    // ── Cities ────────────────────────────────────────────────────────────

    [HttpGet("cities")]
    public async Task<ActionResult<List<City>>> GetCities() => Ok(await masters.GetCitiesAsync());

    [HttpPost("cities")]
    public async Task<ActionResult<Guid>> SaveCity(City city) => Ok(await masters.SaveCityAsync(city));

    [HttpDelete("cities/{id:guid}")]
    public async Task<IActionResult> DeleteCity(Guid id) { await masters.DeleteCityAsync(id); return NoContent(); }

    // ── Owners (other-owner vehicles) ────────────────────────────────────

    [HttpGet("owners")]
    public async Task<ActionResult<List<Owner>>> GetOwners() => Ok(await masters.GetOwnersAsync());

    [HttpPost("owners")]
    public async Task<IActionResult> SaveOwner(Owner owner) => Ok(await masters.SaveOwnerAsync(owner));

    public record OwnerBasicRequest(Guid? ExistingOwnerId, string Name, string? Phone);

    [HttpPost("owners/basic")]
    public async Task<IActionResult> SaveOwnerBasic(OwnerBasicRequest request)
        => Ok(await masters.SaveOwnerBasicAsync(request.ExistingOwnerId, request.Name, request.Phone));

    [HttpDelete("owners/{id:guid}")]
    public async Task<IActionResult> DeleteOwner(Guid id) { await masters.DeleteOwnerAsync(id); return NoContent(); }

    // ── Parties (billing) ────────────────────────────────────────────────

    [HttpGet("parties")]
    public async Task<ActionResult<List<Party>>> GetParties() => Ok(await masters.GetPartiesAsync());

    [HttpPost("parties")]
    public async Task<IActionResult> SaveParty(Party party) => Ok(await masters.SavePartyAsync(party));

    [HttpDelete("parties/{id:guid}")]
    public async Task<IActionResult> DeleteParty(Guid id) { await masters.DeletePartyAsync(id); return NoContent(); }

    // ── Expense categories ───────────────────────────────────────────────

    [HttpGet("expense-categories")]
    public async Task<ActionResult<List<ExpenseCategory>>> GetExpenseCategories() => Ok(await masters.GetExpenseCategoriesAsync());

    [HttpPost("expense-categories")]
    public async Task<IActionResult> SaveExpenseCategory(ExpenseCategory category)
    {
        await masters.SaveExpenseCategoryAsync(category);
        return Ok();
    }

    [HttpDelete("expense-categories/{id:guid}")]
    public async Task<IActionResult> DeleteExpenseCategory(Guid id) { await masters.DeleteExpenseCategoryAsync(id); return NoContent(); }

    // ── Maintenance categories ───────────────────────────────────────────

    [HttpGet("maintenance-categories")]
    public async Task<ActionResult<List<MaintenanceCategory>>> GetMaintenanceCategories() => Ok(await masters.GetMaintenanceCategoriesAsync());

    [HttpPost("maintenance-categories")]
    public async Task<IActionResult> SaveMaintenanceCategory(MaintenanceCategory category)
    {
        await masters.SaveMaintenanceCategoryAsync(category);
        return Ok();
    }

    [HttpDelete("maintenance-categories/{id:guid}")]
    public async Task<IActionResult> DeleteMaintenanceCategory(Guid id) { await masters.DeleteMaintenanceCategoryAsync(id); return NoContent(); }

    // ── Company (own tenant's branding/letterhead only — never license/
    // onboarding fields, which are EnterpriseAdmin-only) ──────────────────

    [HttpGet("company-settings")]
    public async Task<ActionResult<Company>> GetCompanySettings() => Ok(await masters.GetCompanyAsync());

    [HttpPost("company-settings")]
    [Authorize(Policy = Policies.ManageSettings)]
    public async Task<IActionResult> SaveCompanySettings(Company settings)
    {
        await masters.SaveCompanyAsync(settings);
        return Ok();
    }

    public record ThemeRequest(AppThemeKind Theme);

    [HttpPost("company-settings/theme")]
    public async Task<IActionResult> UpdateTheme(ThemeRequest request)
    {
        await masters.UpdateThemeAsync(request.Theme);
        return Ok();
    }
}
