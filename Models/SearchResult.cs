using System;
using System.Diagnostics.CodeAnalysis;
using System.Web.Helpers;

namespace test.Models
{
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public class SearchResult
    {
        public string Title { get; set; }
        public string Link { get; set; }
        public string DisplayLink { get; set; }
        public string Snippet { get; set; }

        public double LoadingTime { get; set; }
        public double DomainAge { get; set; }
        public DateTime DomainLastUpdated { get; set; }
        public DateTime DomainExpiresDate { get; set; }

        public double KeywordMatchesRanking { get; set; }
        public double LoadingTimeRanking { get; set; }
        public double DomainAgeRanking { get; set; }
        public double LastUpdateRanking { get; set; }
        public double ExpiryDateRanking { get; set; }

        public double Rank { get; set; }

        public override string ToString()
        {
            return Json.Encode(this);
        }
    }
}