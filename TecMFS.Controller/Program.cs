using TecMFS.Common;
using TecMFS.Common.DTOs;
using TecMFS.Common.Interfaces;
using TecMFS.Controller.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// configurar httpclient para comunicacion entre componentes
builder.Services.AddHttpClient<IHttpClientService, HttpClientService>(client =>
{
    // configuracion global del cliente http
    client.DefaultRequestHeaders.Add("User-Agent", "TecMFS-Controller/1.0");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// registrar como singleton para reutilizar conexiones
builder.Services.AddSingleton<IHttpClientService, HttpClientService>();

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

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();