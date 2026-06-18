using Microsoft.AspNetCore.Mvc;

namespace WatchTogether.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        // Старый метод - оставляем для совместимости
        public IActionResult Room(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                id = GenerateRoomId();
                return RedirectToAction("Room", new { id });
            }

            ViewData["RoomId"] = id;
            return View();
        }

        // НОВЫЙ МЕТОД - для красивых ссылок /room/ABC123
        [Route("room/{roomId}")]
        public IActionResult RoomById(string roomId)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                roomId = GenerateRoomId();
                return RedirectToAction("RoomById", new { roomId });
            }

            ViewData["RoomId"] = roomId;
            return View("Room"); // Используем то же представление
        }

        private string GenerateRoomId()
        {
            // Генерируем читаемый ID 
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // без 0,1,O,I для читаемости
            var random = new Random();

            return "ROOM-" + new string(Enumerable.Repeat(chars, 4)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}