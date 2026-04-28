using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ClientAccountApp
{
    public static class BillingSellerXmlService
    {
        private static readonly CultureInfo XmlCulture = CultureInfo.InvariantCulture;

        public static string GenerateActSellerXml(int invoiceId)
        {
            return GenerateSellerXml(
                invoiceId,
                documentType: "Акт",
                documentFormat: "XML",
                function: "ДОП",
                operationName: "Оказание услуг",
                filePrefix: "ACT");
        }

        public static string GenerateInvoiceFacturaSellerXml(int invoiceId)
        {
            return GenerateSellerXml(
                invoiceId,
                documentType: "Счёт-фактура",
                documentFormat: "XML",
                function: "СЧФ",
                operationName: "Реализация услуг",
                filePrefix: "SCHFACT");
        }
        public static string GenerateUpdSellerXml(int invoiceId)
        {
            return GenerateSellerXml(
                invoiceId,
                documentType: "Универсальный передаточный документ",
                documentFormat: "XML",
                function: "СЧФДОП",
                operationName: "Реализация товаров, работ, услуг",
                filePrefix: "UPD");
        }
        private static string GenerateSellerXml(
            int invoiceId,
            string documentType,
            string documentFormat,
            string function,
            string operationName,
            string filePrefix)
        {
            using var db = new AppDbContext();

            var invoice = db.Invoices
                .AsNoTracking()
                .FirstOrDefault(x => x.Id == invoiceId);

            if (invoice == null)
                throw new InvalidOperationException("Счёт не найден.");

            var client = db.Clients
                .AsNoTracking()
                .FirstOrDefault(x => x.Id == invoice.ClientInfoId);

            if (client == null)
                throw new InvalidOperationException("Клиент счёта не найден.");

            OrganizationProfile? organization = null;

            if (invoice.OrganizationProfileId.HasValue)
            {
                organization = db.OrganizationProfiles
                    .AsNoTracking()
                    .FirstOrDefault(x => x.Id == invoice.OrganizationProfileId.Value);
            }

            organization ??= ActiveOrganizationService.GetRequired();

            var items = db.InvoiceItems
                .AsNoTracking()
                .Where(x => x.InvoiceId == invoice.Id)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToList();

            if (items.Count == 0)
                throw new InvalidOperationException("В счёте нет строк услуг.");

            ValidateVatRates(items);

            string sellerInn = OnlyDigits(organization.Inn);
            string buyerInn = OnlyDigits(client.Inn);

            string idFile = BuildIdFile(filePrefix, sellerInn, buyerInn);
            string fileName = idFile + ".xml";
            string tempPath = Path.Combine(Path.GetTempPath(), fileName);

            if (File.Exists(tempPath))
                File.Delete(tempPath);

            decimal totalWithoutVat = items.Sum(x => x.AmountWithoutVat);
            decimal vatAmount = items.Sum(x => x.VatAmount);
            decimal totalWithVat = items.Sum(x => x.AmountWithVat);

            var document = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement("Файл",
                    new XAttribute("ИдФайл", idFile),
                    new XAttribute("ВерсФорм", "5.01"),
                    new XAttribute("ВерсПрог", "ClientAccountApp"),

                    new XElement("СвУчДокОбор",
                        new XAttribute("ИдОтпр", BuildEdoParticipantId(sellerInn, "SELLER")),
                        new XAttribute("ИдПол", BuildEdoParticipantId(buyerInn, "BUYER"))
                    ),

                    new XElement("Документ",
                        new XAttribute("КНД", "1115131"),
                        new XAttribute("Функция", function),
                        new XAttribute("ПоФактХЖ", operationName),
                        new XAttribute("НаимДокОпр", documentType),
                        new XAttribute("ДатаИнфПр", DateTime.Today.ToString("dd.MM.yyyy")),
                        new XAttribute("ВремИнфПр", DateTime.Now.ToString("HH.mm.ss")),
                        new XAttribute("НаимЭконСубСост", SafeRequired(organization.Name, "Организация")),

                        CreateInvoiceInfoElement(invoice, organization, client),

                        CreateItemsTableElement(items, totalWithoutVat, vatAmount, totalWithVat),

                        (function == "ДОП" || function == "СЧФДОП")
    ? CreateTransferInfoElement(invoice)
    : null,

                        CreateSignerElement(organization)
                    )
                )
            );

            document.Save(tempPath);

            if (function != "СЧФДОП")
            {
                string xsdPath = documentType == "Акт"
                    ? BillingXsdValidationService.GetActSellerSchemaPath()
                    : BillingXsdValidationService.GetInvoiceFacturaSellerSchemaPath();

                BillingXsdValidationService.ValidateOrThrow(tempPath, xsdPath);
            }

            return tempPath;
        }

        private static XElement CreateInvoiceInfoElement(
            Invoice invoice,
            OrganizationProfile organization,
            ClientInfo client)
        {
            return new XElement("СвСчФакт",
                new XAttribute("НомерСчФ", SafeRequired(invoice.InvoiceNumber, $"СЧ-{invoice.Id}")),
                new XAttribute("ДатаСчФ", invoice.InvoiceDate.ToString("dd.MM.yyyy")),
                new XAttribute("КодОКВ", "643"),

                CreateParticipantElement("СвПрод", organization),
                CreateParticipantElement("СвПокуп", client)
            );
        }

        private static XElement CreateItemsTableElement(
            System.Collections.Generic.List<InvoiceItem> items,
            decimal totalWithoutVat,
            decimal vatAmount,
            decimal totalWithVat)
        {
            var table = new XElement("ТаблСчФакт");

            int index = 1;

            foreach (var item in items)
            {
                string vatRateText = FormatVatRateForXml(item.VatRate);

                var itemElement = new XElement("СведТов",
                    new XAttribute("НомСтр", index.ToString()),
                    new XAttribute("НаимТов", SafeRequired(item.ServiceName, "Услуга")),
                    new XAttribute("ОКЕИ_Тов", "796"),
                    new XAttribute("КолТов", FormatDecimal(item.Quantity)),
                    new XAttribute("ЦенаТов", FormatDecimal(item.UnitPrice)),
                    new XAttribute("СтТовБезНДС", FormatDecimal(item.AmountWithoutVat)),
                    new XAttribute("НалСт", vatRateText),
                    new XAttribute("СтТовУчНал", FormatDecimal(item.AmountWithVat)),

                    new XElement("Акциз",
                        new XElement("БезАкциз", "без акциза")
                    ),

                    CreateVatElement("СумНал", item.VatAmount, item.VatRate)
                );

                table.Add(itemElement);
                index++;
            }

            table.Add(new XElement("ВсегоОпл",
                new XAttribute("СтТовБезНДСВсего", FormatDecimal(totalWithoutVat)),
                new XAttribute("СтТовУчНалВсего", FormatDecimal(totalWithVat)),
                CreateVatElement("СумНалВсего", vatAmount, GetTotalVatRate(items))
            ));

            return table;
        }

        private static XElement CreateTransferInfoElement(Invoice invoice)
        {
            var transfer = new XElement("СвПродПер",
                new XElement("СвПер",
                    new XAttribute("СодОпер", "Оказаны услуги"),
                    new XAttribute("ДатаПер", DateTime.Today.ToString("dd.MM.yyyy")),
                    new XElement("ОснПер",
                        new XAttribute("НаимОсн", "Счёт"),
                        new XAttribute("НомОсн", SafeRequired(invoice.InvoiceNumber, $"СЧ-{invoice.Id}")),
                        new XAttribute("ДатаОсн", invoice.InvoiceDate.ToString("dd.MM.yyyy"))
                    )
                )
            );

            return transfer;
        }

        private static XElement CreateParticipantElement(string elementName, OrganizationProfile organization)
        {
            var participant = new XElement(elementName,
                new XElement("ИдСв",
                    CreateIdentityElement(
                        name: SafeRequired(organization.Name, "Организация"),
                        inn: organization.Inn,
                        kpp: organization.Kpp,
                        directorOrFio: organization.DirectorName)
                )
            );

            if (!string.IsNullOrWhiteSpace(organization.LegalAddress))
            {
                participant.Add(CreateAddressElement(organization.LegalAddress));
            }

            if (!string.IsNullOrWhiteSpace(organization.Phone) ||
                !string.IsNullOrWhiteSpace(organization.Email))
            {
                participant.Add(new XElement("Контакт",
                    OptionalAttribute("Тлф", organization.Phone),
                    OptionalAttribute("ЭлПочта", organization.Email)
                ));
            }

            if (!string.IsNullOrWhiteSpace(organization.SettlementAccount) ||
                !string.IsNullOrWhiteSpace(organization.BankName) ||
                !string.IsNullOrWhiteSpace(organization.BankBic))
            {
                participant.Add(new XElement("БанкРекв",
                    OptionalAttribute("НомерСчета", organization.SettlementAccount),
                    new XElement("СвБанк",
                        OptionalAttribute("НаимБанк", organization.BankName),
                        OptionalAttribute("БИК", organization.BankBic),
                        OptionalAttribute("КорСчет", organization.CorrespondentAccount)
                    )
                ));
            }

            return participant;
        }

        private static XElement CreateParticipantElement(string elementName, ClientInfo client)
        {
            var participant = new XElement(elementName,
                new XElement("ИдСв",
                    CreateIdentityElement(
                        name: SafeRequired(client.Name, "Клиент"),
                        inn: client.Inn,
                        kpp: "",
                        directorOrFio: client.DirectorFullName)
                )
            );

            if (!string.IsNullOrWhiteSpace(client.Address))
            {
                participant.Add(CreateAddressElement(client.Address));
            }

            return participant;
        }

        private static XElement CreateIdentityElement(
            string name,
            string? inn,
            string? kpp,
            string? directorOrFio)
        {
            string digitsInn = OnlyDigits(inn);

            if (digitsInn.Length == 12)
            {
                return new XElement("СвИП",
                    new XAttribute("ИННФЛ", digitsInn),
                    CreateFioElement(ChooseFioText(directorOrFio, name))
                );
            }

            if (digitsInn.Length == 10)
            {
                return new XElement("СвЮЛУч",
                    new XAttribute("НаимОрг", name),
                    new XAttribute("ИННЮЛ", digitsInn),
                    OptionalAttribute("КПП", OnlyDigits(kpp))
                );
            }

            return new XElement("СвЮЛУч",
                new XAttribute("НаимОрг", name),
                new XAttribute("ДефИННЮЛ", "-")
            );
        }

        private static XElement CreateAddressElement(string address)
        {
            return new XElement("Адрес",
                new XElement("АдрИнф",
                    new XAttribute("КодСтр", "643"),
                    new XAttribute("АдрТекст", SafeRequired(address, "Адрес не указан"))
                )
            );
        }

        private static XElement CreateSignerElement(OrganizationProfile organization)
        {
            string inn = OnlyDigits(organization.Inn);
            string signerName = ChooseFioText(organization.DirectorName, organization.Name);
            XElement identity;

            if (inn.Length == 12)
            {
                identity = new XElement("ИП",
                    new XAttribute("ИННФЛ", inn),
                    CreateFioElement(signerName)
                );
            }
            else if (inn.Length == 10)
            {
                identity = new XElement("ЮЛ",
                    new XAttribute("ИННЮЛ", inn),
                    new XAttribute("НаимОрг", SafeRequired(organization.Name, "Организация")),
                    new XAttribute("Должн", SafeRequired(organization.DirectorPosition, "Руководитель")),
                    CreateFioElement(signerName)
                );
            }
            else
            {
                identity = new XElement("ФЛ",
                    CreateFioElement(signerName)
                );
            }

            return new XElement("Подписант",
                new XAttribute("ОблПолн", "6"),
                new XAttribute("Статус", "1"),
                new XAttribute("ОснПолн", "Должностные обязанности"),
                identity
            );
        }

        private static XElement CreateFioElement(string? fullName)
        {
            string safeFullName = SafeRequired(fullName, "Иванов Иван Иванович");

            var parts = safeFullName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            string lastName = parts.Count > 0 ? parts[0] : "Иванов";
            string firstName = parts.Count > 1 ? parts[1] : "Иван";
            string patronymic = parts.Count > 2 ? string.Join(" ", parts.Skip(2)) : "";

            var fio = new XElement("ФИО",
                new XAttribute("Фамилия", lastName),
                new XAttribute("Имя", firstName)
            );

            if (!string.IsNullOrWhiteSpace(patronymic))
                fio.Add(new XAttribute("Отчество", patronymic));

            return fio;
        }

        private static XElement CreateVatElement(string elementName, decimal vatAmount, decimal vatRate)
        {
            if (vatRate <= 0)
            {
                return new XElement(elementName,
                    new XElement("БезНДС", "без НДС")
                );
            }

            return new XElement(elementName,
                new XElement("СумНал", FormatDecimal(vatAmount))
            );
        }

        private static decimal GetTotalVatRate(System.Collections.Generic.List<InvoiceItem> items)
        {
            var rates = items
                .Select(x => x.VatRate)
                .Distinct()
                .ToList();

            if (rates.Count == 1)
                return rates[0];

            return rates.Any(x => x > 0)
                ? 20m
                : 0m;
        }

        private static void ValidateVatRates(System.Collections.Generic.List<InvoiceItem> items)
        {
            foreach (var item in items)
            {
                _ = FormatVatRateForXml(item.VatRate);
            }
        }

        private static string FormatVatRateForXml(decimal vatRate)
        {
            if (vatRate <= 0)
                return "без НДС";

            if (vatRate == 0m)
                return "0%";

            if (vatRate == 10m)
                return "10%";

            if (vatRate == 18m)
                return "18%";

            if (vatRate == 20m)
                return "20%";
            if (vatRate == 22m)
                return "22%";

            throw new InvalidOperationException(
    $"Ставка НДС {vatRate:0.##}% не поддерживается текущей XML-выгрузкой. Доступны 0%, 10%, 18%, 20%, 22% и без НДС.");
        }

        private static string FormatDecimal(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero)
                .ToString("0.00", XmlCulture);
        }

        private static string BuildIdFile(string prefix, string sellerInn, string buyerInn)
        {
            string seller = string.IsNullOrWhiteSpace(sellerInn) ? "SELLER" : sellerInn;
            string buyer = string.IsNullOrWhiteSpace(buyerInn) ? "BUYER" : buyerInn;

            string raw = $"ON_NSCHFDOPPR_{prefix}_{seller}_{buyer}_{DateTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}";

            return SanitizeIdFile(raw);
        }

        private static string BuildEdoParticipantId(string inn, string fallback)
        {
            string safeInn = string.IsNullOrWhiteSpace(inn)
                ? fallback
                : inn;

            string value = "2BM-" + safeInn;

            if (value.Length < 4)
                value = "2BM-" + fallback;

            if (value.Length > 46)
                value = value.Substring(0, 46);

            return value;
        }

        private static string SanitizeIdFile(string value)
        {
            string safe = Regex.Replace(value, @"[^A-Za-zА-Яа-я0-9_\-]", "_");

            if (safe.Length > 240)
                safe = safe.Substring(0, 240);

            return safe;
        }

        private static string OnlyDigits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            return new string(value.Where(char.IsDigit).ToArray());
        }

        private static string SafeRequired(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim();
        }

        private static XAttribute? OptionalAttribute(string name, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return new XAttribute(name, value.Trim());
        }

        private static string ChooseFioText(string? preferred, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(preferred))
                return preferred.Trim();

            if (!string.IsNullOrWhiteSpace(fallback))
                return fallback.Trim();

            return "Иванов Иван Иванович";
        }
    }
}