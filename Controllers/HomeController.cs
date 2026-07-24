using DBPQRPermanent.Data;
using DBPQRPermanent.Models;
using DBPQRPermanent.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DBPQRPermanent.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;
        private readonly QRService _qrService;

        public HomeController(AppDbContext db, QRService qrService)
        {
            _db = db;
            _qrService = qrService;
        }

        // 16 random bytes = 32 hex chars — cryptographically unguessable
        private static string GenerateToken()
        {
            var bytes = new byte[16];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToHexString(bytes).ToLower();
        }

        // GET / — Welcome page
        [HttpGet("/")]
        public IActionResult Index() => View("Welcome");

        // GET /generate — Enter emp ID page
        [HttpGet("/generate")]
        public IActionResult Generate() => View("Generate");

        // POST /generate — look up emp, get/create token, redirect to QR screen
        [HttpPost("/generate")]
        [ValidateAntiForgeryToken]
        public IActionResult GeneratePost(string empId)
        {
            if (string.IsNullOrWhiteSpace(empId))
            {
                TempData["Error"] = "Please enter your Employee ID.";
                return View("Generate");
            }

            empId = empId.Trim().ToUpper();
            var employee = _db.Employees.FirstOrDefault(e => e.EmpId == empId);
            if (employee == null)
            {
                TempData["Error"] = "Employee ID not found. Please contact your administrator.";
                return View("Generate");
            }

            // Get existing QR or create new one with token
            var qr = _db.QRCodes.FirstOrDefault(q => q.EmpId == empId);
            if (qr == null)
            {
                qr = new QRCode
                {
                    EmpId = empId,
                    Token = GenerateToken(),
                    GeneratedAt = DateTime.UtcNow
                };
                _db.QRCodes.Add(qr);
                _db.SaveChanges();
            }

            // Redirect to token-based URL — emp ID never appears in browser address bar
            return Redirect($"/qr/{qr.Token}");
        }

        // GET /qr/{token} — QR screen (token only, no emp ID in URL)
        [HttpGet("/qr/{token}")]
        public IActionResult QRScreen(string token)
        {
            var qr = _db.QRCodes.FirstOrDefault(q => q.Token == token);
            if (qr == null) return View("NotFound");

            var employee = _db.Employees.FirstOrDefault(e => e.EmpId == qr.EmpId);
            if (employee == null) return View("NotFound");

            ViewBag.Employee = employee;
            ViewBag.Token = qr.Token;
            return View("QRScreen");
        }

        // GET /qr-image/{token} — returns QR code PNG (token-based)
        [HttpGet("/qr-image/{token}")]
        public IActionResult QRImage(string token)
        {
            var qr = _db.QRCodes.FirstOrDefault(q => q.Token == token);
            if (qr == null) return NotFound();

            // The URL embedded inside the QR also uses the token
            string contactUrl = $"{Request.Scheme}://{Request.Host}/contact/{qr.Token}";
            byte[] qrBytes = _qrService.GenerateQRCode(contactUrl);
            return File(qrBytes, "image/png");
        }

        // GET /contact/{token} — scanned by phone, returns vCard to save contact
        // URL looks like: /contact/a7f3k9x2m4q8b1p5  — unguessable
        [HttpGet("/contact/{token}")]
        public IActionResult Contact(string token)
        {
            var qr = _db.QRCodes.FirstOrDefault(q => q.Token == token);
            if (qr == null) return NotFound();

            var e = _db.Employees.FirstOrDefault(emp => emp.EmpId == qr.EmpId);
            if (e == null) return NotFound();

            string lastName  = e.Name.Contains(" ") ? e.Name.Substring(e.Name.LastIndexOf(' ') + 1) : "";
            string firstName = e.Name.Contains(" ") ? e.Name.Substring(0, e.Name.IndexOf(' ')) : e.Name;
            string telFull   = e.TelOffice + (!string.IsNullOrWhiteSpace(e.TelLocal) ? $" loc. {e.TelLocal}" : "");

            var sb = new StringBuilder();
            sb.Append("BEGIN:VCARD\r\n");
            sb.Append("VERSION:3.0\r\n");
            sb.Append($"FN:{e.Name}\r\n");
            sb.Append($"N:{lastName};{firstName};;;\r\n");
            sb.Append($"ORG:{e.Organization};{e.Department};{e.Section}\r\n");
            sb.Append($"TITLE:{e.Title}\r\n");
            if (!string.IsNullOrWhiteSpace(e.Mobile))
                sb.Append($"TEL;TYPE=CELL:{e.Mobile}\r\n");
            if (!string.IsNullOrWhiteSpace(e.TelOffice))
                sb.Append($"TEL;TYPE=WORK,VOICE:{telFull}\r\n");
            if (!string.IsNullOrWhiteSpace(e.Email))
                sb.Append($"EMAIL;TYPE=WORK:{e.Email}\r\n");
            if (!string.IsNullOrWhiteSpace(e.Address))
                sb.Append($"ADR;TYPE=WORK:;;{e.Address};;;;\r\n");
            if (!string.IsNullOrWhiteSpace(e.Website))
                sb.Append($"URL:{e.Website}\r\n");
            if (!string.IsNullOrWhiteSpace(e.Facebook))
                sb.Append($"X-SOCIALPROFILE;type=facebook:{e.Facebook}\r\n");
            sb.Append("END:VCARD\r\n");

            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            string filename = e.Name.Replace(" ", "-") + ".vcf";
            Response.Headers["Content-Disposition"] = $"attachment; filename=\"{filename}\"";
            return File(bytes, "text/vcard");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() =>
            View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
