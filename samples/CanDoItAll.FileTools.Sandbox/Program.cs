using CanDoItAll.FileTools.Sandbox.Components;
using CanDoItAll.FileTools.Sandbox.Demo;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton<SandboxFileSystemRoot>();
builder.Services.AddSingleton<SandboxBrowserSessionFactory>();
builder.Services.AddSingleton(SandboxInteractionComposition.Create());
builder.Services.AddScoped<SandboxInteractionGateway>();

var app = builder.Build();

app.UseAntiforgery();
app.MapStaticAssets();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ready",
    component = "file-tools-sandbox"
}));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
