using System.Net;
using System.Text.Json;

namespace PaymentGateway.Server.Common.Models
{
    public class DataWrapper
    {
        public bool Success { get; set; }
        public HttpStatusCode Code { get; set; }
        public string? Message { get; set; }
        public List<string>? Errors { get; set; }
    }

    public class DataWrapper<T> : DataWrapper
    {
        public T? Data { get; set; }

        public DataWrapper()
        {
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }

        public static DataWrapper<T> Succeed(T data, string message = null)
        {
            return new DataWrapper<T>()
            {
                Success = true,
                Data = data,
                Message = message ?? "Successfully processed data",
                Errors = null,
                Code = HttpStatusCode.OK
            };
        }

        public static DataWrapper<T> Fail_InternalError(T data = default, string message = null, List<string> errors = null)
        {
            return new DataWrapper<T>()
            {
                Success = false,
                Data = data,
                Message = message ?? "Failed to process data. Please try again or contact support if problem persists",
                Errors = errors,
                Code = HttpStatusCode.InternalServerError
            };
        }

        public static DataWrapper<T> Unauthorized(T data = default, string message = null, List<string> errors = null)
        {
            return new DataWrapper<T>()
            {
                Success = false,
                Data = data,
                Message = message ?? "You are not unauthorized",
                Errors = errors,
                Code = HttpStatusCode.Unauthorized
            };
        }

        public static DataWrapper<T> Forbidden(T data = default, string message = null, List<string> errors = null)
        {
            return new DataWrapper<T>()
            {
                Success = false,
                Data = data,
                Message = message ?? "You are not unauthorized to access this data",
                Errors = errors,
                Code = HttpStatusCode.Forbidden
            };
        }

        public static DataWrapper<T> MethodNotAllowed(T data = default, string message = null, List<string> errors = null)
        {
            return new DataWrapper<T>()
            {
                Success = false,
                Data = data,
                Message = message ?? "You are not allowed to access this method",
                Errors = errors,
                Code = HttpStatusCode.MethodNotAllowed
            };
        }

        public static DataWrapper<T> NotFound(T data = default, string message = null, List<string> errors = null)
        {
            return new DataWrapper<T>()
            {
                Success = false,
                Data = data,
                Message = message ?? "Data is not found. Please check again, contact support if problem persists",
                Errors = errors,
                Code = HttpStatusCode.NotFound
            };
        }

        public static DataWrapper<T> Fail(HttpStatusCode code, T data = default, string message = null, List<string> errors = null)
        {
            return new DataWrapper<T>()
            {
                Success = false,
                Data = data,
                Message = message ?? "You are not allowed to access this method",
                Errors = errors,
                Code = code
            };
        }

        public static DataWrapper<T> Status(bool success, HttpStatusCode code, T data = default, string message = null, List<string> errors = null)
        {
            return new DataWrapper<T>()
            {
                Success = success,
                Data = data,
                Message = message ?? "You are not allowed to access this method",
                Errors = errors,
                Code = code
            };
        }

        public static DataWrapper<T> BadRequest(T data = default, string message = null, List<string> errors = null)
        {
            return new DataWrapper<T>()
            {
                Success = false,
                Data = data,
                Message = message ?? "Please check your data input",
                Errors = errors,
                Code = HttpStatusCode.BadRequest
            };
        }

        public static DataWrapper<T> Conflict(T data = default, string message = null, List<string> errors = null)
        {
            return new DataWrapper<T>()
            {
                Success = false,
                Data = data,
                Message = message ?? "Conflict data. Please check your data input",
                Errors = errors,
                Code = HttpStatusCode.Conflict
            };
        }

        public static DataWrapper<T> Unprocessable(T data = default, string message = null, List<string> errors = null)
        {
            return new DataWrapper<T>()
            {
                Success = false,
                Data = data,
                Message = message ?? "Unprocessable data. Please try again later",
                Errors = errors,
                Code = HttpStatusCode.UnprocessableContent
            };
        }

        public static DataWrapper<T> Unavailable(T data = default, string message = null, List<string> errors = null)
        {
            return new DataWrapper<T>()
            {
                Success = false,
                Data = data,
                Message = message ?? "Service currently unavailable. Please try again later",
                Errors = errors,
                Code = HttpStatusCode.ServiceUnavailable
            };
        }

        public static DataWrapper<T> CreateFromOther(DataWrapper dataWrapper, T? data = default)
        {
            if (dataWrapper == null)
            {
                return null;
            }

            return new DataWrapper<T>()
            {
                Success = dataWrapper.Success,
                Data = data,
                Message = dataWrapper.Message,
                Errors = dataWrapper.Errors,
                Code = dataWrapper.Code
            };
        }
    }
}
