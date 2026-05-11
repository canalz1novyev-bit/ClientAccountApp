// Добавить в папку Models/

using System;
using System.Collections.Generic;
using System.Linq;

namespace ClientAccountApp.Models
{
    public enum BankOperationType { Credit, Debit }

    public class BankStatementOperation
    {
        public int LocalIndex { get; set; }
        public DateTime Date { get; set; }
        public string DocNumber { get; set; } = "";
        public BankOperationType OperationType { get; set; }
        public decimal Amount { get; set; }

        // Плательщик
        public string PayerAccount { get; set; } = "";
        public string PayerName { get; set; } = "";
        public string PayerINN { get; set; } = "";
        public string PayerKPP { get; set; } = "";
        public string PayerBank { get; set; } = "";
        public string PayerBIC { get; set; } = "";

        // Получатель
        public string ReceiverAccount { get; set; } = "";
        public string ReceiverName { get; set; } = "";
        public string ReceiverINN { get; set; } = "";
        public string ReceiverKPP { get; set; } = "";
        public string ReceiverBank { get; set; } = "";
        public string ReceiverBIC { get; set; } = "";

        // Назначение
        public string PaymentPurpose { get; set; } = "";

        // ─── НДС-разметка (заполняется вручную) ──────────────────────────
        public bool IsMarkedForVatBook { get; set; }
        public string VatBookType { get; set; } = "";  // "Покупки" | "Продажи"
        public string VatInvoiceNumber { get; set; } = "";
        public DateTime? VatInvoiceDate { get; set; }
        public decimal VatRate { get; set; } = 20m;
        public decimal VatAmount { get; set; }
        public decimal AmountWithoutVat => Amount > VatAmount && VatAmount > 0 ? Amount - VatAmount : Amount;

        // Сопоставление с клиентом
        public int? MatchedClientId { get; set; }
        public string MatchedClientName { get; set; } = "";

        // ─── Отображение (для ListView Binding) ──────────────────────────
        public string DateDisplay => Date == default ? "" : Date.ToString("dd.MM.yy");
        public string TypeDisplay => OperationType == BankOperationType.Credit ? "▲ Приход" : "▼ Расход";
        public string AmountDisplay => OperationType == BankOperationType.Credit
                                             ? $"+{Amount:N2}" : $"-{Amount:N2}";
        public string CounterpartyName => OperationType == BankOperationType.Credit ? PayerName : ReceiverName;
        public string CounterpartyINN => OperationType == BankOperationType.Credit ? PayerINN : ReceiverINN;
        public string CounterpartyKPP => OperationType == BankOperationType.Credit ? PayerKPP : ReceiverKPP;
        public string PurposeShort => PaymentPurpose.Length > 80
                                             ? PaymentPurpose[..77] + "…" : PaymentPurpose;
        public string VatMarkShort => IsMarkedForVatBook
                                             ? $"СФ {VatInvoiceNumber} | {VatRate}% = {VatAmount:N2} ₽"
                                             : "—";
        public string MarkButtonLabel => IsMarkedForVatBook ? "✎ Изменить" : "+ Разметить";
    }

    public class BankStatement
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public string AccountNumber { get; set; } = "";
        public string BankName { get; set; } = "";
        // Реквизиты владельца счёта (автоопределяются из операций)
        public string OwnerINN { get; set; } = "";
        public string OwnerKPP { get; set; } = "";  
        public string OwnerName { get; set; } = "";
        public bool OwnerIsIP => OwnerINN.Length == 12; // ИП=12 цифр, ЮЛ=10
        public decimal OpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal TotalDebit { get; set; }
        public List<BankStatementOperation> Operations { get; set; } = new();

        public string PeriodDisplay => $"{DateFrom:dd.MM.yyyy} – {DateTo:dd.MM.yyyy}";
        public int OperationCount => Operations.Count;
        public int CreditCount => Operations.Count(o => o.OperationType == BankOperationType.Credit);
        public int DebitCount => Operations.Count(o => o.OperationType == BankOperationType.Debit);
    }
}