using System.Net;

namespace Chop.Web.Auth;

public static class UiErrorMapper
{
    public static string ToUserMessage(Exception ex)
    {
        if (ex is HttpRequestException http)
        {
            var message = (http.Message ?? string.Empty).Trim();
            if (message.Contains("Сессия истекла", StringComparison.OrdinalIgnoreCase))
            {
                return "Сессия истекла. Войдите в систему повторно.";
            }

            if (message.Contains("Недостаточно прав", StringComparison.OrdinalIgnoreCase))
            {
                return "Недостаточно прав для выполнения операции.";
            }

            if (message.Contains("Endpoint:", StringComparison.OrdinalIgnoreCase))
            {
                return message;
            }

            return "Ошибка обращения к API. Повторите действие.";
        }

        if (ex is TaskCanceledException)
        {
            return "Операция прервана по таймауту. Повторите позже.";
        }

        if (ex is InvalidOperationException)
        {
            return ex.Message;
        }

        return "Не удалось выполнить операцию. Повторите попытку.";
    }
}
