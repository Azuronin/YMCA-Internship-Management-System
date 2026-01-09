using Microsoft.AspNetCore.Mvc;
using IMS.Data;
using IMS.Models;
using Microsoft.EntityFrameworkCore;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;
using Syncfusion.Pdf;

namespace IMS.Controllers
{
    public class AdminController : Controller
    {
        private readonly MyAppContext _context;
        private readonly IWebHostEnvironment _environment;

        public AdminController(MyAppContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }
        //USER MANAGEMENT
        public async Task<IActionResult> ManageUsers()
        {
            var sessionUserId = HttpContext.Session.GetString("UserId"); // ✅ get logged-in user

            var pending = _context.Users.Where(u => u.Status == "Pending").ToList();
            var approved = _context.Users.Where(u => u.Status == "Approved").ToList();
            var disapproved = _context.Users.Where(u => u.Status == "Disapproved").ToList();

            ViewBag.User = sessionUserId;
            await LoadBadgeCounts();
            return View((pending, approved, disapproved));
        }

        [HttpPost]
        public IActionResult Approve(int id)
        {
            try
            {
                var user = _context.Users.Find(id);
                if (user != null)
                {
                    user.Status = "Approved";
                    _context.SaveChanges();
                }
                return RedirectToAction("ManageUsers", "Admin", new { msg = "Approved successfully." });
            }
            catch (Exception ex)
            {
                return RedirectToAction("ManageUsers", "Admin", new { msg = "Error approving user: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult Disapprove(int id)
        {
            try
            {
                var user = _context.Users.Find(id);
                if (user != null)
                {
                    user.Status = "Disapproved";
                    _context.SaveChanges();
                }
                return RedirectToAction("ManageUsers", "Admin", new { msg = "Disapproved successfully." });
            }
            catch (Exception ex)
            {
                return RedirectToAction("ManageUsers", "Admin", new { msg = "Error disapproving user: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var user = _context.Users.FirstOrDefault(u => u.UserId == id);

            if (user != null)
            {
                // ✅ Delete related notifications
                var notifications = _context.Notifications.Where(n => n.UserId == id);
                _context.Notifications.RemoveRange(notifications);

                // ✅ Delete related attendance records
                var attendance = _context.Attendance.Where(a => a.UserId == id);
                _context.Attendance.RemoveRange(attendance);

                // ✅ Delete related tasks
                var tasks = _context.TasksManage.Where(t => t.AssignedToId == id || t.TaskId == id);
                _context.TasksManage.RemoveRange(tasks);

                // ✅ Delete related documents
                var documents = _context.Documents.Where(d => d.UserId == id);
                _context.Documents.RemoveRange(documents);

                // ✅ Delete related applications
                var applications = _context.Announcements.Where(a => a.PosterId == id);
                _context.Announcements.RemoveRange(applications);

                // ✅ Delete related applications
                var evaluation = _context.Evaluations.Where(a => a.EvaluationId == id);
                _context.Evaluations.RemoveRange(evaluation);

                // ✅ Finally delete the user
                _context.Users.Remove(user);

                _context.SaveChanges();
            }

            return RedirectToAction("ManageUsers", "Admin", new { msg = "Deleted permanently." });
        }


        [HttpPost]
        public IActionResult AddUser(User user)
        {
            try
            {
                var existingUser = _context.Users
                    .FirstOrDefault(u => u.Email.ToLower() == user.Email.ToLower());

                if (existingUser != null)
                {
                    return RedirectToAction("ManageUsers", "Admin", new { msg = "Invalid input email already exists." });
                }

                user.HoursToRender = 0;
                user.Status = "Approved";
                user.ProfileImagePath = "/images/default.png";
                user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);

                _context.Users.Add(user);
                _context.SaveChanges();

                return RedirectToAction("ManageUsers", "Admin", new { msg = "Added successfully." });
            }
            catch (Exception ex)
            {
                return RedirectToAction("ManageUsers", "Admin", new { msg = "Error adding user: " + (ex.InnerException?.Message ?? ex.Message) });
            }
        }

        [HttpPost]
        public IActionResult EditUser(User updatedUser, IFormFile ProfileImage, string NewPassword)
        {
            try
            {
                var existingUser = _context.Users.FirstOrDefault(u => u.UserId == updatedUser.UserId);
                if (existingUser != null)
                {
                    existingUser.FirstName = updatedUser.FirstName;
                    existingUser.MiddleName = updatedUser.MiddleName;
                    existingUser.LastName = updatedUser.LastName;
                    existingUser.Email = updatedUser.Email;
                    existingUser.Position = updatedUser.Position;
                    existingUser.Address = updatedUser.Address;
                    // ✅ Reset password only if a new one is provided
                    if (!string.IsNullOrWhiteSpace(NewPassword))
                    {
                        existingUser.Password = BCrypt.Net.BCrypt.HashPassword(NewPassword);
                    }

                    if (ProfileImage != null && ProfileImage.Length > 0)
                    {
                        string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads/profiles");
                        Directory.CreateDirectory(uploadsFolder);

                        if (!string.IsNullOrEmpty(existingUser.ProfileImagePath))
                        {
                            string oldImagePath = Path.Combine(_environment.WebRootPath, existingUser.ProfileImagePath.TrimStart('/'));
                            if (System.IO.File.Exists(oldImagePath))
                                System.IO.File.Delete(oldImagePath);
                        }

                        string fileName = Guid.NewGuid() + Path.GetExtension(ProfileImage.FileName);
                        string fullPath = Path.Combine(uploadsFolder, fileName);
                        using (var stream = new FileStream(fullPath, FileMode.Create))
                        {
                            ProfileImage.CopyTo(stream);
                        }

                        existingUser.ProfileImagePath = "/uploads/profiles/" + fileName;
                    }

                    _context.SaveChanges();
                }

                return RedirectToAction("ManageUsers", "Admin", new { msg = "Updated successfully." });
            }
            catch (Exception ex)
            {
                return RedirectToAction("ManageUsers", "Admin", new { msg = "Error updating user: " + ex.Message });
            }
        }
        public async Task<IActionResult> Applications()
        {
            // Get ALL applicants (to display in the view)
            var applicants = _context.Applicants
                .OrderByDescending(a => a.UploadDate)
                .ToList();

            // Mark unseen applicants as seen
            var unseenApplicants = applicants.Where(a => !a.IsSeen).ToList();
            foreach (var applicant in unseenApplicants)
            {
                applicant.IsSeen = true;
            }

            // Load all documents
            var documents = _context.Documents
                .Include(d => d.User)         // Uploader
                .Include(d => d.ApprovedBy)   // Approver
                .OrderBy(d =>
                    d.Status == "Pending" ? 0 :
                    d.Status == "Approved" ? 1 :
                    d.Status == "Rejected" ? 2 : 3
                )
                .ToList();

            await _context.SaveChangesAsync();

            // Pass documents for the view
            ViewBag.Documents = documents;

            // Refresh badge counts (this will now show reduced count)
            await LoadBadgeCounts();

            return View(applicants); // ✅ Show ALL applicants
        }

        //[HttpPost]
        //public IActionResult QualifyApplicant(int id)
        //{
        //    var applicant = _context.Applicants.Find(id);
        //    if (applicant == null)
        //    {
        //        return RedirectToAction("Applications", "Admin", new { msg = "Applicant not found." });
        //    }

        //    applicant.Status = "Qualified";
        //    _context.SaveChanges();

        //    return RedirectToAction("Applications", "Admin", new { msg = $"{applicant.FullName} marked as Qualified." });
        //}

        //[HttpPost]
        //public IActionResult RejectApplicant(int id)
        //{
        //    var applicant = _context.Applicants.Find(id);
        //    if (applicant == null)
        //    {
        //        return RedirectToAction("Applications", "Admin", new { msg = "Applicant not found." });
        //    }

        //    applicant.Status = "Rejected";
        //    _context.SaveChanges();

        //    return RedirectToAction("Applications", "Admin", new { msg = $"{applicant.FullName} marked as Rejected." });
        //}

        [HttpPost]
        public IActionResult DeleteResume(int id)
        {
            var resume = _context.Applicants.Find(id);
            if (resume != null)
            {
                string fullPath = Path.Combine(_environment.WebRootPath, resume.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
                _context.Applicants.Remove(resume);
                _context.SaveChanges();
            }

            return RedirectToAction("Applications", "Admin", new { msg = "Deleted successfully." });
        }


        [HttpGet]
        public IActionResult DownloadResume(int id)
        {
            var resume = _context.Applicants.FirstOrDefault(r => r.ApplicantId == id);
            if (resume == null) return NotFound();

            string fullPath = Path.Combine(_environment.WebRootPath, resume.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(fullPath)) return NotFound();

            string contentType = GetContentType(fullPath);
            byte[] fileBytes = System.IO.File.ReadAllBytes(fullPath);
            return File(fileBytes, contentType, resume.FileName);
        }
        [HttpPost]
        public IActionResult ApproveDocument(int id)
        {
            var document = _context.Documents.FirstOrDefault(d => d.DocumentId == id);

            if (document != null)
            {
                string? sessionUserId = HttpContext.Session.GetString("UserId");
                string? sessionRole = HttpContext.Session.GetString("UserRole");

                int? senderId = null;
                string senderName = GetSenderName();

                // ✅ If supervisor/trainer, track approver
                if (!string.IsNullOrEmpty(sessionUserId) && int.TryParse(sessionUserId, out int fromUserId) && fromUserId > 0)
                {
                    senderId = fromUserId;
                    document.ApprovedById = fromUserId;
                }
                else if (sessionRole == "Admin")
                {
                    document.ApprovedById = null; // Admin is not in Users table
                }

                document.Status = "Approved";
                _context.SaveChanges();

                // 🔔 Notify intern
                var notif = new Notification
                {
                    UserId = document.UserId,
                    SenderId = senderId,
                    Title = "Document Approved",
                    Message = $"Your document \"{document.FileName}\" has been approved by {senderName}.",
                    NotificationType = "Document",
                    RelatedId = document.DocumentId,
                    RelatedEntity = "Documents",
                    Link = "/Intern/Documents",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                };

                _context.Notifications.Add(notif);
                _context.SaveChanges();
            }

            return RedirectToAction("Applications", "Admin", new { msg = "Approved successfully." });
        }

        [HttpPost]
        public IActionResult RejectDocument(int id)
        {
            var document = _context.Documents.FirstOrDefault(d => d.DocumentId == id);

            if (document != null)
            {
                string? sessionUserId = HttpContext.Session.GetString("UserId");
                string? sessionRole = HttpContext.Session.GetString("UserRole");

                int? senderId = null;
                string senderName = GetSenderName();

                if (!string.IsNullOrEmpty(sessionUserId) && int.TryParse(sessionUserId, out int fromUserId) && fromUserId > 0)
                {
                    senderId = fromUserId;
                    document.ApprovedById = fromUserId;
                }
                else if (sessionRole == "Admin")
                {
                    document.ApprovedById = null;
                }

                document.Status = "Rejected";
                _context.SaveChanges();

                // 🔔 Notify intern
                var notif = new Notification
                {
                    UserId = document.UserId,
                    SenderId = senderId,
                    Title = "Document Rejected",
                    Message = $"Your document \"{document.FileName}\" has been rejected by {senderName}.",
                    NotificationType = "Document",
                    RelatedId = document.DocumentId,
                    RelatedEntity = "Documents",
                    Link = "/Intern/Documents",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                };

                _context.Notifications.Add(notif);
                _context.SaveChanges();
            }

            return RedirectToAction("Applications", "Admin", new { msg = "Rejected successfully." });
        }


        [HttpPost]
        public IActionResult DeleteDocument(int id)
        {
            var document = _context.Documents.Find(id);
            if (document != null)
            {
                string fullPath = Path.Combine(_environment.WebRootPath, document.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);

                _context.Documents.Remove(document);
                _context.SaveChanges();
            }
            return RedirectToAction("Applications", "Admin", new { msg = "Deleted successfully." });
        }


        [HttpGet]
        public IActionResult DownloadDocument(int id)
        {
            var document = _context.Documents.FirstOrDefault(d => d.DocumentId == id);
            if (document == null) return NotFound();

            string fullPath = Path.Combine(_environment.WebRootPath, document.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(fullPath)) return NotFound();

            string contentType = GetContentType(fullPath);
            byte[] fileBytes = System.IO.File.ReadAllBytes(fullPath);
            return File(fileBytes, contentType, document.FileName);
        }

        private string GetContentType(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            return ext switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                _ => "application/octet-stream"
            };
        }
        public async Task<IActionResult> Documents()
        {
            var approvedDocs = _context.Documents
                .Include(d => d.User)
                .Where(d => d.Status == "Approved")
                .ToList();

            await LoadBadgeCounts();
            return View(approvedDocs);
        }
        // GET: Admin/Announcements
        public async Task<IActionResult> Announcements()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = int.Parse(userIdStr);

            // ✅ Get the logged-in user
            var currentUser = await _context.Users.FindAsync(userId);

            IQueryable<Announcements> query = _context.Announcements
                .Include(a => a.Poster);

            // ✅ Role-based filtering
            if (currentUser != null)
            {
                if (currentUser.Position == "Supervisor" || currentUser.Position == "Trainer")
                {
                    // Supervisors/Trainers → only see their own announcements
                    query = query.Where(a => a.PosterId == userId);
                }
                else if (currentUser.Position == "Admin")
                {
                    // Admin → see all announcements
                }
            }

            var announcements = await query
                .OrderByDescending(a => a.DatePosted)
                .ToListAsync();

            await LoadBadgeCounts();
            return View(announcements);
        }

        // POST: Admin/Announcements (Create)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Announcements(Announcements announcement)
        {
            if (ModelState.IsValid)
            {
                announcement.DatePosted = DateTime.Now;

                // Get session user ID
                var userIdStr = HttpContext.Session.GetString("UserId");
                var role = HttpContext.Session.GetString("UserRole");

                if (!string.IsNullOrEmpty(userIdStr) && role != "Admin")
                {
                    announcement.PosterId = int.Parse(userIdStr); // Supervisor/Trainer/Intern
                }
                else
                {
                    announcement.PosterId = null; // Hardcoded Admin
                }

                _context.Announcements.Add(announcement);
                await _context.SaveChangesAsync();

                // 🔔 Notify interns only
                var interns = _context.Users.Where(u => u.Position == "Intern").ToList();
                foreach (var intern in interns)
                {
                    var notif = new Notification
                    {
                        UserId = intern.UserId,
                        Title = "New Announcement",
                        Message = $"A new announcement \"{announcement.Title}\" has been posted.",
                        NotificationType = "Announcement",
                        RelatedId = announcement.AnnouncementId,
                        RelatedEntity = "Announcements",
                        Link = "/Intern/Dashboard",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    };
                    _context.Notifications.Add(notif);
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Announcements), "Admin", new { msg = "Announcement posted." });
            }

            var existing = await _context.Announcements
                .OrderByDescending(a => a.DatePosted)
                .ToListAsync();
            ViewBag.Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return View(existing);
        }


        // POST: Admin/EditAnnouncement
        [HttpPost]
        public async Task<IActionResult> EditAnnouncement(Announcements updated)
        {
            var existing = await _context.Announcements.FindAsync(updated.AnnouncementId);
            if (existing != null)
            {
                existing.Title = updated.Title;
                existing.Type = updated.Type;
                existing.Message = updated.Message;

                // Don't change DatePosted
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Announcements", "Admin", new { msg = "Announcement updated." });
        }

        // POST: Admin/DeleteAnnouncement
        [HttpPost]
        public async Task<IActionResult> DeleteAnnouncement(int id)
        {
            var a = await _context.Announcements.FindAsync(id);
            if (a != null)
            {
                _context.Announcements.Remove(a);
                await _context.SaveChangesAsync();

                return RedirectToAction("Announcements", "Admin",new { msg = "Announcement deleted." });
            }
            return RedirectToAction("Announcements");
        }
        //TASK MANAGEMENT
        public async Task<IActionResult> TaskManagement()
        {
            try
            {
                // ✅ Get logged-in user
                var userIdStr = HttpContext.Session.GetString("UserId");
                var role = HttpContext.Session.GetString("UserRole");

                if (string.IsNullOrEmpty(userIdStr) || string.IsNullOrEmpty(role))
                    return RedirectToAction("Login", "Account");

                int currentUserId = int.Parse(userIdStr);

                // ✅ Load all tasks
                IQueryable<TasksManage> query = _context.TasksManage
                    .Include(t => t.AssignedTo)
                    .Include(t => t.AssignedBy);

                // Supervisors/Trainers only see their assigned tasks
                if (role == "Supervisor" || role == "Trainer")
                {
                    query = query.Where(t => t.AssignedById == currentUserId);
                }

                var tasks = await query
                    .OrderByDescending(t => t.CreatedDate)
                    .ToListAsync();

                // ✅ Load approved interns separately for dropdowns
                ViewBag.ApprovedInterns = await _context.Users
                    .Where(u => u.Position == "Intern" && u.Status == "Approved")
                    .ToListAsync();

                // ✅ Keep badge counts updated
                await LoadBadgeCounts();

                return View(tasks);
            }
            catch (Exception ex)
            {
                return View(new List<TasksManage>());
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTask(
     string Title,
     string Description,
     DateTime DueDate,
     int AssignedToId,
     int Priority = 2,
     string? Category = null,
     IFormFile? TaskImage = null)
        {
            // ✅ Basic validation
            if (AssignedToId == 0 || string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Description))
            {
                return RedirectToAction("TaskManagement", "Admin", new { msg = "Title, Description, and Assignee are required." });
            }
            // ✅ Check due date (cannot be in the past)
            if (DueDate < DateTime.Now)
            {
                return RedirectToAction("TaskManagement", "Admin", new { msg = "Due date cannot be in the past." });
            }
            // ✅ Check assignee exists (intern)
            var intern = await _context.Users.FindAsync(AssignedToId);
            if (intern == null || intern.Position != "Intern")
            {
                return RedirectToAction("TaskManagement", "Admin", new { msg = "Selected intern not found or invalid." });
            }

            // ✅ Handle task image upload
            string? imagePath = null;
            if (TaskImage != null && TaskImage.Length > 0)
            {
                var fileName = Guid.NewGuid() + Path.GetExtension(TaskImage.FileName);
                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/tasks");

                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                var filePath = Path.Combine(uploadPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await TaskImage.CopyToAsync(stream);
                }

                imagePath = "/uploads/tasks/" + fileName;
            }

            // ✅ Figure out who assigned the task
            int? assignedById = null;
            var sessionUserId = HttpContext.Session.GetString("UserId");
            var sessionRole = HttpContext.Session.GetString("UserRole");

            if (int.TryParse(sessionUserId, out int currentUserId))
            {
                if (sessionRole == "Admin" && currentUserId == 0)
                {
                    assignedById = null; // Hardcoded admin (no account in Users table)
                }
                else
                {
                    assignedById = currentUserId; // Supervisor/Trainer (stored in Users)
                }
            }

            // ✅ Create task record
            var task = new TasksManage
            {
                Title = Title,
                Description = Description,
                DueDate = DueDate,
                Status = "Pending",
                AssignedToId = AssignedToId,
                AssignedById = assignedById,
                ImagePath = imagePath,
                Priority = Priority,
                CreatedDate = DateTime.Now
            };

            _context.TasksManage.Add(task);
            await _context.SaveChangesAsync();

            // Get sender name (Admin, Supervisor, Trainer)
            string senderName = GetSenderName();

            // Notify intern only (on task creation)
            var notif = new Notification
            {
                UserId = AssignedToId, // ✅ intern
                Title = "New Task Assigned",
                Message = $"You have been assigned a new task \"{task.Title}\" by {senderName}.",
                NotificationType = "Task",
                RelatedId = task.TaskId,
                RelatedEntity = "TasksManage",
                Link = "/Intern/MyTasks",
                IsRead = false,
                CreatedAt = DateTime.Now,
                SenderId = assignedById // ✅ who assigned it
            };
            _context.Notifications.Add(notif);
            await _context.SaveChangesAsync();

            return RedirectToAction("TaskManagement", "Admin", new { msg = "Task created successfully!"});
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTask(
                int TaskId,
                string Title,
                string Description,
                DateTime DueDate,
                int AssignedToId,
                int Priority,
                string? Category = null,
                IFormFile? NewImage = null,
                string? OldImagePath = null)
        {
            var task = await _context.TasksManage.FindAsync(TaskId);
            if (task == null)
            {
                return RedirectToAction("TaskManagement", "Admin", new { msg = "Task not found." });
            }

            task.Title = Title;
            task.Description = Description;
            task.DueDate = DueDate;
            task.AssignedToId = AssignedToId;
            task.Priority = Priority;

            if (NewImage != null && NewImage.Length > 0)
            {
                if (!string.IsNullOrEmpty(OldImagePath))
                {
                    var oldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", OldImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath))
                    {
                        System.IO.File.Delete(oldPath);
                    }
                }

                var fileName = Guid.NewGuid() + Path.GetExtension(NewImage.FileName);
                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/tasks");

                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                var filePath = Path.Combine(uploadPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await NewImage.CopyToAsync(stream);
                }

                task.ImagePath = "/uploads/tasks/" + fileName;
            }


            await _context.SaveChangesAsync();

            return RedirectToAction("TaskManagement", "Admin", new { msg = "Task updated successfully!" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTask(int id)
        {
            var task = await _context.TasksManage.FindAsync(id);
            if (task != null)
            {
                if (!string.IsNullOrEmpty(task.ImagePath))
                {
                    var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", task.ImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }

                _context.TasksManage.Remove(task);
                await _context.SaveChangesAsync();

                return RedirectToAction("TaskManagement", "Admin", new { msg = "Task deleted successfully!" });
            }
            else
            {
                return RedirectToAction("TaskManagement", "Admin", new { msg = "Task not found" });
            }
        }

        //TASK PROGRESS
        public async Task<IActionResult> TaskProgress()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = int.Parse(userIdStr);

            // ✅ Get the logged-in user
            var currentUser = await _context.Users.FindAsync(userId);

            IQueryable<TasksManage> query = _context.TasksManage
                .Include(t => t.AssignedTo)
                .Include(t => t.AssignedBy);

            // ✅ Role-based filtering
            if (currentUser != null)
            {
                if (currentUser.Position == "Admin")
                {
                    // Admin → see all tasks
                }
                else if (currentUser.Position == "Supervisor" || currentUser.Position == "Trainer")
                {
                    // Supervisor/Trainer → tasks they assigned
                    query = query.Where(t => t.AssignedById == userId);
                }
            }

            var tasks = await query.ToListAsync();

            // ✅ Load approved interns (for assigning tasks dropdown, etc.)
            ViewBag.ApprovedInterns = await _context.Users
                .Where(u => u.Position == "Intern" && u.Status == "Approved")
                .ToListAsync();

            // ✅ Badge counts (for sidebar/dashboard notifications)
            await LoadBadgeCounts();

            return View(tasks);
        }
        // GET: Admin/Reports
        public async Task<IActionResult> Reports()
        {
            string? userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                return RedirectToAction("Login","Account", new { msg = "Session expired. Please log in again." });
            }

            int userId = int.Parse(userIdStr);

            // ✅ Get all intern reports
            var reports = await _context.Reports
                .Include(r => r.Task)
                .Include(r => r.User)
                .OrderByDescending(r => r.DateSubmitted)
                .ToListAsync();

            // ✅ Get all approved interns (for certificates & dropdowns)
            var interns = await _context.Users
                .Where(u => u.Position == "Intern" && u.Status == "Approved")
                .ToListAsync();

            // ✅ Get certificates for each intern
            var internCertificates = new Dictionary<int, Certificate>();
            foreach (var intern in interns)
            {
                var certificate = await _context.Certificate
                    .FirstOrDefaultAsync(c => c.UserId == intern.UserId);
                if (certificate != null)
                {
                    internCertificates[intern.UserId] = certificate;
                }
            }

            // ✅ Task statistics
            var totalTasks = await _context.TasksManage.CountAsync();
            var completedTasks = await _context.TasksManage.CountAsync(t => t.Status == "Completed");
            var remainingTasks = await _context.TasksManage.CountAsync(t => t.Status != "Completed");

            // Pass data to view
            ViewBag.TotalTasks = totalTasks;
            ViewBag.CompletedTasks = completedTasks;
            ViewBag.RemainingTasks = remainingTasks;
            ViewBag.Interns = interns;
            ViewBag.InternCertificates = internCertificates;

            // ✅ Sidebar/dashboard badge counts
            await LoadBadgeCounts();

            return View(reports);
        }

        // GET: Admin/ViewInternDetails/{id}
        public async Task<IActionResult> ViewInternDetails(int id)
        {
            // Get the intern by ID
            var intern = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == id && u.Position == "Intern");

            if (intern == null)
            {
                return NotFound();
            }

            // Get all approved attendance records for this intern
            var attendances = await _context.Attendance
                .Where(a => a.UserId == id && a.ApprovalStatus == "Approved")
                .OrderByDescending(a => a.Date)
                .ToListAsync();

            // ✅ Use stored TotalRenderedHours from intern
            decimal totalRenderedHours = intern.TotalRenderedHours;

            // Get intern's reports
            var reports = await _context.Reports
                .Include(r => r.Task)
                .Where(r => r.UserId == id && r.ReportType == "Task Completion")
                .OrderByDescending(r => r.DateSubmitted)
                .ToListAsync();

            // Get certificate if it existsre
            var certificate = await _context.Certificate
                .FirstOrDefaultAsync(c => c.UserId == id);

            bool hasCertificate = intern.TotalRenderedHours >= intern.HoursToRender;

            // Pass data to the view
            ViewBag.TotalRenderedHours = totalRenderedHours;
            ViewBag.Reports = reports;
            ViewBag.HasCertificate = hasCertificate;
            ViewBag.Attendances = attendances;
            ViewBag.Certificate = certificate;

            return View(intern); // Pass intern as the model
        }
        // Helper method to safely format full name
        private string GetFormattedFullName(User intern)
        {
            string middle = !string.IsNullOrWhiteSpace(intern.MiddleName)
                ? $" {intern.MiddleName[0]}."
                : string.Empty;

            return $"{intern.FirstName}{middle} {intern.LastName}";
        }
        // GET: Admin/ViewCertificate/{id}
        public async Task<IActionResult> ViewCertificate(int id)
        {
            var intern = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == id && u.Position == "Intern" && u.Status == "Approved");

            if (intern == null || intern.TotalRenderedHours < intern.HoursToRender)
                return BadRequest("This intern has not yet completed the required hours.");

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

                string safeFileName = $"Certificate_{intern.FirstName}_{intern.LastName}".Replace(" ", "_") + ".pdf";
                return File(ms.ToArray(), "application/pdf", safeFileName);
            }
            catch (Exception)
            {
                return StatusCode(500, "Failed to generate certificate.");
            }
        }

        // GET: Admin/GetReportDetails/{id}
        public async Task<IActionResult> GetReportDetails(int id)
        {
            try
            {
                var report = await _context.Reports
                    .Include(r => r.Task)
                    .FirstOrDefaultAsync(r => r.ReportId == id);

                if (report == null)
                {
                    return Json(new { success = false, message = "Report not found" });
                }

                return Json(new
                {
                    success = true,
                    taskTitle = report.Task?.Title ?? "No Task",
                    content = report.Content ?? "No content",
                    dateSubmitted = report.DateSubmitted,
                    filePath = report.FilePath
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: Admin/EditIntern/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditIntern(int id, User model)
        {
            try
            {
                var intern = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserId == id && u.Position == "Intern");

                if (intern == null)
                {
                    return NotFound();
                }

                // Update intern properties
                intern.FirstName = model.FirstName;
                intern.LastName = model.LastName;
                intern.Email = model.Email;
                intern.ContactNumber = model.ContactNumber;
                intern.School = model.School;
                intern.Course = model.Course;
                intern.HoursToRender = model.HoursToRender;
                intern.Status = model.Status;
                intern.Address = model.Address;
                intern.TotalRenderedHours = model.TotalRenderedHours;

                _context.Update(intern);
                await _context.SaveChangesAsync();
                return RedirectToAction("ViewInternDetails", "Admin",
    new { id = intern.UserId, msg = "Intern information updated successfully." });

            }
            catch (Exception ex)
            {
                return RedirectToAction("ViewInternDetails", "Admin", new { msg = "Error updating intern information: " + ex.Message });
            }
        }

        // POST: Admin/DeleteReport/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReport(int id)
        {
            var report = await _context.Reports
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.ReportId == id);

            if (report == null)
            {
                return RedirectToAction("Reports", "Admin", new { msg = "Report not found." });
            }

            // Delete associated file
            if (!string.IsNullOrEmpty(report.FilePath))
            {
                var filePath = Path.Combine(_environment.WebRootPath, report.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            // Reset task status to "In Progress" if this was the only report
            var task = await _context.TasksManage.FindAsync(report.TaskId);
            if (task != null)
            {
                var otherReports = await _context.Reports
                    .Where(r => r.TaskId == task.TaskId && r.ReportId != id)
                    .CountAsync();

                if (otherReports == 0)
                {
                    task.Status = "In Progress";
                    task.CompletedDate = null;
                }
            }

            _context.Reports.Remove(report);// ✅ Notify the intern

            await _context.SaveChangesAsync();

            return RedirectToAction("Reports", "Admin", new { msg = $"Report deleted successfully for {report.User?.FirstName} {report.User?.LastName}." });
        }
        public async Task<IActionResult> Interns()
        {
            // Get all interns
            var interns = await _context.Users
                .Where(u => u.Position == "Intern")
                .ToListAsync();

            if (!interns.Any())
            {
                return View(new List<object>());
            }

            var internIds = interns.Select(i => i.UserId).ToList();

            // ✅ Get attendance data for all interns (only approved + not deleted)
            var attendanceData = await _context.Attendance
                .Where(a => internIds.Contains(a.UserId)
                            && a.ApprovalStatus == "Approved"
                            && !a.IsDeleted)
                .GroupBy(a => a.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    TotalRendered = g.Sum(a => a.RenderedHours) // sum approved, not deleted
                })
                .ToDictionaryAsync(a => a.UserId, a => a.TotalRendered);

            // ✅ Get task data
            var tasks = await _context.TasksManage
                .Where(t => internIds.Contains(t.AssignedToId))
                .GroupBy(t => t.AssignedToId)
                .Select(g => new
                {
                    UserId = g.Key,
                    TotalTasks = g.Count(),
                    CompletedTasks = g.Count(t => t.Status == "Completed")
                })
                .ToDictionaryAsync(t => t.UserId, t => new { t.TotalTasks, t.CompletedTasks });

            // ✅ Build the view model
            var internViewModels = interns.Select(i => new
            {
                i.UserId,
                FullName = $"{i.FirstName} {i.LastName}",
                i.Email,
                i.ContactNumber,
                i.Position,
                i.School,
                ProfileImagePath = i.ProfileImagePath ?? "/images/default-profile.png",
                Status = i.Status ?? "Pending",
                HoursToRender = i.HoursToRender ?? 0,
                 RenderedHours = i.TotalRenderedHours,
                TotalTasks = tasks.ContainsKey(i.UserId) ? tasks[i.UserId].TotalTasks : 0,
                CompletedTasks = tasks.ContainsKey(i.UserId) ? tasks[i.UserId].CompletedTasks : 0
            }).ToList();

            ViewBag.Interns = internViewModels;
            await LoadBadgeCounts();
            return View();
        }

        [HttpGet("Admin/Attendance")]
        public async Task<IActionResult> Attendance(string filter = "All")
        {
            try
            {

                // ✅ Default fixed times
                var openingTime = new TimeSpan(8, 0, 0);   // 08:00 AM
                var closingTime = new TimeSpan(17, 0, 0);  // 05:00 PM

                ViewBag.DefaultRequiredTime = openingTime;
                ViewBag.DefaultClosingTime = closingTime;

                var today = DateTime.Today;
                var closingTimeToday = today.Add(closingTime);
                var now = DateTime.Now;

                // --- Auto-mark absent if closing time already passed (NO notifications) ---
                if (now >= closingTimeToday)
                {
                    var interns = await _context.Users
                        .Where(u => u.Position == "Intern")
                        .ToListAsync();

                    var absentsToAdd = new List<Attendance>();

                    foreach (var intern in interns)
                    {
                        var existingRecord = await _context.Attendance
                            .IgnoreQueryFilters()
                            .FirstOrDefaultAsync(a => a.UserId == intern.UserId && a.Date == today);

                        if (existingRecord == null)
                        {
                            absentsToAdd.Add(new Attendance
                            {
                                UserId = intern.UserId,
                                Date = today,
                                ApprovalStatus = "Absent",
                                IsAbsent = true,
                                Remarks = "Automatically marked absent (no time-in)",
                                IsDeleted = false
                            });
                        }
                    }

                    if (absentsToAdd.Any())
                    {
                        await _context.Attendance.AddRangeAsync(absentsToAdd);
                        await _context.SaveChangesAsync();
                    }
                }

                // --- Build attendance query and apply filter ---
                var query = _context.Attendance
                    .Include(a => a.Intern)
                    .Include(a => a.ApprovedBy)
                    .AsQueryable();

                switch (filter)
                {
                    case "Approved":
                        query = query.Where(a => a.ApprovalStatus == "Approved" && !a.IsDeleted);
                        break;
                    case "Rejected":
                        query = query.Where(a => a.ApprovalStatus == "Rejected" && !a.IsDeleted);
                        break;
                    case "Pending":
                        query = query.Where(a => a.ApprovalStatus == "Pending" && !a.IsDeleted);
                        break;
                    case "Deleted":
                        query = query.Where(a => a.IsDeleted);
                        break;
                    default:
                        query = query.Where(a => !a.IsDeleted);
                        break;
                }

                var attendances = await query
                    .OrderByDescending(a => a.Date)
                    .ToListAsync();

                foreach (var record in attendances)
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


                ViewBag.CurrentFilter = filter;
                await LoadBadgeCounts();
                return View(attendances);
            }
            catch (Exception ex)
            {
                return View(new List<Attendance>());
            }
        }
        [HttpPost("Admin/ApproveAttendance")]
        [ValidateAntiForgeryToken]
        public IActionResult ApproveAttendance(int id)
        {
            try
            {
                var userIdString = HttpContext.Session.GetString("UserId");
                var userRole = HttpContext.Session.GetString("UserRole");

                var attendance = _context.Attendance.Find(id);
                if (attendance == null)
                {
                    return RedirectToAction("Attendance", new { filter = TempData["CurrentFilter"] ?? "All" });
                }

                attendance.ApprovalStatus = "Approved";
                attendance.ApprovalDate = DateTime.Now;

                if (userRole == "Admin" && userIdString == "0")
                    attendance.ApprovedById = null;
                else if (int.TryParse(userIdString, out int approverId))
                    attendance.ApprovedById = approverId;

                // ✅ Add rendered hours only if not absent
                if (!attendance.IsAbsent && attendance.RenderedHours.HasValue)
                {
                    var intern = _context.Users.FirstOrDefault(u => u.UserId == attendance.UserId);
                    if (intern != null)
                    {
                        intern.TotalRenderedHours += attendance.RenderedHours.Value;
                        _context.Users.Update(intern);
                    }
                }

                _context.SaveChanges();

                // Notify intern
                if (attendance.UserId != null)
                {
                    string senderName = GetSenderName();
                    var notif = new Notification
                    {
                        UserId = attendance.UserId,
                        Title = "Attendance Approved",
                        Message = $"Your attendance for {attendance.Date:MMM dd, yyyy} was approved by {senderName}.",
                        NotificationType = "Attendance",
                        RelatedId = attendance.AttendanceId,
                        RelatedEntity = "Attendance",
                        Link = "/Intern/Attendance",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    };
                    _context.Notifications.Add(notif);
                    _context.SaveChanges();
                }

                return RedirectToAction("Attendance", "Admin", new { msg = "Attendance approved successfully." });
            }
            catch (Exception ex)
            {
                return RedirectToAction("Attendance", "Admin", new { msg = $"Error approving attendance: {ex.Message}" });
            }
        }

        [HttpPost("Admin/RejectAttendance")]
        [ValidateAntiForgeryToken]
        public IActionResult RejectAttendance(int id, string remarks = "")
        {
            try
            {
                var userIdString = HttpContext.Session.GetString("UserId");
                var userRole = HttpContext.Session.GetString("UserRole");

                var attendance = _context.Attendance.Find(id);
                if (attendance == null)
                {
                    return RedirectToAction("Attendance", new { filter = TempData["CurrentFilter"] ?? "All" });
                }

                // ✅ If it was previously approved, rollback rendered hours
                if (attendance.ApprovalStatus == "Approved" && attendance.UserId != null)
                {
                    var user = _context.Users.Find(attendance.UserId);
                    if (user != null)
                    {
                        user.TotalRenderedHours -= (attendance.RenderedHours ?? 0);
                        if (user.TotalRenderedHours < 0) user.TotalRenderedHours = 0; // safety
                        _context.Users.Update(user);
                    }
                }

                // ✅ Mark as rejected
                attendance.ApprovalStatus = "Rejected";
                attendance.ApprovalDate = DateTime.Now;

                // Hardcoded admin = null, others = their ID
                if (userRole == "Admin" && userIdString == "0")
                {
                    attendance.ApprovedById = null;
                }
                else if (int.TryParse(userIdString, out int approverId))
                {
                    attendance.ApprovedById = approverId;
                }

                _context.Update(attendance);
                _context.SaveChanges();

                // ✅ Notify intern ONLY
                if (attendance.UserId != null)
                {
                    string senderName = GetSenderName();
                    var notif = new Notification
                    {
                        UserId = attendance.UserId, // Intern only
                        Title = "Attendance Rejected",
                        Message = $"Your attendance for {attendance.Date:MMM dd, yyyy} was rejected by {senderName}. {(string.IsNullOrEmpty(remarks) ? "" : remarks)}",
                        NotificationType = "Attendance",
                        RelatedId = attendance.AttendanceId,
                        RelatedEntity = "Attendance",
                        Link = "/Intern/Attendance",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    };
                    _context.Notifications.Add(notif);
                    _context.SaveChanges();
                }

                return RedirectToAction("Attendance", "Admin", new { msg = "Attendance rejected successfully." });
            }
            catch (Exception ex)
            {
                return RedirectToAction("Attendance", "Admin", new { msg = $"Error rejecting attendance: {ex.Message}" });
            }
        }
        [HttpPost("Admin/SoftDeleteAttendance")]
        [ValidateAntiForgeryToken]
        public IActionResult SoftDeleteAttendance(int id, string remarks = "")
        {
            try
            {
                var attendance = _context.Attendance.Find(id);
                if (attendance == null)
                {
                    return RedirectToAction("Attendance", new { filter = TempData["CurrentFilter"] ?? "All" });
                }

                attendance.IsDeleted = true;

                // ✅ Subtract only if approved + not absent
                if (attendance.ApprovalStatus == "Approved" && !attendance.IsAbsent && attendance.RenderedHours.HasValue)
                {
                    var intern = _context.Users.FirstOrDefault(u => u.UserId == attendance.UserId);
                    if (intern != null)
                    {
                        intern.TotalRenderedHours -= attendance.RenderedHours.Value;
                        _context.Users.Update(intern);
                    }
                }

                _context.SaveChanges();

                return RedirectToAction("Attendance", "Admin", new { msg = "Attendance moved to deleted records." });
            }
            catch (Exception ex)
            {
                return RedirectToAction("Attendance", "Admin", new { msg = $"Error deleting attendance: {ex.Message}" });
            }

        }
        [HttpPost("Admin/RestoreAttendance")]
        [ValidateAntiForgeryToken]
        public IActionResult RestoreAttendance(int id)
        {
            try
            {
                var attendance = _context.Attendance.Find(id);
                if (attendance == null)
                {
                    return RedirectToAction("Attendance", new { filter = "Deleted" });
                }

                attendance.IsDeleted = false;

                // ✅ Add back only if approved + not absent
                if (attendance.ApprovalStatus == "Approved" && !attendance.IsAbsent && attendance.RenderedHours.HasValue)
                {
                    var intern = _context.Users.FirstOrDefault(u => u.UserId == attendance.UserId);
                    if (intern != null)
                    {
                        intern.TotalRenderedHours += attendance.RenderedHours.Value;
                        _context.Users.Update(intern);
                    }
                }

                _context.SaveChanges();

                return RedirectToAction("Attendance", "Admin", new { msg = "Attendance restored successfully." });
            }
            catch (Exception ex)
            {
                return RedirectToAction("Attendance", "Admin", new { msg = $"Error restoring attendance: {ex.Message}" });
            }
        }
        [HttpPost("Admin/PermanentDeleteAttendance")]
        [ValidateAntiForgeryToken]
        public IActionResult PermanentDeleteAttendance(int id)
        {
            try
            {
                var attendance = _context.Attendance.Find(id);
                if (attendance == null)
                {
                    return RedirectToAction("Attendance", new { filter = "Deleted" });
                }

                if (!string.IsNullOrEmpty(attendance.ProofImagePath))
                {
                    var fullPath = Path.Combine(_environment.WebRootPath, "uploads", "attendance", attendance.ProofImagePath);
                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }
                }

                // ✅ Subtract before removal only if approved + not absent
                if (attendance.ApprovalStatus == "Approved" && !attendance.IsAbsent && attendance.RenderedHours.HasValue)
                {
                    var intern = _context.Users.FirstOrDefault(u => u.UserId == attendance.UserId);
                    if (intern != null)
                    {
                        intern.TotalRenderedHours -= attendance.RenderedHours.Value;
                        _context.Users.Update(intern);
                    }
                }

                _context.Attendance.Remove(attendance);
                _context.SaveChanges();

                return RedirectToAction("Attendance", "Admin", new { msg = "Attendance permanently deleted." });
            }
            catch (Exception ex)
            {
                return RedirectToAction("Attendance", "Admin", new { msg = $"Error permanently deleting attendance: {ex.Message}" });
            }
        }

        private async Task<string> SaveProofImage(IFormFile proofImage)
        {
            if (proofImage == null || proofImage.Length == 0)
            {
                throw new ArgumentException("No proof image provided");
            }

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "attendance");
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(proofImage.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await proofImage.CopyToAsync(stream);
            }

            return uniqueFileName;
        }
        [HttpGet("Admin/Evaluation")]
        public async Task<IActionResult> Evaluation()
        {
            try
            {
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userIdStr))
                {
                    return RedirectToAction("Login", "Account");
                }

                int userId = int.Parse(userIdStr);

                // Get the logged-in user to check their role
                var currentUser = await _context.Users.FindAsync(userId);

                IQueryable<Evaluation> query = _context.Evaluations
                    .Include(e => e.Intern)
                    .Include(e => e.ReviewedBy)
                    .Where(e => e.Status == "Submitted"
                             || e.Status == "Completed");

                var evaluations = await query
                    .OrderByDescending(e => e.UploadDate)
                    .ToListAsync();

                await LoadBadgeCounts();
                return View(evaluations);
            }
            catch (Exception ex)
            {
                return View(new List<Evaluation>());
            }
        }


        [HttpPost("Admin/UploadReviewedEvaluation")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadReviewedEvaluation(int evaluationId, IFormFile reviewedFile)
        {
            try
            {
                var evaluation = _context.Evaluations
                    .Include(e => e.Intern)
                    .FirstOrDefault(e => e.EvaluationId == evaluationId);

                if (evaluation == null)
                {
                    return RedirectToAction("Evaluation", "Admin", new {msg = "Evaluation not found."});
                }

                if (reviewedFile == null || reviewedFile.Length == 0)
                {
                    return RedirectToAction("Evaluation", "Admin", new { msg = "Please select a valid file." });
                }

                // Delete old file if editing
                if (!string.IsNullOrEmpty(evaluation.ReviewedFilePath))
                {
                    var oldPath = Path.Combine(_environment.WebRootPath, evaluation.ReviewedFilePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                // Validate size and type
                if (reviewedFile.Length > 10 * 1024 * 1024)
                {
                    return RedirectToAction("Evaluation", "Admin", new { msg = "File size must be less than 10MB." });
                }
                var allowedExtensions = new[] { ".pdf", ".docx", ".doc", ".jpg", ".jpeg", ".png" };
                var ext = Path.GetExtension(reviewedFile.FileName).ToLower();
                if (!allowedExtensions.Contains(ext))
                {
                    return RedirectToAction("Evaluation", "Admin", new { msg = "Only PDF, Word, and image files are allowed." });
                }

                // Save new file
                string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "evaluations", "reviewed");
                Directory.CreateDirectory(uploadsFolder);
                string uniqueFileName = $"reviewed_{evaluation.InternId}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await reviewedFile.CopyToAsync(stream);
                }

                evaluation.ReviewedFilePath = $"/uploads/evaluations/reviewed/{uniqueFileName}";
                evaluation.ReviewedDate = DateTime.Now;
                evaluation.Status = "Completed";
                
                var userIdString = HttpContext.Session.GetString("UserId");
                var userRole = HttpContext.Session.GetString("UserRole");

                // Assign the correct reviewer
                if (userRole == "Admin" && userIdString == "0")
                {
                    // For Admin, keep it null
                    evaluation.ReviewedById = null;
                }
                else if (int.TryParse(userIdString, out int reviewerId))
                {
                    // For Supervisor/Trainer, store their real ID
                    evaluation.ReviewedById = reviewerId;
                }

                _context.Evaluations.Update(evaluation);
                await _context.SaveChangesAsync();

                string senderName = GetSenderName();
                var notif = new Notification
                {
                    UserId = evaluation.InternId,
                    Title = "Reviewed Evaluation Uploaded",
                    Message = $"Your reviewed evaluation has been uploaded by {senderName}. Please check your evaluation section.",
                    NotificationType = "Certificate",
                    RelatedId = evaluation.InternId,
                    RelatedEntity = "Evaluation",
                    Link = "/Intern/Evaluation",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                };

                _context.Notifications.Add(notif);
                await _context.SaveChangesAsync();

                return RedirectToAction("Evaluation", "Admin", new { msg = string.IsNullOrEmpty(evaluation.ReviewedFilePath) ? "Reviewed evaluation uploaded." : "Reviewed evaluation updated." });
            }
            catch (Exception ex)
            {
                return RedirectToAction("Evaluation", "Admin", new { msg = $"Error: {ex.Message}" });
            }
        }

        [HttpPost("Admin/DeleteEvaluation")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEvaluation(int id)
        {
            try
            {
                var evaluation = await _context.Evaluations.FindAsync(id);
                if (evaluation == null)
                {
                    return RedirectToAction("Evaluation", "Admin", new { msg = "Evaluation not found." });
                }

                // Delete files
                if (!string.IsNullOrEmpty(evaluation.OriginalFilePath))
                {
                    var path = Path.Combine(_environment.WebRootPath, evaluation.OriginalFilePath.TrimStart('/'));
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                }
                if (!string.IsNullOrEmpty(evaluation.ReviewedFilePath))
                {
                    var path = Path.Combine(_environment.WebRootPath, evaluation.ReviewedFilePath.TrimStart('/'));
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                }

                _context.Evaluations.Remove(evaluation);

                string senderName = GetSenderName();

                var notif = new Notification
                {
                    UserId = evaluation.InternId,
                    Title = "Evaluation Deleted", // or "Reviewed Evaluation Uploaded" depending on context
                    Message = "Your evaluation record has been removed by the administrator.",
                    NotificationType = "Evaluation", // ✅ set type properly
                    RelatedId = evaluation.InternId,
                    RelatedEntity = "Evaluation",
                    Link = "/Intern/Evaluation",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                };

                // Save notification
                _context.Notifications.Add(notif);

                await _context.SaveChangesAsync();

                return RedirectToAction("Evaluation", "Admin", new { msg = "Evaluation deleted successfully." });
            }
            catch (Exception ex)
            {
                return RedirectToAction("Evaluation", "Admin", new { msg = $"Error: {ex.Message}" });
            }
        }

        [HttpGet("Admin/Dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            var role = HttpContext.Session.GetString("UserRole");
            var sessionUserId = HttpContext.Session.GetString("UserId");

            int? userId = null;
            if (int.TryParse(sessionUserId, out int parsedId))
                userId = parsedId;

            var applicants = _context.Applicants.OrderByDescending(a => a.UploadDate).Take(3).ToList();

            var internsRaw = _context.Users
                .Where(u => u.Position == "Intern" && u.Status == "Approved")
                .Select(u => new
                {
                    u.UserId,
                    FullName = u.FirstName + " " + u.LastName,
                    u.Course,
                    u.School,
                    HoursRequired = u.HoursToRender ?? 0,
                    RenderedHours = u.TotalRenderedHours // ✅ pull from User model
                })
                .ToList();

            var interns = internsRaw.Select(u =>
            {
                var renderedHours = Math.Round(u.RenderedHours, 2);

                var taskCount = _context.TasksManage.Count(t => t.AssignedToId == u.UserId);
                var completedTasks = _context.TasksManage.Count(t => t.AssignedToId == u.UserId && t.Status == "Completed");

                var taskCompletion = taskCount > 0
                    ? (int)Math.Round((double)completedTasks / taskCount * 100)
                    : 0;

                return new
                {
                    u.UserId,
                    u.FullName,
                    u.Course,
                    u.School,
                    u.HoursRequired,
                    HoursRendered = $"{renderedHours}", // ✅ string for uniform format
                    TotalTasks = taskCount,
                    TaskCompletion = taskCompletion
                };
            }).ToList();

            ViewBag.Interns = interns;
            ViewBag.TotalInterns = _context.Users.Count(i => i.Position == "Intern" && i.Status == "Approved");
            ViewBag.PendingList = applicants;
            ViewBag.PendingApplicants = applicants.Count; 
            ViewBag.PendingInterns = _context.Users.Count(t => t.Status == "Pending" && t.Position == "Intern");

            ViewBag.TotalTasks = _context.TasksManage.Count();
            ViewBag.PendingTasks = _context.TasksManage.Count(t => t.Status == "Pending");
            ViewBag.CompletedTasks = _context.TasksManage.Count(t => t.Status == "Completed");
            ViewBag.OngoingTasks = _context.TasksManage.Count(t => t.Status == "In Progress");
            ViewBag.OverdueTasks = _context.TasksManage.Count(t => t.Status != "Completed" && t.DueDate < DateTime.Now);

            // Load badge counts using unified method
            await LoadBadgeCounts(userId, role);
            return View();
        }

        // Delete applicant
        [HttpGet]
        public IActionResult DeleteApplicant(int id)
        {
            var applicant = _context.Applicants.FirstOrDefault(a => a.ApplicantId == id);
            if (applicant == null)
            {
                return RedirectToAction("Dashboard", "Admin", new { msg = "Applicant not found." });
            }

            // Remove the file from the server
            if (System.IO.File.Exists(applicant.FilePath))
            {
                System.IO.File.Delete(applicant.FilePath);
            }

            // Remove from database
            _context.Applicants.Remove(applicant);
            _context.SaveChanges();

            return RedirectToAction("Dashboard", "Admin", new { msg = "Applicant deleted successfully." });
        }

        // Main notifications page
        public async Task<IActionResult> Notifications(bool showDeleted = false)
        {
            var role = HttpContext.Session.GetString("UserRole");
            var sessionUserId = HttpContext.Session.GetString("UserId");

            int? userId = null;
            if (int.TryParse(sessionUserId, out int parsedId))
                userId = parsedId;

            IQueryable<Notification> query = _context.Notifications.Include(n => n.Sender);

            if (role == "Admin" && sessionUserId == "0")
            {
                // ✅ Hardcoded Admin → system-level notifications (UserId is null)
                query = query.Where(n => n.UserId == null);
            }
            else if (int.TryParse(sessionUserId, out int userID))
            {
                // ✅ Supervisor/Trainer/Intern → notifications meant for this user only
                query = query.Where(n => n.UserId == userId);
            }
            else
            {
                // fallback (no valid session)
                query = query.Where(n => false);
            }

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

            var unreadCount = await query
                .Where(n => !n.IsRead && !n.IsDeleted)
                .CountAsync();

            ViewBag.UnreadCount = unreadCount;
            await LoadBadgeCounts();
            return View(allNotifications);
        }

        // Mark one notification as read
        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
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
            var sessionRole = HttpContext.Session.GetString("UserRole");

            IQueryable<Notification> query = _context.Notifications.Where(n => !n.IsDeleted);

            if (sessionRole == "Admin" && sessionUserId == "0")
            {
                query = query.Where(n => n.UserId == null);
            }
            else if (int.TryParse(sessionUserId, out int userId))
            {
                query = query.Where(n => n.UserId == userId);
            }

            var unread = await query
                .Where(n => !n.IsRead)
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
            var notification = await _context.Notifications.FindAsync(id);
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
            var userIdString = HttpContext.Session.GetString("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            IQueryable<Notification> query = _context.Notifications.Where(n => !n.IsDeleted);

            if (userRole == "Admin" && userIdString == "0")
            {
                query = query.Where(n => n.UserId == null);
            }
            else if (int.TryParse(userIdString, out int userId))
            {
                query = query.Where(n => n.UserId == userId);
            }

            var allNotifications = await query.ToListAsync();

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
            var notification = await _context.Notifications.FindAsync(id);
            if (notification == null)
                return NotFound();

            notification.IsDeleted = false;
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        // Permanent delete of a notification
        [HttpPost]
        public async Task<IActionResult> PermanentDeleteNotification(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification == null)
                return NotFound();

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }
        // Add this method to your AdminController
        [HttpPost]
        public async Task<IActionResult> PermanentDeleteAllNotifications()
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            IQueryable<Notification> query = _context.Notifications.Where(n => n.IsDeleted);

            if (userRole == "Admin" && userIdString == "0")
            {
                query = query.Where(n => n.UserId == null);
            }
            else if (int.TryParse(userIdString, out int userId))
            {
                query = query.Where(n => n.UserId == userId);
            }

            var allDeletedNotifications = await query.ToListAsync();

            _context.Notifications.RemoveRange(allDeletedNotifications);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, deletedCount = allDeletedNotifications.Count });
        }

        private string GetSenderName()
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userRole == "Admin" && userIdString == "0")
            {
                return "Admin"; // Hardcoded admin name
            }

            if (int.TryParse(userIdString, out int userId))
            {
                var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
                if (user != null)
                {
                    return $"{user.FirstName} {user.LastName} ({user.Position})";
                }
            }

            return "System"; // fallback
        }
        private async Task LoadBadgeCounts(int? userId = null, string role = null)
        {
            // Default counts
            ViewBag.AttendanceCount = 0;
            ViewBag.PendingUsersCount = 0;
            ViewBag.ApplicationsCount = 0;
            ViewBag.NotificationsCount = 0;
            ViewBag.EvaluationCount = 0;
            ViewBag.TaskCount = 0;

            // Fallback: check session if no params were passed
            if (string.IsNullOrEmpty(role))
                role = HttpContext.Session.GetString("UserRole");

            if (!userId.HasValue)
            {
                var sessionUserId = HttpContext.Session.GetString("UserId");
                if (int.TryParse(sessionUserId, out int parsedId))
                    userId = parsedId;
            }

            if (string.IsNullOrEmpty(role))
                return;

            // ✅ Admin (system-wide)
            if (role == "Admin")
            {
                ViewBag.AttendanceCount = await _context.Attendance
                    .CountAsync(a => a.ApprovalStatus == "Pending" && !a.IsDeleted && a.ApprovedById == null);

                ViewBag.PendingUsersCount = await _context.Users
                    .CountAsync(u => u.Status == "Pending");

                var applicantsCount = await _context.Applicants.CountAsync(a => !a.IsSeen);
                var pendingDocsCount = await _context.Documents
                    .CountAsync(d => d.Status == "Pending" && d.ApprovedById == null);
                ViewBag.ApplicationsCount = applicantsCount + pendingDocsCount;

                ViewBag.NotificationsCount = await _context.Notifications
                    .CountAsync(n => !n.IsRead && !n.IsDeleted && n.UserId == null);

                ViewBag.EvaluationCount = await _context.Evaluations
                    .CountAsync(e => e.Status == "Submitted" && e.ReviewedById == null);

                ViewBag.TaskCount = await _context.TasksManage
                    .CountAsync(t => t.Status == "Pending" || t.Status == "In Progress");
            }
            // ✅ Supervisor/Trainer (only their data)
            else if ((role == "Supervisor" || role == "Trainer") && userId.HasValue)
            {
                int uid = userId.Value;
                ViewBag.AttendanceCount = await _context.Attendance
    .CountAsync(a => a.ApprovalStatus == "Pending" && !a.IsDeleted);

                ViewBag.PendingUsersCount = 0; // they don’t handle user approval

                var applicantsCount = await _context.Applicants.CountAsync(a => !a.IsSeen);
                var pendingDocsCount = await _context.Documents
                    .CountAsync(d => d.Status == "Pending");
                ViewBag.ApplicationsCount = applicantsCount + pendingDocsCount;

                ViewBag.NotificationsCount = await _context.Notifications
                    .CountAsync(n => !n.IsRead && !n.IsDeleted && n.UserId == uid);

                ViewBag.EvaluationCount = await _context.Evaluations
                    .CountAsync(e => e.Status == "Submitted");

                ViewBag.TaskCount = await _context.TasksManage
                    .CountAsync(t => t.AssignedById == uid && (t.Status == "Pending" || t.Status == "In Progress"));

                // Intern details
                var trainer = await _context.Users.FirstOrDefaultAsync(t => t.UserId == userId);
                if (trainer != null)
                {
                    ViewBag.Trainer = $"{trainer.FirstName} {trainer.LastName}";
                    ViewBag.Position = trainer.Position;
                }
            }
        }

    }
}




