using Encurtador.Api.Endpoints;
using Encurtador.Application;
using Encurtador.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    var problem = context.ProblemDetails;
    if (problem.Status == StatusCodes.Status400BadRequest && problem is not HttpValidationProblemDetails)
    {
        problem.Title = "A requisição não pôde ser lida.";
        problem.Detail = "O corpo da requisição está ausente ou não é um JSON válido.";
    }
    else if (problem.Status == StatusCodes.Status500InternalServerError)
    {
        problem.Title = "Erro interno.";
    }
});

// Falha de binding vira 400 vazio, e o UseStatusCodePages escreve o ProblemDetails (research.md D8).
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = false);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Interface de testes só em desenvolvimento: /openapi/v1.json e /scalar (research.md D12).
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("Encurtador de Links"));
}

app.MapLinkEndpoints();
app.MapRedirectEndpoints();

await app.Services.ApplyMigrationsAsync();

app.Run();

public partial class Program;
