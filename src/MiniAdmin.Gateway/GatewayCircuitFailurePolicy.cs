using Yarp.ReverseProxy.Forwarder;

namespace MiniAdmin.Gateway;

public static class GatewayCircuitFailurePolicy
{
    public static bool IsTransientFailure(ForwarderError? forwarderError, int statusCode)
    {
        if (forwarderError == ForwarderError.RequestCanceled)
        {
            return false;
        }

        if (forwarderError is not null and not ForwarderError.None)
        {
            return true;
        }

        return statusCode is
            StatusCodes.Status502BadGateway or
            StatusCodes.Status503ServiceUnavailable or
            StatusCodes.Status504GatewayTimeout;
    }
}
