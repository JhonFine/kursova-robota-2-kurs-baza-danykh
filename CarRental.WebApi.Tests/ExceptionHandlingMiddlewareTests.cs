using CarRental.WebApi.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace CarRental.WebApi.Tests;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ShouldReturnBadRequest_ForMalformedDamageMultipartRequest()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidDataException("Missing content-type boundary."),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/damages";
        context.Request.ContentType = "multipart/form-data";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        document.RootElement.GetProperty("detail").GetString()
            .Should()
            .Be("Некоректні дані форми пошкодження. Спробуйте ще раз або збережіть акт без фото.");
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnServerError_ForNonMultipartException()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("boom"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/damages";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        document.RootElement.GetProperty("detail").GetString()
            .Should()
            .Be("The server failed to process the request.");
    }
}
