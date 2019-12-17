using System;
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

            var requestUri = "vacancies?";
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

        private static (string Error, List<Dictionary<string, object>> Vacancies) GetParsedVacancies(string request)
        {
            var (error, response) = GetVacancies(request);

            if (error != null) return (error, null);

            var contentDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content);

            if (contentDictionary.ContainsKey("items") && contentDictionary["items"] is JArray itemsJArray)
            {
                var itemsList = itemsJArray.ToObject<List<Dictionary<string, object>>>();
                return (null, itemsList);
            }
            else
                return ("Содержимое ответа не содержит массив items.", null);
        }

        private static
            Dictionary<string, (string Error, List<Dictionary<string, object>> Vacancies)>
            GetAllParsedVacancies(IList<string> texts)
        {
            var result = new Dictionary<string, (string Error, List<Dictionary<string, object>> Vacancies)>();
            foreach (var text in texts) result.Add(text, GetParsedVacancies(text));

            return result;
        }

        private static void Test_JustShowNames(params string[] texts)
        {
            var vacancies = GetAllParsedVacancies(texts);
            foreach (var vacancy in vacancies)
            {
                Console.ReadKey();
            }
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

        private static void Main()
        {
            Console.WriteLine($"TEST #1 {Test_MultipleTextApplies(0, false)}");
            Console.WriteLine($"TEST #2 {Test_MultipleTextApplies(1, false)}");
            Console.WriteLine($"TEST #3 {Test_MultipleTextApplies(2, true)}");

            Console.ReadKey();
        }
    }
}