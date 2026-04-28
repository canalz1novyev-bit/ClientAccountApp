using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ClientAccountApp
{
    public static class BillingInvoiceFacturaPdfService
    {
        private static readonly CultureInfo Ru = new("ru-RU");

        public static string Generate(int invoiceId)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            using var db = new AppDbContext();

            var invoice = db.Invoices.AsNoTracking().First(x => x.Id == invoiceId);
            var client = db.Clients.AsNoTracking().First(x => x.Id == invoice.ClientInfoId);

            var org = invoice.OrganizationProfileId.HasValue
                ? db.OrganizationProfiles.AsNoTracking().First(x => x.Id == invoice.OrganizationProfileId.Value)
                : ActiveOrganizationService.GetRequired();

            var items = db.InvoiceItems
                .AsNoTracking()
                .Where(x => x.InvoiceId == invoice.Id)
                .OrderBy(x => x.Id)
                .ToList();

            string path = Path.Combine(
                Path.GetTempPath(),
                $"Счет-фактура_{invoice.InvoiceNumber}_{DateTime.Now:yyyyMMddHHmmss}.pdf");

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(18);
                    page.DefaultTextStyle(x => x.FontSize(7).FontFamily("Arial"));

                    page.Content().Column(col =>
                    {
                        col.Spacing(4);

                        col.Item().AlignCenter().Text($"СЧЕТ-ФАКТУРА № {invoice.InvoiceNumber} от {invoice.InvoiceDate:dd.MM.yyyy}")
                            .Bold().FontSize(12);

                        col.Item().Text("ИСПРАВЛЕНИЕ № -- от --").FontSize(7);

                        col.Item().Text($"Продавец: {Dash(org.Name)}");
                        col.Item().Text($"Адрес: {Dash(org.LegalAddress)}");
                        col.Item().Text($"ИНН/КПП продавца: {Dash(org.Inn)} / {Dash(org.Kpp)}");
                        col.Item().Text("Грузоотправитель и его адрес: он же");
                        col.Item().Text("Грузополучатель и его адрес: --");
                        col.Item().Text("К платежно-расчетному документу № -- от --");
                        col.Item().Text($"Покупатель: {Dash(client.Name)}");
                        col.Item().Text($"Адрес: {Dash(client.Address)}");
                        col.Item().Text($"ИНН/КПП покупателя: {Dash(client.Inn)} / --");
                        col.Item().Text("Валюта: наименование, код Российский рубль, 643");

                        col.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(20);
                                columns.RelativeColumn(3);
                                columns.ConstantColumn(35);
                                columns.ConstantColumn(35);
                                columns.ConstantColumn(45);
                                columns.ConstantColumn(55);
                                columns.ConstantColumn(40);
                                columns.ConstantColumn(50);
                                columns.ConstantColumn(55);
                                columns.ConstantColumn(45);
                                columns.ConstantColumn(45);
                            });

                            Header(table, "№");
                            Header(table, "Наименование товара (работ, услуг)");
                            Header(table, "Ед.");
                            Header(table, "Кол-во");
                            Header(table, "Цена");
                            Header(table, "Стоимость без НДС");
                            Header(table, "НДС");
                            Header(table, "Сумма НДС");
                            Header(table, "Стоимость с НДС");
                            Header(table, "Страна");
                            Header(table, "РНПТ");

                            int index = 1;

                            foreach (var item in items)
                            {
                                Cell(table, index.ToString());
                                Cell(table, Dash(item.ServiceName));
                                Cell(table, Dash(item.Unit));
                                Cell(table, item.Quantity.ToString("N2", Ru));
                                Cell(table, Money(item.UnitPrice));
                                Cell(table, Money(item.AmountWithoutVat));
                                Cell(table, VatRate(item.VatRate));
                                Cell(table, Money(item.VatAmount));
                                Cell(table, Money(item.AmountWithVat));
                                Cell(table, "--");
                                Cell(table, "--");

                                index++;
                            }

                            decimal totalWithoutVat = items.Sum(x => x.AmountWithoutVat);
                            decimal vat = items.Sum(x => x.VatAmount);
                            decimal total = items.Sum(x => x.AmountWithVat);

                            Cell(table, "");
                            Cell(table, "Всего к оплате", bold: true);
                            Cell(table, "");
                            Cell(table, "");
                            Cell(table, "");
                            Cell(table, Money(totalWithoutVat), bold: true);
                            Cell(table, "X");
                            Cell(table, Money(vat), bold: true);
                            Cell(table, Money(total), bold: true);
                            Cell(table, "X");
                            Cell(table, "X");
                        });

                        col.Item().PaddingTop(10).Row(row =>
                        {
                            row.RelativeItem().Text($"Руководитель организации ____________ {Dash(org.DirectorName)}");
                            row.RelativeItem().Text("Главный бухгалтер ____________");
                        });

                        col.Item().PaddingTop(5).Text("Индивидуальный предприниматель ____________").FontSize(7);
                    });
                });
            }).GeneratePdf(path);

            return path;
        }

        private static void Header(TableDescriptor table, string text)
        {
            table.Cell().Border(0.5f).Background(Colors.Grey.Lighten3).Padding(2)
                .AlignCenter().AlignMiddle().Text(text).Bold().FontSize(6);
        }

        private static void Cell(TableDescriptor table, string text, bool bold = false)
        {
            var t = table.Cell().Border(0.5f).Padding(2).AlignMiddle().Text(text).FontSize(6);
            if (bold)
                t.Bold();
        }

        private static string Money(decimal value)
        {
            return value.ToString("N2", Ru);
        }

        private static string VatRate(decimal value)
        {
            return value <= 0 ? "без НДС" : value.ToString("0.##", Ru) + "%";
        }

        private static string Dash(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "--" : value.Trim();
        }
    }
}