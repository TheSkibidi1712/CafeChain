using System.Collections.Generic;

namespace CafeChain.Application.Results
{
    public class ServiceResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public List<string> Errors { get; set; } = new List<string>();

        public static ServiceResult Success(string message = null)
        {
            return new ServiceResult { IsSuccess = true, Message = message };
        }

        public static ServiceResult Failure(string message, List<string> errors = null)
        {
            return new ServiceResult 
            { 
                IsSuccess = false, 
                Message = message, 
                Errors = errors ?? new List<string>() 
            };
        }
    }

    public class ServiceResult<T> : ServiceResult
    {
        public T Data { get; set; }

        public static ServiceResult<T> Success(T data, string message = null)
        {
            return new ServiceResult<T> { IsSuccess = true, Data = data, Message = message };
        }

        public new static ServiceResult<T> Failure(string message, List<string> errors = null)
        {
            return new ServiceResult<T> 
            { 
                IsSuccess = false, 
                Message = message, 
                Errors = errors ?? new List<string>() 
            };
        }
    }
}
