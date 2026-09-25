using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend;
using backend.Endpoints;
using backend.Services;
using backend.Planning;
using backend.Calendar;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<ICommitmentService, CommitmentService>();
builder.Services.AddScoped<IDailyPlanService, DailyPlanService>();
builder.Services.AddScoped<IPlanningContextService, PlanningContextService>();
builder.Services.AddScoped<IPlanningContextReader, PlanningContextReader>();
builder.Services.AddScoped<IAgentToolExecutor, AgentToolExecutor>();
builder.Services.AddSingleton<ICalendarProvider, DevelopmentCalendarProvider>();
builder.Services.AddHttpClient("OpenAI", client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/");
    client.Timeout = TimeSpan.FromSeconds(60);
});
if (!builder.Configuration.GetValue<bool>("OpenAI:Enabled"))
{
    builder.Services.AddScoped<IPlanningAgent, DevelopmentPlanningAgent>();
}
else
{
    builder.Services.AddScoped<IPlanningAgent, DevelopmentPlanningAgent.OpenAiPlanningAgent>();
}
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var connectionString = ConnectionStringNormalizer.Normalize(
    builder.Configuration.GetConnectionString("DefaultConnection"));
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

app.MapApplicationEndpoints();
app.MapChatEndpoints();
app.MapCalendarEndpoints();

app.Run();
