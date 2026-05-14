using System;
using System.Collections.Generic;

namespace ClientAccountApp
{
    /// <summary>
    /// Метаданные одного типа договора: имя, шаблон, роли сторон, доступные документы.
    /// </summary>
    public sealed class ContractTypeInfo
    {
        public string Key { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Description { get; init; } = "";
        public string TemplateFileName { get; init; } = "";
        public string Party1Role { get; init; } = "Исполнитель";
        public string Party2Role { get; init; } = "Заказчик";
        public string DefaultSubject { get; init; } = "";
        public bool OffersUPD { get; init; } = true;
        public bool OffersInvoice { get; init; } = true;
    }

    /// <summary>
    /// Реестр всех поддерживаемых типов договоров.
    /// TemplateFileName — имя .docx файла в папке Templates/Contracts/.
    /// Если файла нет, мастер предупредит и предложит использовать базовый шаблон.
    /// </summary>
    public static class ContractTypeDefinitions
    {
        public static readonly IReadOnlyList<ContractTypeInfo> All = new[]
        {
            new ContractTypeInfo
            {
                Key = "services",
                DisplayName = "Оказание услуг",
                Description = "ИТ, бухгалтерия, консалтинг, обслуживание",
                TemplateFileName = "contract_services.docx",
                Party1Role = "Исполнитель",
                Party2Role = "Заказчик",
                DefaultSubject = "Оказание услуг по ___________________",
                OffersUPD = true,
                OffersInvoice = true
            },
            new ContractTypeInfo
            {
                Key = "supply",
                DisplayName = "Поставка товаров",
                Description = "Продажа и поставка материальных ценностей",
                TemplateFileName = "contract_supply.docx",
                Party1Role = "Поставщик",
                Party2Role = "Покупатель",
                DefaultSubject = "Поставка товаров согласно спецификации",
                OffersUPD = true,
                OffersInvoice = true
            },
            new ContractTypeInfo
            {
                Key = "lease",
                DisplayName = "Аренда",
                Description = "Аренда помещений, оборудования, транспорта",
                TemplateFileName = "contract_lease.docx",
                Party1Role = "Арендодатель",
                Party2Role = "Арендатор",
                DefaultSubject = "Аренда ___________________",
                OffersUPD = false,
                OffersInvoice = true
            },
            new ContractTypeInfo
            {
                Key = "work",
                DisplayName = "Подряд",
                Description = "Строительство, монтаж, разработка, производство",
                TemplateFileName = "contract_work.docx",
                Party1Role = "Подрядчик",
                Party2Role = "Заказчик",
                DefaultSubject = "Выполнение работ по ___________________",
                OffersUPD = true,
                OffersInvoice = true
            },
            new ContractTypeInfo
            {
                Key = "agency",
                DisplayName = "Агентский",
                Description = "Представление интересов, посреднические услуги",
                TemplateFileName = "contract_agency.docx",
                Party1Role = "Агент",
                Party2Role = "Принципал",
                DefaultSubject = "Агентские услуги по ___________________",
                OffersUPD = false,
                OffersInvoice = true
            },
            new ContractTypeInfo
            {
                Key = "nda",
                DisplayName = "НДА",
                Description = "Соглашение о конфиденциальности",
                TemplateFileName = "contract_nda.docx",
                Party1Role = "Сторона 1",
                Party2Role = "Сторона 2",
                DefaultSubject = "Соблюдение конфиденциальности",
                OffersUPD = false,
                OffersInvoice = false
            }
        };

        public static ContractTypeInfo GetByKey(string key)
        {
            foreach (var t in All)
                if (t.Key == key) return t;
            return All[0];
        }

        /// <summary>
        /// Возвращает абсолютный путь к шаблону типа договора.
        /// Если специализированный шаблон не найден — возвращает путь к базовому шаблону.
        /// </summary>
        public static string ResolveTemplatePath(string contractTypeKey)
        {
            var info = GetByKey(contractTypeKey);
            string dir = System.IO.Path.Combine(AppContext.BaseDirectory, "Templates", "Contracts");
            string specific = System.IO.Path.Combine(dir, info.TemplateFileName);
            if (System.IO.File.Exists(specific)) return specific;

            // Fallback: базовый шаблон (оказание услуг / общий)
            string fallback = System.IO.Path.Combine(AppContext.BaseDirectory, "Templates", "contract_template_niatek.docx");
            if (System.IO.File.Exists(fallback)) return fallback;

            throw new System.IO.FileNotFoundException(
                $"Шаблон договора не найден.\n\nОжидаемое расположение:\n{specific}\n\nБазовый шаблон:\n{fallback}\n\nСоздайте папку Templates\\Contracts\\ и разместите в ней шаблоны.");
        }
    }
}
