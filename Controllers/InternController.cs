using System.Text.RegularExpressions;
using IMS.Data;
using IMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;
using Syncfusion.Pdf;

namespace IMS.Controllers
{
    public class InternController : Controller
    {
        private readonly MyAppContext _context;
        private readonly IWebHostEnvironment _environment;

        public InternController(MyAppContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            // Get user ID from session as string
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var user = _context.Users.Find(userId);
            if (user == null)
            {
                return NotFound();
            }
            await LoadInternBadgeCounts(userId);
            return View(user);
        }
        [HttpPost]
        public async Task<IActionResult> UpdateProfile(User model, IFormFile? ProfileImage)
        {
            var user = _context.Users.FirstOrDefault(u => u.UserId == model.UserId);
            if (user == null) return NotFound();

            // Clear any existing model state errors to start fresh
            ModelState.Clear();

            // Validate required fields for interns
            if (user.Position == "Intern")
            {
                // Validate First Name
                if (string.IsNullOrWhiteSpace(model.FirstName))
                {
                    ModelState.AddModelError("FirstName", "First name is required.");
                }

                // Validate Last Name
                if (string.IsNullOrWhiteSpace(model.LastName))
                {
                    ModelState.AddModelError("LastName", "Last name is required.");
                }

                // Validate Email
                if (string.IsNullOrWhiteSpace(model.Email))
                {
                    ModelState.AddModelError("Email", "Email is required.");
                }
                else
                {
                    // Check if new email is already taken by another user (not self)
                    if (_context.Users.Any(u => u.Email == model.Email && u.UserId != model.UserId))
                    {
                        ModelState.AddModelError("Email", "Email already exists.");
                    }
                    else if (!System.Text.RegularExpressions.Regex.IsMatch(model.Email, @"^[\w\.-]+@(gmail\.com|ims\.com)$"))
                    {
                        ModelState.AddModelError("Email", "Email must be a valid Gmail or IMS email address.");
                    }
                }

                // Validate Gender
                if (string.IsNullOrWhiteSpace(model.Gender))
                {
                    ModelState.AddModelError("Gender", "Gender is required.");
                }

                // Validate Birthdate and calculate age
                if (model.Birthdate == DateTime.MinValue || model.Birthdate == default(DateTime))
                {
                    ModelState.AddModelError("Birthdate", "Birthdate is required.");
                }
                else
                {
                    // Age Calculation
                    int calculatedAge = DateTime.Now.Year - model.Birthdate.Year;
                    if (DateTime.Now < model.Birthdate.AddYears(calculatedAge)) calculatedAge--;

                    if (calculatedAge < 18)
                    {
                        ModelState.AddModelError("Birthdate", "You must be at least 18 years old.");
                    }
                    else
                    {
                        model.Age = calculatedAge;
                    }
                }

                // Validate Contact Number
                if (string.IsNullOrWhiteSpace(model.ContactNumber))
                {
                    ModelState.AddModelError("ContactNumber", "Contact number is required.");
                }
                else if (!System.Text.RegularExpressions.Regex.IsMatch(model.ContactNumber, @"^09\d{9}$"))
                {
                    ModelState.AddModelError("ContactNumber", "Contact number must be 11 digits starting with 09.");
                }

                // Validate Academic Information
                if (string.IsNullOrWhiteSpace(model.Course))
                {
                    ModelState.AddModelError("Course", "Course is required.");
                }

                if (string.IsNullOrWhiteSpace(model.School))
                {
                    ModelState.AddModelError("School", "School is required.");
                }

                if (!model.HoursToRender.HasValue || model.HoursToRender <= 0)
                {
                    ModelState.AddModelError("HoursToRender", "Hours to render is required and must be greater than 0.");
                }
            }

            // Handle profile image upload BEFORE checking ModelState
            if (ProfileImage != null && ProfileImage.Length > 0)
            {
                var extension = Path.GetExtension(ProfileImage.FileName).ToLower();
                var allowedExtensions = new[] { ".jfif", ".jpg", ".jpeg", ".png", ".gif", ".webp" };

                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("ProfileImage", "Only JPG, PNG, GIF, or WEBP formats are allowed.");
                }
                else if (ProfileImage.Length > 5 * 1024 * 1024) // 5MB limit
                {
                    ModelState.AddModelError("ProfileImage", "Profile image must be less than 5MB.");
                }
            }

            // Password change validation
            string currentPassword = Request.Form["CurrentPassword"];
            string newPassword = Request.Form["NewPassword"];
            string confirmPassword = Request.Form["ConfirmNewPassword"];

            bool isChangingPassword = !string.IsNullOrWhiteSpace(currentPassword) ||
                                     !string.IsNullOrWhiteSpace(newPassword) ||
                                     !string.IsNullOrWhiteSpace(confirmPassword);

            if (isChangingPassword)
            {
                if (string.IsNullOrWhiteSpace(currentPassword))
                {
                    ModelState.AddModelError("CurrentPassword", "Current password is required when changing password.");
                }
                else if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.Password))
                {
                    ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                }

                if (string.IsNullOrWhiteSpace(newPassword))
                {
                    ModelState.AddModelError("NewPassword", "New password is required.");
                }
                else if (newPassword.Length < 8)
                {
                    ModelState.AddModelError("NewPassword", "New password must be at least 8 characters.");
                }

