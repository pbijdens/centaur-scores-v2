using CentaurScores.Api.Application;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
       .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
       .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
       .AddEnvironmentVariables();

var connectionString = builder.Configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("ConnectionStrings:Default is required");
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret is required");

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)).UseSnakeCaseNamingConvention());
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IScoringService, ScoringService>();
builder.Services.AddScoped<ILiveScoringService, LiveScoringService>();
builder.Services.AddScoped<ICompetitionService, CompetitionService>();
builder.Services.AddScoped<IScorekeeperService, ScorekeeperService>();
builder.Services.AddScoped<IParticipantListExcelService, ParticipantListExcelService>();
builder.Services.AddScoped<IPersonalBestContext, PersonalBestContext>();
builder.Services.AddScoped<IPersonalBestEngine, PersonalBestEngine>();
builder.Services.AddScoped<IPersonalBestExcelService, PersonalBestExcelService>();
builder.Services.AddScoped<IPersonalBestRegistrationService, PersonalBestRegistrationService>();
builder.Services.AddScoped<IPersonalBestLiveLookup, PersonalBestLiveLookup>();
builder.Services.AddScoped<IBackupService, BackupService>();
builder.Services.AddScoped<IRestoreService, RestoreService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Centaur Scores API",
        Version = "v1",
        Description = "Tenant management, match scoring, competition results, live scores, and scorekeeper access. See API_ENDPOINTS.md for workflow guidance."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a JWT returned by POST /api/auth/login."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
    });
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
});
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
    ValidateIssuer = false,
    ValidateAudience = false,
    ValidateLifetime = true,
    ClockSkew = TimeSpan.FromMinutes(1)
});
builder.Services.AddAuthorization();

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    await DatabaseInitializer.InitializeAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
}
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Centaur Scores API v1"));
app.MapGet("/health", async (ApplicationDbContext db) => Results.Ok(new { status = await db.Database.CanConnectAsync() ? "ok" : "database_unavailable", utc = DateTimeOffset.UtcNow }));
app.MapControllers();
app.Run();

public partial class Program { }