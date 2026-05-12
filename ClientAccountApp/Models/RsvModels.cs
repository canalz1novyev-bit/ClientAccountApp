// Модели данных для Мастера РСВ
using System.Collections.Generic;
using System;

namespace ClientAccountApp.Models
{
    // ── Сотрудник: разделённые потоки НР и ПВ ────────────────────────────────

    public class RsvEmployee
    {
        // Из ПФС
        public string SNILS      { get; set; } = "";
        public string INN        { get; set; } = "";
        public string Surname    { get; set; } = "";
        public string Name       { get; set; } = "";
        public string Patronymic { get; set; } = "";

        // Выплаты по НР (основной тариф: оклад, премии и т.д.)
        public decimal PayNR1 { get; set; }
        public decimal PayNR2 { get; set; }
        public decimal PayNR3 { get; set; }

        // Выплаты по ПВ (льготный тариф: сезонные, временные)
        public decimal PayPV1 { get; set; }
        public decimal PayPV2 { get; set; }
        public decimal PayPV3 { get; set; }

        // Необлагаемые суммы из НР-части (вводится вручную)
        public decimal ExemptNR1 { get; set; }
        public decimal ExemptNR2 { get; set; }
        public decimal ExemptNR3 { get; set; }

        // Категория для НР-части
        public string CategoryCodeNR { get; set; } = "НР";

        // Доп. данные для ДанФЛПолуч (опционально)
        public string BirthDate   { get; set; } = "";
        public string Gender      { get; set; } = "";
        public string Citizenship { get; set; } = "643";
        public string DocType     { get; set; } = "21";
        public string DocSeries   { get; set; } = "";

        // ── Вычисляемые базы ОПС ────────────────────────────────────────────
        public decimal OpsBaseNR1 => Math.Max(0, PayNR1 - ExemptNR1);
        public decimal OpsBaseNR2 => Math.Max(0, PayNR2 - ExemptNR2);
        public decimal OpsBaseNR3 => Math.Max(0, PayNR3 - ExemptNR3);

        // ПВ — необлагаемых нет (премиальные выплаты полностью облагаются)
        public decimal OpsBasePV1 => PayPV1;
        public decimal OpsBasePV2 => PayPV2;
        public decimal OpsBasePV3 => PayPV3;

        // Суммарные выплаты (для отображения и итогов блока НР)
        public decimal TotalPayNR    => PayNR1 + PayNR2 + PayNR3;
        public decimal TotalPayPV    => PayPV1 + PayPV2 + PayPV3;
        public decimal TotalPay      => TotalPayNR + TotalPayPV;
        public decimal TotalExemptNR => ExemptNR1 + ExemptNR2 + ExemptNR3;
        public decimal TotalBaseNR   => OpsBaseNR1 + OpsBaseNR2 + OpsBaseNR3;
        public decimal TotalBasePV   => OpsBasePV1 + OpsBasePV2 + OpsBasePV3;

        // Начисленные взносы (заполняются CalculateContributions)
        public decimal ContribNR1 { get; set; }
        public decimal ContribNR2 { get; set; }
        public decimal ContribNR3 { get; set; }
        public decimal ContribPV1 { get; set; }
        public decimal ContribPV2 { get; set; }
        public decimal ContribPV3 { get; set; }

        public decimal TotalContribNR => ContribNR1 + ContribNR2 + ContribNR3;
        public decimal TotalContribPV => ContribPV1 + ContribPV2 + ContribPV3;
        public decimal TotalContrib   => TotalContribNR + TotalContribPV;

        // Обратная совместимость — общие выплаты по месяцу (для PFS merge)
        public decimal Pay1 => PayNR1 + PayPV1;
        public decimal Pay2 => PayNR2 + PayPV2;
        public decimal Pay3 => PayNR3 + PayPV3;

        // Для отображения в ListView
        public string FullName => $"{Surname} {Name} {Patronymic}".Trim();
        public bool HasPvPayments => TotalPayPV > 0;
    }

    // ── Настройки генерации ───────────────────────────────────────────────────

    public class RsvSettings
    {
        // Организация
        public string OrgINN       { get; set; } = "";
        public string OrgKPP       { get; set; } = "";
        public string OrgName      { get; set; } = "";
        public string Phone        { get; set; } = "";
        public int    AvgHeadcount { get; set; } = 1;

        // Подписант
        public string DirectorSurname     { get; set; } = "";
        public string DirectorName        { get; set; } = "";
        public string DirectorPatronymic  { get; set; } = "";

        // Период
        public int    Year    { get; set; } = DateTime.Now.Year;
        public int    Quarter { get; set; } = 1;
        public string KodNO   { get; set; } = "";

        // Основной тариф (НР)
        public string  NrTariffCode { get; set; } = "01";  // 01, 06...
        public decimal NrRate       { get; set; } = 0.30m; // 30%
        public string  NrCatCode    { get; set; } = "НР";  // НР, ОДИТ...

        // Льготный тариф (ПВ) — активен только если есть ПВ-выплаты
        public string  PvTariffCode { get; set; } = "32";  // 32 = АПК
        public decimal PvRate       { get; set; } = 0.15m; // 15%

        // Платёж
        public string  OKTMO { get; set; } = "";
        public string  KBK   { get; set; } = "18210201000011000160";

        // Фактически уплачено (0 = авторасчёт = начислено)
        public decimal PaidM1 { get; set; }
        public decimal PaidM2 { get; set; }
        public decimal PaidM3 { get; set; }

        // IT-льгота (ПравТариф3_18.1.427) — для тарифа 06
        public bool    HasItBenefit     { get; set; }
        public string  ItBenefitRegDate { get; set; } = "";
        public string  ItBenefitRegNum  { get; set; } = "";
        public decimal ItIncomePer      { get; set; }
        public decimal ItItIncomePer    { get; set; }

        // АПК-льгота (ПравТариф13.2.427) — для тарифа 32
        public bool    HasAgrBenefit  { get; set; }
        public decimal AgrIncomePer   { get; set; }  // Дох26.2Пер
        public decimal AgrIncomeBasePer { get; set; } // ДохОснПер

        // Автоматическая разбивка НР/ПВ по правилу 1.5 × МРОТ
        // Суммы до порога = НР (основной тариф), выше = ПВ (льготный 15%)
        public bool    AutoSplitPv   { get; set; } = true;
        public decimal MROTPerMonth  { get; set; } = 27093m; // Федеральный МРОТ РФ 2026

        public decimal PvThreshold   => Math.Round(MROTPerMonth * 1.5m, 2);

        // Период → код СБИС
        public string PeriodCode => Quarter switch
        {
            1 => "21",
            2 => "31",
            3 => "33",
            _ => "34"
        };
    }

    // ── Краткая информация о ПФС-файле ───────────────────────────────────────

    public class PfsFileInfo
    {
        public string FilePath       { get; set; } = "";
        public int    MonthNum       { get; set; }
        public int    QuarterMonth   { get; set; }
        public string OrgINN         { get; set; } = "";
        public string OrgKPP         { get; set; } = "";
        public string OrgName        { get; set; } = "";
        public string KodNO          { get; set; } = "";
        public int    Year           { get; set; }
        public int    EmployeeCount  { get; set; }
        public string StatusText     { get; set; } = "";
    }
}
