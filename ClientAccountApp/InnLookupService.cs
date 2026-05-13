using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace ClientAccountApp
{
    public static class InnLookupService
    {
        private static readonly HttpClient _httpClient = new();

        public static async Task<InnLookupResult> FindByInnAsync(string inn, string currentClientType)
        {
            if (string.IsNullOrWhiteSpace(InnLookupSettings.DadataToken) ||
    InnLookupSettings.DadataToken == "PASTE_REAL_DADATA_TOKEN_HERE")
            {
                throw new InvalidOperationException("Токен DaData не указан в файле InnLookupSettings.cs.");
            }

            string searchType = IsEntrepreneurType(currentClientType) ? "INDIVIDUAL" : "LEGAL";

            var requestBody = new DadataFindPartyRequest
            {
                Query = inn,
                Type = searchType
            };

            var requestJson = JsonSerializer.Serialize(requestBody);

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://suggestions.dadata.ru/suggestions/api/4_1/rs/findById/party");

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Token", InnLookupSettings.DadataToken);
            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            string responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Ошибка DaData: {response.StatusCode}. {responseJson}");
            }

            var parsed = JsonSerializer.Deserialize<DadataFindPartyResponse>(responseJson);

            var suggestion = parsed?.Suggestions?.FirstOrDefault();
            if (suggestion == null)
            {
                throw new InvalidOperationException("По этому ИНН ничего не найдено.");
            }

            return MapSuggestionToResult(suggestion, currentClientType);
        }

        private static InnLookupResult MapSuggestionToResult(DadataPartySuggestion suggestion, string currentClientType)
        {
            var data = suggestion.Data ?? new DadataPartyData();

            bool isIndividual = string.Equals(data.Type, "INDIVIDUAL", StringComparison.OrdinalIgnoreCase);

            string clientType = currentClientType;
            if (isIndividual)
            {
                clientType = data.Opf?.Short?.Contains("КФХ", StringComparison.OrdinalIgnoreCase) == true
                    ? "ИПГКФХ"
                    : "ИП";
            }
            else
            {
                if (string.Equals(data.Opf?.Short, "АНО", StringComparison.OrdinalIgnoreCase))
                {
                    clientType = "АНО";
                }
                else if (string.Equals(data.Opf?.Short, "ООО", StringComparison.OrdinalIgnoreCase))
                {
                    clientType = "ООО";
                }
            }

            string entrepreneurFio = BuildFio(data.Fio);

            string clientName = isIndividual
                ? (!string.IsNullOrWhiteSpace(entrepreneurFio)
                    ? $"ИП {entrepreneurFio}"
                    : (suggestion.Value ?? ""))
                : (data.Name?.ShortWithOpf ?? suggestion.Value ?? "");

            string directorName = isIndividual
                ? entrepreneurFio
                : (data.Management?.Name ?? "");

            string address = data.Address?.UnrestrictedValue
                             ?? data.Address?.Value
                             ?? "";

            string mainOkved = data.Okved ?? string.Empty;
            string businessCategory = OkvedBusinessCategoryService.DetectCategory(mainOkved);

            return new InnLookupResult
            {
                ClientType = clientType,
                ClientName = clientName,
                DirectorName = directorName,
                Inn = data.Inn ?? string.Empty,
                Ogrn = data.Ogrn ?? string.Empty,
                LegalAddress = address,
                MainOkved = mainOkved,
                BusinessCategory = businessCategory
            };
        }

        private static string BuildFio(DadataFio? fio)
        {
            if (fio == null)
            {
                return "";
            }

            var parts = new[] { fio.Surname, fio.Name, fio.Patronymic }
                .Where(x => !string.IsNullOrWhiteSpace(x));

            return string.Join(" ", parts);
        }

        private static bool IsEntrepreneurType(string clientType)
        {
            return clientType == "ИП" || clientType == "ИПГКФХ";
        }

        private sealed class DadataFindPartyRequest
        {
            [JsonPropertyName("query")]
            public string Query { get; set; } = "";

            [JsonPropertyName("type")]
            public string Type { get; set; } = "";
        }

        private sealed class DadataFindPartyResponse
        {
            [JsonPropertyName("suggestions")]
            public List<DadataPartySuggestion> Suggestions { get; set; } = new();
        }

        private sealed class DadataPartySuggestion
        {
            [JsonPropertyName("value")]
            public string? Value { get; set; }

            [JsonPropertyName("data")]
            public DadataPartyData? Data { get; set; }
        }

        private sealed class DadataPartyData
        {
            [JsonPropertyName("type")]
            public string? Type { get; set; }
            [JsonPropertyName("inn")]
            public string? Inn { get; set; }

            [JsonPropertyName("ogrn")]
            public string? Ogrn { get; set; }
            [JsonPropertyName("okved")]
            public string? Okved { get; set; }

            [JsonPropertyName("name")]
            public DadataPartyName? Name { get; set; }

            [JsonPropertyName("opf")]
            public DadataOpf? Opf { get; set; }

            [JsonPropertyName("management")]
            public DadataManagement? Management { get; set; }

            [JsonPropertyName("fio")]
            public DadataFio? Fio { get; set; }

            [JsonPropertyName("address")]
            public DadataAddress? Address { get; set; }
        }

        private sealed class DadataPartyName
        {
            [JsonPropertyName("short_with_opf")]
            public string? ShortWithOpf { get; set; }
        }

        private sealed class DadataOpf
        {
            [JsonPropertyName("short")]
            public string? Short { get; set; }
        }

        private sealed class DadataManagement
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }
        }

        private sealed class DadataFio
        {
            [JsonPropertyName("surname")]
            public string? Surname { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("patronymic")]
            public string? Patronymic { get; set; }
        }

        private sealed class DadataAddress
        {
            [JsonPropertyName("value")]
            public string? Value { get; set; }

            [JsonPropertyName("unrestricted_value")]
            public string? UnrestrictedValue { get; set; }
        }
    }
}