using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CMCS.Web.Data;
using CMCS.Web.Models;
using CMCS.Web.Services;
using System.Text;

namespace CMCS.Web.Controllers
{
    public class ClaimsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ClaimAutomationService _automation;

        public ClaimsController(ApplicationDbContext db, IWebHostEnvironment env, ClaimAutomationService automation)
        {
            _db = db;
            _env = env;
            _automation = automation;
        }

        // MyClaims and other existing actions unchanged (keep your previous code)
        public async Task<IActionResult> MyClaims(int lecturerId = 1)
        {
            var claims = await _db.Claims.Include(c => c.Documents).Where(c => c.LecturerId == lecturerId).OrderByDescending(c => c.ClaimDate).ToListAsync();
            ViewBag.LecturerId = lecturerId;
            return View(claims);
        }

        [HttpGet]
        public IActionResult Create(int lecturerId = 1)
        {
            ViewBag.LecturerId = lecturerId;
            return View();
        }

        // POST: Create claim (server side calculates Amount and optionally auto-approves)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int lecturerId, decimal hoursWorked, decimal hourlyRate, string? notes, List<IFormFile>? files)
        {
            // Construct claim
            var claim = new Claim
            {
                LecturerId = lecturerId,
                ClaimDate = DateTime.UtcNow,
                HoursWorked = hoursWorked,
                HourlyRate = hourlyRate,
                Notes = notes
            };

            // server-side auto-calculation
            claim.Amount = Math.Round(claim.HoursWorked * claim.HourlyRate, 2);

            // Validate via automation service
            if (!_automation.ValidateClaim(claim, out var validationMessage))
            {
                ModelState.AddModelError("", validationMessage ?? "Validation failed.");
                ViewBag.LecturerId = lecturerId;
                return View();
            }

            // add to DB
            _db.Claims.Add(claim);
            await _db.SaveChangesAsync(); // so claim has ClaimId

            // file upload logic (same as earlier)
            if (files != null && files.Count > 0)
            {
                var uploadsRoot = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
                Directory.CreateDirectory(uploadsRoot);

                foreach (var file in files)
                {
                    if (file.Length == 0) continue;

                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    var allowed = new[] { ".pdf", ".docx", ".xlsx" };
                    if (!allowed.Contains(ext)) continue;

                    var unique = $"{Guid.NewGuid()}{ext}";
                    var savePath = Path.Combine(uploadsRoot, unique);
                    using (var fs = new FileStream(savePath, FileMode.Create))
                    {
                        await file.CopyToAsync(fs);
                    }

                    var doc = new SupportingDocument
                    {
                        ClaimId = claim.ClaimId,
                        FileName = Path.GetFileName(file.FileName),
                        FilePath = Path.Combine("uploads", unique).Replace("\\", "/")
                    };
                    _db.SupportingDocuments.Add(doc);
                }
                await _db.SaveChangesAsync();
            }

            // AUTOMATION: decide whether to auto-approve
            if (_automation.ShouldAutoApprove(claim))
            {
                claim.Status = ClaimStatus.Approved;
                _db.Update(claim);
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(MyClaims), new { lecturerId = claim.LecturerId });
        }

        // A manual endpoint to run automation across all pending claims (for demo / scheduled runs)
        [HttpPost]
        public async Task<IActionResult> AutoVerifyAll()
        {
            var pending = await _db.Claims.Where(c => c.Status == ClaimStatus.Pending).ToListAsync();
            foreach (var c in pending)
            {
                // ensure Amount is computed
                c.Amount = Math.Round(c.HoursWorked * c.HourlyRate, 2);

                if (_automation.ShouldAutoApprove(c))
                {
                    c.Status = ClaimStatus.Approved;
                    _db.Update(c);
                }
            }
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Pending)); // or return Ok for API
        }

        public async Task<IActionResult> Pending()
        {
            var pending = await _db.Claims.Include(c => c.Lecturer).Include(c => c.Documents).Where(c => c.Status == ClaimStatus.Pending).OrderBy(c => c.ClaimDate).ToListAsync();
            return View(pending);
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var claim = await _db.Claims.FindAsync(id);
            if (claim == null) return NotFound();

            claim.Status = ClaimStatus.Approved;
            _db.Update(claim);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Pending));
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id, string? reason)
        {
            var claim = await _db.Claims.FindAsync(id);
            if (claim == null) return NotFound();

            claim.Status = ClaimStatus.Rejected;
            if (!string.IsNullOrWhiteSpace(reason))
            {
                claim.Notes = (claim.Notes ?? "") + $"\nRejection reason: {reason}";
            }
            _db.Update(claim);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Pending));
        }

        // HR Report - download CSV of Approved claims (simple invoice-like report)
        public async Task<IActionResult> HRReport()
        {
            var approved = await _db.Claims.Include(c => c.Lecturer).Where(c => c.Status == ClaimStatus.Approved).OrderBy(c => c.ClaimDate).ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("ClaimId,Lecturer,ClaimDate,HoursWorked,HourlyRate,Amount,Notes");

            foreach (var c in approved)
            {
                var line = $"{c.ClaimId},\"{c.Lecturer?.FullName}\",{c.ClaimDate:yyyy-MM-dd},{c.HoursWorked},{c.HourlyRate},{c.Amount},\"{(c.Notes ?? "").Replace("\"", "'")}\"";
                csv.AppendLine(line);
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"ApprovedClaims_{DateTime.UtcNow:yyyyMMddHHmm}.csv");
        }

        // Download supporting documents unchanged
        public async Task<IActionResult> DownloadDoc(int id)
        {
            var doc = await _db.SupportingDocuments.FindAsync(id);
            if (doc == null) return NotFound();

            var path = Path.Combine(_env.WebRootPath ?? "wwwroot", doc.FilePath);
            if (!System.IO.File.Exists(path)) return NotFound();
            return PhysicalFile(path, "application/octet-stream", doc.FileName);
        }
    }
}
