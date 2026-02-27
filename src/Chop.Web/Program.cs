using Chop.Web.Components;
using Chop.Web.Auth;
using Chop.Web.Backoffice;
using Chop.Web.Incidents;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpClient("Api", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var baseUrl = config["Api:BaseUrl"];
    client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(baseUrl) ? "https://localhost:5001" : baseUrl);
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<WebAuthSession>();
builder.Services.AddScoped<IncidentsApiClient>();
builder.Services.AddScoped<IncidentRealtimeClient>();
builder.Services.AddScoped<BackofficeApiClient>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
