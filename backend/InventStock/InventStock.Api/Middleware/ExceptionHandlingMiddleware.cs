using System.Net;
using FluentValidation;
using InventStock.Application.Common.Exceptions;
using InventStock.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace InventStock.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    /// <summary>Nginx convention for "client closed the connection before the response was produced".</summary>
    private const int ClientClosedRequest = 499;

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogError(exception, "Response already started; the exception could not be converted to a problem response.");
            throw exception;
        }

        var problem = Translate(exception);

        if (problem.Status >= (int)HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception while processing {Method} {Path}.",
                context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogInformation("Request {Method} {Path} rejected: {Detail}",
                context.Request.Method, context.Request.Path, problem.Detail);
        }

        problem.Instance = context.Request.Path;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.Clear();
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        await context.Response.WriteAsJsonAsync(
            problem,
            problem.GetType(),
            options: null,
            contentType: "application/problem+json");
    }

    private ProblemDetails Translate(Exception exception) => exception switch
    {
        ValidationException validation => BuildValidationProblem(validation),

        NotFoundException notFound => Build(StatusCodes.Status404NotFound, "Resource not found", notFound.Message),

        ConflictException conflict => Build(StatusCodes.Status409Conflict, "Request conflicts with current state", conflict.Message),

        InsufficientStockException insufficient =>
            Build(StatusCodes.Status409Conflict, "Insufficient stock", insufficient.Message),

        DomainException domain => Build(StatusCodes.Status400BadRequest, "Invalid operation", domain.Message),

        AuthenticationFailedException auth => Build(StatusCodes.Status401Unauthorized, "Authentication failed", auth.Message),

        UnauthorizedAccessException => Build(StatusCodes.Status403Forbidden, "Forbidden", "You are not allowed to perform this action."),

        OperationCanceledException => Build(ClientClosedRequest, "Request cancelled", "The request was cancelled."),

        // Internal failures never leak their message to the caller outside development.
        _ => Build(
            StatusCodes.Status500InternalServerError,
            "Unexpected error",
            _environment.IsDevelopment() ? exception.ToString() : "An unexpected error occurred.")
    };

    private static ValidationProblemDetails BuildValidationProblem(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred."
        };
    }

    private static ProblemDetails Build(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail
    };
}
