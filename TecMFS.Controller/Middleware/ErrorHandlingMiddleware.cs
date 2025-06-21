using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading.Tasks;

namespace TecMFS.Controller.Middleware
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;

        public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                // Continuar con el siguiente middleware o controlador
                await _next(context);
            }
            catch (Exception ex)
            {
                // Registrar el error
                _logger.LogError(ex, "Se produjo una excepción no controlada");

                // Preparar la respuesta de error
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";

                var response = new
                {
                    error = "Error interno del servidor.",
                    detalle = ex.Message
                };

                await context.Response.WriteAsJsonAsync(response);
            }
        }
    }
}
// This code defines a middleware for handling errors in an ASP.NET Core application.