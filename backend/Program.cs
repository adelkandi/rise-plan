using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));
}

if (builder.Environment.IsDevelopment())
{
    builder.Services
        .AddAuthentication("Development")
        .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(
            "Development",
            _ => { });
    builder.Services.AddAuthorization();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseAuthentication();
    app.UseAuthorization();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors("Frontend");

app.MapHealthChecks("/health")
    .WithName("Health")
    .WithSummary("Checks whether the API is running.");

app.MapGet("/api/me", (HttpContext context) =>
{
    var user = context.User;
    return Results.Ok(new
    {
        id = user.FindFirst("sub")?.Value ?? "anonymous",
        name = user.Identity?.Name ?? "Anonymous"
    });
})
.WithName("GetCurrentUser")
.WithSummary("Returns the current development user.")
.Produces(StatusCodes.Status200OK);

app.Run();