                if (string.IsNullOrWhiteSpace(confirmPassword))
                {
                    ModelState.AddModelError("ConfirmNewPassword", "Password confirmation is required.");
                }
                else if (newPassword != confirmPassword)
                {
                    ModelState.AddModelError("ConfirmNewPassword", "New password and confirmation do not match.");
                }
            }

            // If there are validation errors, preserve data and return to view
            if (!ModelState.IsValid)
            {
                // Preserve the current profile image and total rendered hours
                model.ProfileImagePath = user.ProfileImagePath;
                model.OriginalProfileFileName = user.OriginalProfileFileName;
                model.TotalRenderedHours = user.TotalRenderedHours;
                model.Position = user.Position;

                return View("Profile", model);
            }

            // If we get here, all validation passed - update the user
            user.Email = model.Email;
            user.FirstName = model.FirstName;
            user.MiddleName = model.MiddleName;
            user.LastName = model.LastName;
            user.Birthdate = model.Birthdate;
            user.Age = model.Age;
            user.Gender = model.Gender;
            user.ContactNumber = model.ContactNumber;
            user.Address = model.Address;
            user.Course = model.Course;
            user.School = model.School;
            user.HoursToRender = model.HoursToRender;

            // Handle profile image upload
            if (ProfileImage != null && ProfileImage.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/profiles");
                Directory.CreateDirectory(uploadsFolder);

                // Delete old image
                if (!string.IsNullOrEmpty(user.ProfileImagePath) && !user.ProfileImagePath.Contains("default.png"))
                {
                    var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.ProfileImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath)) System.IO.File.Delete(oldFilePath);
                }

                // Save new image
                var extension = Path.GetExtension(ProfileImage.FileName).ToLower();
                var newFileName = Guid.NewGuid() + extension;
                var newFilePath = Path.Combine(uploadsFolder, newFileName);
                using (var stream = new FileStream(newFilePath, FileMode.Create))
                {
                    await ProfileImage.CopyToAsync(stream);
                }

                user.ProfileImagePath = "/uploads/profiles/" + newFileName;
                user.OriginalProfileFileName = ProfileImage.FileName;
            }

            // Update password if needed
            if (isChangingPassword)
            {
                user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
            }

            try
            {
                await _context.SaveChangesAsync();
                return RedirectToAction("Profile", "Intern", new { msg = "Profile updated successfully." });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while saving your profile. Please try again.");
                model.ProfileImagePath = user.ProfileImagePath;
                model.OriginalProfileFileName = user.OriginalProfileFileName;
                model.TotalRenderedHours = user.TotalRenderedHours;
                model.Position = user.Position;
                return View("Profile", model);
            }
        }
        public async Task<IActionResult> Documents()
        {
            string sessionUserId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(sessionUserId))
                return RedirectToAction("Login", "Account");

            int userId = int.Parse(sessionUserId);

            var docs = _context.Documents
                .Where(d => d.UserId == userId)
                .Include(d => d.ApprovedBy)
                .OrderBy(d => d.Status == "Pending" ? 0 : d.Status == "Approved" ? 1 : 2)
                .ThenByDescending(d => d.UploadDate)
                .ToList();

            // ✅ Load badge counts
            await LoadInternBadgeCounts(userId);

            return View(docs);
        }

        [HttpPost]
        public async Task<IActionResult> UploadDocument(IFormFile file, string documentType, string otherTypeName)
        {
            if (file == null || file.Length == 0)
            {
                return RedirectToAction("Documents", "Intern", new { msg = "File is required." });
            }

            string sessionUserId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(sessionUserId)) return RedirectToAction("Login", "Account");

            int userId = int.Parse(sessionUserId);

            // Check if same filename already exists for this user
            bool fileExists = _context.Documents.Any(d => d.UserId == userId && d.FileName == file.FileName);
            if (fileExists)
            {
                return RedirectToAction("Documents", "Intern", new { msg = "A file with the same name already exists." });
            }

            // Save to physical path with GUID to avoid overwriting
            string folderPath = Path.Combine(_environment.WebRootPath, "documents");
            Directory.CreateDirectory(folderPath);

            string uniqueFileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            string filePath = Path.Combine(folderPath, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Handle custom document type
            string finalType = documentType == "Other" && !string.IsNullOrEmpty(otherTypeName)
                ? otherTypeName
                : documentType;

            var doc = new Documents
            {
                UserId = userId,
                FileName = file.FileName, // original name for download
                FilePath = "/documents/" + uniqueFileName, // saved filename
                UploadDate = DateTime.Now,
                DocumentType = finalType,
                Status = "Pending"
            };

            _context.Documents.Add(doc);
            await _context.SaveChangesAsync();

            // 🔔 Notify staff + admin
            var user = await _context.Users.FindAsync(userId);
            string title = "New Document Uploaded";
            string message = $"{user.FirstName} {user.LastName} uploaded a document ({finalType}).";
            string type = "Document";
            string entity = "Applications"; // keep consistent with your system
            string link = "/Admin/Applications"; // where admin reviews uploads

            await NotifyStaff(title, message, type, doc.DocumentId, entity, link);

            return RedirectToAction("Documents", "Intern", new { msg = "Upload successful." });
        }

        [HttpGet]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            var doc = await _context.Documents.FindAsync(id);

            if (doc == null)
                return NotFound();

            // Optionally: delete physical file
            string filePath = Path.Combine(_environment.WebRootPath, doc.FilePath.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            _context.Documents.Remove(doc);
            await _context.SaveChangesAsync();
            // 🔔 Notify Admin about deletion
            var user = await _context.Users.FindAsync(doc.UserId);
            var notif = new Notification
            {
                UserId = doc.UserId,
                Title = "Document Deleted",
                Message = $"{user.FirstName} {user.LastName} deleted a document ({doc.DocumentType}).",
                NotificationType = "Document",
                RelatedId = id,
                RelatedEntity = "Document",
                Link = "/Admin/Applications",
                IsRead = false,
                CreatedAt = DateTime.Now
            };

            _context.Notifications.Add(notif);
            await _context.SaveChangesAsync();
            return RedirectToAction("Documents", "Intern", new { msg = "Deleted successfully." });
        }
        public async Task<IActionResult> MyTasks()
        {
            string? userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                return RedirectToAction("Login", "Account", new {msg = "Session expired. Please log in again." });
            }

            int userId = int.Parse(userIdStr);

            var tasks = await _context.TasksManage
                .Include(t => t.AssignedTo)
                .Include(t => t.AssignedBy) // ✅ include who assigned
                .Where(t => t.AssignedToId == userId)
                .ToListAsync();

            // ✅ Load badge counts
            await LoadInternBadgeCounts(userId);

            return View(tasks);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptTask(int id)
        {
            var task = await _context.TasksManage
                .Include(t => t.AssignedTo)
                .Include(t => t.AssignedBy)
                .FirstOrDefaultAsync(t => t.TaskId == id);

            if (task == null || task.Status != "Pending")
            {
                return RedirectToAction("MyTasks", "Intern", new { msg = "Task not found or cannot be accepted." });
            }

            task.Status = "In Progress";
            task.AcceptedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            // 🔔 Notify ONLY the assigner (or Admin if AssignedById is null)
            var notif = new Notification
            {
                UserId = task.AssignedById, // ✅ if null → goes to Admin (system-level)
                Title = "Task Accepted",
                Message = $"{task.AssignedTo.FirstName} {task.AssignedTo.LastName} accepted the task '{task.Title}'.",
                NotificationType = "Task",
                RelatedId = task.TaskId,
                RelatedEntity = "TasksManage",
                Link = "/Admin/TaskManagement",
                IsRead = false,
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(notif);
            await _context.SaveChangesAsync();

            return RedirectToAction("MyTasks", "Intern", new { msg = "Task accepted." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnacceptTask(int id)
        {
            var task = await _context.TasksManage
                .Include(t => t.AssignedTo)
                .Include(t => t.AssignedBy)
                .FirstOrDefaultAsync(t => t.TaskId == id);

            if (task == null || task.Status != "In Progress")
            {
                return RedirectToAction("MyTasks", "Intern", new { msg = "Task not found or cannot be unaccepted." });
            }

            task.Status = "Pending";
            task.AcceptedDate = null;
            await _context.SaveChangesAsync();

            // 🔔 Notify ONLY the assigner (or Admin if AssignedById is null)
            var notif = new Notification
            {
                UserId = task.AssignedById, // ✅ if null → goes to Admin (system-level)
                Title = "Task Unaccepted",
                Message = $"{task.AssignedTo.FirstName} {task.AssignedTo.LastName} moved the task '{task.Title}' back to Pending.",
                NotificationType = "Task",
                RelatedId = task.TaskId,
                RelatedEntity = "TasksManage",
                Link = "/Admin/TaskManagement",
                IsRead = false,
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(notif);
            await _context.SaveChangesAsync();

            return RedirectToAction("MyTasks", "Intern", new { msg = "Task unaccepted and moved back to Pending." });
        }

        // GET: Intern/MyReports
        public async Task<IActionResult> MyReports()
        {
            string? userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                return RedirectToAction("Login", "Account", new {msg = "Session expired. Please log in again."});
            }

            int userId = int.Parse(userIdStr);

            var reports = await _context.Reports
       .Include(r => r.Task)
       .Where(r => r.UserId == userId)
       .OrderByDescending(r => r.DateSubmitted)
       .ToListAsync();

            // Check if certificate exists
            var certificate = await _context.Certificate
                .FirstOrDefaultAsync(c => c.UserId == userId);

            var hasCertificate = certificate != null;
            var certificatePath = certificate?.FilePath;

            ViewBag.HasCertificate = hasCertificate;
            ViewBag.CertificatePath = certificatePath; // NEW: Pass certificate path

            // Get available tasks
            var availableTasks = await _context.TasksManage
                .Where(t => t.AssignedToId == userId && t.Status == "In Progress")
                .ToListAsync();

            ViewBag.AvailableTasks = availableTasks; 
            ViewBag.CompletedCount = _context.TasksManage
                .Where(t => t.Status == "Completed")
                .Count();


            // ✅ Load badge counts for sidebar/header
            await LoadInternBadgeCounts(userId);
            return View(reports);
        }

        // POST: Intern/SubmitReport
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitReport(int taskId, string reportContent, string reportType, IFormFile reportFile)
        {
            string? userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Account");
            int userId = int.Parse(userIdStr);


            var task = await _context.TasksManage
                .Include(t => t.AssignedTo)
                .FirstOrDefaultAsync(t => t.TaskId == taskId && t.AssignedToId == userId);

            if (task == null)
            {
                return RedirectToAction("MyReports", "Intern", new {msg = "Task not found or you don't have permission to report on it." });
            }

            // Validate that a file is uploaded
            if (reportFile == null || reportFile.Length == 0)
            {
                return RedirectToAction("MyReports", "Intern", new { msg = "Please upload an image as proof of task completion." });
            }

            // Validate file type (only images)
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf", ".gif", ".bmp", ".webp" };
            var fileExtension = Path.GetExtension(reportFile.FileName).ToLower();
            if (!allowedExtensions.Contains(fileExtension))
            {
                return RedirectToAction("MyReports", "Intern", new { msg = "Only image files are allowed (JPG, JPEG, PNG, PDF, GIF, BMP, WEBP)." });
            }

            var report = new InternReport
            {
                TaskId = taskId,
                UserId = userId,
                Content = reportContent,
                DateSubmitted = DateTime.Now,
                ReportType = reportType // ✅ Save type
            };

            // Upload report file (image)
            var uploads = Path.Combine(_environment.WebRootPath, "uploads/reports");
            if (!Directory.Exists(uploads))
                Directory.CreateDirectory(uploads);

            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploads, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await reportFile.CopyToAsync(stream);
            }
            report.FilePath = "/uploads/reports/" + fileName;
            // ✅ If type is Task Completion, mark task as completed
            if (reportType == "Task Completion")
            {
                task.Status = "Completed";
                task.CompletedDate = DateTime.Now;
            }
            else
            {
                task.Status = "In Progress";
            }

            task.ProgressNotes = reportContent;
            task.ImagePath = report.FilePath; // Also store image in task for proof

            _context.Reports.Add(report);
            await _context.SaveChangesAsync();
            // 🔔 Notify staff + admin
            string title = "Report Submitted";
            string message = $"{task.AssignedTo.FirstName} {task.AssignedTo.LastName} submitted a report for task '{task.Title}'.";
            string type = "Report";
            string link = "/Admin/Reports";

            await NotifyStaff(title, message, type, report.ReportId, "Report", link);

            return RedirectToAction("MyReports", "Intern", new { msg = "Report submitted successfully with image proof and task marked as completed." });
        }

        // POST: Intern/EditReport/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditReport(int id, string content, string reportType, IFormFile? reportFile)
        {
            string? userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("Login", "Account");

            int userId = int.Parse(userIdStr);

            var report = await _context.Reports
                .Include(r => r.Task)
                .ThenInclude(t => t.AssignedTo)
                .FirstOrDefaultAsync(r => r.ReportId == id && r.UserId == userId);

            if (report == null)
            {
                return RedirectToAction("MyReports", "Intern", new { msg = "Report not found." });
            }

            // ✅ Update fields
            report.Content = content;
            report.ReportType = reportType;
            report.DateSubmitted = DateTime.Now;

            // ✅ Handle file upload if new file is provided
            if (reportFile != null && reportFile.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".pdf" };
                var fileExtension = Path.GetExtension(reportFile.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return RedirectToAction("MyReports", "Intern", new { msg = "Only JPG, JPEG, PNG, GIF, BMP, WEBP, or PDF files are allowed." });
                }

                // Delete old file if exists
                if (!string.IsNullOrEmpty(report.FilePath))
                {
                    var oldFilePath = Path.Combine(_environment.WebRootPath, report.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                // Upload new file
                var uploads = Path.Combine(_environment.WebRootPath, "uploads/reports");
                if (!Directory.Exists(uploads))
                    Directory.CreateDirectory(uploads);

                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploads, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await reportFile.CopyToAsync(stream);
                }

                report.FilePath = "/uploads/reports/" + fileName;
            }

            // ✅ Sync with Task
            var task = await _context.TasksManage
                .Include(t => t.AssignedTo)
                .FirstOrDefaultAsync(t => t.TaskId == report.TaskId);

            if (task != null)
            {
                task.ProgressNotes = content;
                if (!string.IsNullOrEmpty(report.FilePath))
                    task.ImagePath = report.FilePath;
            }

            await _context.SaveChangesAsync();
            
            return RedirectToAction("MyReports", "Intern", new { msg = "Report updated successfully." });
        }
        // POST: Intern/DeleteReport/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReport(int id)
        {
            string? userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("Login", "Account");

            int userId = int.Parse(userIdStr);

            var report = await _context.Reports
                .FirstOrDefaultAsync(r => r.ReportId == id && r.UserId == userId);

            if (report == null)
            {
                return RedirectToAction("MyReports", "Intern", new { msg = "Report not found." });
            }

            // ✅ Delete associated file if exists
            if (!string.IsNullOrEmpty(report.FilePath))
            {
                var filePath = Path.Combine(_environment.WebRootPath, report.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            // ✅ Reset task status if necessary
            var task = await _context.TasksManage
                .FirstOrDefaultAsync(t => t.TaskId == report.TaskId);

            if (task != null)
            {
                // Count remaining reports for this task
                var otherReports = await _context.Reports
                    .Where(r => r.TaskId == task.TaskId && r.ReportId != id)
                    .ToListAsync();

                if (otherReports.Count == 0)
                {
                    // If no reports left → task goes back to "In Progress"
                    task.Status = "In Progress";
                    task.CompletedDate = null;
                }
                else
                {
                    // If reports still exist, check if any is a "Task Completion"
                    bool hasCompletionReport = otherReports.Any(r => r.ReportType == "Task Completion");

                    if (hasCompletionReport)
                    {
                        task.Status = "Completed";
                        task.CompletedDate ??= DateTime.Now; // preserve or set completed date
                    }
                    else
                    {
                        task.Status = "In Progress";
                        task.CompletedDate = null;
                    }
                }
            }

            // ✅ Delete report from DB
            _context.Reports.Remove(report);
            await _context.SaveChangesAsync();

            return RedirectToAction("MyReports", "Intern", new { msg = "Report deleted successfully." });
        }
        // Helper method to safely format full name
        private string GetFormattedFullName(User intern)
        {
            string middle = !string.IsNullOrWhiteSpace(intern.MiddleName)
                ? $" {intern.MiddleName[0]}."
                : string.Empty;

            return $"{intern.FirstName}{middle} {intern.LastName}";
        }

        // GET: Intern/MyCertificate
        public async Task<IActionResult> MyCertificate()
        {
            string? userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("Login", "Account");

            int userId = int.Parse(userIdStr);

            // Check if user exists and is an intern
            var intern = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId && u.Position == "Intern" && u.Status == "Approved");

            ViewBag.Certificate = await _context.Users
    .Where(a => a.UserId == userId)
    .Select(a => a.TotalRenderedHours >= a.HoursToRender)
    .FirstOrDefaultAsync();

            var notif = new Notification
            {
                UserId = intern.UserId,
                Title = "Certificate Issued",
                Message = $"🎉 Congratulations {intern.FirstName}, your certificate has been issued by YMCA Cebu, Philippines.",
                NotificationType = "Certificate",
                RelatedId = intern.UserId,
                RelatedEntity = "Certificate",
                Link = "/Intern/MyReports",
                IsRead = false,
                CreatedAt = DateTime.Now
            };

            // Get certificate record
            var certificate = await _context.Certificate
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (certificate == null)
            {
                return RedirectToAction("MyReports", "Intern", new { msg = "No certificate has been issued for your account yet." });
            }

            string certificatePath = Path.Combine(_environment.WebRootPath, certificate.FilePath.TrimStart(Path.DirectorySeparatorChar));

            if (!System.IO.File.Exists(certificatePath))
            {
                return RedirectToAction("MyReports", "Intern", new { msg = "Certificate file not found. Please contact administrator." });
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(certificatePath);
            string filename = $"Certificate_{userId}_{certificate.DateIssued:yyyyMMdd}.pdf";

            return File(fileBytes, "application/pdf", filename);
        }

        // 🔹 Preview in modal (returns inline PDF preview)
        public async Task<IActionResult> PreviewCertificate()
        {
            string? userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);
            var intern = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);

            if (intern == null || intern.TotalRenderedHours < intern.HoursToRender)
                return BadRequest("Certificate not available yet, please complete your required hours.");

            try
            {
                string fullName = GetFormattedFullName(intern);
                string templatePath = Path.Combine(_environment.WebRootPath, "templates", "Certificate.docx");
                using var templateStream = new FileStream(templatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                WordDocument doc = new WordDocument(templateStream, Syncfusion.DocIO.FormatType.Docx);

                doc.Replace("{{FullName}}", fullName, false, true);
                doc.Replace("{{Hours}}", intern.HoursToRender.ToString(), false, true);
                doc.Replace("{{Date}}", DateTime.Now.ToString("MMMM dd, yyyy"), false, true);

                using var renderer = new DocIORenderer();
                PdfDocument pdfDoc = renderer.ConvertToPDF(doc);

                using var ms = new MemoryStream();
                pdfDoc.Save(ms);
                pdfDoc.Close(true);
                doc.Close();

                ms.Position = 0;
                return File(ms.ToArray(), "application/pdf");
            }
            catch (Exception ex)
            {
                // Optional: Log exception here
                return StatusCode(500, "Failed to generate certificate preview.");
            }
        }

        // 🔹 Download Certificate (force download as PDF)
        public async Task<IActionResult> DownloadCertificate()
        {
            string? userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);
            var intern = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);

            if (intern == null || intern.TotalRenderedHours < intern.HoursToRender)
                return BadRequest("Certificate not available yet.");

            try
            {
                string fullName = GetFormattedFullName(intern);
                string templatePath = Path.Combine(_environment.WebRootPath, "templates", "Certificate.docx");
                using var templateStream = new FileStream(templatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                WordDocument doc = new WordDocument(templateStream, Syncfusion.DocIO.FormatType.Docx);

                doc.Replace("{{FullName}}", fullName, false, true);
                doc.Replace("{{Hours}}", intern.HoursToRender.ToString(), false, true);
                doc.Replace("{{Date}}", DateTime.Now.ToString("MMMM dd, yyyy"), false, true);

                using var renderer = new DocIORenderer();
                PdfDocument pdfDoc = renderer.ConvertToPDF(doc);

                using var ms = new MemoryStream();
                pdfDoc.Save(ms);
                pdfDoc.Close(true);
                doc.Close();

                ms.Position = 0;

                // Safe filename
                string safeFileName = $"Certificate_{intern.FirstName}_{intern.LastName}".Replace(" ", "_") + ".pdf";
                return File(ms.ToArray(), "application/pdf", safeFileName);
            }
            catch (Exception ex)
            {
                // Optional: Log exception here
                return StatusCode(500, "Failed to generate certificate.");
            }
        }

        [HttpGet("Intern/Attendance")]
        public async Task<IActionResult> Attendance()
        {
            try
            {
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userIdStr))
                {
                    return RedirectToAction("Login", "Account", new { msg = "Please login to access Attendance." });
                }

                int userId = int.Parse(userIdStr);

                // --- Default Time Settings (no DB needed) ---
                ViewBag.OpeningTime = "08:00";
                ViewBag.ClosingTime = "17:00";
                ViewBag.MorningStart = "08:00";
                ViewBag.MorningEnd = "12:00";
                ViewBag.AfternoonStart = "13:00";
                ViewBag.AfternoonEnd = "17:00";

                var attendanceRecords = _context.Attendance
                    .Include(a => a.Intern)
                    .Include(a => a.ApprovedBy)
                    .Where(a => a.UserId == userId && !a.IsDeleted)
                    .OrderByDescending(a => a.Date)
                    .ToList();

                foreach (var record in attendanceRecords)
                {
                    if (record.TimeIn.HasValue && record.TimeOut.HasValue)
                    {
                        decimal totalWorked = (decimal)(record.TimeOut.Value - record.TimeIn.Value).TotalHours;

                        // Deduct break if worked > 4h
                        if (record.Session == "whole" && totalWorked > 4)
                        {
                            totalWorked -= 1;
                        }
                        else if ((record.Session == "morning" || record.Session == "afternoon") && totalWorked > 4)
                        {
                            totalWorked -= 1;
                        }

                        // Session cap
                        decimal sessionMax = record.Session switch
                        {
                            "morning" => 4m,
                            "afternoon" => 4m,
                            "whole" => 8m,
                            _ => 0m
                        };

                        // ✅ Rendered includes ALL hours worked
                        record.RenderedHours = Math.Round(totalWorked, 2);

                        // ✅ Overtime is just the difference beyond session max
                        record.Overtime = totalWorked > sessionMax
                            ? Math.Round(totalWorked - sessionMax, 2)
                            : 0m;
                    }
                    else if (record.TimeIn.HasValue && !record.TimeOut.HasValue)
                    {
                        // TimeIn exists but still working
                        record.RenderedHours = null; // leave null to show "No Hours"
                        record.Overtime = null;
                    }
                    else
                    {
                        // No TimeIn at all (e.g. Absent or not started yet)
                        record.RenderedHours = 0;
                        record.Overtime = 0;
                    }
                }

                // Auto mark absent logic
                var today = DateTime.Today;
                var currentTime = DateTime.Now;
                var todayRecord = _context.Attendance
                    .IgnoreQueryFilters()
                    .FirstOrDefault(a => a.UserId == userId && a.Date == today);

                var hasTimeInToday = todayRecord?.TimeIn.HasValue ?? false;

                if (!hasTimeInToday)
                {
                    var closingTimeToday = today.Add(new TimeSpan(17, 0, 0)); // default 5PM
                    if (currentTime > closingTimeToday && todayRecord == null)
                    {
                        var absentRecord = new Attendance
                        {
                            UserId = userId,
                            Date = today,
                            ApprovalStatus = "Absent",
                            IsAbsent = true,
                            Remarks = "Automatically marked absent (no time-in)",
                            IsDeleted = false
                        };

                        _context.Attendance.Add(absentRecord);
                        await _context.SaveChangesAsync();

                        attendanceRecords.Insert(0, absentRecord);
                    }
                }

                var user = await _context.Users.FindAsync(int.Parse(userIdStr));
                bool internshipCompleted = user.TotalRenderedHours >= user.HoursToRender;

                ViewBag.InternshipCompleted = internshipCompleted;
                // Show absent form if still before closing time
                ViewBag.ShowAbsentForm = !hasTimeInToday &&
                                         currentTime.TimeOfDay <= new TimeSpan(17, 0, 0).Add(TimeSpan.FromMinutes(1));

                // ✅ Load sidebar/header badge counts
                await LoadInternBadgeCounts(userId);

                return View(attendanceRecords);
            }
            catch (Exception ex)
            {
                return RedirectToAction("Attendance", "Intern", new { msg = $"Error loading attendance: {ex.Message}" });
            }
        }
        [HttpPost("Intern/TimeIn")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TimeIn(IFormCollection form)
        {
            try
            {
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userIdStr))
                {
                    return RedirectToAction("Attendance", "Intern", new { msg = "Session expired. Please login again." });
                }

                int userId = int.Parse(userIdStr);
                var today = DateTime.Today;
                var currentTime = DateTime.Now;
                var sessionType = form["sessionType"].ToString();

                if (string.IsNullOrEmpty(sessionType) ||
                    !new[] { "whole", "morning", "afternoon" }.Contains(sessionType))
                {
                    return RedirectToAction("Attendance", "Intern", new { msg = "Please select a valid session type." });
                }

                // Session times
                TimeSpan sessionStartTime, sessionEndTime;
                TimeSpan lateThreshold;
                string sessionDisplayName;

                switch (sessionType)
                {
                    case "morning":
                        sessionStartTime = new TimeSpan(8, 0, 0);
                        sessionEndTime = new TimeSpan(12, 0, 0);
                        lateThreshold = new TimeSpan(8, 1, 0);
                        sessionDisplayName = "Morning Session (8:00 AM - 12:00 PM)";
                        break;
                    case "afternoon":
                        sessionStartTime = new TimeSpan(13, 0, 0);
                        sessionEndTime = new TimeSpan(17, 0, 0);
                        lateThreshold = new TimeSpan(13, 1, 0);
                        sessionDisplayName = "Afternoon Session (1:00 PM - 5:00 PM)";
                        break;
                    default:
                        sessionStartTime = new TimeSpan(8, 0, 0);
                        sessionEndTime = new TimeSpan(17, 0, 0);
                        lateThreshold = new TimeSpan(8, 1, 0);
                        sessionDisplayName = "Whole Day (8:00 AM - 5:00 PM)";
                        break;
                }

                var sessionStart = today.Add(sessionStartTime);
                var sessionEnd = today.Add(sessionEndTime);

                if (currentTime < sessionStart)
                {
                    return RedirectToAction("Attendance", "Intern", new { msg = $"You cannot time in before {sessionStart:h:mm tt} for {sessionDisplayName}." });
                }

                if (currentTime >= sessionEnd)
                {
                    return RedirectToAction("Attendance", "Intern", new { msg = $"Cannot time in after {sessionEnd:h:mm tt} for {sessionDisplayName}." });
                }

                var existingRecord = await _context.Attendance
                    .FirstOrDefaultAsync(a => a.UserId == userId && a.Date == today && !a.IsDeleted);

                if (existingRecord != null && existingRecord.TimeIn.HasValue)
                {
                    return RedirectToAction("Attendance", "Intern", new { msg = "You've already timed in today." });
                }
                var user = _context.Users.FirstOrDefault(r => r.UserId == userId);
                if (user?.HoursToRender == null || user.HoursToRender == 0)
                {
                    return RedirectToAction("Attendance", "Intern", new { msg = "Please fill your required hours before time-in. Hours to render cannot be 0 or empty." });
                }

                if (user.TotalRenderedHours >= user.HoursToRender)
                {
                    return RedirectToAction("Attendance", "Intern", new { msg = "Congratulations!! You completely rendered your required hours." });
                }
                    // ---- ✅ Handle Proof Image or Camera Capture ----
                var proofImage = form.Files["proofImage"];
                var capturedImage = form["capturedImage"].ToString();
                string proofImagePath = null;

                if (proofImage != null && proofImage.Length > 0)
                {
                    if (proofImage.Length > 5 * 1024 * 1024)
                    {
                        return RedirectToAction("Attendance", "Intern", new { msg = "Proof image must be less than 5MB." });
                    }

                    var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".jfif" };
                    var fileExtension = Path.GetExtension(proofImage.FileName).ToLower();
                    if (!validExtensions.Contains(fileExtension))
                    {
                        return RedirectToAction("Attendance", "Intern", new { msg = "Only JPG, PNG, JFIF, and GIF images are allowed." });
                    }

                    proofImagePath = await SaveProofImage(proofImage);
                }
                else if (!string.IsNullOrEmpty(capturedImage))
                {
                    // Captured from camera (base64 string)
                    var base64Data = Regex.Match(capturedImage, @"data:image/(?<type>.+?),(?<data>.+)").Groups["data"].Value;
                    var bytes = Convert.FromBase64String(base64Data);

                    // Save manually to disk
                    string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads/proofs");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    string fileName = $"{Guid.NewGuid()}.png";
                    string filePath = Path.Combine(uploadsFolder, fileName);
                    await System.IO.File.WriteAllBytesAsync(filePath, bytes);

                    proofImagePath = "/uploads/proofs/" + fileName;
                }
                else
                {
                    return RedirectToAction("Attendance", "Intern", new { msg = "Proof image is required." });
                }

                // ---- Attendance Record ----
                var attendance = existingRecord ?? new Attendance
                {
                    UserId = userId,
                    Date = today,
                    ApprovalStatus = "Pending",
                    Session = sessionType
                };

                // Late check
                var lateTime = today.Add(lateThreshold);
                string statusMessage;
                if (currentTime > lateTime)
                {
                    var lateDuration = currentTime - lateTime;
                    var formattedLate = $"{(int)lateDuration.TotalHours}:{lateDuration.Minutes:D2}";
                    statusMessage = $"{user.FirstName} {user.LastName} timed in late for {sessionDisplayName} at {currentTime:hh:mm tt} ({formattedLate} late).";
                    attendance.IsLate = (int)lateDuration.TotalMinutes;
                }
                else
                {
                    statusMessage = $"{user.FirstName} {user.LastName} timed in on time for {sessionDisplayName} at {currentTime:hh:mm tt}.";
                    attendance.IsLate = 0;
                }

                attendance.TimeIn = currentTime;
                attendance.Remarks = form["remarks"];
                attendance.ProofImagePath = proofImagePath;
                attendance.IsDeleted = false;
                attendance.IsAbsent = false;

                if (existingRecord == null)
                {
                    _context.Attendance.Add(attendance);
                }

                await _context.SaveChangesAsync();

                await NotifyStaff("Time In", statusMessage, "Attendance", attendance.AttendanceId, "Attendance", Url.Action("Attendance", "Admin"));

                return RedirectToAction("Attendance", "Intern", new { msg = $"Time in recorded successfully for {sessionDisplayName}!" });
            }
            catch (Exception ex)
            {
                return RedirectToAction("Attendance", "Intern", new { msg = $"Error: {ex.Message}" });
            }
        }

        [HttpPost("Intern/TimeOut")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TimeOut()
        {
            try
            {
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userIdStr))
                {
                    return RedirectToAction("Attendance", "Intern", new { msg = "Session expired. Please login again." });
                }

                int userId = int.Parse(userIdStr);
                var today = DateTime.Today;

                // ✅ Change this line to simulate fake timeout (example: +3h30m after now)
                var currentTime = DateTime.Now; //.AddHours(6) sample

                var attendance = await _context.Attendance
                    .FirstOrDefaultAsync(a => a.UserId == userId && a.Date == today && !a.IsDeleted);

                if (attendance == null)
                {
                    return RedirectToAction("Attendance", "Intern", new { msg = "No time in record found for today." });
                }

                if (attendance.TimeOut.HasValue)
                {
                    return RedirectToAction("Attendance", "Intern", new { msg = "You've already timed out today." });
                }

                // Session-based validation
                if (!string.IsNullOrEmpty(attendance.Session))
                {
                    TimeSpan sessionEndTime = attendance.Session switch
                    {
                        "morning" => new TimeSpan(12, 0, 0),
                        "afternoon" => new TimeSpan(17, 0, 0),
                        "whole" => new TimeSpan(17, 0, 0),
                        _ => new TimeSpan(17, 0, 0)
                    };

                    var sessionEnd = attendance.Date.Date.Add(sessionEndTime);

                    if (currentTime < sessionEnd.AddMinutes(-30))
                    {
                        var sessionName = attendance.Session switch
                        {
                            "morning" => "Morning Session",
                            "afternoon" => "Afternoon Session",
                            "whole" => "Whole Day",
                            _ => "Session"
                        };

                        attendance.Remarks = string.IsNullOrEmpty(attendance.Remarks)
                            ? $"Early timeout from {sessionName}"
                            : attendance.Remarks + $" (Early timeout from {sessionName})";
                    }
                }
                attendance.TimeOut = currentTime;

                var oldRendered = attendance.RenderedHours ?? 0;

                if (attendance.TimeIn.HasValue)
                {
                    // Total hours worked
                    decimal totalWorked = (decimal)(attendance.TimeOut.Value - attendance.TimeIn.Value).TotalHours;

                    // Deduct lunch/break if worked > 4h
                    if (attendance.Session == "whole" && totalWorked > 4)
                    {
                        totalWorked -= 1;
                    }
                    else if ((attendance.Session == "morning" || attendance.Session == "afternoon") && totalWorked > 4)
                    {
                        totalWorked -= 1; // break
                    }

                    // Session cap
                    decimal sessionMax = attendance.Session switch
                    {
                        "morning" => 4m,
                        "afternoon" => 4m,
                        "whole" => 8m,
                        _ => 0m
                    };

                    // ✅ Rendered includes ALL worked hours (after break)
                    attendance.RenderedHours = Math.Round(totalWorked, 2);

                    // ✅ Overtime is just the difference (for display)
                    attendance.Overtime = totalWorked > sessionMax
                        ? Math.Round(totalWorked - sessionMax, 2)
                        : 0m;
                }
                else
                {
                    attendance.RenderedHours = 0;
                    attendance.Overtime = 0;
                }

                _context.Attendance.Update(attendance);

                var user = await _context.Users.FindAsync(attendance.UserId);
                if (user != null)
                {
                    user.TotalRenderedHours = (user.TotalRenderedHours - oldRendered) + (attendance.RenderedHours ?? 0);
                    _context.Users.Update(user);
                }

                await _context.SaveChangesAsync();

                var sessionDisplayName = attendance.Session switch
                {
                    "morning" => "Morning Session",
                    "afternoon" => "Afternoon Session",
                    "whole" => "Whole Day",
                    _ => "Session"
                };

                var message = $"{user.FirstName} {user.LastName} timed out from {sessionDisplayName} at {attendance.TimeOut?.ToString("hh:mm tt")}.";
                await NotifyStaff("Time Out", message, "Attendance", attendance.AttendanceId, "Attendance", Url.Action("Attendance", "Admin"));

                return RedirectToAction("Attendance", "Intern", new { msg = "Time out recorded successfully!" });
            }
            catch (Exception ex)
            {
                return RedirectToAction("Attendance", "Intern", new { msg = $"Error: {ex.Message}" });
            }
        }

        [HttpPost("Intern/MarkAbsent")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAbsent(string absentReason)
        {
            try
            {
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userIdStr))
                {
                    return RedirectToAction("Attendance", "Intern", new {msg = "Session expired. Please login again." });
                }

                int userId = int.Parse(userIdStr);
                var today = DateTime.Today;

                // Check if already has attendance record for today
                var existingRecord = await _context.Attendance
                    .FirstOrDefaultAsync(a => a.UserId == userId && a.Date == today && !a.IsDeleted);

                if (existingRecord != null)
                {
                    return RedirectToAction("Attendance", "Intern", new { msg = "You already have an attendance record for today." });
                }

                // Create absent record (no TimeSettings anymore)
                var attendance = new Attendance
                {
                    UserId = userId,
                    Date = today,
                    ApprovalStatus = "",
                    IsAbsent = true,
                    Remarks = absentReason,
                    IsDeleted = false
                };

                _context.Attendance.Add(attendance);
                await _context.SaveChangesAsync();

                var user = await _context.Users.FindAsync(userId);
                var message = $"{user.FirstName} {user.LastName} marked absent. Reason: {absentReason}";
                await NotifyStaff("Marked Absent", message, "Attendance", attendance.AttendanceId, "Attendance", Url.Action("Attendance", "Admin"));

                return RedirectToAction("Attendance", "Intern", new { msg = "Absence recorded successfully!" });
            }
            catch (Exception ex)
            {
                return RedirectToAction("Attendance", "Intern", new { msg = $"Error: {ex.Message}" });
            }
        }

        [HttpPost("Intern/UpdateAttendance")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAttendance(int attendanceId, string remarks, IFormFile proofImage)
        {
            try
            {
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userIdStr))
                {
                    return RedirectToAction("Attendance", "Intern", new { msg = "Session expired. Please login again." });
                }

                int userId = int.Parse(userIdStr);
                var attendance = await _context.Attendance
                    .FirstOrDefaultAsync(a => a.AttendanceId == attendanceId && a.UserId == userId && !a.IsDeleted);

                if (attendance == null)
                {
                    return RedirectToAction("Attendance", "Intern", new { msg = "Attendance record not found." });
                }

                // Allow editing remarks for absent records
                if (attendance.IsAbsent)
                {
                    attendance.Remarks = remarks;
                    await _context.SaveChangesAsync();
                    return RedirectToAction("Attendance", "Intern", new { msg = "Absence remarks updated successfully!" });
                }

                // For regular attendance records
                if (attendance.ApprovalStatus == "Approved")
                {
                    return RedirectToAction("Attendance", "Intern", new { msg = "Cannot edit approved attendance records." });
                }

                // Update proof image if provided
                if (proofImage != null && proofImage.Length > 0)
                {
                    if (proofImage.Length > 5 * 1024 * 1024) // 5MB max
                    {
                        return RedirectToAction("Attendance", "Intern", new { msg = "Proof image must be less than 5MB." });
                    }

                    var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".jfif" };
                    var fileExtension = Path.GetExtension(proofImage.FileName).ToLower();
                    if (!validExtensions.Contains(fileExtension))
                    {
                        return RedirectToAction("Attendance", "Intern", new { msg = "Only JPG, PNG, GIF, and JFIF images are allowed." });
                    }

                    // Delete old image if exists
                    if (!string.IsNullOrEmpty(attendance.ProofImagePath))
                    {
                        var oldImagePath = Path.Combine(_environment.WebRootPath, "uploads", "attendance", attendance.ProofImagePath);
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }

                    // Save new image
                    attendance.ProofImagePath = await SaveProofImage(proofImage);
                }

                // Update remarks
                attendance.Remarks = remarks;

                await _context.SaveChangesAsync();

                return RedirectToAction("Attendance", "Intern", new { msg = "Attendance updated successfully!" });
            }
            catch (Exception ex)
            {
                return RedirectToAction("Attendance", "Intern", new { msg = $"Error: {ex.Message}" });
            }
        }

        [HttpPost("Intern/DeleteAttendance")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAttendance(int attendanceId)
        {
            try
            {
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userIdStr))
                {
                    return RedirectToAction("Login", "Account", new {msg = "Session expired. Please login again." });
                }

                int userId = int.Parse(userIdStr);
                var attendance = await _context.Attendance
                    .FirstOrDefaultAsync(a => a.AttendanceId == attendanceId && a.UserId == userId);

                if (attendance == null)
                {
                    return RedirectToAction("Attendance", "Intern", new { msg = "Attendance record not found." });
                }

                if (attendance.ApprovalStatus == "Approved")
                {
                    return RedirectToAction("Attendance", "Intern", new { msg = "Cannot delete approved attendance records." });
                }

                // Delete proof image if exists
                if (!string.IsNullOrEmpty(attendance.ProofImagePath))
                {
                    var imagePath = Path.Combine(_environment.WebRootPath, "uploads", "attendance", attendance.ProofImagePath);
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }

                // Soft delete
                attendance.IsDeleted = true;
                await _context.SaveChangesAsync();

                return RedirectToAction("Attendance", "Intern", new { msg = "Attendance record deleted successfully." });
            }
            catch (Exception ex)
            {
                return RedirectToAction("Attendance", "Intern", new { msg = $"Error deleting attendance: {ex.Message}" });
            }
        }

        private async Task<string> SaveProofImage(IFormFile proofImage)
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "attendance");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(proofImage.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await proofImage.CopyToAsync(fileStream);
            }

            return uniqueFileName;
        }


        [HttpGet("Intern/Evaluation")]
        public async Task<IActionResult> Evaluation()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("Login", "Account");

            int userId = int.Parse(userIdStr);

            var evaluations = _context.Evaluations
                .Where(e => e.InternId == userId)
                .Include(e => e.ReviewedBy)
                .ToList();

            // ✅ Load badge counts
            await LoadInternBadgeCounts(userId);

            return View(evaluations);
        }
        [HttpPost("Intern/UploadEvaluation")]
        public async Task<IActionResult> UploadEvaluation(IFormFile file)
        {
            try
            {
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                {
                    return RedirectToAction("Login", "Account");
                }

                // Get logged-in intern
                var intern = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                if (intern == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Ensure hours requirement is set
                if (intern.HoursToRender == 0)
                {
                    return RedirectToAction("Evaluation", "Intern", new
                    {
                        msg = "Please set your required hours in your profile page before uploading evaluations."
                    });
                }

                // ✅ Allow only one evaluation per intern
                var existingEval = await _context.Evaluations.FirstOrDefaultAsync(e => e.InternId == userId);
                if (existingEval != null)
                {
                    return RedirectToAction("Evaluation", "Intern", new
                    {
                        msg = "You already uploaded an evaluation. Please delete it before uploading a new one."
                    });
                }

                // ✅ Calculate rendered hours as decimal, rounded
                var totalRenderedDecimalHours = await _context.Attendance
                    .Where(a => a.UserId == userId && !a.IsDeleted && !a.IsAbsent && a.RenderedHours.HasValue)
                    .SumAsync(a => a.RenderedHours ?? 0);

                var renderedHoursRounded = Math.Round(totalRenderedDecimalHours, 2);
                var requiredHours = intern.HoursToRender;
                decimal hours = intern.TotalRenderedHours;
                // ✅ Check completion - FIXED: Use the same calculated value in the message
                if (hours < requiredHours)
                {
                    return RedirectToAction("Evaluation", "Intern", new
                    {
                        msg = $"Your required hours are not yet complete. " +
                              $"Rendered: {hours}h, " +
                              $"Required: {requiredHours}h"
                    });
                }


                // ✅ Validate file
                if (file == null || file.Length == 0)
                {
                    return RedirectToAction("Evaluation", "Intern", new
                    {
                        msg = "Please select a valid file to upload."
                    });
                }

                if (file.Length > 10 * 1024 * 1024)
                {
                    return RedirectToAction("Evaluation", "Intern", new
                    {
                        msg = "File size must be less than 10MB."
                    });
                }

                var allowedExtensions = new[] { ".pdf", ".docx", ".jfif" };
                var fileExtension = Path.GetExtension(file.FileName).ToLower();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return RedirectToAction("Evaluation", "Intern", new
                    {
                        msg = "Only PDF, Word documents (DOCX), and JFIF images are allowed."
                    });
                }

                // ✅ Save file
                string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "evaluations");
                Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = $"{userId}_{DateTime.Now:yyyyMMddHHmmss}{fileExtension}";
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // ✅ Create evaluation record
                var evaluation = new Evaluation
                {
                    InternId = userId,
                    OriginalFilePath = $"/uploads/evaluations/{uniqueFileName}",
                    Status = "Submitted",
                    UploadDate = DateTime.Now
                };

                _context.Evaluations.Add(evaluation);
                await _context.SaveChangesAsync();

                // ✅ Notify Admin
                await _context.SaveChangesAsync();
                await NotifyStaff("Marked Absent", $"{intern.FirstName} {intern.LastName} uploaded an evaluation.", "Evaluation", evaluation.EvaluationId, "Attendance", Url.Action("Evaluation", "Admin"));

                return RedirectToAction("Evaluation", "Intern", new
                {
                    msg = " Evaluation uploaded successfully!"
                });
            }
            catch (Exception ex)
            {
                return RedirectToAction("Evaluation", "Intern", new
                {
                    msg = $"Error uploading evaluation: {ex.Message}"
                });
            }
        }

        [HttpPost("Intern/DeleteEvaluation")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEvaluation(int id)
        {
            try
            {
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                {
                    return RedirectToAction("Login", "Account");
                }

                var evaluation = await _context.Evaluations
                    .Include(e => e.Intern)
                    .FirstOrDefaultAsync(e => e.EvaluationId == id);

                if (evaluation == null)
                {
                    return RedirectToAction("Evaluation", "Intern", new
                    {
                        msg = "Evaluation not found."
                    });
                }

                if (evaluation.InternId != userId)
                {
                    return RedirectToAction("Evaluation", "Intern", new
                    {
                        msg = "You are not authorized to delete this evaluation."
                    });
                }

                if (evaluation.Status == "Completed" || evaluation.Status == "Reviewed")
                {
                    return RedirectToAction("Evaluation", "Intern", new
                    {
                        msg = "Cannot delete evaluations that have been reviewed or completed."
                    });
                }

                if (!string.IsNullOrEmpty(evaluation.OriginalFilePath))
                {
                    var filePath = Path.Combine(_environment.WebRootPath, evaluation.OriginalFilePath.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }

                if (!string.IsNullOrEmpty(evaluation.ReviewedFilePath))
                {
                    var reviewedFilePath = Path.Combine(_environment.WebRootPath, evaluation.ReviewedFilePath.TrimStart('/'));
                    if (System.IO.File.Exists(reviewedFilePath))
                    {
                        System.IO.File.Delete(reviewedFilePath);
                    }
                }

                _context.Evaluations.Remove(evaluation);
                await _context.SaveChangesAsync();


                return RedirectToAction("Evaluation", "Intern", new
                {
                    msg = "Evaluation deleted successfully."
                });
            }
            catch (Exception ex)
            {
                return RedirectToAction("Evaluation", "Intern", new
                {
                    msg = $"Error deleting evaluation: {ex.Message}"
                });
            }
        }
        public async Task<IActionResult> Dashboard()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = int.Parse(userIdStr);

            // ✅ Fetch logged-in user
            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // ✅ Role check (only Intern allowed here)
            if (!string.Equals(user.Position, "Intern", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Login", "Account");
                // 👉 Create an AccessDenied page with a message
            }

            // ✅ Hours tracking from User model
            var hoursCompleted = user.TotalRenderedHours;
            var totalHours = user.HoursToRender ?? 0;

            var hoursProgress = totalHours > 0
                ? Math.Min((int)Math.Round((hoursCompleted / totalHours) * 100), 100)
                : 0;

            ViewBag.HoursCompleted = hoursCompleted;
            ViewBag.TotalHours = totalHours;
            ViewBag.HoursProgress = hoursProgress;

            // Tasks
            var tasks = _context.TasksManage
                .Where(t => t.AssignedToId == userId)
                .ToList();

            // Announcements
            var announcements = _context.Announcements
                .OrderByDescending(a => a.DatePosted)
                .Include(a => a.Poster)
                .ToList();

            ViewData["Announcements"] = announcements;

            // ✅ Load badge counts for sidebar/header
            await LoadInternBadgeCounts(userId);

            return View(tasks);
        }

        // Main notifications page
        public async Task<IActionResult> Notifications(bool showDeleted = false)
        {
            var sessionUserId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(sessionUserId) || !int.TryParse(sessionUserId, out int userId))
            {
                return RedirectToAction("Login", "Account"); // not logged in
            }

            IQueryable<Notification> query = _context.Notifications
                .Include(n => n.Sender)
                .Where(n => n.UserId == userId); // ✅ Fetch ONLY this intern's notifications

            // Filter by deleted status
            if (showDeleted)
            {
                query = query.Where(n => n.IsDeleted);
                ViewBag.ShowDeleted = true;
            }
            else
            {
                query = query.Where(n => !n.IsDeleted);
                ViewBag.ShowDeleted = false;
            }

            var allNotifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .ToListAsync();

            // ✅ Count unread (only non-deleted)
            var unreadCount = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead && !n.IsDeleted)
                .CountAsync();

            ViewBag.UnreadCount = unreadCount;

            // ✅ Load all sidebar badge counts
            await LoadInternBadgeCounts(userId);

            return View(allNotifications);
        }

        // Mark one notification as read
        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var sessionUserId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(sessionUserId) || !int.TryParse(sessionUserId, out int userId))
                return Unauthorized();

            // ✅ Ownership check
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

            if (notification == null)
                return NotFound();

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        // Mark all notifications as read
        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var sessionUserId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(sessionUserId) || !int.TryParse(sessionUserId, out int userId))
                return Unauthorized();

            var unread = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead && !n.IsDeleted)
                .ToListAsync();

            foreach (var n in unread)
                n.IsRead = true;

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // Soft delete one notification (move to trash)
        [HttpPost]
        public async Task<IActionResult> SoftDeleteNotification(int id)
        {
            var sessionUserId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(sessionUserId) || !int.TryParse(sessionUserId, out int userId))
                return Unauthorized();

            // ✅ Ownership check
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

            if (notification == null)
                return NotFound();

            notification.IsDeleted = true;
            notification.DeletedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        // Soft delete all notifications (move to trash)
        [HttpPost]
        public async Task<IActionResult> SoftDeleteAllNotifications()
        {
            var sessionUserId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(sessionUserId) || !int.TryParse(sessionUserId, out int userId))
                return Unauthorized();

            var allNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsDeleted)
                .ToListAsync();

            foreach (var notification in allNotifications)
            {
                notification.IsDeleted = true;
                notification.DeletedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // Restore a deleted notification
        [HttpPost]
        public async Task<IActionResult> RestoreNotification(int id)
        {
            var sessionUserId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(sessionUserId) || !int.TryParse(sessionUserId, out int userId))
                return Unauthorized();

            // ✅ Ownership check
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

            if (notification == null)
                return NotFound();

            notification.IsDeleted = false;
            notification.DeletedAt = null;
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        // Permanent delete of a notification
        [HttpPost]
        public async Task<IActionResult> PermanentDeleteNotification(int id)
        {
            var sessionUserId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(sessionUserId) || !int.TryParse(sessionUserId, out int userId))
                return Unauthorized();

            // ✅ Ownership check
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

            if (notification == null)
                return NotFound();

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        // Permanent delete all notifications in trash
        [HttpPost]
        public async Task<IActionResult> PermanentDeleteAllNotifications()
        {
            var sessionUserId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(sessionUserId) || !int.TryParse(sessionUserId, out int userId))
                return Unauthorized();

            var allDeletedNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && n.IsDeleted)
                .ToListAsync();

            _context.Notifications.RemoveRange(allDeletedNotifications);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, deletedCount = allDeletedNotifications.Count });
        }

        // DEPRECATED: Keep for backward compatibility but redirect to SoftDeleteNotification
        [HttpPost]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            return await SoftDeleteNotification(id);
        }

        // DEPRECATED: Keep for backward compatibility but redirect to SoftDeleteAllNotifications  
        [HttpPost]
        public async Task<IActionResult> ClearAllNotifications()
        {
            return await SoftDeleteAllNotifications();
        }

        // Helper method to notify staff (same as before)
        private async Task NotifyStaff(string title, string message, string type, int relatedId, string relatedEntity, string link)
        {
            var staff = await _context.Users
                .Where(u => u.Position == "Supervisor" || u.Position == "Trainer")
                .ToListAsync();

            var notifications = new List<Notification>();

            // Notify all supervisors/trainers
            foreach (var s in staff)
            {
                notifications.Add(new Notification
                {
                    UserId = s.UserId,
                    Title = title,
                    Message = message,
                    NotificationType = type,
                    RelatedId = relatedId,
                    RelatedEntity = relatedEntity,
                    Link = link,
                    CreatedAt = DateTime.Now,
                    IsRead = false
                });
            }

            // ✅ Hardcoded Admin notification (UserId = null)
            notifications.Add(new Notification
            {
                UserId = null,
                Title = title,
                Message = message,
                NotificationType = type,
                RelatedId = relatedId,
                RelatedEntity = relatedEntity,
                Link = link,
                CreatedAt = DateTime.Now,
                IsRead = false
            });

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }
        // InternController.cs
        private async Task LoadInternBadgeCounts(int userId)
        {
            // Attendance → show their pending attendance requests (waiting for approval)
            ViewBag.AttendanceCount = await _context.Attendance
                .CountAsync(a => a.ApprovalStatus == "Pending"
                                 && !a.IsDeleted
                                 && a.UserId == userId);

            // MyTasks → tasks assigned to them that are still pending/in progress
            ViewBag.TasksCount = await _context.TasksManage
                .CountAsync(t => (t.Status == "Pending" || t.Status == "In Progress")
                                 && t.AssignedToId == userId);

            // Documents → documents they uploaded that are pending approval
            ViewBag.DocumentsCount = await _context.Documents
                .CountAsync(d => d.Status == "Pending"
                                 && d.UserId == userId);    

            // Evaluation → new evaluations they can review (marked "Reviewed" by supervisor)
            ViewBag.EvaluationCount = await _context.Evaluations
                .CountAsync(e => e.Status == "Submitted"
                                 && e.InternId == userId);

            // Notifications → unread + not deleted
            ViewBag.NotificationsCount = await _context.Notifications
                .CountAsync(n => !n.IsRead
                                 && !n.IsDeleted
                                 && n.UserId == userId);


            // Intern details
            var intern = await _context.Users.FindAsync(userId);
            if (intern != null)
            {
                ViewBag.ProfileImagePath = string.IsNullOrEmpty(intern.ProfileImagePath)
                    ? null // or set a default image path here, e.g., "~/images/default-avatar.png"
                    : intern.ProfileImagePath;
                ViewBag.Intern = $"{intern.FirstName} {intern.LastName}";
                ViewBag.Position = intern.Position;
            }
        }
    
    }
}