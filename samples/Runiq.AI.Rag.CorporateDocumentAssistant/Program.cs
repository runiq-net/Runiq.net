using Runiq.AI.Core;
using Runiq.AI.Rag.CorporateDocumentAssistant;

var builder = WebApplication.CreateBuilder(args);

var documentsPath = Path.Combine(AppContext.BaseDirectory, "SampleDocuments");
CorporateDocumentAssistantSetup.Configure(builder.Services, builder.Configuration, documentsPath);
builder.Services.AddControllers();

var app = builder.Build();

app.UseRuniqDashboard(options =>
{
    options.Path = "/dashboard";
    options.Title = "Runiq Corporate Document Assistant";
    options.Authentication(auth =>
    {
        // Demo/sample only. Do not use AllowAnonymous in production.
        auth.AllowAnonymous();
    });
});

app.MapControllers();

app.Run();
