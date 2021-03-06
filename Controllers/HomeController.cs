﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            Console.WriteLine("Entering HomeController.ActionResult with parameters: " + model);
            if (string.IsNullOrEmpty(model.SearchText)) return View(model);

            var watch = new Stopwatch();
            watch.Start();
            model.Results = PageRankController.ExecuteSearch(model.SearchText,
                                                             model.NumberOfKeywords,
                                                             model.LastUpdate,
                                                             model.DomainAge,
                                                             model.DomainExpiryDate,
                                                             model.LoadingSpeed);
            watch.Stop();
            model.ElapsedMilliseconds = watch.ElapsedMilliseconds;
            model.ElapsedSeconds = watch.ElapsedMilliseconds / 1000;
            Console.WriteLine("Total elapsed time: " + watch.ElapsedMilliseconds);

            return View(model);
        }
    }
}