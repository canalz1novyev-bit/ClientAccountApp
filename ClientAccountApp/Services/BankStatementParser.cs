// Добавить в папку Services/
// Формат 1CClientBankExchange (1С_PRC_8003) — универсальный обменный формат
// Поддерживается: Сбербанк, ВТБ, Тинькофф, Альфа-Банк, Точка и другие.

using ClientAccountApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ClientAccountApp.Services
{
    public static class BankStatementParser
    {
        public static BankStatement Parse(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Файл выписки не найден.", filePath);

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Определяем кодировку из первых байт файла
            byte[] peek = new byte[512];
            using (var fs = File.OpenRead(filePath))
                fs.Read(peek, 0, peek.Length);

            string peekStr = Encoding.UTF8.GetString(peek);
            bool isUtf8 = peekStr.Contains("Кодировка=UTF-8", StringComparison.OrdinalIgnoreCase)
                       || peekStr.Contains("Кодировка=utf-8", StringComparison.OrdinalIgnoreCase);

            var enc = isUtf8 ? Encoding.UTF8 : Encoding.GetEncoding(1251);
            string content = File.ReadAllText(filePath, enc);

            return ParseContent(content);
        }

        // ─────────────────────────────────────────────────────────────────────
        private static BankStatement ParseContent(string content)
        {
            var stmt = new BankStatement();
            var ops = new List<BankStatementOperation>();

            bool inAccount = false, inDoc = false;
            var f = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int opIdx = 0;

            foreach (var rawLine in content.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;

                // ── Маркеры секций ───────────────────────────────────────────
                if (line == "СекцияРасчСчет") { inAccount = true; continue; }
                if (line == "КонецРасчСчет") { inAccount = false; continue; }

                if (line.StartsWith("СекцияДокумент"))
                {
                    inDoc = true;
                    f.Clear();
                    int eq = line.IndexOf('=');
                    if (eq >= 0) f["_Тип"] = line[(eq + 1)..].Trim();
                    continue;
                }
                if (line == "КонецДокумента")
                {
                    inDoc = false;
                    var op = BuildOperation(f, opIdx++);
                    if (op is not null) ops.Add(op);
                    f.Clear();
                    continue;
                }

                // ── Key=Value ────────────────────────────────────────────────
                int eqIdx = line.IndexOf('=');
                if (eqIdx < 0) continue;
                var key = line[..eqIdx].Trim();
                var val = line[(eqIdx + 1)..].Trim();

                if (inDoc)
                {
                    f[key] = val;
                }
                else if (inAccount)
                {
                    switch (key)
                    {
                        case "ДатаНачала": stmt.DateFrom = ParseDate(val); break;
                        case "ДатаКонца": stmt.DateTo = ParseDate(val); break;
                        case "РасчСчет": stmt.AccountNumber = val; break;
                        case "НачальныйОстаток": stmt.OpeningBalance = ParseMoney(val); break;
                        case "КонечныйОстаток": stmt.ClosingBalance = ParseMoney(val); break;
                        case "ВсегоПоступило": stmt.TotalCredit = ParseMoney(val); break;
                        case "ВсегоСписано": stmt.TotalDebit = ParseMoney(val); break;
                    }
                }
                else
                {
                    // Заголовок файла
                    if (key == "ДатаНачала" && stmt.DateFrom == default)
                        stmt.DateFrom = ParseDate(val);
                    if (key == "ДатаКонца" && stmt.DateTo == default)
                        stmt.DateTo = ParseDate(val);
                    if (key == "РасчСчет" && string.IsNullOrEmpty(stmt.AccountNumber))
                        stmt.AccountNumber = val;
                    if (key == "Отправитель")
                        stmt.BankName = val;
                }
            }

            stmt.Operations = ops;

            // Пересчитываем реальный период по фактическим операциям
            var datedOps = ops.Where(o => o.Date != default).ToList();
            if (datedOps.Count > 0)
            {
                var minDate = datedOps.Min(o => o.Date);
                var maxDate = datedOps.Max(o => o.Date);
                if (stmt.DateFrom == default || stmt.DateFrom > minDate) stmt.DateFrom = minDate;
                if (stmt.DateTo == default || stmt.DateTo < maxDate) stmt.DateTo = maxDate;
            }

            // Определяем реквизиты владельца счёта из операций выписки
            // Дебет → мы плательщик; Кредит → мы получатель
            var ownerDebit = ops.FirstOrDefault(o =>
                o.OperationType == BankOperationType.Debit && !string.IsNullOrEmpty(o.PayerINN));
            if (ownerDebit != null)
            {
                stmt.OwnerINN = ownerDebit.PayerINN;
                stmt.OwnerKPP = ownerDebit.PayerKPP;
                stmt.OwnerName = ownerDebit.PayerName;
            }
            else
            {
                var ownerCredit = ops.FirstOrDefault(o =>
                    o.OperationType == BankOperationType.Credit && !string.IsNullOrEmpty(o.ReceiverINN));
                if (ownerCredit != null)
                {
                    stmt.OwnerINN = ownerCredit.ReceiverINN;
                    stmt.OwnerKPP = ownerCredit.ReceiverKPP;
                    stmt.OwnerName = ownerCredit.ReceiverName;
                }
            }

            return stmt;
        }

        // ─────────────────────────────────────────────────────────────────────
        private static BankStatementOperation? BuildOperation(Dictionary<string, string> f, int idx)
        {
            if (!f.TryGetValue("Сумма", out var amtStr) || string.IsNullOrEmpty(amtStr))
                return null;

            bool hasCredit = f.TryGetValue("ДатаПоступило", out var dCredit) && !string.IsNullOrEmpty(dCredit);
            bool hasDebit = f.TryGetValue("ДатаСписано", out var dDebit) && !string.IsNullOrEmpty(dDebit);

            BankOperationType opType;
            DateTime opDate;

            if (hasCredit && !hasDebit)
            {
                opType = BankOperationType.Credit;
                opDate = ParseDate(dCredit!);
            }
            else if (hasDebit)
            {
                opType = BankOperationType.Debit;
                opDate = ParseDate(dDebit!);
            }
            else
            {
                opType = BankOperationType.Debit;
                opDate = ParseDate(f.GetValueOrDefault("Дата", ""));
            }

            if (opDate == default)
                opDate = ParseDate(f.GetValueOrDefault("Дата", ""));

            return new BankStatementOperation
            {
                LocalIndex = idx + 1,
                Date = opDate,
                DocNumber = f.GetValueOrDefault("Номер", ""),
                OperationType = opType,
                Amount = ParseMoney(amtStr),

                PayerAccount = f.GetValueOrDefault("ПлательщикСчет", ""),
                PayerName = f.GetValueOrDefault("Плательщик", ""),
                PayerINN = f.GetValueOrDefault("ПлательщикИНН", ""),
                PayerKPP = f.GetValueOrDefault("ПлательщикКПП", ""),
                PayerBank = f.GetValueOrDefault("ПлательщикБанк1", ""),
                PayerBIC = f.GetValueOrDefault("ПлательщикБИК", ""),

                ReceiverAccount = f.GetValueOrDefault("ПолучательСчет", ""),
                ReceiverName = f.GetValueOrDefault("Получатель", ""),
                ReceiverINN = f.GetValueOrDefault("ПолучательИНН", ""),
                ReceiverKPP = f.GetValueOrDefault("ПолучательКПП", ""),
                ReceiverBank = f.GetValueOrDefault("ПолучательБанк1", ""),
                ReceiverBIC = f.GetValueOrDefault("ПолучательБИК", ""),

                PaymentPurpose = f.GetValueOrDefault("НазначениеПлатежа", ""),
            };
        }

        // ─── Helpers ─────────────────────────────────────────────────────────
        private static DateTime ParseDate(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return default;
            return DateTime.TryParseExact(s.Trim(), "dd.MM.yyyy",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var d) ? d : default;
        }

        private static decimal ParseMoney(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0m;
            s = s.Trim().Replace(" ", "").Replace("\u00A0", "");
            if (s.Contains(',') && !s.Contains('.')) s = s.Replace(',', '.');
            return decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m;
        }
    }
}