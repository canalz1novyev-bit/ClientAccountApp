// Добавить в папку Services/
// Генерирует КУДиР в формате SpreadsheetML (.xls) по официальной форме КНД 1110385.
// Два листа: титульный и Раздел I с поквартальной разбивкой и Справкой.

using ClientAccountApp.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using static ClientAccountApp.Services.BankStatementUsnXmlExporter;

namespace ClientAccountApp.Services
{
    public static class BankStatementUsnExcelExporter
    {
        public static string ExportKudirToExcel(
            IEnumerable<BankStatementOperation> ops,
            OrgInfo org, string accountNumber,
            int year, int quarter, UsnType usnType, string outputFolder)
        {
            bool isIP = org.INN.Length == 12;
            bool inclExpenses = usnType == UsnType.IncomeMinus15;
            string usnLabel = inclExpenses
                ? "доходы, уменьшенные на величину расходов"
                : "доходы";
            int cols = inclExpenses ? 5 : 4;

            var rows = ops
                .Where(o => o.IsMarkedForVatBook &&
                    (o.VatBookType == "Продажи" ||
                     (inclExpenses && o.VatBookType == "Покупки")))
                .OrderBy(o => o.Date).ToList();

            // Группируем по кварталам
            var byQ = Enumerable.Range(1, 4).ToDictionary(
                q => q,
                q => rows.Where(o => (o.Date.Month - 1) / 3 + 1 == q).ToList());

            decimal[] qD = new decimal[5], qR = new decimal[5];
            for (int q = 1; q <= 4; q++)
            {
                qD[q] = byQ[q].Where(o => o.VatBookType == "Продажи").Sum(o => o.Amount);
                qR[q] = inclExpenses ? byQ[q].Where(o => o.VatBookType == "Покупки").Sum(o => o.Amount) : 0m;
            }
            decimal totalD = qD[1] + qD[2] + qD[3] + qD[4];
            decimal totalR = qR[1] + qR[2] + qR[3] + qR[4];

            Directory.CreateDirectory(outputFolder);
            string file = $"КУДИР_{org.INN}_{year}_Q{quarter}_{DateTime.Now:HHmmss}.xls";
            string path = Path.Combine(outputFolder, file);

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
            sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
            sb.AppendLine("  xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\"");
            sb.AppendLine("  xmlns:x=\"urn:schemas-microsoft-com:office:excel\">");

            // ── Стили ───────────────────────────────────────────────────────
            sb.AppendLine("<Styles>");
            S(sb, "Ref", sz: 8, al: "Right");
            S(sb, "TitBig", sz: 14, bold: true, al: "Center", wrap: true);
            S(sb, "TitSub", sz: 10, al: "Center", wrap: true);
            S(sb, "Lbl", sz: 10);
            S(sb, "LblVal", sz: 10, bold: true);
            S(sb, "SecHdr", sz: 11, bold: true, al: "Center", bg: "#FFFFFF");
            S(sb, "Hdr1", sz: 9, bold: true, al: "Center", wrap: true, bg: "#BDD7EE");
            S(sb, "Hdr2", sz: 9, bold: true, al: "Center", wrap: true, bg: "#DDEBF7");
            S(sb, "HdrNum", sz: 9, bold: true, al: "Center", bg: "#DDEBF7");
            S(sb, "D", sz: 9);
            S(sb, "DN", sz: 9, numFmt: "#,##0.00");
            S(sb, "Alt", sz: 9, bg: "#EEF5FB");
            S(sb, "AltN", sz: 9, bg: "#EEF5FB", numFmt: "#,##0.00");
            S(sb, "Tot", sz: 9, bold: true, bg: "#BDD7EE");
            S(sb, "TotN", sz: 9, bold: true, bg: "#BDD7EE", numFmt: "#,##0.00");
            S(sb, "Cum", sz: 9, bold: true, bg: "#9DC3E6");
            S(sb, "CumN", sz: 9, bold: true, bg: "#9DC3E6", numFmt: "#,##0.00");
            S(sb, "Ref0", sz: 9);
            S(sb, "Ref0N", sz: 9, numFmt: "#,##0.00");
            sb.AppendLine("</Styles>");

            // ══ ЛИС 1: ТИТУЛЬНЫЙ ════════════════════════════════════════════
            sb.AppendLine("<Worksheet ss:Name=\"стр.1\">");
            sb.AppendLine("<Table>");
            sb.AppendLine("  <Column ss:Width=\"20\"/>");
            sb.AppendLine("  <Column ss:Width=\"260\"/>");
            sb.AppendLine("  <Column ss:Width=\"130\"/>");
            sb.AppendLine("  <Column ss:Width=\"100\"/>");

            R(sb, () => C(sb, "Ref", "String", "к приказу ФНС России от 07.11.2023 № ЕА-7-3/816@", mg: 3));
            E(sb);
            R(sb, 24, () => C(sb, "TitBig", "String", "КНИГА", mg: 3));
            R(sb, 44, () => C(sb, "TitSub", "String",
                "учёта доходов и расходов организаций и индивидуальных предпринимателей,\n" +
                "применяющих упрощённую систему налогообложения", mg: 3));
            E(sb);
            R(sb, () => { C(sb, "Lbl", "String", $"на {year} год"); C(sb, "Lbl", "String", "Форма по ОКУД"); C(sb, "LblVal", "String", "0301213"); });
            E(sb);
            R(sb, () => C(sb, "Lbl", "String", "Налогоплательщик (наименование организации / ФИО предпринимателя):", mg: 1));
            R(sb, () => { C(sb, "Lbl", "String", ""); C(sb, "LblVal", "String", X(org.Name), mg: 2); });
            E(sb);
            if (!isIP)
            {
                R(sb, () => { C(sb, "Lbl", "String", "ИНН/КПП (для организации):", mg: 1); C(sb, "LblVal", "String", $"{org.INN} / {org.KPP}", mg: 1); });
                R(sb, () => C(sb, "Lbl", "String", "ИНН (для индивидуального предпринимателя):", mg: 3));
            }
            else
            {
                R(sb, () => C(sb, "Lbl", "String", "ИНН/КПП (для организации):", mg: 3));
                R(sb, () => { C(sb, "Lbl", "String", "ИНН (для индивидуального предпринимателя):", mg: 1); C(sb, "LblVal", "String", org.INN, mg: 1); });
            }
            E(sb);
            R(sb, () => { C(sb, "Lbl", "String", "Объект налогообложения:", mg: 1); C(sb, "LblVal", "String", usnLabel, mg: 1); });
            R(sb, () => C(sb, "Lbl", "String", "(в соответствии со статьёй 346.14 Налогового кодекса Российской Федерации)", mg: 3));
            E(sb);
            R(sb, () => { C(sb, "Lbl", "String", "Номера расчётных и иных счетов:", mg: 1); C(sb, "LblVal", "String", X(accountNumber), mg: 1); });
            sb.AppendLine("</Table></Worksheet>");

            // ══ ЛИСТ 2: РАЗДЕЛ I ════════════════════════════════════════════
            sb.AppendLine("<Worksheet ss:Name=\"стр.2_3\">");
            sb.AppendLine("<Table>");
            sb.AppendLine("  <Column ss:Width=\"36\"/>");
            sb.AppendLine("  <Column ss:Width=\"110\"/>");
            sb.AppendLine("  <Column ss:Width=\"300\"/>");
            sb.AppendLine("  <Column ss:Width=\"130\"/>");
            if (inclExpenses) sb.AppendLine("  <Column ss:Width=\"130\"/>");

            // Заголовок раздела
            R(sb, 20, () => C(sb, "SecHdr", "String", "I. Доходы и расходы", mg: cols - 1));
            E(sb);

            void WriteQSection(int q)
            {
                // Двухуровневые заголовки
                R(sb, 18, () =>
                {
                    C(sb, "Hdr1", "String", "Регистрация", mg: 2);
                    C(sb, "Hdr1", "String", "Сумма", mg: inclExpenses ? 1 : 0);
                });
                R(sb, 52, () =>
                {
                    C(sb, "Hdr2", "String", "№\nп/п");
                    C(sb, "Hdr2", "String", "Дата и номер\nпервичного документа");
                    C(sb, "Hdr2", "String", "Содержание операции");
                    C(sb, "Hdr2", "String", "Доходы, учитываемые\nпри исчислении\nналоговой базы\n(рублей)");
                    if (inclExpenses)
                        C(sb, "Hdr2", "String", "Расходы, учитываемые\nпри исчислении\nналоговой базы\n(рублей)");
                });
                R(sb, 14, () =>
                {
                    C(sb, "HdrNum", "Number", "1"); C(sb, "HdrNum", "Number", "2");
                    C(sb, "HdrNum", "Number", "3"); C(sb, "HdrNum", "Number", "4");
                    if (inclExpenses) C(sb, "HdrNum", "Number", "5");
                });

                var qRows = byQ[q];
                if (qRows.Count == 0)
                {
                    R(sb, () =>
                    {
                        C(sb, "D", "String", ""); C(sb, "D", "String", "");
                        C(sb, "D", "String", ""); C(sb, "DN", "Number", "0");
                        if (inclExpenses) C(sb, "DN", "Number", "0");
                    });
                }
                else
                {
                    int rn = rows.IndexOf(qRows[0]) + 1;
                    foreach (var op in qRows)
                    {
                        bool alt = rn % 2 == 0;
                        bool isInc = op.VatBookType == "Продажи";
                        string doc = $"{op.Date:dd.MM.yyyy} / {(string.IsNullOrEmpty(op.DocNumber) ? $"п/п {rn}" : op.DocNumber)}";
                        string pur = op.PaymentPurpose.Length > 150 ? op.PaymentPurpose[..147] + "…" : op.PaymentPurpose;
                        R(sb, 14, () =>
                        {
                            C(sb, alt ? "Alt" : "D", "Number", rn.ToString());
                            C(sb, alt ? "Alt" : "D", "String", X(doc));
                            C(sb, alt ? "Alt" : "D", "String", X(pur));
                            C(sb, alt ? "AltN" : "DN", "Number", isInc ? M(op.Amount) : "0");
                            if (inclExpenses) C(sb, alt ? "AltN" : "DN", "Number", !isInc ? M(op.Amount) : "0");
                        });
                        rn++;
                    }
                }
            }

            // Кварталы
            string[] ql = { "", "I", "II", "III", "IV" };
            for (int q = 1; q <= 4; q++)
            {
                WriteQSection(q);
                // Итого за квартал
                R(sb, 16, () =>
                {
                    C(sb, "Tot", "String", $"Итого за {ql[q]} квартал", mg: 2);
                    C(sb, "TotN", "Number", M(qD[q]));
                    if (inclExpenses) C(sb, "TotN", "Number", M(qR[q]));
                });
                // Нарастающий
                if (q >= 2)
                {
                    decimal cd = qD.Take(q + 1).Skip(1).Sum();
                    decimal cr = qR.Take(q + 1).Skip(1).Sum();
                    string cl = q == 2 ? "полугодие" : q == 3 ? "9 месяцев" : "год";
                    R(sb, 16, () =>
                    {
                        C(sb, "Cum", "String", $"Итого за {cl}", mg: 2);
                        C(sb, "CumN", "Number", M(cd));
                        if (inclExpenses) C(sb, "CumN", "Number", M(cr));
                    });
                }
                E(sb);
            }

            // Справка к разделу I
            E(sb);
            R(sb, () => C(sb, "Tot", "String", "Справка к разделу I:", mg: cols - 1));

            void RefRow(string code, string label, decimal val)
            {
                R(sb, () =>
                {
                    C(sb, "Ref0", "String", code);
                    C(sb, "Ref0", "String", X(label), mg: cols - 3);
                    C(sb, "Ref0N", "Number", M(val));
                });
            }

            RefRow("010", "Сумма полученных доходов за налоговый период", totalD);
            if (inclExpenses)
            {
                RefRow("020", "Сумма произведённых расходов за налоговый период", totalR);
                RefRow("030", "Разница между суммой уплаченного минимального налога и исчисленного в общем порядке за предыдущий период", 0m);
                RefRow("040", "Итого получено: — доходов (стр. 010 − стр. 020 − стр. 030)", Math.Max(totalD - totalR, 0m));
                RefRow("041", "— убытков (стр. 020 + стр. 030 − стр. 010)", Math.Max(totalR - totalD, 0m));
            }

            sb.AppendLine("</Table>");
            sb.AppendLine("<WorksheetOptions xmlns=\"urn:schemas-microsoft-com:office:excel\">");
            sb.AppendLine("  <FreezePanes/><FrozenNoSplit/><SplitHorizontal>1</SplitHorizontal><TopRowBottomPane>1</TopRowBottomPane>");
            sb.AppendLine("</WorksheetOptions>");
            sb.AppendLine("</Worksheet></Workbook>");

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            return path;
        }

