using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.IO;

namespace CarRental.WebApi.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogWarning("Request aborted by client. TraceId={TraceId}", context.TraceIdentifier);
        }
        catch (BadHttpRequestException exception) when (IsMalformedDamageMultipartRequest(context, exception))
        {
            logger.LogWarning(exception, "Malformed multipart damage request. TraceId={TraceId}", context.TraceIdentifier);
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Invalid damage upload request",
                "Некоректні дані форми пошкодження. Спробуйте ще раз або збережіть акт без фото.");
        }
        catch (InvalidDataException exception) when (IsMalformedDamageMultipartRequest(context, exception))
        {
            logger.LogWarning(exception, "Malformed multipart damage request. TraceId={TraceId}", context.TraceIdentifier);
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Invalid damage upload request",
                "Некоректні дані форми пошкодження. Спробуйте ще раз або збережіть акт без фото.");
        }
        catch (ArgumentException exception) when (IsModelValidationConfigurationException(exception))
        {
            logger.LogError(
                exception,
                "Request validation configuration failure. TraceId={TraceId} Path={Path}",
                context.TraceIdentifier,
                context.Request.Path);
            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Validation configuration error",
                "The server failed to process the request.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception. TraceId={TraceId}", context.TraceIdentifier);
            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Unhandled server error",
                "The server failed to process the request.");
        }
    }

    private static bool IsMalformedDamageMultipartRequest(HttpContext context, Exception exception)
    {
        if (!context.Request.Path.StartsWithSegments("/api/damages", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!context.Request.HasFormContentType)
        {
            var contentType = context.Request.ContentType ?? string.Empty;
            if (!contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var message = exception.Message ?? string.Empty;
        return message.Contains("multipart", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("boundary", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("form", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsModelValidationConfigurationException(ArgumentException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is not ArgumentException && current is not FormatException)
            {
                continue;
            }

            var message = current.Message ?? string.Empty;
            if (message.Contains("not a valid value for Decimal", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var stackTrace = current.StackTrace ?? string.Empty;
            if (stackTrace.Contains($"{typeof(RangeAttribute).FullName}.SetupConversion", StringComparison.Ordinal) ||
                stackTrace.Contains($"{typeof(RangeAttribute).FullName}.IsValid", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string title, string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;

        return context.Response.WriteAsJsonAsync(problem);
    }
}
