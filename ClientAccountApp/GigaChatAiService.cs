using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClientAccountApp
{
    public static class GigaChatAiService
    {
        private static readonly HttpClient HttpClient = new();

        // Турникет: только один поток одновременно может обновлять токен
        private static readonly SemaphoreSlim _tokenLock = new SemaphoreSlim(1, 1);

        private static string? _accessToken;
        private static DateTimeOffset _accessTokenExpiresAt;

        public static async Task<string> AskAsync(string systemPrompt, string userPrompt)
        {
            var settings = AiSettingsService.Load();

            if (!settings.IsEnabled)
                throw new InvalidOperationException("ИИ-помощник GigaChat выключен в настройках.");

            string accessToken = await GetAccessTokenAsync(settings);

            var requestBody = new
            {
                model = settings.GigaChatModel,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = systemPrompt
                    },
                    new
                    {
                        role = "user",
                        content = userPrompt
                    }
                },
                temperature = 0.2,
                stream = false
            };

            string json = JsonSerializer.Serialize(requestBody);

            using var request = new HttpRequestMessage(HttpMethod.Post, settings.GigaChatApiUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await HttpClient.SendAsync(request);
            string responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Ошибка GigaChat API: {(int)response.StatusCode} {response.ReasonPhrase}. {responseJson}");
            }

            var parsed = JsonSerializer.Deserialize<GigaChatChatResponse>(responseJson);

            string? content = parsed?.choices?
                .FirstOrDefault()?
                .message?
                .content;

            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("GigaChat вернул пустой ответ.");

            return content.Trim();
        }

        public static async Task<string> TestConnectionAsync()
        {
            return await AskAsync(
                "Ты кратко отвечаешь на русском языке.",
                "Ответь одним предложением: подключение к GigaChat работает.");
        }

        private static async Task<string> GetAccessTokenAsync(AiSettings settings)
        {
            // Быстрая проверка БЕЗ блокировки — токен ещё валиден?
            // Если да — сразу возвращаем, никого не задерживаем
            if (!string.IsNullOrWhiteSpace(_accessToken) &&
                _accessTokenExpiresAt > DateTimeOffset.Now.AddMinutes(2))
            {
                return _accessToken;
            }

            // Токен устарел — входим в турникет.
            // Если другой поток уже зашёл — ждём здесь пока он не выйдет.
            await _tokenLock.WaitAsync();

            try
            {
                // ВАЖНО: проверяем ещё раз уже внутри турникета.
                // Пока мы ждали — другой поток мог уже обновить токен.
                // Тогда нам не нужно делать лишний запрос.
                if (!string.IsNullOrWhiteSpace(_accessToken) &&
                    _accessTokenExpiresAt > DateTimeOffset.Now.AddMinutes(2))
                {
                    return _accessToken;
                }

                // Токен точно устарел — запрашиваем новый
                string authorizationKey = AiSettingsService.GetGigaChatAuthorizationKey();

                if (string.IsNullOrWhiteSpace(authorizationKey))
                    throw new InvalidOperationException("Authorization Key GigaChat не указан.");

                authorizationKey = authorizationKey.Trim();

                if (authorizationKey.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                {
                    authorizationKey = authorizationKey.Substring("Basic ".Length).Trim();
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, settings.GigaChatOAuthUrl);

                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authorizationKey);
                request.Headers.Add("RqUID", Guid.NewGuid().ToString());

                request.Content = new StringContent(
                    $"scope={Uri.EscapeDataString(settings.GigaChatScope)}",
                    Encoding.UTF8,
                    "application/x-www-form-urlencoded");

                using HttpResponseMessage response = await HttpClient.SendAsync(request);
                string responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"Ошибка получения токена GigaChat: {(int)response.StatusCode} {response.ReasonPhrase}. {responseJson}");
                }

                var parsed = JsonSerializer.Deserialize<GigaChatTokenResponse>(responseJson);

                if (string.IsNullOrWhiteSpace(parsed?.access_token))
                    throw new InvalidOperationException("GigaChat не вернул access_token.");

                _accessToken = parsed.access_token;

                // expires_at у GigaChat приходит как Unix timestamp в миллисекундах.
                if (parsed.expires_at > 0)
                {
                    _accessTokenExpiresAt = DateTimeOffset.FromUnixTimeMilliseconds(parsed.expires_at);
                }
                else
                {
                    _accessTokenExpiresAt = DateTimeOffset.Now.AddMinutes(25);
                }

                return _accessToken;
            }
            finally
            {
                // Выходим из турникета — ВСЕГДА, даже если выбросилось исключение.
                // Без этого турникет навсегда заблокируется и приложение зависнет.
                _tokenLock.Release();
            }
        }

        private sealed class GigaChatTokenResponse
        {
            public string? access_token { get; set; }
            public long expires_at { get; set; }
        }

        private sealed class GigaChatChatResponse
        {
            public GigaChatChoice[]? choices { get; set; }
        }

        private sealed class GigaChatChoice
        {
            public GigaChatMessage? message { get; set; }
        }

        private sealed class GigaChatMessage
        {
            public string? role { get; set; }
            public string? content { get; set; }
        }
    }
}