        // ─── SpreadsheetML helpers ────────────────────────────────────────────

        private static void S(StringBuilder sb, string id,
            int sz = 10, bool bold = false, string? al = null, bool wrap = false,
            string? bg = null, string? numFmt = null)
        {
            sb.AppendLine($"  <Style ss:ID=\"{id}\">");
            sb.Append($"    <Font ss:Size=\"{sz}\"");
            if (bold) sb.Append(" ss:Bold=\"1\"");
            sb.AppendLine("/>");
            if (bg != null) sb.AppendLine($"    <Interior ss:Color=\"{bg}\" ss:Pattern=\"Solid\"/>");
            if (al != null || wrap)
            {
                sb.Append("    <Alignment");
                if (al != null) sb.Append($" ss:Horizontal=\"{al}\"");
                if (wrap) sb.Append(" ss:WrapText=\"1\"");
                sb.AppendLine("/>");
            }
            if (numFmt != null) sb.AppendLine($"    <NumberFormat ss:Format=\"{numFmt}\"/>");
            sb.AppendLine("  </Style>");
        }

        private static void R(StringBuilder sb, Action inner)
        { sb.AppendLine("  <Row>"); inner(); sb.AppendLine("  </Row>"); }
        private static void R(StringBuilder sb, int h, Action inner)
        { sb.AppendLine($"  <Row ss:Height=\"{h}\">"); inner(); sb.AppendLine("  </Row>"); }
        private static void E(StringBuilder sb) => sb.AppendLine("  <Row/>");

        private static void C(StringBuilder sb, string style, string type, string val, int mg = 0)
        {
            string m = mg > 0 ? $" ss:MergeAcross=\"{mg}\"" : "";
            sb.AppendLine($"    <Cell ss:StyleID=\"{style}\"{m}><Data ss:Type=\"{type}\">{val}</Data></Cell>");
        }

        private static string M(decimal v) => v.ToString("F2", CultureInfo.InvariantCulture);
        private static string X(string s) => s
            .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}