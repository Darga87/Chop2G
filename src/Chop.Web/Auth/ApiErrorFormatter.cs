using System.Net;

namespace Chop.Web.Auth;

public static class ApiErrorFormatter
{
    public static string Build(HttpStatusCode statusCode, string uri, string? responseBody = null)
    {
        var status = (int)statusCode;
        var message = statusCode switch
        {
            HttpStatusCode.Unauthorized => "Сессия истекла или недействительна. Выполните вход повторно.",
            HttpStatusCode.Forbidden => "Недостаточно прав для выполнения операции.",
            HttpStatusCode.NotFound => "Запрошенный ресурс не найден.",
            HttpStatusCode.BadRequest => "Некорректный запрос. Проверьте введённые данные.",
            HttpStatusCode.Conflict => "Конфликт состояния данных. Обновите страницу и повторите действие.",
            HttpStatusCode.UnprocessableEntity => "Данные не прошли валидацию.",
            HttpStatusCode.InternalServerError => "Внутренняя ошибка сервера.",
            HttpStatusCode.BadGateway => "Ошибка шлюза. Повторите позже.",
            HttpStatusCode.ServiceUnavailable => "Сервис временно недоступен. Повторите позже.",
            HttpStatusCode.GatewayTimeout => "Сервис не ответил вовремя. Повторите позже.",
            _ => $"Ошибка API ({status}).",
        };

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return $"{message} Endpoint: {uri}.";
        }

        return $"{message} Endpoint: {uri}. Детали: {responseBody}";
    }
}
