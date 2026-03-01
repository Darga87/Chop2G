using System.Net;
using System.Text.Json;

namespace Chop.Web.Auth;

public static class ApiErrorFormatter
{
    public static string Build(HttpStatusCode statusCode, string uri, string? responseBody = null)
    {
        var (code, userMessage) = Map(statusCode, responseBody);
        var traceId = ExtractTraceId(responseBody);

        var details = string.IsNullOrWhiteSpace(responseBody) ? string.Empty : $" Детали: {responseBody}";
        var tracePart = string.IsNullOrWhiteSpace(traceId) ? string.Empty : $" traceId: {traceId}.";

        return $"{userMessage} Код: {code}. Endpoint: {uri}.{tracePart}{details}";
    }

    private static (string Code, string Message) Map(HttpStatusCode statusCode, string? responseBody)
    {
        var hasValidationErrors = HasValidationErrors(responseBody);

        return statusCode switch
        {
            HttpStatusCode.BadRequest when hasValidationErrors => ("API.VALIDATION", "Проверьте корректность введенных данных."),
            HttpStatusCode.BadRequest => ("API.BAD_REQUEST", "Некорректный запрос."),
            HttpStatusCode.Unauthorized => ("API.UNAUTHORIZED", "Сессия истекла или недействительна. Выполните вход повторно."),
            HttpStatusCode.Forbidden => ("API.FORBIDDEN", "Недостаточно прав для выполнения операции."),
            HttpStatusCode.NotFound => ("API.NOT_FOUND", "Запрошенный ресурс не найден."),
            HttpStatusCode.Conflict => ("API.CONFLICT", "Конфликт состояния данных. Обновите страницу и повторите действие."),
            HttpStatusCode.UnprocessableEntity => ("API.UNPROCESSABLE", "Данные не прошли валидацию."),
            HttpStatusCode.TooManyRequests => ("API.RATE_LIMIT", "Слишком много запросов. Повторите через минуту."),
            HttpStatusCode.InternalServerError => ("API.INTERNAL", "Внутренняя ошибка сервера."),
            HttpStatusCode.BadGateway => ("API.BAD_GATEWAY", "Проблема на промежуточном шлюзе."),
            HttpStatusCode.ServiceUnavailable => ("API.UNAVAILABLE", "Сервис временно недоступен."),
            HttpStatusCode.GatewayTimeout => ("API.TIMEOUT", "Сервис не ответил вовремя."),
            _ => ($"API.HTTP_{(int)statusCode}", $"Ошибка API ({(int)statusCode})."),
        };
    }

    private static bool HasValidationErrors(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Object
                && errors.EnumerateObject().Any();
        }
        catch
        {
            return false;
        }
    }

    private static string? ExtractTraceId(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("traceId", out var traceId)
                && traceId.ValueKind == JsonValueKind.String)
            {
                return traceId.GetString();
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}
