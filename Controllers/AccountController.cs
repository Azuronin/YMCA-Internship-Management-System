using Microsoft.AspNetCore.Mvc;
using IMS.Models;
using IMS.Data;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace IMS.Controllers
{
    public class AccountController : Controller
    {
        private readonly MyAppContext _context;
        private readonly IWebHostEnvironment _environment;

        public AccountController(MyAppContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(UserLogin model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return RedirectToAction("Login", "Account", new { msg = "Please fill in all required fields." });
                }

                // Basic email format validation
                if (string.IsNullOrWhiteSpace(model.Email) || !model.Email.Contains("@"))
                {
                    return RedirectToAction("Login", "Account", new { msg = "Invalid email address." });
                }

                // Trim email to handle any extra spaces
                string email = model.Email.Trim();

                // ✅ Admin login
                if (email == "admin@ims.com" && model.Password == "Admin123!")
                {
                    HttpContext.Session.SetString("UserEmail", email);
                    HttpContext.Session.SetString("UserRole", "Admin");
                    HttpContext.Session.SetString("UserId", "0");

                    return RedirectToAction("Dashboard", "Admin", new { msg = "Login successful." });
                }

                // ✅ Regular user login
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user != null)
                {
                    bool passwordValid = BCrypt.Net.BCrypt.Verify(model.Password, user.Password);

                    if (passwordValid)
                    {
                        if (user.Status != "Approved")
                        {
                            // Not approved yet
                            return RedirectToAction("Login", "Account", new { msg = "Your account is not approved yet." });
                        }

                        // Store session values
                        HttpContext.Session.SetString("UserId", user.UserId.ToString());
                        HttpContext.Session.SetString("UserEmail", user.Email);
                        HttpContext.Session.SetString("UserRole", user.Position ?? "User");

                        // Redirect to role-based dashboard with confirmation
                        switch (user.Position)
                        {
                            case "Trainer":
                                return RedirectToAction("Dashboard", "Admin", new { msg = "Login successful." });

                            case "Intern":
                                return RedirectToAction("Dashboard", "Intern", new { msg = "Login successful." });

                            case "Admin":
                                return RedirectToAction("Dashboard", "Admin", new { msg = "Login successful." });

                            default:
                                return RedirectToAction("Dashboard", "Intern", new { msg = "Login successful." });
                        }
                    }
                }

                // Invalid credentials
                return RedirectToAction("Login", "Account", new { msg = "Invalid email or password." });
            }
            catch (Exception ex)
            {
                return RedirectToAction("Login", "Account", new { msg = "An unexpected error occurred: " + ex.Message });
            }
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(User user)
        {
            try
            {
                // Check if email already exists
                if (_context.Users.Any(u => u.Email == user.Email))
                {
                    ModelState.AddModelError("Email", "This email is already registered.");
                }

                // Validate email format (gmail.com or ims.com)
                if (!Regex.IsMatch(user.Email, @"^[\w\.-]+@(gmail\.com|ims\.com)$"))
                {
                    ModelState.AddModelError("Email", "Email must be from gmail.com or ims.com domain.");
                }
                // Validate School field
                if (string.IsNullOrWhiteSpace(user.School))
                {
                    ModelState.AddModelError("School", "School/University is required.");
                }

                // Validate Course field
                if (string.IsNullOrWhiteSpace(user.Course))
                {
                    ModelState.AddModelError("Course", "Course/Program is required.");
                }

                // Validate Hours to Render
                if (!user.HoursToRender.HasValue || user.HoursToRender.Value <= 0)
                {
                    ModelState.AddModelError("HoursToRender", "Please enter valid internship hours.");
                }
                else if (user.HoursToRender.Value > 2000)
                {
                    ModelState.AddModelError("HoursToRender", "Hours to render cannot exceed 1000.");
                }
                // Additional password validation
                if (!string.IsNullOrEmpty(user.Password))
                {
                    if (user.Password.Length < 6)
                    {
                        ModelState.AddModelError("Password", "Password must be at least 6 characters long.");
                    }

                    // Check if password contains at least one uppercase, one lowercase, and one digit
                    if (!Regex.IsMatch(user.Password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$"))
                    {
                        ModelState.AddModelError("Password", "Password must contain at least one uppercase letter, one lowercase letter, and one number.");
                    }
                }

                // Validate secret answer - FIXED: Check for null/empty SecretQuestion and SecretAnswer
                if (!string.IsNullOrEmpty(user.SecretQuestion) && !string.IsNullOrEmpty(user.SecretAnswer))
                {
                    if (user.SecretAnswer.Length < 3)
                    {
                        ModelState.AddModelError("SecretAnswer", "Answer must be at least 3 characters long.");
                    }
                }
                else
                {
                    ModelState.AddModelError("SecretQuestion", "Security question and answer are required.");
                }

                if (ModelState.IsValid)
                {
                    // Hash password
                    user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);

                    // Hash secret answer for security - FIXED: Only hash if not null/empty
                    if (!string.IsNullOrEmpty(user.SecretAnswer))
                    {
                        user.SecretAnswer = BCrypt.Net.BCrypt.HashPassword(user.SecretAnswer.ToLower().Trim());
                    }

                    // Set default values
                    user.Position = "Intern";
                    user.Status = "Pending";
                    user.CreatedAt = DateTime.Now;

                    // Save to database
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();

                    return RedirectToAction("Login", "Account", new { msg = "Registration successful! Please wait for admin approval before you can login." });
                }

                return View(user);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again later.");
                return View(user);
            }
        }

        [HttpPost]
        public async Task<IActionResult> CheckEmailForRecovery([FromBody] EmailCheckRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.Email))
                {
                    return Json(new { success = false, message = "Please enter your email address." });
                }

                // Trim the email to remove any extra spaces
                string email = request.Email.Trim();

                // FIXED: Added async/await for database query
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    return Json(new { success = false, message = "Email not found in our system." });
                }

                // FIXED: Check if user account is approved
                if (user.Status != "Approved")
                {
                    return Json(new { success = false, message = "Your account is not approved yet. Please wait for admin approval." });
                }

                if (string.IsNullOrEmpty(user.SecretQuestion))
                {
                    return Json(new { success = false, message = "No security question found for this account. Please contact the administrator." });
                }

                return Json(new { success = true, question = user.SecretQuestion });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred. Please try again." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> VerifySecretAnswer([FromBody] PasswordRecoveryRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.Email) || string.IsNullOrEmpty(request?.Answer))
                {
                    return Json(new { success = false, message = "Please provide both email and answer." });
                }

                // FIXED: Added async/await for database query
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email.Trim());
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                // FIXED: Check if user account is approved
                if (user.Status != "Approved")
                {
                    return Json(new { success = false, message = "Account is not approved yet." });
                }

                // FIXED: Added null check for SecretAnswer
                if (string.IsNullOrEmpty(user.SecretAnswer))
                {
                    return Json(new { success = false, message = "No security answer found for this account." });
                }

                // Verify the secret answer (case-insensitive comparison)
                bool isAnswerCorrect = BCrypt.Net.BCrypt.Verify(request.Answer.ToLower().Trim(), user.SecretAnswer);

                if (!isAnswerCorrect)
                {
                    return Json(new { success = false, message = "Incorrect answer. Please try again." });
                }

                // Generate a temporary password
                string tempPassword = GenerateTemporaryPassword();

                // FIXED: Update user's password with the temporary one and ensure it's properly hashed
                user.Password = BCrypt.Net.BCrypt.HashPassword(tempPassword);

                // FIXED: Mark the changes and save
                _context.Entry(user).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return Json(new { success = true, password = tempPassword });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred during password recovery." });
            }
        }

        // FIXED: Improved password generation with better randomness and simpler format
        private string GenerateTemporaryPassword()
        {
            const string upperCase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lowerCase = "abcdefghijklmnopqrstuvwxyz";
            const string digits = "0123456789";
            const string simpleSpecialChars = "@#$%"; // Simplified special characters

            var random = new Random();
            var password = new System.Text.StringBuilder();

            // Ensure at least one character from each category for a 8-character password
            password.Append(upperCase[random.Next(upperCase.Length)]);
            password.Append(lowerCase[random.Next(lowerCase.Length)]);
            password.Append(digits[random.Next(digits.Length)]);
            password.Append(simpleSpecialChars[random.Next(simpleSpecialChars.Length)]);

            // Fill the rest with random alphanumeric characters (easier to type)
            const string simpleChars = upperCase + lowerCase + digits;
            for (int i = 4; i < 8; i++) // Make it 8 characters total
            {
                password.Append(simpleChars[random.Next(simpleChars.Length)]);
            }

            // Shuffle the password to randomize character positions
            var passwordArray = password.ToString().ToCharArray();
            for (int i = passwordArray.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (passwordArray[i], passwordArray[j]) = (passwordArray[j], passwordArray[i]);
            }

            var finalPassword = new string(passwordArray);

            return finalPassword;
        }

        [HttpPost]
        public async Task<IActionResult> SubmitResume(IFormFile resumeFile, string email, string fullName)
        {
            try
            {
                if (resumeFile == null || resumeFile.Length == 0)
                    return Json(new { success = false, message = "No file selected." });

                string extension = Path.GetExtension(resumeFile.FileName)?.ToLowerInvariant();
                var allowed = new[] { ".pdf", ".jpg", ".jpeg", ".png" };

                if (!allowed.Contains(extension))
                    return Json(new { success = false, message = "Invalid file type." });

                // FIXED: Added file size validation (10MB limit)
                if (resumeFile.Length > 10 * 1024 * 1024)
                    return Json(new { success = false, message = "File size must be less than 10MB." });

                //if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(fullName))
                //    return Json(new { success = false, message = "Please provide your name and email." });
                //// ✅ Prevent duplicate submissions
                //var existing = await _context.Applicants
                //    .FirstOrDefaultAsync(a => a.Email == email && (a.Status == "Pending" || a.Status == "Qualified"));
                //if (existing != null)
                //    return Json(new { success = false, message = "You already have an active application." });


                string uploads = Path.Combine(_environment.WebRootPath, "applicants");
                Directory.CreateDirectory(uploads);

                string uniqueName = Guid.NewGuid() + extension;
                string filePath = Path.Combine(uploads, uniqueName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await resumeFile.CopyToAsync(stream);
                }

                var applicant = new Applicants
                {
                    FullName = fullName,
                    Email = email,
                    FileName = resumeFile.FileName,
                    FilePath = "/applicants/" + uniqueName,
                    UploadDate = DateTime.Now,
                    Status = "Pending"
                };

                _context.Applicants.Add(applicant);
                await _context.SaveChangesAsync();

                // Notify Admin (system-level)
                _context.Notifications.Add(new Notification
                {
                    UserId = null,
                    Title = "New Applicant Resume",
                    Message = $"A new resume was uploaded: {resumeFile.FileName}.",
                    NotificationType = "Applications",
                    RelatedId = applicant.ApplicantId,
                    RelatedEntity = "Applicants",
                    Link = "/Admin/Applications",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });

                // Notify all Supervisors/Trainers
                var supervisors = await _context.Users
                    .Where(u => u.Position == "Supervisor" || u.Position == "Trainer")
                    .ToListAsync();

                foreach (var sup in supervisors)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = sup.UserId,
                        Title = "New Applicant Resume",
                        Message = $"A new resume was uploaded: {resumeFile.FileName}.",
                        NotificationType = "Applications",
                        RelatedId = applicant.ApplicantId,
                        RelatedEntity = "Applicants",
                        Link = "/Admin/Applications",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Resume submitted successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SubmitResume error: {ex.Message}");
                return Json(new { success = false, message = "Server error: " + ex.Message });
            }
        }
      
        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }
    }

    // Request models for the forgot password functionality
    public class EmailCheckRequest
    {
        public string Email { get; set; }
    }

    public class PasswordRecoveryRequest
    {
        public string Email { get; set; }
        public string Answer { get; set; }
    }
}