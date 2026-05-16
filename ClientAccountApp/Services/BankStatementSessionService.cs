// Сервис сохранения / загрузки сессий Мастера выписки.
// Каждая сессия — JSON-файл в папке Documents\ClientAccountApp\Sessions\.
// Хранит все операции с НДС-разметкой, реквизиты организации, метаданные.

using ClientAccountApp.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClientAccountApp.Services
{
    // ─── DTO ─────────────────────────────────────────────────────────────────

    public class BankStatementSession
    {
        public string Id          { get; set; } = Guid.NewGuid().ToString("N");
        public string Name        { get; set; } = "";   // Для отображения в списке
        public DateTime SavedAt   { get; set; } = DateTime.Now;
        public string SourceFilePath { get; set; } = "";

        // Реквизиты организации (из Шага 3)
        public string OrgInn  { get; set; } = "";
        public string OrgKpp  { get; set; } = "";
        public string OrgName { get; set; } = "";

        // Данные выписки
        public SessionStatement Statement { get; set; } = new();
    }

    public class SessionStatement
    {
        public DateTime DateFrom      { get; set; }
        public DateTime DateTo        { get; set; }
        public string AccountNumber   { get; set; } = "";
        public string BankName        { get; set; } = "";
        public string OwnerINN        { get; set; } = "";
        public string OwnerKPP        { get; set; } = "";
        public string OwnerName       { get; set; } = "";
        public decimal TotalCredit    { get; set; }
        public decimal TotalDebit     { get; set; }
        public List<SessionOperation> Operations { get; set; } = new();
    }

    public class SessionOperation
    {
        public int      LocalIndex       { get; set; }
        public DateTime Date             { get; set; }
        public string   DocNumber        { get; set; } = "";
        public int      OperationType    { get; set; } // 0=Credit, 1=Debit
        public decimal  Amount           { get; set; }

        public string PayerAccount  { get; set; } = "";
        public string PayerName     { get; set; } = "";
        public string PayerINN      { get; set; } = "";
        public string PayerKPP      { get; set; } = "";
        public string PayerBank     { get; set; } = "";
        public string PayerBIC      { get; set; } = "";

        public string ReceiverAccount { get; set; } = "";
        public string ReceiverName    { get; set; } = "";
        public string ReceiverINN     { get; set; } = "";
        public string ReceiverKPP     { get; set; } = "";
        public string ReceiverBank    { get; set; } = "";
        public string ReceiverBIC     { get; set; } = "";

        public string PaymentPurpose  { get; set; } = "";

        // НДС-разметка
        public bool      IsMarkedForVatBook { get; set; }
        public string    VatBookType        { get; set; } = "";
        public string    VatInvoiceNumber   { get; set; } = "";
        public DateTime? VatInvoiceDate     { get; set; }
        public decimal   VatRate            { get; set; } = 20m;
        public decimal   VatAmount          { get; set; }
    }

    // ─── Краткая карточка для отображения в списке (без операций) ────────────

    public class SessionCard
    {
        public string   Id           { get; set; } = "";
        public string   Name         { get; set; } = "";
        public DateTime SavedAt      { get; set; }
        public string   FilePath     { get; set; } = "";
        public int      TotalOps     { get; set; }
        public int      MarkedOps    { get; set; }
        public string   OrgName      { get; set; } = "";
        public string   AccountShort { get; set; } = "";
        public string   Period       { get; set; } = "";

        public string SavedAtDisplay  => SavedAt.ToString("dd.MM.yyyy HH:mm");
        public string MarkedDisplay   => $"{MarkedOps} / {TotalOps} размечено";
    }

    // ─── Сервис ───────────────────────────────────────────────────────────────

    public static class BankStatementSessionService
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public static string SessionsFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                         "ClientAccountApp", "Sessions");

        // ── Сохранить сессию ──────────────────────────────────────────────────

        public static string Save(
            BankStatement stmt,
            List<BankStatementOperation> ops,
            string sourceFilePath,
            string orgInn, string orgKpp, string orgName,
            string? existingId = null)
        {
            Directory.CreateDirectory(SessionsFolder);

            string id = existingId ?? Guid.NewGuid().ToString("N");

            var session = new BankStatementSession
            {
                Id             = id,
                Name           = BuildName(stmt),
                SavedAt        = DateTime.Now,
                SourceFilePath = sourceFilePath,
                OrgInn         = orgInn,
                OrgKpp         = orgKpp,
                OrgName        = orgName,
                Statement      = ToSessionStatement(stmt, ops),
            };

            string path = Path.Combine(SessionsFolder, $"session_{id}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(session, JsonOpts));
            return path;
        }

        // ── Загрузить сессию ──────────────────────────────────────────────────

        public static (BankStatement stmt, List<BankStatementOperation> ops,
                        string orgInn, string orgKpp, string orgName, string id)
            Load(string filePath)
        {
            var json    = File.ReadAllText(filePath);
            var session = JsonSerializer.Deserialize<BankStatementSession>(json, JsonOpts)
                          ?? throw new InvalidDataException("Повреждённый файл сессии.");

            var stmt = FromSessionStatement(session.Statement);
            var ops  = session.Statement.Operations.Select(FromSessionOp).ToList();

            // Переиндексируем
            for (int i = 0; i < ops.Count; i++) ops[i].LocalIndex = i + 1;
            stmt.Operations = ops;

            return (stmt, ops,
                    session.OrgInn, session.OrgKpp, session.OrgName,
                    session.Id);
        }

        // ── Список сохранённых сессий (от новых к старым) ─────────────────────

        public static List<SessionCard> GetAll()
        {
            if (!Directory.Exists(SessionsFolder))
                return new List<SessionCard>();

            var result = new List<SessionCard>();

            foreach (var f in Directory.GetFiles(SessionsFolder, "session_*.json")
                                       .OrderByDescending(File.GetLastWriteTime))
            {
                try
                {
                    var json    = File.ReadAllText(f);
                    var session = JsonSerializer.Deserialize<BankStatementSession>(json, JsonOpts);
                    if (session is null) continue;

                    int marked = session.Statement.Operations.Count(o => o.IsMarkedForVatBook);
                    string acc = session.Statement.AccountNumber;
                    string accShort = acc.Length > 8
                        ? "…" + acc[^8..] : acc;

                    result.Add(new SessionCard
                    {
                        Id           = session.Id,
                        Name         = session.Name,
                        SavedAt      = session.SavedAt,
                        FilePath     = f,
                        TotalOps     = session.Statement.Operations.Count,
                        MarkedOps    = marked,
                        OrgName      = session.OrgName,
                        AccountShort = accShort,
                        Period       = $"{session.Statement.DateFrom:dd.MM.yy} – {session.Statement.DateTo:dd.MM.yy}",
                    });
                }
                catch { /* пропускаем повреждённые файлы */ }
            }

            return result;
        }

        // ── Удалить сессию ────────────────────────────────────────────────────

        public static void Delete(string filePath)
        {
            try { if (File.Exists(filePath)) File.Delete(filePath); } catch (Exception _exDel) { AppLogger.LogWarning("FileDelete", _exDel.Message); }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string BuildName(BankStatement stmt)
        {
            string acc = stmt.AccountNumber.Length > 8
                ? "р/с …" + stmt.AccountNumber[^8..] : stmt.AccountNumber;
            return $"{stmt.DateFrom:dd.MM.yyyy} – {stmt.DateTo:dd.MM.yyyy} | {acc}";
        }

        private static SessionStatement ToSessionStatement(
            BankStatement stmt, List<BankStatementOperation> ops) => new()
        {
            DateFrom      = stmt.DateFrom,
            DateTo        = stmt.DateTo,
            AccountNumber = stmt.AccountNumber,
            BankName      = stmt.BankName,
            OwnerINN      = stmt.OwnerINN,
            OwnerKPP      = stmt.OwnerKPP,
            OwnerName     = stmt.OwnerName,
            TotalCredit   = stmt.TotalCredit,
            TotalDebit    = stmt.TotalDebit,
            Operations    = ops.Select(o => new SessionOperation
            {
                LocalIndex         = o.LocalIndex,
                Date               = o.Date,
                DocNumber          = o.DocNumber,
                OperationType      = (int)o.OperationType,
                Amount             = o.Amount,
                PayerAccount       = o.PayerAccount,
                PayerName          = o.PayerName,
                PayerINN           = o.PayerINN,
                PayerKPP           = o.PayerKPP,
                PayerBank          = o.PayerBank,
                PayerBIC           = o.PayerBIC,
                ReceiverAccount    = o.ReceiverAccount,
                ReceiverName       = o.ReceiverName,
                ReceiverINN        = o.ReceiverINN,
                ReceiverKPP        = o.ReceiverKPP,
                ReceiverBank       = o.ReceiverBank,
                ReceiverBIC        = o.ReceiverBIC,
                PaymentPurpose     = o.PaymentPurpose,
                IsMarkedForVatBook = o.IsMarkedForVatBook,
                VatBookType        = o.VatBookType,
                VatInvoiceNumber   = o.VatInvoiceNumber,
                VatInvoiceDate     = o.VatInvoiceDate,
                VatRate            = o.VatRate,
                VatAmount          = o.VatAmount,
            }).ToList(),
        };

        private static BankStatement FromSessionStatement(SessionStatement s) => new()
        {
            DateFrom      = s.DateFrom,
            DateTo        = s.DateTo,
            AccountNumber = s.AccountNumber,
            BankName      = s.BankName,
            OwnerINN      = s.OwnerINN,
            OwnerKPP      = s.OwnerKPP,
            OwnerName     = s.OwnerName,
            TotalCredit   = s.TotalCredit,
            TotalDebit    = s.TotalDebit,
        };

        private static BankStatementOperation FromSessionOp(SessionOperation o) => new()
        {
            LocalIndex         = o.LocalIndex,
            Date               = o.Date,
            DocNumber          = o.DocNumber,
            OperationType      = (BankOperationType)o.OperationType,
            Amount             = o.Amount,
            PayerAccount       = o.PayerAccount,
            PayerName          = o.PayerName,
            PayerINN           = o.PayerINN,
            PayerKPP           = o.PayerKPP,
            PayerBank          = o.PayerBank,
            PayerBIC           = o.PayerBIC,
            ReceiverAccount    = o.ReceiverAccount,
            ReceiverName       = o.ReceiverName,
            ReceiverINN        = o.ReceiverINN,
            ReceiverKPP        = o.ReceiverKPP,
            ReceiverBank       = o.ReceiverBank,
            ReceiverBIC        = o.ReceiverBIC,
            PaymentPurpose     = o.PaymentPurpose,
            IsMarkedForVatBook = o.IsMarkedForVatBook,
            VatBookType        = o.VatBookType,
            VatInvoiceNumber   = o.VatInvoiceNumber,
            VatInvoiceDate     = o.VatInvoiceDate,
            VatRate            = o.VatRate,
            VatAmount          = o.VatAmount,
        };
    }
}
