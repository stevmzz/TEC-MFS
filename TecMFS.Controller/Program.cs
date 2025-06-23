using TecMFS.Common;
using TecMFS.Common.DTOs;
using TecMFS.Common.Interfaces;
using TecMFS.Controller.Services;
using TecMFS.Controller.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// registrar como singleton para reutilizar conexiones
builder.Services.AddSingleton<IHttpClientService, HttpClientService>();

// registrar servicio principal de gestion RAID
builder.Services.AddSingleton<IRaidManager, RaidManager>();

// configurar logging
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
    config.SetMinimumLevel(LogLevel.Information);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();
app.UseMiddleware<TecMFS.Controller.Middleware.ErrorHandlingMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();