// Генерирует УПД статус 1 по форме Приложения № 1
// к Постановлению Правительства РФ от 26.12.2011 № 1137
// (в ред. Постановления от 23.01.2026 № 26).
// HTML → открывается в браузере → Печать/PDF.

using ClientAccountApp.Models;
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ClientAccountApp.Services
{
    public static class BankStatementUpdHtmlService
    {
        private static readonly CultureInfo Ru = new("ru-RU");

        public static string GenerateUpd(
            BankStatementOperation op,
            string sellerINN, string sellerKPP, string sellerName,
            string outputFolder,
            string sellerAddress = "",
            string sellerDirectorName = "",
            string sellerDirectorPosition = "Руководитель")
        {
            bool isSales = op.VatBookType == "Продажи";

            string sellName = isSales ? sellerName : op.CounterpartyName;
            string sellINN = isSales ? sellerINN : op.CounterpartyINN;
            string sellKPP = isSales ? sellerKPP : op.CounterpartyKPP;
            string buyName = isSales ? op.CounterpartyName : sellerName;
            string buyINN = isSales ? op.CounterpartyINN : sellerINN;
            string buyKPP = isSales ? op.CounterpartyKPP : sellerKPP;

            string sfNum = string.IsNullOrEmpty(op.VatInvoiceNumber) ? op.DocNumber : op.VatInvoiceNumber;
            DateTime sfDt = op.VatInvoiceDate ?? op.Date;
            string sfDateLong = sfDt.ToString("dd MMMM yyyy г.", Ru);
            string sfDateShrt = sfDt.ToString("dd.MM.yyyy");
            string opDateShrt = op.Date.ToString("dd.MM.yyyy");

            decimal vatBase = op.AmountWithoutVat;
            decimal vatAmt = op.VatAmount;
            decimal total = op.Amount;
            string vatRate = $"{(int)op.VatRate}%";

            var sb = new StringBuilder();
            sb.AppendLine($"<!DOCTYPE html><html lang='ru'><head><meta charset='utf-8'><title>УПД № {H(sfNum)} от {sfDateLong}</title>");
            sb.AppendLine(@"<style>
*{box-sizing:border-box;margin:0;padding:0}
@page{size:A4;margin:5mm 4mm 5mm 6mm}
body{font-family:Arial,sans-serif;font-size:7pt;color:#000}
.btn{background:#1d4ed8;color:#fff;padding:6px 14px;border:none;border-radius:4px;cursor:pointer;font-size:10pt;margin-bottom:5px}
@media print{.btn{display:none}}
table{border-collapse:collapse;width:100%}
td,th{border:.5pt solid #000;padding:1pt 2pt;vertical-align:top;font-size:7pt}
.nb{border:none!important}
.info td{padding:1.5pt 3pt}
.lbl{font-weight:bold;background:#f8f8f8;width:26%;white-space:nowrap}
.fn{font-size:5.5pt;color:#555;margin-left:2pt}
.g th{font-size:5.5pt;text-align:center;background:#efefef;padding:1pt}
.g td{font-size:6.5pt;padding:1pt 2pt}
.ctr{text-align:center}
.rgt{text-align:right}
.hdr2 td{text-align:center;background:#e0e0e0;font-weight:bold;font-size:7pt}
.tot td{font-weight:bold;background:#f0f0f0}
.sig td{border:none;padding:3pt 4pt;vertical-align:bottom}
.sln{display:inline-block;border-bottom:.5pt solid #000;min-width:80pt}
.slns{display:inline-block;border-bottom:.5pt solid #000;min-width:50pt}
.sll{font-size:6pt;color:#555}
.status-wrap{overflow:hidden;margin-bottom:2pt}
.status-box{float:right;border:1.5pt solid #000;padding:3pt 10pt;font-size:10pt;font-weight:bold}
.status-sub{float:right;clear:right;font-size:5.5pt;text-align:right;max-width:105pt;margin-top:2pt;line-height:1.4}
.decree{font-size:5.5pt;color:#333;text-align:right;margin-bottom:1pt}
.title{font-size:9pt;font-weight:bold;text-align:center;margin:3pt 0 1pt}
.sub{font-size:6.5pt;text-align:center;color:#555;margin-bottom:3pt}
.upd-title{font-size:8pt;font-weight:bold;margin-bottom:3pt}
.p2{page-break-before:always;padding-top:3mm}
.tr td{padding:2pt 3pt}
.tr .rl{font-size:6pt;color:#444;width:38%}
.tr .rv{border-bottom:.5pt solid #000}
hr{border:.3pt solid #aaa;margin:4pt 0}
.foot{color:#bbb;font-size:6pt;text-align:right;margin-top:4pt}
</style></head><body>");

            sb.AppendLine("<button class='btn' onclick='window.print()'>🖨 Печать / Сохранить как PDF</button>");

            // ══ СТРАНИЦА 1: СЧЁТ-ФАКТУРА ════════════════════════════════════

            // Статус + ссылка на постановление
            sb.AppendLine("<div class='status-wrap'>");
            sb.AppendLine("<div class='status-box'>Статус: 1</div>");
            sb.AppendLine(@"<div class='status-sub'>1 – счёт-фактура и<br>передаточный документ (акт)<br>2 – передаточный документ (акт)</div>");
            sb.AppendLine("<div class='decree'>Приложение № 1 к постановлению Правительства Российской Федерации от 26 декабря 2011 г. № 1137<br>(в ред. Постановления Правительства РФ от 23 января 2026 г. № 26)</div>");
            sb.AppendLine("</div>");

            sb.AppendLine($"<div class='title'>Счёт-фактура № {H(sfNum)} от ({sfDateShrt}) <span class='fn'>(1)</span></div>");
            sb.AppendLine("<div class='sub'>Исправление № &ndash;&ndash; от &ndash;&ndash; <span class='fn'>(1а)</span></div>");

            // Реквизиты сторон
            sb.AppendLine("<table class='info'>");
            R2(sb, "Продавец:", "(2)", sellName, "Покупатель:", "(6)", buyName);
            string dispSellerAddr = isSales ? sellerAddress : "";
            R2(sb, "Адрес:", "(2а)", dispSellerAddr, "Адрес:", "(6а)", "");
            R2(sb, "ИНН/КПП продавца:", "(2б)", $"{sellINN}/{sellKPP}", "ИНН/КПП покупателя:", "(6б)", $"{buyINN}/{buyKPP}");
            sb.AppendLine($"<tr><td class='lbl'>Грузоотправитель и его адрес: <span class='fn'>(3)</span></td><td colspan='3'>он же</td></tr>");
            sb.AppendLine($"<tr><td class='lbl'>Грузополучатель и его адрес: <span class='fn'>(4)</span></td><td colspan='3'>{H(buyName)}</td></tr>");
            sb.AppendLine($"<tr><td class='lbl'>К платёжно-расчётному документу № <span class='fn'>(5)</span></td><td colspan='3'>{H(op.DocNumber)} от {opDateShrt}</td></tr>");
            sb.AppendLine($"<tr><td class='lbl'>Документ об отгрузке: <span class='fn'>(5а)</span></td><td colspan='3'>Универсальный передаточный документ, № {H(sfNum)} от {sfDateShrt} г.</td></tr>");
            sb.AppendLine($"<tr><td class='lbl'>К счёту-фактуре, выставленному при получении оплаты <span class='fn'>(5б)</span></td><td colspan='3'>№ &ndash;&ndash; от &ndash;&ndash;</td></tr>");
            R2(sb, "Валюта: наименование, код:", "(7)", "Российский рубль, 643", "Идентификатор гос. контракта:", "(8)", "");
            sb.AppendLine("</table>");

            // Таблица товаров/услуг
            sb.AppendLine("<table class='g' style='margin-top:3pt'>");

            // Заголовки - описания
            sb.AppendLine("<tr>");
            sb.AppendLine("<th style='width:3%'>№<br>п/п</th>");
            sb.AppendLine("<th style='width:18%'>Наименование товара (описание выполненных работ, оказанных услуг), имущественного права</th>");
            sb.AppendLine("<th style='width:4%'>Код товара/<br>работ,<br>услуг</th>");
            sb.AppendLine("<th style='width:3%'>Код<br>вида<br>товара</th>");
            sb.AppendLine("<th style='width:3%'>Ед.<br>код</th>");
            sb.AppendLine("<th style='width:5%'>Ед.<br>изм.</th>");
            sb.AppendLine("<th style='width:5%'>Кол-во<br>(объём)</th>");
            sb.AppendLine("<th style='width:8%'>Цена<br>(тариф)<br>без НДС</th>");
            sb.AppendLine("<th style='width:8%'>Стоим.<br>товаров (работ,<br>услуг) без НДС</th>");
            sb.AppendLine("<th style='width:5%'>В т.ч.<br>сумма<br>акциза</th>");
            sb.AppendLine("<th style='width:4%'>Нал.<br>ставка</th>");
            sb.AppendLine("<th style='width:8%'>Сумма<br>налога,<br>предъявл.<br>покупателю</th>");
            sb.AppendLine("<th style='width:8%'>Стоим.<br>товаров<br>(работ, услуг)<br>с налогом</th>");
            sb.AppendLine("<th style='width:3.5%'>Страна<br>происх.<br>код</th>");
            sb.AppendLine("<th style='width:4%'>Страна<br>происх.<br>назв.</th>");
            sb.AppendLine("<th style='width:5%'>Рег.<br>номер<br>декл.<br>/ партии</th>");
            sb.AppendLine("</tr>");

            // Номера столбцов
            sb.AppendLine("<tr class='hdr2'>");
            foreach (var c in new[] { "А", "1", "1а", "1б", "2", "2а", "3", "4", "5", "6", "7", "8", "9", "10", "10а", "11" })
                sb.AppendLine($"<td>{c}</td>");
            sb.AppendLine("</tr>");

            // Строка услуги
            string pur = op.PaymentPurpose.Length > 80 ? op.PaymentPurpose[..77] + "…" : op.PaymentPurpose;
            sb.AppendLine("<tr>");
            sb.AppendLine("<td class='ctr'>1</td>");
            sb.AppendLine($"<td>{H(pur)}</td>");
            sb.AppendLine("<td class='ctr'>&ndash;&ndash;</td><td class='ctr'>&ndash;&ndash;</td>");
            sb.AppendLine("<td class='ctr'>&ndash;&ndash;</td><td class='ctr'>услуга</td>");
            sb.AppendLine("<td class='rgt'>1</td>");
            sb.AppendLine($"<td class='rgt'>{M(vatBase)}</td>");
            sb.AppendLine($"<td class='rgt'>{M(vatBase)}</td>");
            sb.AppendLine("<td class='ctr'>без<br>акциза</td>");
            sb.AppendLine($"<td class='ctr'>{H(vatRate)}</td>");
            sb.AppendLine($"<td class='rgt'>{M(vatAmt)}</td>");
            sb.AppendLine($"<td class='rgt'>{M(total)}</td>");
            sb.AppendLine("<td class='ctr'>&ndash;&ndash;</td><td class='ctr'>&ndash;&ndash;</td><td class='ctr'>&ndash;&ndash;</td>");
            sb.AppendLine("</tr>");

            // Итого
            sb.AppendLine("<tr class='tot'>");
            sb.AppendLine("<td colspan='8' class='rgt'>Всего к оплате <span class='fn'>(9)</span></td>");
            sb.AppendLine($"<td class='rgt'>{M(vatBase)}</td><td class='ctr'>X</td><td></td>");
            sb.AppendLine($"<td class='rgt'>{M(vatAmt)}</td><td class='rgt'>{M(total)}</td>");
            sb.AppendLine("<td colspan='3'></td>");
            sb.AppendLine("</tr></table>");

            // Подписи (стр.1)
            sb.AppendLine("<table class='sig' style='margin-top:5pt'><tr>");
            SigCell(sb, "Руководитель организации или иное уполномоченное лицо", sellerDirectorPosition, sellerDirectorName);
            SigCell(sb, "Главный бухгалтер или иное уполномоченное лицо", "", "");
            sb.AppendLine("</tr><tr>");
            SigCell(sb, "Индивидуальный предприниматель или иное уполномоченное лицо<br><span class='sll'>(ОГРНИП и дата присвоения)</span>", "", "");
            sb.AppendLine("<td></td>");
            sb.AppendLine("</tr></table>");

            // ══ СТРАНИЦА 2: ПЕРЕДАТОЧНЫЙ ДОКУМЕНТ ════════════════════════════
            sb.AppendLine("<div class='p2'>");
            sb.AppendLine($"<div class='upd-title'>Универсальный передаточный документ № {H(sfNum)} от {sfDateLong}. Лист 2</div>");
            sb.AppendLine("<table class='tr'>");

            TR(sb, "Основание передачи (сдачи) / получения (приёмки) (8)", "(договор; доверенность и др.)");
            TR(sb, "Данные о транспортировке и грузе (9)", "");
            TRsig(sb, "Товар (груз) передал / услуги, результаты работ, права сдал (10)");
            sb.AppendLine($"<tr><td class='rl'>Дата отгрузки, передачи (сдачи) <b>(11)</b></td>" +
                          $"<td class='rv'>&laquo;{op.Date:dd}&raquo; {op.Date.ToString("MMMM yyyy г.", Ru)}</td></tr>");
            TR(sb, "Иные сведения об отгрузке, передаче (12)", "");
            TRsig(sb, "Ответственный за правильность оформления факта хозяйственной жизни (13)");
            string sellerSubject = $"{H(sellName)}, ИНН/КПП {H(sellINN)}/{H(sellKPP)}";
            if (!string.IsNullOrEmpty(sellerDirectorName))
                sellerSubject += $"<br><b>{H(sellerDirectorPosition)} {H(sellerDirectorName)}</b>";
            sb.AppendLine($"<tr><td class='rl'>Наименование экономического субъекта – составителя документа (в т.ч. комиссионера / агента) <b>(14)</b></td>" +
                          $"<td class='rv'>{sellerSubject} &nbsp;&nbsp;<b>М.П.</b></td></tr>");

            sb.AppendLine("</table><hr/><table class='tr'>");

            TRsig(sb, "Товар (груз) получил / услуги, результаты работ, права принял (15)");
            TR(sb, "Дата получения (приёмки) (16)", "");
            TR(sb, "Иные сведения о получении, приёмке (информация о наличии / отсутствии претензии и др.) (17)", "");
            TRsig(sb, "Ответственный за правильность оформления факта хозяйственной жизни (18)");
            sb.AppendLine($"<tr><td class='rl'>Наименование экономического субъекта – составителя документа <b>(19)</b></td>" +
                          $"<td class='rv'>{H(buyName)}, ИНН/КПП {H(buyINN)}/{H(buyKPP)} &nbsp;&nbsp;<b>М.П.</b></td></tr>");

            sb.AppendLine("</table>");
            sb.AppendLine($"<p class='foot'>Сформировано {DateTime.Now:dd.MM.yyyy HH:mm} · ClientAccountApp</p>");
            sb.AppendLine("</div></body></html>");

            Directory.CreateDirectory(outputFolder);
            string safe = sfNum.Replace("/", "-").Replace("\\", "-");
            string fname = $"УПД_{safe}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
            string fpath = Path.Combine(outputFolder, fname);
            File.WriteAllText(fpath, sb.ToString(), new UTF8Encoding(true));
            return fpath;
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static void R2(StringBuilder sb,
            string l1, string f1, string v1, string l2, string f2, string v2)
        {
            sb.AppendLine($"<tr>" +
                $"<td class='lbl'>{H(l1)} <span class='fn'>{f1}</span></td><td>{H(v1)}</td>" +
                $"<td class='lbl'>{H(l2)} <span class='fn'>{f2}</span></td><td>{H(v2)}</td>" +
                $"</tr>");
        }

        private static void TR(StringBuilder sb, string label, string val)
        {
            sb.AppendLine($"<tr><td class='rl'>{label}</td>" +
                $"<td class='rv'>{(string.IsNullOrEmpty(val) ? "&nbsp;" : H(val))}</td></tr>");
        }

        private static void TRsig(StringBuilder sb, string label)
        {
            sb.AppendLine($"<tr><td class='rl'>{label}</td><td class='rv'>" +
                "<span class='sll'>(должность)</span> " +
                "<span class='slns'>&nbsp;</span> " +
                "<span class='sll'>(подпись)</span> " +
                "<span class='sln'>&nbsp;</span> " +
                "<span class='sll'>(ф.и.о.)</span></td></tr>");
        }

        private static void SigCell(StringBuilder sb, string title, string position = "", string name = "")
        {
            string posLine = string.IsNullOrEmpty(position) ? "" : $"<br><span class='sll'>{H(position)}</span>";
            string nameLine = string.IsNullOrEmpty(name) ? "" : $" <b>{H(name)}</b>";
            sb.AppendLine($"<td><b>{title}</b>{posLine}<br><br>" +
                "<span class='slns'>&nbsp;</span> / <span class='sll'>(подпись)</span> " +
                $"<span class='sln'>&nbsp;</span>{nameLine} <span class='sll'>(ф.и.о.)</span></td>");
        }

        private static string H(string? s) =>
            string.IsNullOrEmpty(s) ? "" :
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        private static string M(decimal v) => v.ToString("N2", Ru);
    }
}