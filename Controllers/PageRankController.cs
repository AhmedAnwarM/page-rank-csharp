using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Web.Helpers;
using System.Xml;
using test.Models;

namespace test.Controllers
{
    public class PageRankController
    {
        private static readonly CustomWebClient Client = new CustomWebClient();

        public static List<SearchResult> ExecuteSearch(string query)
        {
            var searchResults = GetResults(query);
            return !searchResults.Any()
                       ? new List<SearchResult>()
                       : RankResults(searchResults, true, true, true, true, true);
        }

        static List<SearchResult> GetResults(string terms)
        {
            Console.WriteLine("PageRankCtrl.GetResults - Entering with parameter: " + terms);
            if (string.IsNullOrEmpty(terms))
                return new List<SearchResult>();
            var json = Client
               .DownloadString(
                               "https://www.googleapis.com/customsearch/v1?" +
                               "key=AIzaSyCi3plpgvHzaGRVcYD6tU5v0tsIPquiR58&" +
                               "cx=017576662512468239146:omuauf_lfve&" +
                               "q=" + terms.Replace(' ', '+'));
            var itemsArrayStart = json.IndexOf("\"items\": [", StringComparison.Ordinal) + 9;
            json = json.Substring(itemsArrayStart, json.LastIndexOf('}') - itemsArrayStart);
            //            Console.WriteLine("JSON string with items array only: " + json);
            var searchResults = Json.Decode<SearchResult[]>(json);
            Console.WriteLine("PageRankCtrl.GetResults - Exiting with return value: " + searchResults);
            return searchResults.ToList();
        }

        static List<SearchResult> RankResults(List<SearchResult> searchResults,
                                              bool numberOfKeywords,
                                              bool lastUpdate,
                                              bool domainAge,
                                              bool domainRegisterDate,
                                              bool loadingSpeed)
        {
            if (!searchResults.Any())
                return new List<SearchResult>();
            if (numberOfKeywords)
            {
            }

            if (lastUpdate || domainAge || domainRegisterDate)
            {
                searchResults = RankDomainQuality(searchResults, lastUpdate, domainAge, domainRegisterDate);
            }

            if (loadingSpeed)
            {
                searchResults = RankLoadingSpeed(searchResults);
            }

            foreach (var result in searchResults)
            {
                result.Rank = (int) (result.LoadingTimeRanking + result.DomainAgeRanking);
            }

            return searchResults.OrderBy(r => r.Rank).ToList();
        }

        private static List<SearchResult> RankLoadingSpeed(List<SearchResult> results)
        {
            foreach (var result in results)
            {
                try
                {
                    var watch = Stopwatch.StartNew();
                    while (Client.IsBusy)
                    {
                        Thread.Sleep(50);
                    }

                    Client.DownloadData(result.Link);
                    watch.Stop();
                    result.LoadingTime = watch.ElapsedMilliseconds;
                    Console.WriteLine(result.Link + ": Loading time in millis: " + watch.ElapsedMilliseconds);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message + "\t" + e.StackTrace);
                    result.LoadingTime = -1;
                }
            }

            var maxLoadingTime = results.Max(r => r.LoadingTime);
            Console.WriteLine("maxLoadingTime in millis: " + maxLoadingTime);
            foreach (var result in results)
            {
                Console.WriteLine("loadingTime in millis: " + result.LoadingTime);
                if (Math.Abs(result.LoadingTime + 1) <= 0)
                    result.LoadingTimeRanking = 0;
                else
                    result.LoadingTimeRanking = 100 - result.LoadingTime * 100 / maxLoadingTime;
                Console.WriteLine("Normalized loading time (ranking - pct): " + result.LoadingTimeRanking);
            }

            return results;
        }

