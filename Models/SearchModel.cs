using System.Collections.Generic;
using System.Web.Helpers;

namespace test.Models
{
    public class SearchModel
    {
        public string SearchText { get; set; }
        public bool NumberOfKeywords { get; set; }
        public bool LastUpdate { get; set; }
        public bool DomainAge { get; set; }
        public bool DomainExpiryDate { get; set; }
        public bool LoadingSpeed { get; set; }
        public List<SearchResult> Results;

        public override string ToString()
        {
            return Json.Encode(this);
        }
    }
}