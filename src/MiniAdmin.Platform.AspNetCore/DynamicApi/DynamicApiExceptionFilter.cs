using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MiniAdmin.Platform.AspNetCore.DynamicApi;

internal sealed class DynamicApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var statusCode = context.Exception switch
        {
            KeyNotFoundException => StatusCodes.Status404NotFound,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            ArgumentException => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status400BadRequest,
            _ => 0
        };
        if (statusCode == 0)
        {
            return;
        }

        context.Result = new ObjectResult(new
        {
            code = statusCode,
            message = context.Exception.Message
        })
        {
            StatusCode = statusCode
        };
        context.ExceptionHandled = true;
    }
}
