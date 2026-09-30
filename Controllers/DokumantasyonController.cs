using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace gamabelmvc.Controllers;

[Authorize(Policy = "PrsMenuDokumantasyon")]
public class DokumantasyonController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
