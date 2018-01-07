using System.Web.Helpers;

namespace test.Models
{
    public class SearchModel
    {
        public string SearchText { get; set; }
        private bool NumberOfKeywords { get; set; }
        private bool LastUpdate { get; set; }
        private bool DomainAge { get; set; }
        private bool DomainExpiryDate { get; set; }
        private bool LoadingSpeed { get; set; }

        public override string ToString()
        {
            return Json.Encode(this);
        }
    }
}