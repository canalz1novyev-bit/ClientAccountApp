// Сохранение / загрузка сессий Мастера РСВ в JSON.
// Аналог BankStatementSessionService, но для РСВ-данных.

using ClientAccountApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClientAccountApp.Services
{
    // ── DTO для JSON ──────────────────────────────────────────────────────────

    public class RsvSession
    {
        public string   Id        { get; set; } = Guid.NewGuid().ToString("N");
        public string   Name      { get; set; } = "";
        public DateTime SavedAt   { get; set; } = DateTime.Now;

        public RsvSessionSettings Settings  { get; set; } = new();
        public List<RsvSessionEmployee> Employees { get; set; } = new();
    }

    public class RsvSessionSettings
    {
        public string OrgINN      { get; set; } = "";
        public string OrgKPP      { get; set; } = "";
        public string OrgName     { get; set; } = "";
        public string Phone       { get; set; } = "";
        public int    AvgHeadcount{ get; set; } = 1;
        public string DirectorSurname    { get; set; } = "";
        public string DirectorName       { get; set; } = "";
        public string DirectorPatronymic { get; set; } = "";
        public int    Year        { get; set; } = DateTime.Now.Year;
        public int    Quarter     { get; set; } = 1;
        public string KodNO       { get; set; } = "";
        public string NrTariffCode{ get; set; } = "01";
        public decimal NrRate     { get; set; } = 0.30m;
        public string OKTMO       { get; set; } = "";
        public string KBK         { get; set; } = "18210201000011000160";
        public bool   AutoSplitPv  { get; set; } = true;
        public decimal MROTPerMonth{ get; set; } = 27093m;
    }

    public class RsvSessionEmployee
    {
        public string SNILS      { get; set; } = "";
        public string INN        { get; set; } = "";
        public string Surname    { get; set; } = "";
        public string Name       { get; set; } = "";
        public string Patronymic { get; set; } = "";
        public string CategoryCodeNR { get; set; } = "НР";
        public string BirthDate  { get; set; } = "";
        public string Gender     { get; set; } = "";
        public string DocSeries  { get; set; } = "";

        // Выплаты НР
        public decimal PayNR1 { get; set; }
        public decimal PayNR2 { get; set; }
        public decimal PayNR3 { get; set; }

        // Выплаты ПВ
        public decimal PayPV1 { get; set; }
        public decimal PayPV2 { get; set; }
        public decimal PayPV3 { get; set; }

        // Необлагаемые
        public decimal ExemptNR1 { get; set; }
        public decimal ExemptNR2 { get; set; }
        public decimal ExemptNR3 { get; set; }
    }

    // ── Карточка для списка ───────────────────────────────────────────────────

    public class RsvSessionCard
    {
        public string   Id           { get; set; } = "";
        public string   Name         { get; set; } = "";
        public DateTime SavedAt      { get; set; }
        public string   FilePath     { get; set; } = "";
        public string   OrgName      { get; set; } = "";
        public int      EmpCount     { get; set; }
        public int       Quarter     { get; set; }
        public int       Year        { get; set; }

        public string SavedAtDisplay => SavedAt.ToString("dd.MM.yyyy HH:mm");
        public string PeriodDisplay  => $"Q{Quarter}/{Year}";
    }

    // ── Сервис ────────────────────────────────────────────────────────────────

    public static class RsvSessionService
    {
        private static readonly JsonSerializerOptions Opts = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public static string SessionsFolder =>
            Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments),
                "ClientAccountApp", "РСВ_Сессии");

        // ── Сохранить ─────────────────────────────────────────────────────────

        public static string Save(
            List<RsvEmployee> employees,
            RsvSettings s,
            string? existingId = null)
        {
            Directory.CreateDirectory(SessionsFolder);

            string id = existingId ?? Guid.NewGuid().ToString("N");
            var session = new RsvSession
            {
                Id      = id,
                Name    = $"{s.OrgName} | Q{s.Quarter}/{s.Year}",
                SavedAt = DateTime.Now,
                Settings = new RsvSessionSettings
                {
                    OrgINN      = s.OrgINN,
                    OrgKPP      = s.OrgKPP,
                    OrgName     = s.OrgName,
                    Phone       = s.Phone,
                    AvgHeadcount= s.AvgHeadcount,
                    DirectorSurname    = s.DirectorSurname,
                    DirectorName       = s.DirectorName,
                    DirectorPatronymic = s.DirectorPatronymic,
                    Year        = s.Year,
                    Quarter     = s.Quarter,
                    KodNO       = s.KodNO,
                    NrTariffCode= s.NrTariffCode,
                    NrRate      = s.NrRate,
                    OKTMO       = s.OKTMO,
                    KBK         = s.KBK,
                    AutoSplitPv  = s.AutoSplitPv,
                    MROTPerMonth = s.MROTPerMonth,
                },
                Employees = employees.Select(e => new RsvSessionEmployee
                {
                    SNILS       = e.SNILS,  INN = e.INN,
                    Surname     = e.Surname, Name = e.Name,
                    Patronymic  = e.Patronymic,
                    CategoryCodeNR = e.CategoryCodeNR,
                    BirthDate   = e.BirthDate, Gender = e.Gender,
                    DocSeries   = e.DocSeries,
                    PayNR1 = e.PayNR1, PayNR2 = e.PayNR2, PayNR3 = e.PayNR3,
                    PayPV1 = e.PayPV1, PayPV2 = e.PayPV2, PayPV3 = e.PayPV3,
                    ExemptNR1 = e.ExemptNR1, ExemptNR2 = e.ExemptNR2, ExemptNR3 = e.ExemptNR3,
                }).ToList(),
            };

            string path = Path.Combine(SessionsFolder, $"rsv_session_{id}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(session, Opts));
            return id;
        }

        // ── Загрузить ─────────────────────────────────────────────────────────

        public static (List<RsvEmployee> employees, RsvSettings settings, string id)
            Load(string filePath)
        {
            var json    = File.ReadAllText(filePath);
            var session = JsonSerializer.Deserialize<RsvSession>(json, Opts)
                          ?? throw new InvalidDataException("Повреждённый файл сессии.");

            var employees = session.Employees.Select(e => new RsvEmployee
            {
                SNILS = e.SNILS, INN = e.INN,
                Surname = e.Surname, Name = e.Name, Patronymic = e.Patronymic,
                CategoryCodeNR = e.CategoryCodeNR,
                BirthDate = e.BirthDate, Gender = e.Gender, DocSeries = e.DocSeries,
                PayNR1 = e.PayNR1, PayNR2 = e.PayNR2, PayNR3 = e.PayNR3,
                PayPV1 = e.PayPV1, PayPV2 = e.PayPV2, PayPV3 = e.PayPV3,
                ExemptNR1 = e.ExemptNR1, ExemptNR2 = e.ExemptNR2, ExemptNR3 = e.ExemptNR3,
            }).ToList();

            var ss = session.Settings;
            var settings = new RsvSettings
            {
                OrgINN      = ss.OrgINN, OrgKPP = ss.OrgKPP, OrgName = ss.OrgName,
                Phone       = ss.Phone,  AvgHeadcount = ss.AvgHeadcount,
                DirectorSurname    = ss.DirectorSurname,
                DirectorName       = ss.DirectorName,
                DirectorPatronymic = ss.DirectorPatronymic,
                Year        = ss.Year,  Quarter = ss.Quarter, KodNO = ss.KodNO,
                NrTariffCode= ss.NrTariffCode, NrRate = ss.NrRate,
                OKTMO       = ss.OKTMO, KBK = ss.KBK,
                AutoSplitPv  = ss.AutoSplitPv,
                MROTPerMonth = ss.MROTPerMonth,
            };

            return (employees, settings, session.Id);
        }

        // ── Список всех сессий ────────────────────────────────────────────────

        public static List<RsvSessionCard> GetAll()
        {
            if (!Directory.Exists(SessionsFolder)) return new();
            var result = new List<RsvSessionCard>();
            foreach (var f in Directory.GetFiles(SessionsFolder, "rsv_session_*.json")
                                       .OrderByDescending(File.GetLastWriteTime))
            {
                try
                {
                    var s = JsonSerializer.Deserialize<RsvSession>(
                        File.ReadAllText(f), Opts);
                    if (s == null) continue;
                    result.Add(new RsvSessionCard
                    {
                        Id       = s.Id, Name = s.Name, SavedAt = s.SavedAt,
                        FilePath = f,
                        OrgName  = s.Settings.OrgName,
                        EmpCount = s.Employees.Count,
                        Quarter  = s.Settings.Quarter,
                        Year     = s.Settings.Year,
                    });
                }
                catch { }
            }
            return result;
        }

        // ── Удалить ───────────────────────────────────────────────────────────

        public static void Delete(string filePath)
        {
            try { if (File.Exists(filePath)) File.Delete(filePath); } catch { }
        }
    }
}
