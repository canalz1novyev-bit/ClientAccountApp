// Парсит ПФС и генерирует РСВ XML с двумя тарифными блоками (НР + ПВ).
// Формат ВерсФорм 5.07, WINDOWS-1251, совместим с СБИС/SABY.

using ClientAccountApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ClientAccountApp.Services
{
    public static class RsvGeneratorService
    {
        // ── Парсинг ПФС ───────────────────────────────────────────────────────

        public static (PfsFileInfo info, List<RsvEmployee> employees)
            ParsePfs(string filePath)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var win1251 = Encoding.GetEncoding("windows-1251");

            byte[] raw = File.ReadAllBytes(filePath);
            string xml;
            try   { xml = win1251.GetString(raw); }
            catch { xml = Encoding.UTF8.GetString(raw); }

            var doc = XDocument.Parse(xml.Contains("?>")
                ? xml[(xml.IndexOf("?>") + 2)..]
                : xml);

            var root  = doc.Root!;
            var docEl = root.Element("Документ")!;

            int monthNum = int.Parse(
                docEl.Attribute("Период")?.Value ?? "1");
            int year = int.Parse(
                docEl.Attribute("ОтчетГод")?.Value
                ?? DateTime.Now.Year.ToString());
            string kodNO = docEl.Attribute("КодНО")?.Value ?? "";

            var нпюл = docEl.Descendants("НПЮЛ").FirstOrDefault();
            string orgINN  = нпюл?.Attribute("ИННЮЛ")?.Value ?? "";
            string orgKPP  = нпюл?.Attribute("КПП")?.Value   ?? "";
            string orgName = нпюл?.Attribute("НаимОрг")?.Value ?? "";

            int quarterMonth = ((monthNum - 1) % 3) + 1; // 1, 2 или 3

            var employees = new List<RsvEmployee>();
            foreach (var el in docEl.Elements("ПерсСвФЛ"))
            {
                decimal сум = decimal.TryParse(
                    el.Attribute("СумВыпл")?.Value,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var v) ? v : 0m;

                var фио = el.Element("ФИО");
                var emp = new RsvEmployee
                {
                    SNILS      = el.Attribute("СНИЛС")?.Value  ?? "",
                    INN        = el.Attribute("ИННФЛ")?.Value  ?? "",
                    Surname    = фио?.Attribute("Фамилия")?.Value ?? "",
                    Name       = фио?.Attribute("Имя")?.Value     ?? "",
                    Patronymic = фио?.Attribute("Отчество")?.Value ?? "",
                };

                // Все суммы из ПФС идут в НР по умолчанию.
                // Пользователь может перевести часть в ПВ через диалог.
                switch (quarterMonth)
                {
                    case 1: emp.PayNR1 = сум; break;
                    case 2: emp.PayNR2 = сум; break;
                    case 3: emp.PayNR3 = сум; break;
                }
                employees.Add(emp);
            }

            var info = new PfsFileInfo
            {
                FilePath      = filePath,
                MonthNum      = monthNum,
                QuarterMonth  = quarterMonth,
                OrgINN        = orgINN,
                OrgKPP        = orgKPP,
                OrgName       = orgName,
                KodNO         = kodNO,
                Year          = year,
                EmployeeCount = employees.Count,
                StatusText    = $"Месяц {monthNum}/{year} — {employees.Count} сотр.",
            };
            return (info, employees);
        }

        // ── Слияние трёх ПФС ─────────────────────────────────────────────────

        public static List<RsvEmployee> MergeEmployees(
            List<RsvEmployee>? m1,
            List<RsvEmployee>? m2,
            List<RsvEmployee>? m3)
        {
            var result = new Dictionary<string, RsvEmployee>(
                StringComparer.OrdinalIgnoreCase);

            void Merge(List<RsvEmployee>? src, int slot)
            {
                if (src == null) return;
                foreach (var e in src)
                {
                    if (!result.TryGetValue(e.SNILS, out var ex))
                    {
                        ex = new RsvEmployee
                        {
                            SNILS      = e.SNILS, INN = e.INN,
                            Surname    = e.Surname, Name = e.Name,
                            Patronymic = e.Patronymic,
                        };
                        result[e.SNILS] = ex;
                    }
                    // Берём только НР-часть из соответствующего месяца;
                    // ПВ-выплаты добавляются пользователем вручную
                    switch (slot)
                    {
                        case 1: ex.PayNR1 = e.PayNR1; break;
                        case 2: ex.PayNR2 = e.PayNR2; break;
                        case 3: ex.PayNR3 = e.PayNR3; break;
                    }
                }
            }

            Merge(m1, 1); Merge(m2, 2); Merge(m3, 3);
            return result.Values.ToList();
        }

        // ── Авто-разбивка НР / ПВ по правилу 1.5 × МРОТ ────────────────────
        // Всё до порога — НР (основной тариф).
        // Выплата выше порога — ПВ (льготный тариф 15%).
        // Применяется ПОСЛЕ слияния ПФС-файлов.

        public static void AutoSplitNrPv(
            List<RsvEmployee> employees,
            decimal mrotPerMonth)
        {
            decimal threshold = Math.Round(mrotPerMonth * 1.5m, 2);

            foreach (var e in employees)
            {
                // Каждый месяц разбиваем: totalPay = NR_old (из ПФС) + PV_old (из ПФС)
                // Если ПВ ещё не выставлен вручную — авто-разбивка по порогу
                SplitMonth(e.PayNR1 + e.PayPV1, threshold,
                    out decimal nr1, out decimal pv1);
                SplitMonth(e.PayNR2 + e.PayPV2, threshold,
                    out decimal nr2, out decimal pv2);
                SplitMonth(e.PayNR3 + e.PayPV3, threshold,
                    out decimal nr3, out decimal pv3);

                e.PayNR1 = nr1; e.PayPV1 = pv1;
                e.PayNR2 = nr2; e.PayPV2 = pv2;
                e.PayNR3 = nr3; e.PayPV3 = pv3;
            }
        }

        private static void SplitMonth(decimal total, decimal threshold,
            out decimal nr, out decimal pv)
        {
            nr = Math.Min(total, threshold);
            pv = Math.Max(0, total - threshold);
        }

        // ── Расчёт взносов ─────────────────────────────────────────────────────────

        public static void CalculateContributions(
            List<RsvEmployee> employees,
            decimal nrRate,
            decimal pvRate = 0.15m)
        {
            foreach (var e in employees)
            {
                // НР — ставка организации
                e.ContribNR1 = Math.Round(e.OpsBaseNR1 * nrRate, 2);
                e.ContribNR2 = Math.Round(e.OpsBaseNR2 * nrRate, 2);
                e.ContribNR3 = Math.Round(e.OpsBaseNR3 * nrRate, 2);

                // ПВ — льготная ставка 15% (фиксировано)
                e.ContribPV1 = Math.Round(e.OpsBasePV1 * pvRate, 2);
                e.ContribPV2 = Math.Round(e.OpsBasePV2 * pvRate, 2);
                e.ContribPV3 = Math.Round(e.OpsBasePV3 * pvRate, 2);
            }
        }

        // ── Генерация РСВ XML ─────────────────────────────────────────────────

        public static string GenerateRsv(
            List<RsvEmployee> employees,
            RsvSettings s,
            string outputFolder)
        {
            bool hasPV = employees.Any(e => e.TotalPayPV > 0);

            // ── Агрегаты НР ──────────────────────────────────────────────────
            decimal nrPay1 = employees.Sum(e => e.PayNR1);
            decimal nrPay2 = employees.Sum(e => e.PayNR2);
            decimal nrPay3 = employees.Sum(e => e.PayNR3);
            decimal nrPayT = nrPay1 + nrPay2 + nrPay3;

            decimal nrEx1  = employees.Sum(e => e.ExemptNR1);
            decimal nrEx2  = employees.Sum(e => e.ExemptNR2);
            decimal nrEx3  = employees.Sum(e => e.ExemptNR3);
            decimal nrExT  = nrEx1 + nrEx2 + nrEx3;

            decimal nrB1   = nrPay1 - nrEx1;
            decimal nrB2   = nrPay2 - nrEx2;
            decimal nrB3   = nrPay3 - nrEx3;
            decimal nrBT   = nrB1 + nrB2 + nrB3;

            decimal nrC1   = employees.Sum(e => e.ContribNR1);
            decimal nrC2   = employees.Sum(e => e.ContribNR2);
            decimal nrC3   = employees.Sum(e => e.ContribNR3);
            decimal nrCT   = nrC1 + nrC2 + nrC3;

            // ── Агрегаты ПВ ──────────────────────────────────────────────────
            decimal pvPay1 = employees.Sum(e => e.PayPV1);
            decimal pvPay2 = employees.Sum(e => e.PayPV2);
            decimal pvPay3 = employees.Sum(e => e.PayPV3);
            decimal pvPayT = pvPay1 + pvPay2 + pvPay3;

            decimal pvC1   = employees.Sum(e => e.ContribPV1);
            decimal pvC2   = employees.Sum(e => e.ContribPV2);
            decimal pvC3   = employees.Sum(e => e.ContribPV3);
            decimal pvCT   = pvC1 + pvC2 + pvC3;

            // ── УплПерОПС = НР + ПВ взносы ───────────────────────────────────
            decimal totalCT = nrCT + pvCT;
            decimal totalC1 = nrC1 + pvC1;
            decimal totalC2 = nrC2 + pvC2;
            decimal totalC3 = nrC3 + pvC3;

            bool autoPaid = s.PaidM1 + s.PaidM2 + s.PaidM3 == 0;
            decimal paidT = autoPaid ? totalCT : s.PaidM1 + s.PaidM2 + s.PaidM3;
            decimal paid1 = autoPaid ? totalC1 : s.PaidM1;
            decimal paid2 = autoPaid ? totalC2 : s.PaidM2;
            decimal paid3 = autoPaid ? totalC3 : s.PaidM3;

            // ── Количество застрахованных НР ─────────────────────────────────
            int nrVs = employees.Count;
            int nrC1n = employees.Count(e => e.PayNR1 > 0 || (e.PayNR2 == 0 && e.PayNR3 == 0));
            int nrC2n = employees.Count(e => e.PayNR2 > 0);
            int nrC3n = employees.Count(e => e.PayNR3 > 0);
            int nrNachVs = employees.Count(e => e.TotalContribNR > 0);
            int nrNach1  = employees.Count(e => e.ContribNR1 > 0);
            int nrNach2  = employees.Count(e => e.ContribNR2 > 0);
            int nrNach3  = employees.Count(e => e.ContribNR3 > 0);

            // ── Количество застрахованных ПВ ─────────────────────────────────
            int pvVs = employees.Count(e => e.TotalPayPV > 0);
            int pvC1c= employees.Count(e => e.PayPV1 > 0);
            int pvC2c= employees.Count(e => e.PayPV2 > 0);
            int pvC3c= employees.Count(e => e.PayPV3 > 0);

            // ── Имя файла ─────────────────────────────────────────────────────
            string region = s.OrgINN.Length >= 4 ? s.OrgINN[..4] : s.OrgINN;
            string innKpp = s.OrgINN + s.OrgKPP;
            string date8  = DateTime.Now.ToString("yyyyMMdd");
            string guid   = Guid.NewGuid().ToString().ToLower();
            string fname  = $"NO_RASCHSV_{region}_{region}_{innKpp}_{date8}_{guid}.xml";
            string idFail = $"NO_RASCHSV_{region}_{region}_{innKpp}_{date8}_{guid}";

            Directory.CreateDirectory(outputFolder);
            string fpath = Path.Combine(outputFolder, fname);

            var sb = new StringBuilder();
            var xs = new XmlWriterSettings
            {
                Indent = true, IndentChars = " ",
                OmitXmlDeclaration = true,
                ConformanceLevel = ConformanceLevel.Document,
            };

            using (var xw = XmlWriter.Create(sb, xs))
            {
                xw.WriteStartElement("Файл");
                xw.WriteAttributeString("ИдФайл", idFail);
                xw.WriteAttributeString("ВерсПрог", "ClientAccountApp 1.1");
                xw.WriteAttributeString("ВерсФорм", "5.08");

                // <Документ>
                xw.WriteStartElement("Документ");
                xw.WriteAttributeString("КНД", "1151111");
                xw.WriteAttributeString("ДатаДок", DateTime.Now.ToString("dd.MM.yyyy"));
                xw.WriteAttributeString("НомКорр", "0");
                xw.WriteAttributeString("Период", s.PeriodCode);
                xw.WriteAttributeString("ОтчетГод", s.Year.ToString());
                xw.WriteAttributeString("КодНО", s.KodNO);
                xw.WriteAttributeString("ПоМесту", "214");

                // <СвНП>
                xw.WriteStartElement("СвНП");
                xw.WriteAttributeString("СрЧисл", s.AvgHeadcount.ToString());
                if (!string.IsNullOrEmpty(s.Phone))
                    xw.WriteAttributeString("Тлф", s.Phone);
                xw.WriteStartElement("НПЮЛ");
                xw.WriteAttributeString("НаимОрг", s.OrgName);
                xw.WriteAttributeString("ИННЮЛ", s.OrgINN);
                xw.WriteAttributeString("КПП", s.OrgKPP);
                xw.WriteEndElement();
                xw.WriteEndElement(); // СвНП

                // <Подписант>
                xw.WriteStartElement("Подписант");
                xw.WriteAttributeString("ПрПодп", "1");
                xw.WriteStartElement("ФИО");
                xw.WriteAttributeString("Фамилия", s.DirectorSurname);
                xw.WriteAttributeString("Имя", s.DirectorName);
                xw.WriteAttributeString("Отчество", s.DirectorPatronymic);
                xw.WriteEndElement();
                xw.WriteEndElement(); // Подписант

                // <РасчетСВ>
                xw.WriteStartElement("РасчетСВ");
                xw.WriteStartElement("ОбязПлатСВ");
                xw.WriteAttributeString("ТипПлат", "1");
                xw.WriteAttributeString("ОКТМО", s.OKTMO);

                // <УплПерОПС> — суммарная уплата по ВСЕМ тарифам
                xw.WriteStartElement("УплПерОПС");
                xw.WriteAttributeString("КБК", s.KBK);
                xw.WriteAttributeString("СумСВУплПер", F(paidT));
                xw.WriteAttributeString("СумСВУпл1М",  F(paid1));
                xw.WriteAttributeString("СумСВУпл2М",  F(paid2));
                xw.WriteAttributeString("СумСВУпл3М",  F(paid3));
                xw.WriteEndElement();

                // ── Блок НР (основной тариф) ─────────────────────────────────
                WriteOpsBlock(xw, s.NrTariffCode,
                    нрCount:    (nrVs, nrC1n, nrC2n, nrC3n),
                    nachCount:  (nrNachVs, nrNach1, nrNach2, nrNach3),
                    vypl:       (nrPayT, nrPay1, nrPay2, nrPay3),
                    exempt:     (nrExT, nrEx1, nrEx2, nrEx3),
                    baz:        (nrBT, nrB1, nrB2, nrB3),
                    nach:       (nrCT, nrC1, nrC2, nrC3));

                // ── Блок ПВ (льготный тариф, только если есть ПВ-выплаты) ────
                if (hasPV)
                {
                    WriteOpsBlock(xw, s.PvTariffCode,
                        нрCount:   (pvVs, pvC1c, pvC2c, pvC3c),
                        nachCount: (pvVs, pvC1c, pvC2c, pvC3c),
                        vypl:      (pvPayT, pvPay1, pvPay2, pvPay3),
                        exempt:    (0, 0, 0, 0),
                        baz:       (pvPayT, pvPay1, pvPay2, pvPay3),
                        nach:      (pvCT, pvC1, pvC2, pvC3));
                }

                // IT-льгота (ст. 427 п.3)
                if (s.HasItBenefit && s.NrTariffCode == "06")
                {
                    xw.WriteStartElement("ПравТариф3_18.1.427");
                    xw.WriteAttributeString("КодПлат", "1");
                    xw.WriteAttributeString("Дох427_Пер", F(s.ItIncomePer, "0"));
                    xw.WriteAttributeString("ДохКр5.427_Пер", F(s.ItItIncomePer, "0"));
                    decimal d = s.ItIncomePer > 0
                        ? Math.Round(s.ItItIncomePer / s.ItIncomePer * 100, 2) : 0;
                    xw.WriteAttributeString("ДолДох5.427_Пер", F(d));
                    if (!string.IsNullOrEmpty(s.ItBenefitRegDate))
                    {
                        xw.WriteStartElement("СвРеестрОрг");
                        xw.WriteAttributeString("ДатаЗапОрг", s.ItBenefitRegDate);
                        xw.WriteAttributeString("НомЗапОрг",  s.ItBenefitRegNum);
                        xw.WriteEndElement();
                    }
                    xw.WriteEndElement();
                }

                // АПК-льгота (ст. 427 п.13.2)
                if (s.HasAgrBenefit && hasPV)
                {
                    xw.WriteStartElement("ПравТариф13.2.427");
                    xw.WriteAttributeString("КодПлат", "2");
                    xw.WriteAttributeString("Дох26.2Пер",   F(s.AgrIncomePer, "0"));
                    xw.WriteAttributeString("ДохОснПер",    F(s.AgrIncomeBasePer, "0"));
                    decimal доля = s.AgrIncomePer > 0
                        ? Math.Round(s.AgrIncomeBasePer / s.AgrIncomePer * 100, 2) : 0;
                    xw.WriteAttributeString("ДолДох13.2.427Пер", F(доля));
                    xw.WriteEndElement();
                }

                xw.WriteEndElement(); // ОбязПлатСВ

                // ── Персональные сведения ─────────────────────────────────────
                foreach (var emp in employees)
                {
                    xw.WriteStartElement("ПерсСвСтрахЛиц");

                    // <ДанФЛПолуч>
                    xw.WriteStartElement("ДанФЛПолуч");
                    if (!string.IsNullOrEmpty(emp.INN))
                        xw.WriteAttributeString("ИННФЛ", emp.INN);
                    xw.WriteAttributeString("СНИЛС", emp.SNILS);
                    if (!string.IsNullOrEmpty(emp.BirthDate))
                        xw.WriteAttributeString("ДатаРожд", emp.BirthDate);
                    if (!string.IsNullOrEmpty(emp.Citizenship))
                        xw.WriteAttributeString("Гражд", emp.Citizenship);
                    if (!string.IsNullOrEmpty(emp.Gender))
                        xw.WriteAttributeString("Пол", emp.Gender);
                    if (!string.IsNullOrEmpty(emp.DocSeries))
                    {
                        xw.WriteAttributeString("КодВидДок", emp.DocType);
                        xw.WriteAttributeString("СерНомДок",  emp.DocSeries);
                    }
                    xw.WriteStartElement("ФИО");
                    xw.WriteAttributeString("Фамилия", emp.Surname);
                    xw.WriteAttributeString("Имя",     emp.Name);
                    xw.WriteAttributeString("Отчество",emp.Patronymic);
                    xw.WriteEndElement();
                    xw.WriteEndElement(); // ДанФЛПолуч

                    // <СвВыплСВОПС>
                    xw.WriteStartElement("СвВыплСВОПС");
                    xw.WriteStartElement("СвВыпл");

                    // Каждый месяц: строка НР + строка ПВ (если есть)
                    WriteEmpMonth(xw, 1, emp.CategoryCodeNR,
                        emp.PayNR1, emp.OpsBaseNR1, emp.ContribNR1);
                    if (emp.PayPV1 > 0)
                        WriteEmpMonth(xw, 1, "ПВ",
                            emp.PayPV1, emp.OpsBasePV1, emp.ContribPV1);

                    WriteEmpMonth(xw, 2, emp.CategoryCodeNR,
                        emp.PayNR2, emp.OpsBaseNR2, emp.ContribNR2);
                    if (emp.PayPV2 > 0)
                        WriteEmpMonth(xw, 2, "ПВ",
                            emp.PayPV2, emp.OpsBasePV2, emp.ContribPV2);

                    WriteEmpMonth(xw, 3, emp.CategoryCodeNR,
                        emp.PayNR3, emp.OpsBaseNR3, emp.ContribNR3);
                    if (emp.PayPV3 > 0)
                        WriteEmpMonth(xw, 3, "ПВ",
                            emp.PayPV3, emp.OpsBasePV3, emp.ContribPV3);

                    xw.WriteEndElement(); // СвВыпл
                    xw.WriteEndElement(); // СвВыплСВОПС
                    xw.WriteEndElement(); // ПерсСвСтрахЛиц
                }

                xw.WriteEndElement(); // РасчетСВ
                xw.WriteEndElement(); // Документ
                xw.WriteEndElement(); // Файл
            }

            string fullXml = "<?xml version=\"1.0\" encoding=\"WINDOWS-1251\"?>\n"
                + sb.ToString().Replace("\r\n", "\n").Replace(" />", "/>");

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            File.WriteAllText(fpath, fullXml, Encoding.GetEncoding("windows-1251"));
            return fpath;
        }

        // ── Блок РасчСВ_ОПС_ОМС ──────────────────────────────────────────────

        private static void WriteOpsBlock(XmlWriter xw, string tariff,
            (int vs, int m1, int m2, int m3) нрCount,
            (int vs, int m1, int m2, int m3) nachCount,
            (decimal t, decimal m1, decimal m2, decimal m3) vypl,
            (decimal t, decimal m1, decimal m2, decimal m3) exempt,
            (decimal t, decimal m1, decimal m2, decimal m3) baz,
            (decimal t, decimal m1, decimal m2, decimal m3) nach)
        {
            xw.WriteStartElement("РасчСВ_ОПС_ОМС");
            xw.WriteAttributeString("ТарифПлат", tariff);

            xw.WriteStartElement("РасчСВ_ОПСОМС");
            WriteCountElem(xw, "КолСтрахЛицВс",  нрCount.vs, нрCount.m1, нрCount.m2, нрCount.m3);
            WriteCountElem(xw, "КолЛицНачСВВс",  nachCount.vs, nachCount.m1, nachCount.m2, nachCount.m3);
            WriteCountElem(xw, "НеПревБазОПС",   nachCount.vs, nachCount.m1, nachCount.m2, nachCount.m3);
            WriteCountElem(xw, "ПревБазОПС",      0, 0, 0, 0);
            WriteSumElem(xw, "ВыплНачислФЛ", vypl.t, vypl.m1, vypl.m2, vypl.m3);
            WriteSumElem(xw, "НеОбложенСВ",  exempt.t, exempt.m1, exempt.m2, exempt.m3);
            WriteSumElem(xw, "РасхПринВыч",  0, 0, 0, 0);
            WriteSumElem(xw, "БазНачислСВ",  baz.t, baz.m1, baz.m2, baz.m3);
            WriteSumElem(xw, "БазНеПревышОПС", baz.t, baz.m1, baz.m2, baz.m3);
            WriteSumElem(xw, "БазПревышОПС", 0, 0, 0, 0);
            WriteSumElem(xw, "НачислСВ",     nach.t, nach.m1, nach.m2, nach.m3);
            WriteSumElem(xw, "НачислСВНеПрев",nach.t, nach.m1, nach.m2, nach.m3);
            WriteSumElem(xw, "НачислСВПрев", 0, 0, 0, 0);
            xw.WriteEndElement(); // РасчСВ_ОПСОМС
            xw.WriteEndElement(); // РасчСВ_ОПС_ОМС
        }

        private static void WriteCountElem(XmlWriter xw, string name,
            int vs, int m1, int m2, int m3)
        {
            xw.WriteStartElement(name);
            xw.WriteAttributeString("КолВсегоПер", vs.ToString());
            xw.WriteAttributeString("Кол1Посл3М",  m1.ToString());
            xw.WriteAttributeString("Кол2Посл3М",  m2.ToString());
            xw.WriteAttributeString("Кол3Посл3М",  m3.ToString());
            xw.WriteEndElement();
        }

        private static void WriteSumElem(XmlWriter xw, string name,
            decimal vs, decimal m1, decimal m2, decimal m3)
        {
            xw.WriteStartElement(name);
            xw.WriteAttributeString("СумВсегоПер", F(vs));
            xw.WriteAttributeString("Сум1Посл3М",  F(m1));
            xw.WriteAttributeString("Сум2Посл3М",  F(m2));
            xw.WriteAttributeString("Сум3Посл3М",  F(m3));
            xw.WriteEndElement();
        }

        private static void WriteEmpMonth(XmlWriter xw,
            int month, string cat, decimal pay, decimal opsBase, decimal contrib)
        {
            xw.WriteStartElement("СвВыплМК");
            xw.WriteAttributeString("Месяц",      month.ToString());
            xw.WriteAttributeString("КодКатЛиц",  cat);
            xw.WriteAttributeString("СумВыпл",     F(pay));
            if (pay > 0 && opsBase > 0)
            {
                xw.WriteAttributeString("ВыплОПС",   F(opsBase));
                xw.WriteAttributeString("НачислСВ",  F(contrib));
            }
            xw.WriteEndElement();
        }

        private static string F(decimal v, string fmt = "0.00") =>
            v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);
    }
}
