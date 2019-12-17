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
        private static (string Error, IRestResponse Response) GetVacancies(string text)
        {
            var client = new RestClient("https://api.hh.ru/");

            var getRequest = new RestRequest("vacancies", Method.GET);
            getRequest.AddUrlSegment("text", text);

            var getResponse = client.Execute(getRequest);

            if (getResponse.IsSuccessful)
                return (null, getResponse);
            else
                return (getResponse.ErrorMessage, getResponse);
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

        private static void Main()
        {
            Test_JustShowNames("", "Full-Stack", "pornhub");

            Console.ReadKey();
        }
    }
}