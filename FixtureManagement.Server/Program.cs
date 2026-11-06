using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FixtureManagement.Server.Components;
using FixtureManagement.Server.Components.Account;
using FixtureManagement.Server.Data;
using FixtureManagement.Server.Services;
using FixtureManagement.Server;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();


builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

// Website pages sign in with the Identity cookie; API clients (e.g. the desktop app)
// send "Authorization: Bearer <token>". The policy scheme picks the right one per request.
const string CookieOrBearerScheme = "CookieOrBearer";

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieOrBearerScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddBearerToken(IdentityConstants.BearerScheme)
    .AddPolicyScheme(CookieOrBearerScheme, "Cookie or bearer token", options =>
    {
        options.ForwardDefaultSelector = context =>
            context.Request.Headers.Authorization.ToString()
                .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? IdentityConstants.BearerScheme
                : IdentityConstants.ApplicationScheme;
    })
    .AddIdentityCookies();

// API calls should get 401/403 rather than being redirected to the login page.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        else
            context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
        else
            context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContextFactory<AppDBContext>(options => {
    options.UseSqlite(connectionString);
    // options.EnableSensitiveDataLogging();
 });

builder.Services.AddQuickGridEntityFrameworkAdapter();
builder.Services.AddDbContext<AuthenticationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AuthenticationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders()
    .AddApiEndpoints();


builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
builder.Services.AddSingleton<FullTimeReconciliationService>();
builder.Services.AddScoped<PageHistoryService>();
builder.Services.AddScoped<FixtureManagement.Shared.Services.ITeamService, EfTeamService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseMigrationsEndPoint();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(FixtureManagement.Client._Imports).Assembly)
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

// Token login for API clients: POST /api/auth/login, POST /api/auth/refresh, etc.
app.MapGroup("/api/auth").WithTags("Auth").MapIdentityApi<ApplicationUser>();

app.MapEventEndpoints();
app.MapFixtureEndpoints();
app.MapTeamEndpoints();
app.Run();

// Lets the test project start the app with WebApplicationFactory<Program>.
public partial class Program { }
