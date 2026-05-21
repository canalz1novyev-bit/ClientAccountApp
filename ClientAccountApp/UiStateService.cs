using System;
using System.IO;
using System.Text.Json;
using Windows.Storage;

namespace ClientAccountApp
{
    public sealed class UiState
    {
        public string LastPageKey { get; set; } = "Dashboard";
        public ContractsPageState ContractsPage { get; set; } = new();
        public ClientsPageState ClientsPage { get; set; } = new();
    }

    public sealed class ContractsPageState
    {
        public string SearchText { get; set; } = "";
        public string StatusFilter { get; set; } = "В работе";
        public string SortMode { get; set; } = "Сначала требующие внимания";
    }

    public sealed class ClientsPageState
    {
        public string SearchText { get; set; } = "";
        public string ViewMode { get; set; } = "Табличный";
        public string SignatureFilter { get; set; } = "Все клиенты";
        public string StatusFilter { get; set; } = "Все статусы";
        public int? SelectedClientId { get; set; }
    }

    public static class UiStateService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private static UiState? _cache;

        private static string FilePath =>
            Path.Combine(AppPaths.AppDataFolder, "ui-state.json");

        public static UiState Load()
        {
            if (_cache != null)
                return _cache;

            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    _cache = JsonSerializer.Deserialize<UiState>(json, JsonOptions) ?? new UiState();
                }
                else
                {
                    _cache = new UiState();
                }
            }
            catch
            {
                _cache = new UiState();
            }

            return _cache;
        }

        public static void Save(Action<UiState> updateAction)
        {
            var state = Load();
            updateAction(state);

            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(state, JsonOptions));
        }
    }
}