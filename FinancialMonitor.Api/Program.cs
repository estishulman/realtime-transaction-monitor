using FinancialMonitor.Api.Application.Abstractions;
using FinancialMonitor.Api.Application.Transactions;
using FinancialMonitor.Api.Infrastructure.Persistence;
using FinancialMonitor.Api.Infrastructure.Realtime;
using FinancialMonitor.Api.Infrastructure.DependencyInjection;
using FinancialMonitor.Api.Presentation.Hubs;
using Microsoft.EntityFrameworkCore;
using FinancialMonitor.Api.Infrastructure.Caching;
using FinancialMonitor.Api.Infrastructure.Health;

var builder = WebApplication.CreateBuilder(args);

const string frontendCorsPolicy = "FrontendCors";

builder.Services.AddControllers();
builder.Services.AddHealthChecks()
	.AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddCors(options =>
{
	options.AddPolicy(frontendCorsPolicy, policy =>
	{
		policy.WithOrigins(
			"http://localhost:5173",
			"http://localhost:8081")
			.AllowAnyHeader()
			.AllowAnyMethod()
			.AllowCredentials();
	});
});
var databaseProvider = builder.Configuration["Database:Provider"]?.ToLowerInvariant() ?? "sqlite";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
	?? "Data Source=financial-monitor.db";
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
builder.Services.AddDbContextFactory<FinancialMonitorDbContext>(options =>
	_ = databaseProvider switch
	{
		"postgres" => options.UseNpgsql(connectionString),
		"sqlite" => options.UseSqlite(connectionString),
		_ => throw new InvalidOperationException("Database:Provider must be Sqlite or Postgres.")
	});
builder.Services.AddSingleton<EfTransactionRepository>();
builder.Services.AddTransactionCaching(databaseProvider, builder.Configuration);
builder.Services.AddSingleton<ITransactionService, TransactionService>();
builder.Services.AddSingleton<ITransactionProcessor, TransactionProcessor>();
builder.Services.AddSingleton<ITransactionBroadcaster, SignalRTransactionBroadcaster>();
var signalRBuilder = builder.Services.AddSignalR();

if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
	signalRBuilder.AddStackExchangeRedis(redisConnectionString);
}

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

app.UseCors(frontendCorsPolicy);
app.MapHealthChecks("/health");
app.MapControllers();
app.MapHub<TransactionHub>("/hubs/transactions");

app.Run();

public partial class Program;
