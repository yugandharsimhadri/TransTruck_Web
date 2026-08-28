using System.Net.Http.Json;
using System.Text.Json;

namespace TransTrack.Automation;

/// <summary>
/// The fixed cast every workflow expects to find on screen. Held here rather than inline in the
/// scenarios so a narration script and a workflow can name the same lorry.
/// </summary>
public static class DemoData
{
    public const string CompanyName = "Sri Balaji Transports";
    public const string OwnerName = "Ramesh Kumar";

    /// <summary>Usernames are phone numbers in this product — that is the sign-in identity.</summary>
    public const string OwnerPhone = "9848000001";

    /// <summary>
    /// Set by the seeder on first sign-in, replacing the temporary password onboarding issues. Fixed
    /// so the DemoRunner and the UAT sign in identically, and long enough to clear the eight-character
    /// minimum the API enforces.
    /// </summary>
    public const string OwnerPassword = "Lorry@12345";

    public const string DriverName = "Suresh Babu";
    public const string DriverPhone = "9848000002";
    public const string PartyName = "Sri Venkateswara Traders";
    public const string VehicleRegNo = "AP 16 TT 4041";
    public const string VehicleType = "Tipper";
}

/// <summary>
/// Builds the dataset a run is filmed against, through the product's own HTTP API — the same calls
/// the client makes, in the same order a real customer is onboarded.
///
/// Seeding through the API rather than by writing rows is deliberate. It means the seed exercises
/// (and therefore cannot silently break) the onboarding path, and it means this file never has to
/// know the schema. When a migration adds a column, this keeps working.
/// </summary>
public sealed class DemoDataSeeder(string apiBaseUrl, Action<string>? log = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string _api = apiBaseUrl.TrimEnd('/');

    /// <summary>
    /// Onboards the company, sets the owner's password, and creates just enough fleet and history
    /// for every workflow to have something real to point at. Idempotent by inspection: if the
    /// company is already there from a previous run against the same database, the seed stops early.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        if (await OwnerCanSignInAsync(http, cancellationToken))
        {
            log?.Invoke("Demo data already present — skipping the seed.");
            return;
        }

        log?.Invoke($"Seeding {DemoData.CompanyName} through {_api}");

        var recoveryToken = await EnterpriseAdminTokenAsync(http, cancellationToken);
        var temporaryPassword = await OnboardCompanyAsync(http, recoveryToken, cancellationToken);
        await SetOwnerPasswordAsync(http, temporaryPassword, cancellationToken);

        // From here on the seeder acts as the owner, exactly as the app does.
        var owner = await SignInAsOwnerAsync(http, cancellationToken);

        var cities = await GetAsync<List<Ref>>(http, owner, "/api/masters/cities", cancellationToken);
        if (cities is not { Count: >= 2 })
            throw new InvalidOperationException("Onboarding did not create the starter cities the workflows navigate between.");

        var vehicleId = await CreateVehicleAsync(http, owner, cancellationToken);
        var driverId = await CreateDriverAsync(http, owner, cancellationToken);
        var partyId = await CreatePartyAsync(http, owner, cancellationToken);

        // Enough trips that the list is worth showing and paging has something to page, but not so
        // many that a seed costs real time. 30 crosses the 25-row page boundary by design.
        await CreateTripsAsync(http, owner, vehicleId, driverId, partyId, cities[0].Id, cities[1].Id, count: 30, cancellationToken);

        log?.Invoke("Seed complete.");
    }

    private async Task<bool> OwnerCanSignInAsync(HttpClient http, CancellationToken cancellationToken)
    {
        var response = await http.PostAsJsonAsync($"{_api}/api/auth/login",
            new { username = DemoData.OwnerPhone, password = DemoData.OwnerPassword }, Json, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return false;

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(Json, cancellationToken);
        return body?.Status == "Success";
    }

    private async Task<string> EnterpriseAdminTokenAsync(HttpClient http, CancellationToken cancellationToken)
    {
        // EnterpriseAdmin is a constant in AuthService, never a row, so it is there on a database
        // created seconds ago — which is exactly what makes seeding from empty possible.
        var response = await http.PostAsJsonAsync($"{_api}/api/auth/login",
            new { username = "EnterpriseAdmin", password = "SivAyAAn@HMS" }, Json, cancellationToken);

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(Json, cancellationToken)
            ?? throw new InvalidOperationException("EnterpriseAdmin sign-in returned no body.");

        return body.Token ?? throw new InvalidOperationException("EnterpriseAdmin sign-in returned no token.");
    }

    private async Task<string> OnboardCompanyAsync(HttpClient http, string recoveryToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_api}/api/enterprise/companies")
        {
            Content = JsonContent.Create(new
            {
                companyName = DemoData.CompanyName,
                ownerName = DemoData.OwnerName,
                ownerPhone = DemoData.OwnerPhone,
                licenseMonths = 12,
            }, options: Json),
        };
        request.Headers.Authorization = new("Bearer", recoveryToken);

        var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<OnboardResponse>(Json, cancellationToken)
            ?? throw new InvalidOperationException("Onboarding returned no body.");

        return body.TemporaryPassword;
    }

    /// <summary>
    /// The owner's first sign-in is refused into the app and handed a change-password token instead,
    /// so the seed has to walk that path rather than skip it.
    /// </summary>
    private async Task SetOwnerPasswordAsync(HttpClient http, string temporaryPassword, CancellationToken cancellationToken)
    {
        var login = await http.PostAsJsonAsync($"{_api}/api/auth/login",
            new { username = DemoData.OwnerPhone, password = temporaryPassword }, Json, cancellationToken);
        login.EnsureSuccessStatusCode();

        var body = await login.Content.ReadFromJsonAsync<LoginResponse>(Json, cancellationToken)!;
        if (body!.Status != "MustChangePassword")
            throw new InvalidOperationException($"Expected a forced password change on first sign-in, got '{body.Status}'.");

        using var change = new HttpRequestMessage(HttpMethod.Post, $"{_api}/api/auth/change-password")
        {
            Content = JsonContent.Create(new { newPassword = DemoData.OwnerPassword, confirmPassword = DemoData.OwnerPassword }, options: Json),
        };
        change.Headers.Authorization = new("Bearer", body.Token);

        (await http.SendAsync(change, cancellationToken)).EnsureSuccessStatusCode();
    }

    private async Task<string> SignInAsOwnerAsync(HttpClient http, CancellationToken cancellationToken)
    {
        var response = await http.PostAsJsonAsync($"{_api}/api/auth/login",
            new { username = DemoData.OwnerPhone, password = DemoData.OwnerPassword }, Json, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(Json, cancellationToken)!;
        return body!.Token ?? throw new InvalidOperationException("Owner sign-in returned no token.");
    }

    private Task<Guid> CreateVehicleAsync(HttpClient http, string token, CancellationToken cancellationToken)
        => PostForIdAsync(http, token, "/api/vehicles", new
        {
            id = Guid.Empty,
            regNo = DemoData.VehicleRegNo,
            ownership = "Own",
            vehicleType = DemoData.VehicleType,
            capacity = 16,
            isActive = true,
        }, cancellationToken);

    private Task<Guid> CreateDriverAsync(HttpClient http, string token, CancellationToken cancellationToken)
        => PostForIdAsync(http, token, "/api/drivers", new
        {
            id = Guid.Empty,
            name = DemoData.DriverName,
            phone = DemoData.DriverPhone,
            isActive = true,
        }, cancellationToken);

    private Task<Guid> CreatePartyAsync(HttpClient http, string token, CancellationToken cancellationToken)
        => PostForIdAsync(http, token, "/api/masters/parties", new
        {
            id = Guid.Empty,
            name = DemoData.PartyName,
            isActive = true,
        }, cancellationToken);

    private async Task CreateTripsAsync(
        HttpClient http, string token, Guid vehicleId, Guid driverId, Guid partyId,
        Guid fromCityId, Guid toCityId, int count, CancellationToken cancellationToken)
    {
        // Dated backwards from a fixed anchor rather than from DateTime.Now, so the same run in
        // January and in July produces the same list order and the same "this month" figures.
        var anchor = new DateTime(2026, 7, 28);

        for (var i = 0; i < count; i++)
        {
            await PostAsync(http, token, "/api/trips", new
            {
                id = Guid.Empty,
                vehicleId,
                driverId,
                partyId,
                fromCityId,
                toCityId,
                date = anchor.AddDays(-i).ToString("yyyy-MM-dd"),
                amount = 18000 + (i * 500),
                status = "Open",
            }, cancellationToken);
        }
    }

    private async Task<Guid> PostForIdAsync(HttpClient http, string token, string path, object body, CancellationToken cancellationToken)
    {
        var text = await PostAsync(http, token, path, body, cancellationToken);
        return Guid.TryParse(text.Trim('"', ' ', '\n', '\r'), out var id)
            ? id
            : throw new InvalidOperationException($"POST {path} did not return an id. Body: {text}");
    }

    private async Task<string> PostAsync(HttpClient http, string token, string path, object body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _api + path) { Content = JsonContent.Create(body, options: Json) };
        request.Headers.Authorization = new("Bearer", token);

        var response = await http.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"POST {path} failed ({(int)response.StatusCode}). Body: {text}");

        return text;
    }

    private async Task<T?> GetAsync<T>(HttpClient http, string token, string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _api + path);
        request.Headers.Authorization = new("Bearer", token);

        var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
    }

    private sealed record LoginResponse(string Status, string? Token, string? Message);
    private sealed record OnboardResponse(Guid CompanyId, string OwnerUsername, string TemporaryPassword);
    private sealed record Ref(Guid Id, string Name);
}
