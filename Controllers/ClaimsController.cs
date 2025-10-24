using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CMCS.Web.Data;
using CMCS.Web.Models;

namespace CMCS.Web.Controllers
{
    public class ClaimsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly long _maxFileBytes = 5 * 1024 * 1024; // 5 MB
        private readonly string[] _permittedExts = { ".pdf", ".docx", ".xlsx" };

        public ClaimsController(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // Lecturer: view own claims 
        public async Task<IActionResult> MyClaims(int lecturerId = 1)
        {
            
            if (!await _db.Lecturers.AnyAsync())
            {
                _db.Lecturers.Add(new Lecturer { FullName = "Demo Lecturer", Email = "lecturer@demo.com" });
                await _db.SaveChangesAsync();
            }

            var claims = await _db.Claims
                .Include(c => c.Documents)
                .Include(c => c.Lecturer)
                .Where(c => c.LecturerId == lecturerId)
                .OrderByDescending(c => c.ClaimDate)
                .ToListAsync();

            ViewBag.LecturerId = lecturerId;
            return View(claims);
        }

        // GET create form
        [HttpGet]
        public IActionResult Create(int lecturerId = 1)
        {
            ViewBag.LecturerId = lecturerId;
            return View();
        }

        // POST create claim
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int lecturerId, decimal hoursWorked, decimal hourlyRate, string? notes, List<IFormFile>? files)
        {
            if (hoursWorked <= 0 || hourlyRate < 0)
            {
                ModelState.AddModelError("", "Hours and hourly rate must be valid.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.LecturerId = lecturerId;
                return View();
            }

            // Ensure lecturer exists (simple demo)
            var lecturer = await _db.Lecturers.FindAsync(lecturerId) ?? new Lecturer { FullName = "Lecturer", Email = "no-reply@demo" };

            var claim = new Claim
            {
                LecturerId = lecturer.LecturerId == 0 ? lecturerId : lecturer.LecturerId,
                ClaimDate = DateTime.UtcNow,
                HoursWorked = hoursWorked,
                HourlyRate = hourlyRate,
                Notes = notes,
                Status = ClaimStatus.Pending
            };

            _db.Claims.Add(claim);
            await _db.SaveChangesAsync(); // get ClaimId

            if (files != null && files.Count > 0)
            {
                var uploadsRoot = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
                Directory.CreateDirectory(uploadsRoot);

                foreach (var file in files)
                {
                    if (file.Length == 0 || file.Length > _maxFileBytes) continue;

                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (string.IsNullOrEmpty(ext) || !_permittedExts.Contains(ext)) continue;

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

            return RedirectToAction(nameof(MyClaims), new { lecturerId = claim.LecturerId });
        }

        // Approver: list pending claims
        public async Task<IActionResult> Pending()
        {
            var pending = await _db.Claims
                .Include(c => c.Lecturer)
                .Include(c => c.Documents)
                .Where(c => c.Status == ClaimStatus.Pending)
                .OrderBy(c => c.ClaimDate)
                .ToListAsync();

            return View(pending);
        }

        // Approve claim
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

        // Reject claim
        [HttpPost]
        public async Task<IActionResult> Reject(int id, string? reason)
        {
            var claim = await _db.Claims.FindAsync(id);
            if (claim == null) return NotFound();

            claim.Status = ClaimStatus.Rejected;
            _db.Update(claim);

            if (!string.IsNullOrWhiteSpace(reason))
            {
                claim.Notes = (claim.Notes ?? "") + $"\nRejection reason: {reason}";
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Pending));
        }

        // Download supporting document
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
