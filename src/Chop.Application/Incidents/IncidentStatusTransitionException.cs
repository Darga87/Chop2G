namespace Chop.Application.Incidents;

public sealed class IncidentStatusTransitionException : Exception
{
    private IncidentStatusTransitionException(string code, string message, int httpStatusCode)
        : base(message)
    {
        Code = code;
        HttpStatusCode = httpStatusCode;
    }

    public string Code { get; }

    public int HttpStatusCode { get; }

    public static IncidentStatusTransitionException BadRequest(string code, string message) =>
        new(code, message, 400);

    public static IncidentStatusTransitionException Conflict(string code, string message) =>
        new(code, message, 409);
}
