I just found my freelancing code through my old desktop!

The Internship Management System (IMS) is a web-based application built using .NET and Entity Framework Core that streamlines the management of internship programs for organizations and educational institutions.
The system centralizes intern records, internship postings, applications, evaluations, and progress tracking into a single, efficient platform.
IMS enables administrators to manage internship listings, assign supervisors, and monitor intern performance, while interns can apply for internships, submit requirements, and track their progress. 
Supervisors can evaluate interns, provide feedback, and manage assigned interns through a structured workflow.
The system follows a layered architecture with a RESTful API backend, ensuring scalability, maintainability, and clean separation of concerns. Entity Framework Core is used for database access, enabling efficient CRUD operations, data validation, and migrations. 
The application emphasizes secure authentication, role-based access control, and reliable data persistence.

System Features
1. User & Role Management
- Manages user accounts and access levels.
- Differentiates roles such as Intern, Supervisor/Trainer, and Administrator.
- Ensures secure role-based access throughout the system.

2. Dashboard
- Intern Dashboard
- View assigned tasks, task progress, and due dates.
- Monitor rendered hours and internship status.
- Supervisor/Trainer Dashboard
- Track intern progress and attendance.
- View pending applications and tasks requiring approval.

3. Application Management
- Handles internship application submissions.
- Tracks application status (pending, approved, rejected).
- Allows supervisors to review and approve applications.

4. Digital Attendance Management
- Tracks intern log-in and log-out times.
- Records late arrivals and absences.
- Requires supervisor approval for attendance validation.

5. Document Management
- Enables interns and supervisors to upload, view, and manage internship-related documents.
- Keeps documents organized, secure, and easily accessible.

6. Intern Management
- Provides a centralized profile for each intern.
- Displays rendered hours, assigned tasks, and engagement status.
- Helps supervisors monitor and support interns effectively.

7. Supervisor / Trainer Management
- Allows supervisors to manage intern data.
- Approves internship applications and attendance.
- Creates announcements and oversees internship activities.

8. Task Management
- Enables supervisors/trainers to create, assign, and manage tasks.
- Organizes tasks from creation to completion.
- Tracks deadlines and task status.

9. Task Progress Management
- Allows interns to update task progress.
- Displays completed, ongoing, and pending tasks.
- Keeps both interns and supervisors informed on deadlines.

10. Reports & Certification
- Allows interns to submit progress and task reports.
- Generates internship completion certificates upon fulfillment of requirements.

11. Evaluation
- Enables supervisors to provide performance evaluations.
- Supports structured feedback for intern development.

12. Notifications
- Sends alerts and reminders to interns and supervisors.
- Notifies users about:
- Task deadlines
- Absences and attendance issues
- Announcements and important updates

Technologies Used
- Backend: ASP.NET Core (.NET)
- ORM: Entity Framework Core
- Database: SQL Server / MySQL
- Architecture: RESTful API, layered architecture
- Tools: Git for version control