        private static List<SearchResult> RankDomainQuality(List<SearchResult> results,
                                                            bool lastUpdate, bool domainAge, bool expiryDate)
        {
            foreach (var result in results)
            {
                while (Client.IsBusy)
                {
                    Thread.Sleep(50);
                }

                var xml = Client
                   .DownloadString("https://www.whoisxmlapi.com/whoisserver/WhoisService?" +
                                   "apiKey=at_zjamXqyabDnMCdA6oWw0Y91aFQm3t&" +
                                   "domainName=" + result.DisplayLink);
                Console.WriteLine("Fetched site info from Whois: \r\n" + xml);
                var doc = new XmlDocument();
                doc.LoadXml(xml);

                var updatedDateNormalized = DateTime.MinValue;
                if (lastUpdate)
                {
                    var updateDateXml = doc.GetElementsByTagName("updatedDateNormalized");
                    if (updateDateXml.Item(0) != null && updateDateXml.Item(0)?.InnerText != null)
                        updatedDateNormalized =
                            DateTime.Parse(updateDateXml
                                          .Item(0)
                                         ?.InnerText.Substring(0, updateDateXml.Item(0).InnerText.Length - 4));
                }

                var expiresDateNormalized = DateTime.Now;
                if (expiryDate)
                {
                    var expiresDateXml = doc.GetElementsByTagName("expiresDateNormalized");
                    if (expiresDateXml.Item(0) != null)
                        expiresDateNormalized =
                            DateTime.Parse(expiresDateXml
                                          .Item(0)
                                         ?.InnerText.Substring(0, expiresDateXml.Item(0).InnerText.Length - 4));
                }

                var estimatedDomainAge = -1;
                if (domainAge)
                {
                    var domainAgeXml = doc.GetElementsByTagName("estimatedDomainAge");
                    if (domainAgeXml.Item(0) != null && domainAgeXml.Item(0)?.InnerText != null)
                        estimatedDomainAge =
                            int.Parse(domainAgeXml.Item(0)?.InnerText ?? throw new NullReferenceException());
                }

                Console.WriteLine("Mapped XML: " +
                                  updatedDateNormalized + "\r\n" +
                                  expiresDateNormalized + "\r\n" +
                                  estimatedDomainAge);
                result.DomainAge = estimatedDomainAge;
                result.DomainLastUpdated = updatedDateNormalized;
                result.DomainExpiresDate = expiresDateNormalized;
            }

            var oldestUpdate = results.Min(result => result.DomainLastUpdated);
            var newestUpdate = results.Max(result => result.DomainLastUpdated);
            var lastUpdateDifference = (newestUpdate - oldestUpdate).TotalDays;
            Console.WriteLine("oldestUpdate: " + oldestUpdate + "\tnewestUpdate: " + newestUpdate +
                              "\tlastUpdateDifference: " + lastUpdateDifference);

            var nearestExpiry = results.Min(result => result.DomainExpiresDate);
            var furthestExpiry = results.Max(result => result.DomainExpiresDate);
            var expiryDateDifference = (furthestExpiry - nearestExpiry).TotalDays;
            Console.WriteLine("nearestExpiry: " + nearestExpiry + "\tfurthestExpiry: " + furthestExpiry +
                              "\texpiryDateDifference: " + expiryDateDifference);

            var newestDomain = results.Max(result => result.DomainAge);
            var oldestDomain = results.Min(result => result.DomainAge);
            var domainAgeDifference = newestDomain - oldestDomain;
            Console.WriteLine("newestDomain: " + newestDomain + "\toldestDomain: " + oldestDomain
                              + "\tdomainAgeDifference: " + domainAgeDifference);

            foreach (var result in results)
            {
                if (lastUpdate)
                    result.LastUpdateRanking =
                        (result.DomainLastUpdated - oldestUpdate).TotalDays / lastUpdateDifference;
                if (expiryDate)
                    result.ExpiryDateRanking =
                        (result.DomainExpiresDate - nearestExpiry).TotalDays / expiryDateDifference;
                if (domainAge)
                    result.DomainAgeRanking = (result.DomainAge - oldestDomain) / domainAgeDifference;
            }

            return results;
        }
    }
}