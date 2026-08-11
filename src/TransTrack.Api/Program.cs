using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TransTrack.Api.Auth;
using TransTrack.Data;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// The production deployment sits behind a Cloudflare Tunnel: Cloudflare
// terminates TLS at its edge (https://ttapi.sivayaantechnologies.com) and
// cloudflared forwards plain HTTP to this process on localhost:6041. Without
// trusting the forwarded headers, Kestrel would see every request as
// insecure HTTP — breaking the Secure cookie flag and turning
// UseHttpsRedirection into a redirect loop. cloudflared isn't a "known
// proxy" IP ASP.NET Core recognises by default, so the known-network checks
// are cleared and the headers are trusted outright; that's fine here since
// nothing routes to this port except through the tunnel (no public port
// forwarding), so there's no untrusted network path that could forge them.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// ── Data layer — same IDbContextFactory<AppDbContext> pattern the desktop
// app uses; every TransTrack.Data service opens its own short-lived context
// per call, so this stays a factory rather than a scoped DbContext. ────────
builder.Services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(DbBootstrapper.ConnectionString));

// ── TransTrack.Data services — mirrors App.xaml.cs's registrations. ───────
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<MasterDataService>();
builder.Services.AddSingleton<DriverService>();
builder.Services.AddSingleton<VehicleService>();
builder.Services.AddSingleton<TripService>();
builder.Services.AddSingleton<TripTransactionService>();
builder.Services.AddSingleton<MaintenanceService>();
builder.Services.AddSingleton<DriverLedgerService>();
builder.Services.AddSingleton<ReportsService>();
builder.Services.AddSingleton<DashboardService>();
builder.Services.AddSingleton<AuditService>();

// EnterpriseAdmin's cross-company surface — onboarding, password reset,
// license renewal. Deliberately not one of the tenant-scoped services above.
builder.Services.AddSingleton<EnterpriseAdminService>();

// ── Request-scoped current user, in place of the desktop's DI-singleton
// CurrentUserService (there's exactly one signed-in user per WPF process;
// the API serves concurrent users, so this reads off the ambient request). ─
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ICurrentUserContext, HttpCurrentUserContext>();

builder.Services.AddSingleton<JwtTokenService>();

var jwtKey = builder.Configuration["Jwt:Key"]
             ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Without this, the JwtBearer handler silently remaps "sub" to
        // ClaimTypes.NameIdentifier on the way in, so every
        // FindFirst(JwtRegisteredClaimNames.Sub) lookup (HttpCurrentUserContext,
        // AuthController) comes back null despite a perfectly valid token.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // The web client carries the token as an httpOnly cookie, not an
        // Authorization header — pull it from there when present. The header
        // still works too, which is what Swagger's "Authorize" button uses.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue(AuthCookie.Name, out var cookieToken))
                    context.Token = cookieToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim(TokenTypes.ClaimType, TokenTypes.Full)
        .Build())
    .AddPolicy(Policies.ChangePasswordToken, p => p.RequireClaim(TokenTypes.ClaimType, TokenTypes.ChangePassword))
    .AddPolicy(Policies.RecoveryToken, p => p.RequireClaim(TokenTypes.ClaimType, TokenTypes.Recovery))
    .AddPolicy(Policies.Owner, p => p
        .RequireClaim(TokenTypes.ClaimType, TokenTypes.Full)
        .RequireRole(nameof(TransTrack.Core.UserRole.Owner)))
    .AddPolicy(Policies.ManageSettings, p => p
        .RequireClaim(TokenTypes.ClaimType, TokenTypes.Full)
        .RequireRole(nameof(TransTrack.Core.UserRole.Owner), nameof(TransTrack.Core.UserRole.CoOwner)));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // Cities/States and similar reference pairs can round-trip a cycle
        // (City -> State -> Cities) once collection navigations are loaded;
        // ignore rather than throw. Entities serialize directly in Phase 1 —
        // no separate DTOs yet.
        o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Entities post FK ids only (VehicleId, not Vehicle) — [ApiController]'s
// automatic model validation otherwise infers [Required] on every
// non-nullable navigation property (Vehicle, Driver, ...) and 400s a body
// that never set them. The services themselves already validate what
// actually matters (SaveTripAsync etc.), so this filter is redundant here.
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(o =>
    o.SuppressModelStateInvalidFilter = true);

// One place that turns a broken business rule into a 400 and anything
// unexpected into a logged 500 — see ApiExceptionHandler for why this is
// central rather than a try/catch per endpoint.
builder.Services.AddExceptionHandler<TransTrack.Api.ApiExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Description = "JWT bearer token — the /api/auth/login response also sets this as an httpOnly cookie for browser clients.",
        Name = "Authorization",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(_ => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        { new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", null), [] }
    });
});

var app = builder.Build();

// Must run before anything that reads Request.Scheme/RemoteIpAddress
// (UseHttpsRedirection, the license-check middleware's IP-agnostic logic,
// the Secure cookie flag AuthController sets) — see the registration above.
app.UseForwardedHeaders();

// Migrate on startup — this product's own database (see DbBootstrapper's
// TransTruckWeb.db / TRANSTRUCKWEB_DB), entirely separate from the
// TransTruck_WPF desktop product's database. No seeding here: every
// company's starter data is created per-company by
// EnterpriseAdminService.OnboardCompanyAsync at onboarding time.
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await DbBootstrapper.InitialiseAsync(factory);
}

// Before everything else, so it catches whatever the pipeline throws. This
// deliberately replaces the developer exception page even in Development:
// the client should see the same shaped response in both environments, and
// the full detail still goes to the log.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthentication();

// A full-session token stays valid (per its own expiry) for up to
// Jwt:FullSessionHours regardless of what happens to the company's license
// in the meantime — EnterpriseAdmin deactivating a company or a license
// lapsing mid-session must take effect immediately, not just on the next
// login. One extra lookup per authenticated request is cheap at this app's
// scale (small trucking companies, not high QPS).
app.Use(async (context, next) =>
{
    var isFullSession = context.User.Identity?.IsAuthenticated == true
        && context.User.HasClaim(TokenTypes.ClaimType, TokenTypes.Full);

    if (isFullSession)
    {
        var companyIdClaim = context.User.FindFirst(TokenTypes.CompanyIdClaimType)?.Value;
        if (Guid.TryParse(companyIdClaim, out var companyId))
        {
            var factory = context.RequestServices.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var db = await factory.CreateDbContextAsync();
            var valid = await db.Companies.AsNoTracking()
                .Where(c => c.Id == companyId)
                .Select(c => c.IsActive && DateTime.UtcNow <= c.LicenseExpiresOn)
                .FirstOrDefaultAsync();

            if (!valid)
            {
                context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Your company's license has expired or is inactive. Contact your provider to renew it."
                });
                return;
            }
        }
    }

    await next();
});

app.UseAuthorization();

app.MapControllers();

app.Run();
