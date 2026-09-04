using Microsoft.AspNetCore.Mvc;

namespace BookShelf.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Dashboard()
    {
        return View("Index");
    }
}
