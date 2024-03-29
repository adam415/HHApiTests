﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using RestSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TestingApp
{
    internal static class Program
    {
        private static (string Error, IRestResponse Response) GetVacancies(string text, uint applies = 1)
        {
            var client = new RestClient("https://api.hh.ru/");

            var requestUri = "vacancies?no_magic=true&";
            var paramAppend = $"text={Uri.EscapeDataString(text)}&";

            for (uint applied = 0; applied < applies; applied++)
                requestUri += paramAppend;

            var getRequest = new RestRequest(requestUri, Method.GET);

            var getResponse = client.Execute(getRequest);

            if (getResponse.IsSuccessful)
                return (null, getResponse);
            else
                return (getResponse.StatusDescription, getResponse);
        }

        private static (string Error, List<Dictionary<string, object>> Vacancies) GetParsedVacancies(string text)
        {
            var (error, response) = GetVacancies(text);

            if (error != null) return (error, null);

            var contentDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content)
                ?? new Dictionary<string, object>();

            if (contentDictionary.ContainsKey("items") && contentDictionary["items"] is JArray itemsJArray)
            {
                var itemsList = itemsJArray.ToObject<List<Dictionary<string, object>>>();
                return (null, itemsList);
            }
            else
                return ("Содержимое ответа не содержит массив items.", null);
        }

        private static string Test_MultipleTextApplies(uint applies, bool mustFail)
        {
            var (error, _) = GetVacancies("", applies);

            bool failed = error != null;

            return string.Format(
                    "[{0}]: The property 'text' was applied {1} time(s). Error expected: {2}. Error: {3}",
                    failed == mustFail ? "PASSED" : "FAILED",
                    applies,
                    mustFail ? "YES" : "NO",
                    error ?? "<There was no error>"
                );
        }

        private static IEnumerable<string> Test_TextSize(int initial = 32400, int step = 2)
        {
            string text = new string('A', initial);
            int length = initial;
            const int limit = 32600 + 500;
            while (length <= limit)
            {
                length += step; text += new string('A', step);

                var (error, _) = GetVacancies(text);

                yield return string.Format(
                    "[{0}]: Requesting response attempt with 'text' length: {1}. Error: {2}",
                    error == null ? "PASSED (partial)" : "FAIL (partial)",
                    length,
                    error ?? "<No Error>"
                    );

                if (error != null) break;
            }
            
            yield return
                length > limit ?
                $"\r\n[ALL SUCCEEDED]: The response did not fail with 'text' length up until: {limit}." :
                $"\r\n[LIMITED SUCCEDED]: The response failed with 'text' length {length}";
        }

        private static string Test_TextSizeV2()
        {
            int ignoredFrom = -1, failedFrom = -1;

            string text = new string('A', 20);
            const int step = 2, limit = 33_000; // 32600
            
            while (text.Length <= limit)
            {
                text += new string('A', step);

                var (error, vacancies) = GetParsedVacancies(text);

                if (ignoredFrom == -1 && error == null && vacancies.Count > 0)
                    { ignoredFrom = text.Length; text = new string('A', 32400); }

                if (error != null)
                {
                    failedFrom = text.Length;

                    return string.Format(
                        "[INFO]: Field 'text' got ignored from length {0}. Request failed from length {1} with error: {2}",
                        ignoredFrom != -1 ? $"{ignoredFrom}" : "<Never ignored>",
                        failedFrom,
                        error
                    );
                }
            }

            return string.Format(
                "[INFO]: Field 'text' got ignored from length {0}. Request did not fail with length up until {1}.",
                ignoredFrom != -1 ? $"{ignoredFrom}" : "<Never ignored>",
                limit
            );
        }

        private static IEnumerable<string> Test_Critical()
        {
            IEnumerable<string> requests = new List<string>
            {
                "I like this site because <script>alert('Injected!');</script> teaches me a lot"
            };

            foreach (var req in requests)
            {
                var (err, vac) = GetVacancies(req);
                yield return "[INFO] Cannot test JS injections while black-box testing.";
            }
        }

        private static readonly string[][] SetOfStrings = new string[][]
        {
            new[] { "Coca", "Cola" },
            new[] { "PHP", "SQL" },
            new[] { "Директор", "Руководитель" },
            new[] { "Бухгалтер", "Cola", "Fanta", "Sprite", "KFC" },
        };

        private static bool VacancyContainsWord(this Dictionary<string, object> vacancy, string word)
        {
            var jsonedVacancy = JsonConvert.SerializeObject(vacancy);
            return jsonedVacancy.ToLower().Contains(word.ToLower());
        }

        private static bool VacancyContainsAllWords(this Dictionary<string, object> vacancy, IEnumerable<string> words)
        {
            var jsonedVacancy = JsonConvert.SerializeObject(vacancy);
            return words.All(word => jsonedVacancy.ToLower().Contains(word.ToLower()));
        }

        private static IEnumerable<string> Test_Content_OneExactWord(IEnumerable<string> set)
        {
            foreach (var word in set)
            {
                var (error, vacancies) = GetParsedVacancies($"!{word}");
                int passed = vacancies.Count(vac => vac.VacancyContainsWord(word));

                yield return string.Format(
                        "[{0}]: Requested \"{1}\". Word was found in {2} out of {3}.",
                        passed == vacancies.Count ? "PASSED" : "FAIL",
                        word,
                        passed,
                        vacancies.Count
                    );
            }
        }

        private static IEnumerable<string> Test_Content_AndOperator(IEnumerable<string[]> set)
        {
            foreach (var words in set)
            {
                var (error, vacancies) = GetParsedVacancies($"(!{words[0]} AND !{words[1]}");
                int passed = vacancies.Count(vac =>
                    vac.VacancyContainsWord(words[0]) &&
                    vac.VacancyContainsWord(words[1]));

                yield return string.Format(
                        "[{0}]: Requested \"{1}\". Words were found in {2} out of {3}.",
                        passed == vacancies.Count ? "PASSED" : "FAIL",
                        $"(!{words[0]} AND !{words[1]})",
                        passed,
                        vacancies.Count
                    );
            }
        }

        private static IEnumerable<string> Test_Content_OrOperator(IEnumerable<string[]> set)
        {
            foreach (var words in set)
            {
                var (error, vacancies) = GetParsedVacancies($"(!{words[0]} OR !{words[1]}");
                int passed = vacancies.Count(vac =>
                    vac.VacancyContainsWord(words[0]) ||
                    vac.VacancyContainsWord(words[1]));

                yield return string.Format(
                        "[{0}]: Requested \"{1}\". Words were found in {2} out of {3}.",
                        passed == vacancies.Count ? "PASSED" : "FAIL",
                        $"(!{words[0]} OR !{words[1]})",
                        passed,
                        vacancies.Count
                    );
            }
        }

        private static IEnumerable<string> Test_Content_NotOperator(IEnumerable<string[]> set)
        {
            foreach (var words in set)
            {
                var (error, vacancies) = GetParsedVacancies($"(!{words[0]} NOT !{words[1]}");
                int passed = vacancies.Count(vac =>
                    vac.VacancyContainsWord(words[0]) &&
                    !vac.VacancyContainsWord(words[1]));

                yield return string.Format(
                        "[{0}]: Requested \"{1}\". Words were found in {2} out of {3}.",
                        passed == vacancies.Count ? "PASSED" : "FAIL",
                        $"(!{words[0]} NOT !{words[1]})",
                        passed,
                        vacancies.Count
                    );
            }
        }

        private static void Main()
        {
            Console.WriteLine($"\r\n*** Поле text не может принимать несколько значений ***\r\n");
            Console.WriteLine($"{Test_MultipleTextApplies(0, false)}");
            Console.WriteLine($"{Test_MultipleTextApplies(1, false)}");
            Console.WriteLine($"{Test_MultipleTextApplies(2, true)}");
            Console.WriteLine($"\r\nНажмите любую клавишу, чтобы перейти к следующим тестам..\r\n");
            Console.ReadKey();

            Console.WriteLine($"\r\n*** Проверка на объем вводимых даннных в поле text ***\r\n");
            foreach (var report in Test_TextSize()) Console.WriteLine($"{report}");
            Console.WriteLine($"\r\nНажмите любую клавишу, чтобы перейти к следующим тестам..\r\n");
            Console.ReadKey();

            Console.WriteLine($"\r\n*** Проверка на объем вводимых даннных в поле text ***\r\n");
            Console.WriteLine($"{Test_TextSizeV2()}");
            Console.WriteLine($"\r\nНажмите любую клавишу, чтобы перейти к следующим тестам..\r\n");
            Console.ReadKey();

            Console.WriteLine($"\r\n*** Проверка безопасности. JS Injections ***\r\n");
            foreach (var report in Test_Critical()) Console.WriteLine(report);
            Console.WriteLine($"\r\nНажмите любую клавишу, чтобы перейти к следующим тестам..\r\n");
            Console.ReadKey();

            Console.WriteLine($"\r\n*** Проверка содержания точного слово в вакансии ***\r\n");
            foreach (var report in Test_Content_OneExactWord(SetOfStrings.Last())) Console.WriteLine(report);
            Console.WriteLine($"\r\nНажмите любую клавишу, чтобы перейти к следующим тестам..\r\n");
            Console.ReadKey();

            Console.WriteLine($"\r\n*** Проверка оператора AND ***\r\n");
            foreach (var report in Test_Content_AndOperator(SetOfStrings)) Console.WriteLine(report);
            Console.WriteLine($"\r\nНажмите любую клавишу, чтобы перейти к следующим тестам..\r\n");
            Console.ReadKey();

            Console.WriteLine($"\r\n*** Проверка оператора OR ***\r\n");
            foreach (var report in Test_Content_OrOperator(SetOfStrings)) Console.WriteLine(report);
            Console.WriteLine($"\r\nНажмите любую клавишу, чтобы перейти к следующим тестам..\r\n");
            Console.ReadKey();

            Console.WriteLine($"\r\n*** Проверка оператора NOT ***\r\n");
            foreach (var report in Test_Content_NotOperator(SetOfStrings)) Console.WriteLine(report);
            Console.WriteLine($"\r\nНажмите любую клавишу, чтобы ВЫЙТИ..\r\n");
            Console.ReadKey();
        }
    }
}