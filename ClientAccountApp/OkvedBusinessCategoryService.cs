using System;
using System.Linq;

namespace ClientAccountApp
{
    public static class OkvedBusinessCategoryService
    {
        public const string EmptyCategory = "Не указано";

        public static string DetectCategory(string? okved)
        {
            if (string.IsNullOrWhiteSpace(okved))
                return EmptyCategory;

            string clean = okved.Trim();

            // Берём первые две цифры ОКВЭД: 47.11 -> 47, 49.41 -> 49
            string firstTwoDigits = new string(clean.Where(char.IsDigit).Take(2).ToArray());

            if (!int.TryParse(firstTwoDigits, out int section))
                return EmptyCategory;

            return section switch
            {
                >= 01 and <= 03 => "С/Х",
                >= 05 and <= 09 => "Добыча",
                >= 10 and <= 33 => "Производство",
                >= 35 and <= 39 => "Энергетика / ЖКХ",
                >= 41 and <= 43 => "Строительство",
                >= 45 and <= 47 => "Торговля",
                >= 49 and <= 53 => "Перевозки / логистика",
                >= 55 and <= 56 => "Гостиницы / общепит",
                >= 58 and <= 63 => "IT / связь",
                >= 64 and <= 66 => "Финансы",
                68 => "Недвижимость",
                >= 69 and <= 70 => "Юр. / бух. / консалтинг",
                71 => "Инженерные услуги",
                72 => "Наука / разработки",
                73 => "Реклама / маркетинг",
                77 => "Аренда / лизинг",
                78 => "Кадры / персонал",
                79 => "Туризм",
                >= 80 and <= 82 => "Административные услуги",
                84 => "Госсектор",
                85 => "Образование",
                >= 86 and <= 88 => "Медицина / соцуслуги",
                >= 90 and <= 93 => "Культура / спорт",
                >= 94 and <= 96 => "Услуги населению",
                _ => "Прочее"
            };
        }

        public static string NormalizeCategory(string? category)
        {
            return string.IsNullOrWhiteSpace(category)
                ? EmptyCategory
                : category.Trim();
        }
    }
}