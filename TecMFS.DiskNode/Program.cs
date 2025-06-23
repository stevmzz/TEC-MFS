using TecMFS.Common.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Obtener el nodeId desde argumentos
int nodeId = 0;
if (args.Length > 0 && int.TryParse(args[0], out int parsedId))
    nodeId = parsedId;

//
builder.Services.Configure<NodeConfiguration>(builder.Configuration);

// Cargar configuración específica del nodo
builder.Host.ConfigureAppConfiguration((hostingContext, config) =>
{
    config.AddJsonFile($"appsettings.Node{nodeId}.json", optional: false);
});

// Aplicar configuración de Kestrel del archivo JSON cargado
builder.WebHost.ConfigureKestrel((context, options) =>
{
    options.Configure(context.Configuration.GetSection("Kestrel"));
});

// Registrar servicios
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
