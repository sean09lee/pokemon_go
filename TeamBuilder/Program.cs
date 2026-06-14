using TeamBuilder.Components;
using TeamBuilder.Services.Helpers;
using TeamBuilder.Services.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
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

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
