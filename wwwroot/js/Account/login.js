
        function togglePassword(fieldId, icon) {
            const field = document.getElementById(fieldId);
            if (field.type === "password") {
                field.type = "text";
                icon.classList.remove("fa-eye");
                icon.classList.add("fa-eye-slash");
            } else {
                field.type = "password";
                icon.classList.remove("fa-eye-slash");
                icon.classList.add("fa-eye");
            }
        }

    function openForgotPasswordModal() {
        document.getElementById('forgotPasswordModal').style.display = 'flex';
        resetForgotPasswordModal();
    }

    function closeForgotPasswordModal() {
        document.getElementById('forgotPasswordModal').style.display = 'none';
        resetForgotPasswordModal();
    }

    function resetForgotPasswordModal() {
        // Reset all steps
        document.getElementById('step1').style.display = 'block';
        document.getElementById('step2').style.display = 'none';
        document.getElementById('step3').style.display = 'none';

        // Clear all inputs
        document.getElementById('forgotEmail').value = '';
        document.getElementById('securityAnswer').value = '';
        document.getElementById('displayQuestion').textContent = '';

        // Clear all error messages
        document.getElementById('emailError').textContent = '';
        document.getElementById('answerError').textContent = '';

        // Reset buttons
        const checkEmailBtn = document.getElementById('checkEmailBtn');
        const verifyAnswerBtn = document.getElementById('verifyAnswerBtn');
        checkEmailBtn.disabled = false;
        verifyAnswerBtn.disabled = false;
        checkEmailBtn.textContent = 'Next';
        verifyAnswerBtn.textContent = 'Verify';

        // Reset password display
        const passwordSpan = document.getElementById('recoveredPassword');
        const showIcon = document.getElementById('showPasswordIcon');
        passwordSpan.textContent = '********';
        passwordSpan.removeAttribute('data-password');
        showIcon.classList.remove('fa-eye-slash');
        showIcon.classList.add('fa-eye');
    }

    function backToStep1() {
        document.getElementById('step2').style.display = 'none';
        document.getElementById('step1').style.display = 'block';
        document.getElementById('answerError').textContent = '';
    }

    function checkEmail() {
        const email = document.getElementById('forgotEmail').value.trim();
        const emailError = document.getElementById('emailError');
        const checkEmailBtn = document.getElementById('checkEmailBtn');

        // Clear previous error
        emailError.textContent = '';

        if (!email) {
            emailError.textContent = 'Please enter your email';
            return;
        }

        // Basic email format check (just to ensure it has @ symbol)
        if (!email.includes('@')) {
            emailError.textContent = 'Please enter a valid email address';
            return;
        }

        // Disable button and show loading state
        checkEmailBtn.disabled = true;
        checkEmailBtn.textContent = 'Checking...';

        fetch('/Account/CheckEmailForRecovery', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify({ email: email })
        })
            .then(response => {
                if (!response.ok) {
                    throw new Error('Network response was not ok');
                }
                return response.json();
            })
            .then(data => {
                if (data.success) {
                    document.getElementById('displayQuestion').textContent = data.question;
                    document.getElementById('step1').style.display = 'none';
                    document.getElementById('step2').style.display = 'block';
                } else {
                    emailError.textContent = data.message || 'An error occurred';
                }
            })
            .catch(err => {
                console.error('Error:', err);
                emailError.textContent = 'An error occurred. Please try again.';
            })
            .finally(() => {
                // Re-enable button
                checkEmailBtn.disabled = false;
                checkEmailBtn.textContent = 'Next';
            });
    }

    function verifyAnswer() {
        const email = document.getElementById('forgotEmail').value.trim();
        const answer = document.getElementById('securityAnswer').value.trim();
        const answerError = document.getElementById('answerError');
        const verifyAnswerBtn = document.getElementById('verifyAnswerBtn');

        // Clear previous error
        answerError.textContent = '';

        if (!answer) {
            answerError.textContent = 'Please enter your answer';
            return;
        }

        // Disable button and show loading state
        verifyAnswerBtn.disabled = true;
        verifyAnswerBtn.textContent = 'Verifying...';

        fetch('/Account/VerifySecretAnswer', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify({ email: email, answer: answer })
        })
            .then(response => {
                if (!response.ok) {
                    throw new Error('Network response was not ok');
                }
                return response.json();
            })
            .then(data => {
                if (data.success) {
                    const passwordSpan = document.getElementById('recoveredPassword');
                    passwordSpan.textContent = '********';
                    passwordSpan.setAttribute('data-password', data.password);
                    document.getElementById('step2').style.display = 'none';
                    document.getElementById('step3').style.display = 'block';
                } else {
                    answerError.textContent = data.message || 'An error occurred';
                }
            })
            .catch(err => {
                console.error('Error:', err);
                answerError.textContent = 'An error occurred. Please try again.';
            })
            .finally(() => {
                // Re-enable button
                verifyAnswerBtn.disabled = false;
                verifyAnswerBtn.textContent = 'Verify';
            });
    }

    function toggleRecoveredPassword() {
        const passwordSpan = document.getElementById('recoveredPassword');
        const icon = document.getElementById('showPasswordIcon');
        const actualPassword = passwordSpan.getAttribute('data-password');

        if (!actualPassword) {
            console.error('No password data found');
            return;
        }

        if (passwordSpan.textContent === '********') {
            passwordSpan.textContent = actualPassword;
            icon.classList.remove('fa-eye');
            icon.classList.add('fa-eye-slash');
        } else {
            passwordSpan.textContent = '********';
            icon.classList.remove('fa-eye-slash');
            icon.classList.add('fa-eye');
        }
    }

    function triggerFileUpload() {
        document.getElementById('resumeUpload').click();
    }

    function openApplyModal() {
        const modal = document.getElementById("resumeModal");
        if (modal) {
            modal.classList.remove("d-none");
            modal.style.display = "flex";
        }
    }

    function closeApplyModal() {
        const modal = document.getElementById("resumeModal");
        if (modal) {
            modal.classList.add("d-none");
            modal.style.display = "none";
            document.getElementById("resumeUpload").value = "";
            document.getElementById("resumeFilename").innerText = "Click here to select a file";
        }
    }

    function handleFileUpload(input) {
        const file = input.files[0];
        const fileNameText = document.getElementById("resumeFilename");

        if (file) {
            const validTypes = ["application/pdf", "image/jpeg", "image/jpg", "image/png"];
            const maxSize = 10 * 1024 * 1024; // 10MB in bytes

            if (!validTypes.includes(file.type)) {
                alert("Only PDF, JPG, JPEG, and PNG files are allowed.");
                input.value = "";
                fileNameText.innerText = "Click here to select a file";
                return;
            }

            if (file.size > maxSize) {
                alert("File size must be less than 10MB.");
                input.value = "";
                fileNameText.innerText = "Click here to select a file";
                return;
            }

            // Show selected file name with file size
            const fileSizeKB = Math.round(file.size / 1024);
            const fileSizeMB = (file.size / (1024 * 1024)).toFixed(2);
            const sizeText = fileSizeKB > 1024 ? `${fileSizeMB} MB` : `${fileSizeKB} KB`;

            fileNameText.innerHTML = `<i class="fas fa-file-alt"></i> ${file.name}<br><small style="color: #666;">(${sizeText})</small>`;
        } else {
            fileNameText.innerText = "Click here to select a file";
        }
    }

    function submitResume() {
        const fileInput = document.getElementById("resumeUpload");
        const file = fileInput.files[0];
        const email = document.getElementById("applicantEmail").value;
        const fullName = document.getElementById("applicantName").value;

        if (!file) {
            alert("Please select a file to upload.");
            return;
        }
        if (!email || !fullName) {
            alert("Please fill in your name and email before submitting.");
            return;
        }

        const formData = new FormData();
        formData.append("resumeFile", file);
        formData.append("email", email);
        formData.append("fullName", fullName);

        // Show loading state (optional)
        const submitBtn = event.target;
        const originalText = submitBtn.textContent;
        submitBtn.disabled = true;
        submitBtn.textContent = "Uploading...";

        fetch('/Account/SubmitResume', {
            method: "POST",
            body: formData
        })
            .then(response => {
                if (!response.ok) {
                    throw new Error('Network response was not ok');
                }
                return response.json();
            })
            .then(data => {
                alert(data.message);
                if (data.success) {
                    closeApplyModal();
                }
            })
            .catch(err => {
                console.error('Error:', err);
                alert("An error occurred while uploading the file.");
            })
            .finally(() => {
                // Reset button state
                submitBtn.disabled = false;
                submitBtn.textContent = originalText;
            });
    }

    // Add event listeners for Enter key support
    document.addEventListener('DOMContentLoaded', function () {
        // Enter key support for forgot password modal
        document.getElementById('forgotEmail').addEventListener('keypress', function (e) {
            if (e.key === 'Enter') {
                checkEmail();
            }
        });

        document.getElementById('securityAnswer').addEventListener('keypress', function (e) {
            if (e.key === 'Enter') {
                verifyAnswer();
            }
        });

        // Close modal when clicking outside
        document.getElementById('forgotPasswordModal').addEventListener('click', function (e) {
            if (e.target === this) {
                closeForgotPasswordModal();
            }
        });

        document.getElementById('resumeModal').addEventListener('click', function (e) {
            if (e.target === this) {
                closeApplyModal();
            }
        });
    });