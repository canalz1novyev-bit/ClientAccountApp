using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ClientAccountApp
{
    public static class BankLookupService
    {
        private static readonly HttpClient _httpClient = new();

        public static async Task<BankLookupResult?> FindByBicAsync(string bic)
        {
            bic = new string((bic ?? string.Empty).Where(char.IsDigit).ToArray());

            if (bic.Length != 9)
                return null;

            if (string.IsNullOrWhiteSpace(InnLookupSettings.DadataToken))
                throw new InvalidOperationException("Токен DaData не указан в InnLookupSettings.cs.");

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://suggestions.dadata.ru/suggestions/api/4_1/rs/findById/bank");

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Token", InnLookupSettings.DadataToken);

            var requestJson = JsonSerializer.Serialize(new
            {
                query = bic
            });

            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request);
            string responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Ошибка DaData (банк): {response.StatusCode}. {responseJson}");

            var parsed = JsonSerializer.Deserialize<DadataFindBankResponse>(responseJson);
            var suggestion = parsed?.Suggestions?.FirstOrDefault();

            if (suggestion == null)
                return null;

            return new BankLookupResult
            {
                Bic = suggestion.Data?.Bic ?? bic,
                BankName = suggestion.Data?.Name?.Payment
                           ?? suggestion.Data?.Name?.Short
                           ?? suggestion.Value
                           ?? "",
                CorrespondentAccount = suggestion.Data?.CorrespondentAccount ?? ""
            };
        }

        private sealed class DadataFindBankResponse
        {
            [JsonPropertyName("suggestions")]
            public List<DadataBankSuggestion> Suggestions { get; set; } = new();
        }

        private sealed class DadataBankSuggestion
        {
            [JsonPropertyName("value")]
            public string? Value { get; set; }

            [JsonPropertyName("data")]
            public DadataBankData? Data { get; set; }
        }

        private sealed class DadataBankData
        {
            [JsonPropertyName("bic")]
            public string? Bic { get; set; }

            [JsonPropertyName("correspondent_account")]
            public string? CorrespondentAccount { get; set; }

            [JsonPropertyName("name")]
            public DadataBankName? Name { get; set; }
        }

        private sealed class DadataBankName
        {
            [JsonPropertyName("payment")]
            public string? Payment { get; set; }

            [JsonPropertyName("short")]
            public string? Short { get; set; }
        }
    }
}