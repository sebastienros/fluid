using Microsoft.AspNetCore.Mvc;
using Fluid.MvcSample.Models;
using System.Collections.Generic;

namespace Fluid.MvcSample.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            var model = new List<Person>();
            model.Add(new Person { Firstname = "Bill", Lastname = "Gates" });
            model.Add(new Person { Firstname = "Steve", Lastname = "Balmer" });
            
            return View(model);
        }    
    }    
}
