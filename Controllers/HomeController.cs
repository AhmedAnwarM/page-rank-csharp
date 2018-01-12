using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
using test.Models;

namespace test.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index(SearchModel model)
        {
            Console.WriteLine("Entering ActionResult with parameters: " + model);
            if (!string.IsNullOrEmpty(model.SearchText))
                model.Results = PageRankController.ExecuteSearch(model.SearchText, model.NumberOfKeywords,
                                                                 model.LastUpdate,
                                                                 model.DomainAge, model.DomainExpiryDate,
                                                                 model.LoadingSpeed);
            return View(model);
        }
    }
}