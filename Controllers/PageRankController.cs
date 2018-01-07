using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Web.Helpers;
using System.Xml;
using test.Models;

namespace test.Controllers
{
    public static class PageRankController
    {
        private static readonly WebClient Client = new WebClient();

        public static List<SearchResult> ExecuteSearch(string query,
                                                       bool numberOfKeywords,
                                                       bool lastUpdate,
                                                       bool domainAge,
                                                       bool domainExpiryDate,
                                                       bool loadingSpeed)
        {
            var searchResults = GetResults(query);
            return searchResults.Any()
                       ? RankResults(searchResults,
                                     numberOfKeywords,
                                     lastUpdate,
                                     domainAge,
                                     domainExpiryDate,
                                     loadingSpeed)
                       : new List<SearchResult>();
        }

        private static List<SearchResult> GetResults(string terms)
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
            var searchResults = Json.Decode<SearchResult[]>(json);
            Console.WriteLine("PageRankCtrl.GetResults - Exiting with return value: " + searchResults);
            return searchResults.ToList();
        }

        private static List<SearchResult> RankResults(List<SearchResult> searchResults,
                                                      bool numberOfKeywords,
                                                      bool lastUpdate,
                                                      bool domainAge,
                                                      bool domainExpiryDate,
                                                      bool loadingSpeed)
        {
            if (!searchResults.Any())
                return new List<SearchResult>();
            if (numberOfKeywords)
            {
                searchResults = RankKeywordMatches(searchResults);
            }

            if (lastUpdate || domainAge || domainExpiryDate)
            {
                searchResults = RankDomainQuality(searchResults, lastUpdate, domainAge, domainExpiryDate);
            }

            if (loadingSpeed)
            {
                searchResults = RankLoadingSpeed(searchResults);
            }

            Console.WriteLine("[");
            foreach (var result in searchResults)
            {
                result.Rank = (int) ((numberOfKeywords ? result.KeywordMatchesRanking : 0) +
                                     (loadingSpeed ? result.LoadingTimeRanking : 0) +
                                     (domainAge ? result.DomainAgeRanking : 0) +
                                     (domainExpiryDate ? result.ExpiryDateRanking : 0) +
                                     (lastUpdate ? result.LastUpdateRanking : 0));
                Console.WriteLine(result + ",");
            }

            Console.WriteLine("]");

            return searchResults.OrderByDescending(r => r.Rank).ToList();
        }

        private static List<SearchResult> RankLoadingSpeed(List<SearchResult> results)
        {
            foreach (var result in results)
            {
                try
                {
                    while (Client.IsBusy)
                    {
                        Thread.Sleep(50);
                    }

                    var request = (HttpWebRequest) WebRequest.Create(result.Link);
                    request.AllowAutoRedirect = false;
                    request.Method = WebRequestMethods.Http.Head;
                    try
                    {
                        var watch = Stopwatch.StartNew();
                        request.GetResponse();
                        watch.Stop();
                        result.LoadingTime = watch.ElapsedMilliseconds;
                        Console.WriteLine(result.Link + ": Loading time in millis: " + watch.ElapsedMilliseconds);
                    }
                    catch (WebException e)
                    {
                        Console.WriteLine(e);
                        result.LoadingTime = -1;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message + "\t" + e.StackTrace);
                    result.LoadingTime = -1;
                }
            }

            var maxLoadingTime = results.Max(r => r.LoadingTime);
            var minLoadingTime = results.Min(r => r.LoadingTime);
            var loadingTimeDifference = maxLoadingTime - minLoadingTime;
            Console.WriteLine("maxLoadingTime in millis: " + maxLoadingTime + "\tMin: " + minLoadingTime + "\tDiff: " +
                              loadingTimeDifference);
            foreach (var result in results)
            {
                Console.WriteLine("loadingTime in millis: " + result.LoadingTime);
                if (result.LoadingTime < 0)
                    result.LoadingTimeRanking = 0;
                else
                    result.LoadingTimeRanking = (result.LoadingTime - minLoadingTime) * 100 / loadingTimeDifference;
                Console.WriteLine("Normalized loading time (ranking - pct): " + result.LoadingTimeRanking);
            }

            return results;
        }

        private static List<SearchResult> RankDomainQuality(List<SearchResult> results,
                                                            bool lastUpdate, bool domainAge,
                                                            bool expiryDate)
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
                var doc = new XmlDocument();
                doc.LoadXml(xml);

                var updatedDateNormalized = DateTime.MinValue;
                if (lastUpdate)
                {
                    var updateDateXml = doc.GetElementsByTagName("updatedDateNormalized");
                    var updateDateXmlNode = updateDateXml.Item(0);
                    var updateDateStr = updateDateXmlNode?.InnerText;
                    if (updateDateStr != null)
                        updatedDateNormalized =
                            DateTime.Parse(updateDateStr.Substring(0, updateDateStr.Length - 4));
                }

                var expiresDateNormalized = DateTime.Now;
                if (expiryDate)
                {
                    var expiresDateXml = doc.GetElementsByTagName("expiresDateNormalized");
                    var expiryDateXmlNode = expiresDateXml.Item(0);
                    var expiryDateStr = expiryDateXmlNode?.InnerText;
                    if (expiryDateStr != null)
                        expiresDateNormalized =
                            DateTime.Parse(expiryDateStr.Substring(0, expiryDateStr.Length - 4));
                }

                var estimatedDomainAge = -1;
                if (domainAge)
                {
                    var domainAgeXml = doc.GetElementsByTagName("estimatedDomainAge");
                    var domainAgeXmlNode = domainAgeXml.Item(0);
                    var domainAgeStr = domainAgeXmlNode?.InnerText;
                    if (domainAgeStr != null)
                        estimatedDomainAge = int.Parse(domainAgeStr);
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
                if (lastUpdate && lastUpdateDifference > 0)
                    result.LastUpdateRanking =
                        (result.DomainLastUpdated - oldestUpdate).TotalDays * 100 / lastUpdateDifference;
                if (expiryDate && expiryDateDifference > 0)
                    result.ExpiryDateRanking =
                        (result.DomainExpiresDate - nearestExpiry).TotalDays * 100 / expiryDateDifference;
                if (domainAge && domainAgeDifference > 0)
                    result.DomainAgeRanking = (result.DomainAge - oldestDomain) * 100 / domainAgeDifference;
            }

            return results;
        }

        private static List<SearchResult> RankKeywordMatches(List<SearchResult> results)
        {
            //TODO: Implement
            return results;
        }
    }
}