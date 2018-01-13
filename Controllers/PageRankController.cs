using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
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
            searchResults = searchResults.Any()
                                ? RankResults(searchResults,
                                              numberOfKeywords,
                                              lastUpdate,
                                              domainAge,
                                              domainExpiryDate,
                                              loadingSpeed,
                                              query)
                                : new List<SearchResult>();
            return searchResults;
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
                                                      bool loadingSpeed,
                                                      string query)
        {
            if (!searchResults.Any())
                return new List<SearchResult>();
            if (numberOfKeywords)
            {
//                var watch = new Stopwatch();
//                watch.Start();
                searchResults = RankKeywordMatches(searchResults, query);
//                watch.Stop();
//                Console.WriteLine("RankKeywordMatches elapsed time: " + watch.ElapsedMilliseconds);
            }

            if (lastUpdate || domainAge || domainExpiryDate)
            {
//                var watch = new Stopwatch();
//                watch.Start();
                searchResults = RankDomainQuality(searchResults, lastUpdate, domainAge, domainExpiryDate);
//                watch.Stop();
//                Console.WriteLine("RankDomainQuality elapsed time: " + watch.ElapsedMilliseconds);
            }

            if (loadingSpeed)
            {
//                var watch = new Stopwatch();
//                watch.Start();
                searchResults = RankLoadingSpeed(searchResults);
//                watch.Stop();
//                Console.WriteLine("RankLoadingSpeed elapsed time: " + watch.ElapsedMilliseconds);
            }

            foreach (var result in searchResults)
            {
//                Console.WriteLine("result.KeywordMatchesRanking: " + result.KeywordMatchesRanking +
//                                  "result.LoadingTimeRanking: " + result.LoadingTimeRanking +
//                                  "result.DomainAgeRanking: " + result.DomainAgeRanking +
//                                  "result.ExpiryDateRanking: " + result.ExpiryDateRanking +
//                                  "result.LastUpdateRanking: " + result.LastUpdateRanking);

                result.Rank = (numberOfKeywords ? result.KeywordMatchesRanking : 0) +
                              (loadingSpeed ? result.LoadingTimeRanking : 0) +
                              (domainAge ? result.DomainAgeRanking : 0) +
                              (domainExpiryDate ? result.ExpiryDateRanking : 0) +
                              (lastUpdate ? result.LastUpdateRanking : 0);
//                Console.WriteLine("result.Rank: " + result.Rank);
            }

            searchResults = searchResults.OrderByDescending(r => r.Rank).ToList();

//            Console.WriteLine("[");
//            foreach (var result in searchResults)
//            {
//                Console.WriteLine(result + ",");
//            }
//
//            Console.WriteLine("]");

            return searchResults;
        }

        private static List<SearchResult> RankLoadingSpeed(List<SearchResult> results)
        {
            foreach (var result in results)
            {
                try
                {
                    var request = WebRequest.Create(result.Link);

                    var watch = Stopwatch.StartNew();
                    var response = (HttpWebResponse) request.GetResponse();
                    watch.Stop();
                    if (response.StatusCode != HttpStatusCode.OK)
                        result.LoadingTime = -1;
                    else
                        result.LoadingTime = watch.ElapsedMilliseconds;
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
//            Console.WriteLine("maxLoadingTime in millis: " + maxLoadingTime + "\tMin: " + minLoadingTime + "\tDiff: " +
//                              loadingTimeDifference);
            foreach (var result in results)
            {
//                Console.WriteLine("loadingTime in millis: " + result.LoadingTime);
                if (result.LoadingTime < 0)
                    result.LoadingTimeRanking = 0;
                else
                    result.LoadingTimeRanking =
                        100 - (result.LoadingTime - minLoadingTime) * 100 / loadingTimeDifference;
//                Console.WriteLine("Normalized loading time (ranking - pct): " + result.LoadingTimeRanking);
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

//                Console.WriteLine("Mapped XML: " +
//                                  updatedDateNormalized + "\r\n" +
//                                  expiresDateNormalized + "\r\n" +
//                                  estimatedDomainAge);
                result.DomainAge = estimatedDomainAge;
                result.DomainLastUpdated = updatedDateNormalized;
                result.DomainExpiresDate = expiresDateNormalized;
            }

            var oldestUpdate = results.Min(result => result.DomainLastUpdated);
            var newestUpdate = results.Max(result => result.DomainLastUpdated);
            var lastUpdateDifference = (newestUpdate - oldestUpdate).TotalDays;
//            Console.WriteLine("oldestUpdate: " + oldestUpdate + "\tnewestUpdate: " + newestUpdate +
//                              "\tlastUpdateDifference: " + lastUpdateDifference);

            var nearestExpiry = results.Min(result => result.DomainExpiresDate);
            var furthestExpiry = results.Max(result => result.DomainExpiresDate);
            var expiryDateDifference = (furthestExpiry - nearestExpiry).TotalDays;
//            Console.WriteLine("nearestExpiry: " + nearestExpiry + "\tfurthestExpiry: " + furthestExpiry +
//                              "\texpiryDateDifference: " + expiryDateDifference);

            var newestDomain = results.Max(result => result.DomainAge);
            var oldestDomain = results.Min(result => result.DomainAge);
            var domainAgeDifference = newestDomain - oldestDomain;
//            Console.WriteLine("newestDomain: " + newestDomain + "\toldestDomain: " + oldestDomain
//                              + "\tdomainAgeDifference: " + domainAgeDifference);

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

        private static List<SearchResult> RankKeywordMatches(List<SearchResult> results, string query)
        {
            var keywords = query.ToLower().Split(' ').ToList();
            foreach (var result in results)
            {
                var culture = CultureInfo.InstalledUICulture;
//                Console.WriteLine("Result: " + result.Link);
                foreach (var keyword in keywords)
                {
                    if (culture.CompareInfo.IndexOf(result.Link, keyword, CompareOptions.IgnoreCase) >= 0 ||
                        culture.CompareInfo.IndexOf(result.Title, keyword, CompareOptions.IgnoreCase) >= 0)
                    {
                        Math.DivRem(100, keywords.Count, out var divResult);
                        result.KeywordMatchesRanking = divResult;
                    }

//                    Console.WriteLine("Keyword: " + keyword + "\nCurrent Rank: " + result.KeywordMatchesRanking);
                }

//                Console.WriteLine("Link & Title: " + result.KeywordMatchesRanking);

                var countMatches = result.Snippet.ToLower().CountMatches(keywords) * 10;
                result.KeywordMatchesRanking += countMatches;
//                Console.WriteLine("Count Matches: " + countMatches + "\nFinal Rank: " + result.KeywordMatchesRanking);
            }

            return results;
        }
    }

    public static class StringExtensionMethods
    {
        public static int CountMatches(this string text, List<string> keywords)
        {
            var matchCount = 0;
            foreach (var keyword in keywords)
            {
                var pat = @"(\w+)\s+(" + keyword + ")";
                var r = new Regex(pat, RegexOptions.IgnoreCase);
                var m = r.Match(text);
                while (m.Success)
                {
                    matchCount++;
                    m = m.NextMatch();
                }
            }

            return matchCount;
        }
    }
}