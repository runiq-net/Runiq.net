using Runiq.AI.Core;
using Runiq.AI.Expense.Agents;
using Runiq.AI.Expense.Configuration;
using Runiq.AI.Expense.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<ExpenseDataOptions>()
    .Bind(builder.Configuration.GetSection(ExpenseDataOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Directory), "ExpenseData:Directory is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ReportingCurrency), "ExpenseData:ReportingCurrency is required.")
    .Validate(options => options.MaxWorkbookCount is >= 2 and <= 100, "ExpenseData:MaxWorkbookCount must be between 2 and 100.")
    .Validate(options => options.MaxRowsPerSheet is >= 1 and <= 100_000, "ExpenseData:MaxRowsPerSheet must be between 1 and 100,000.")
    .Validate(options => options.DefaultToolResultLimit is >= 1 and <= 500, "ExpenseData:DefaultToolResultLimit must be between 1 and 500.")
    .Validate(options => options.MaxToolResultLimit is >= 1 and <= 1_000, "ExpenseData:MaxToolResultLimit must be between 1 and 1,000.")
    .Validate(options => options.DefaultToolResultLimit <= options.MaxToolResultLimit, "ExpenseData:DefaultToolResultLimit cannot exceed MaxToolResultLimit.")
    .Validate(options => options.MaxDuplicateCandidateComparisons is >= 1 and <= 10_000_000, "ExpenseData:MaxDuplicateCandidateComparisons must be between 1 and 10,000,000.")
    .ValidateOnStart();
builder.Services.AddSingleton<ExcelExpenseDataSource>();
builder.Services.AddSingleton(sp => sp.GetRequiredService<ExcelExpenseDataSource>().Load());

var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];
builder.Services.AddRuniqServer(options => options.AddAgent(ExpenseAnalystAgent.Create(openAiApiKey)));

var app = builder.Build();

_ = app.Services.GetRequiredService<ExpenseDataSet>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseRuniqDashboard(options =>
{
    options.Path = "/dashboard";
    options.Title = "Runiq Corporate Expense Analyst";
    options.Authentication(authentication =>
    {
        // This anonymous dashboard is intentionally limited to the local sample experience.
        authentication.AllowAnonymous();
    });
});

app.Run();
