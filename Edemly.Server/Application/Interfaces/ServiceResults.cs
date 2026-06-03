using Microsoft.AspNetCore.Http;

namespace Edemly.Server.Api.Services
{
    public sealed record ServiceMessageResult(bool Success, int StatusCode, string Message)
    {
        public static ServiceMessageResult Ok(string message) => new(true, StatusCodes.Status200OK, message);

        public static ServiceMessageResult BadRequest(string message) => new(false, StatusCodes.Status400BadRequest, message);

        public static ServiceMessageResult Forbidden(string message = "Forbidden") => new(false, StatusCodes.Status403Forbidden, message);

        public static ServiceMessageResult NotFound(string message) => new(false, StatusCodes.Status404NotFound, message);

        public static ServiceMessageResult Unexpected(string message = "Unexpected server error.") => new(false, StatusCodes.Status500InternalServerError, message);
    }

    public sealed record ServiceDataResult<T>(bool Success, int StatusCode, T? Data, string? Message)
    {
        public static ServiceDataResult<T> Ok(T? data) => new(true, StatusCodes.Status200OK, data, null);

        public static ServiceDataResult<T> BadRequest(string message) => new(false, StatusCodes.Status400BadRequest, default, message);

        public static ServiceDataResult<T> Forbidden(string message = "Forbidden") => new(false, StatusCodes.Status403Forbidden, default, message);

        public static ServiceDataResult<T> NotFound(string message) => new(false, StatusCodes.Status404NotFound, default, message);

        public static ServiceDataResult<T> Unexpected(string message = "Unexpected server error.") => new(false, StatusCodes.Status500InternalServerError, default, message);
    }
}
