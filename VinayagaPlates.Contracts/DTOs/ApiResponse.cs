namespace VinayagaPlates.Contracts.DTOs
{
    public class ApiResponse<T>
    {
        public bool IsSuccess { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }

        public static ApiResponse<T> Success(T data, string message = "Success", int statusCode = 200) =>
            new ApiResponse<T> { IsSuccess = true, StatusCode = statusCode, Message = message, Data = data };

        public static ApiResponse<T> Fail(string message, int statusCode = 400) =>
            new ApiResponse<T> { IsSuccess = false, StatusCode = statusCode, Message = message, Data = default };
    }
}
