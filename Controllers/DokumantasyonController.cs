using Microsoft.AspNetCore.Mvc;

namespace gamabelmvc.Controllers;

public class DokumantasyonController : Controller
{
    public IActionResult Index()
    {
        if (HttpContext.Session.GetString("Rol") != "admin")
            return RedirectToAction("Login", "Account");

        return View();
    }
}
