using Microsoft.AspNetCore.Http;

namespace Edemly.Server.Api.Services
{
    public sealed record ServiceResult(
        bool Success,
        int StatusCode,
        string? Message = null)
    {
        public static ServiceResult Ok(string? message = null) =>
            new(true, StatusCodes.Status200OK, message);

        public static ServiceResult Created(string? message = null) =>
            new(true, StatusCodes.Status201Created, message);

        public static ServiceResult NoContent() =>
            new(true, StatusCodes.Status204NoContent);

        public static ServiceResult BadRequest(string message) =>
            new(false, StatusCodes.Status400BadRequest, message);

        public static ServiceResult Unauthorized(string message = "Unauthorized") =>
            new(false, StatusCodes.Status401Unauthorized, message);

        public static ServiceResult Forbidden(string message = "Forbidden") =>
            new(false, StatusCodes.Status403Forbidden, message);

        public static ServiceResult NotFound(string message) =>
            new(false, StatusCodes.Status404NotFound, message);

        public static ServiceResult Conflict(string message) =>
            new(false, StatusCodes.Status409Conflict, message);

        public static ServiceResult Unexpected(string message = "Unexpected server error.") =>
            new(false, StatusCodes.Status500InternalServerError, message);
    }

    public sealed record ServiceResult<T>(
        bool Success,
        int StatusCode,
        T? Data = default,
        string? Message = null)
    {
        public static ServiceResult<T> Ok(T? data, string? message = null) =>
            new(true, StatusCodes.Status200OK, data, message);

        public static ServiceResult<T> Created(T? data, string? message = null) =>
            new(true, StatusCodes.Status201Created, data, message);

        public static ServiceResult<T> BadRequest(string message) =>
            new(false, StatusCodes.Status400BadRequest, default, message);

        public static ServiceResult<T> Unauthorized(string message = "Unauthorized") =>
            new(false, StatusCodes.Status401Unauthorized, default, message);

        public static ServiceResult<T> Forbidden(string message = "Forbidden") =>
            new(false, StatusCodes.Status403Forbidden, default, message);

        public static ServiceResult<T> NotFound(string message) =>
            new(false, StatusCodes.Status404NotFound, default, message);

        public static ServiceResult<T> Conflict(string message) =>
            new(false, StatusCodes.Status409Conflict, default, message);

        public static ServiceResult<T> Unexpected(string message = "Unexpected server error.") =>
            new(false, StatusCodes.Status500InternalServerError, default, message);
    }
}