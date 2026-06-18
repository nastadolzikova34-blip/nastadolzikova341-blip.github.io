using Microsoft.AspNetCore.Mvc;

namespace WatchTogether.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult Login()
        {
            return View();
        }

        public IActionResult Profile()
        {
            return View();
        }
    }
}