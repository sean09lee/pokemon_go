using TeamBuilder.Components;
using TeamBuilder.Services.Helpers;
using TeamBuilder.Services.Services;
using TeamBuilder.AI;
using TeamBuilder.AI.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

var builder = WebApplication.CreateBuilder(args);

var appConfigConnectionString = builder.Configuration.GetConnectionString("AppConfig");
var appConfigLabel = builder.Configuration["AppConfiguration:Label"];

if (!string.IsNullOrWhiteSpace(appConfigConnectionString))
{
    builder.Configuration.AddAzureAppConfiguration(options =>
    {
        options.Connect(appConfigConnectionString)
            .Select(KeyFilter.Any, appConfigLabel);
    });
}

// Add services to the container.
builder.Services.AddHealthChecks();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<CsvParserService>();
builder.Services.AddScoped<RankingService>();
builder.Services.AddSingleton<CacheService>();
builder.Services.AddScoped<HubApiService>();
builder.Services.AddScoped<PogoApiService>();
builder.Services.AddHttpClient<PogoHelpers>(client =>
{
    client.BaseAddress = new Uri("https://pogoapi.net/");
});

builder.Services.Configure<AIAdvisorOptions>(builder.Configuration.GetSection(AIAdvisorOptions.SectionName));
builder.Services.AddSingleton<FoundryChatClient>();
builder.Services.AddScoped<AIAdvisorService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

var leagueImagesCandidates = new[]
{
    Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "wwwroot", "assets", "images")),
    Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "assets", "images"))
};

var leagueImagesPath = leagueImagesCandidates.FirstOrDefault(Directory.Exists);
if (!string.IsNullOrWhiteSpace(leagueImagesPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(leagueImagesPath),
        RequestPath = "/assets/images"
    });
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapHealthChecks("/health");

app.Run();
