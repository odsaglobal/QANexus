using System.Text;
using ATIP.Api.Hubs;
using ATIP.Api.Middleware;
using ATIP.Api.Services;
using ATIP.Application;
using ATIP.Application.Common.Interfaces;
using ATIP.Infrastructure;
using ATIP.Infrastructure.Configuration;
using ATIP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Structured logging via Serilog, configured from appsettings.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// ----- Application & Infrastructure layers -----
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Current-user accessor bridges HTTP identity into the Application layer.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Real-time exploration streaming (SignalR + tenant-scoped live screencast).
builder.Services.AddSignalR();
builder.Services.AddSingleton<IExplorationLiveStream, ExplorationLiveStream>();

// ----- Web API -----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// RFC 7807 problem details + centralized exception handling.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ----- Authentication (JWT bearer) -----
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? new JwtOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep original claim types (sub, tenant_id, ...)
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };

        // WebSocket clients can't send an Authorization header, so SignalR passes the token
        // as the `access_token` query string parameter for hub connections.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ----- CORS for the React dev server -----
const string spaCorsPolicy = "spa";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
    options.AddPolicy(spaCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

// ----- Swagger / OpenAPI with bearer auth -----
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Autonomous AI Test Intelligence Platform (ATIP) API",
        Version = "v1",
        Description = "Enterprise API for AI-driven autonomous test automation."
    });

    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter the JWT access token (without the 'Bearer ' prefix).",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = JwtBearerDefaults.AuthenticationScheme
        }
    };
    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, scheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = [] });
});

var app = builder.Build();

// ----- HTTP pipeline -----
app.UseSerilogRequestLogging();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(ui => ui.SwaggerEndpoint("/swagger/v1/swagger.json", "ATIP API v1"));
    await app.ApplyMigrationsAsync();
}

app.UseHttpsRedirection();
app.UseCors(spaCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ExplorationHub>("/hubs/exploration").RequireCors(spaCorsPolicy);
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

app.Run();

/// <summary>Development helper that applies pending EF Core migrations at startup.</summary>
internal static class MigrationExtensions
{
    public static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }
}

// Exposes the implicit Program class to the integration-test host.
public partial class Program;
