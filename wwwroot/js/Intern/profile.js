// Profile Page JavaScript - ~/js/Intern/profile.js
document.addEventListener('DOMContentLoaded', function () {
    initializeProfilePage();
});

function initializeProfilePage() {
    setupImageModal();
    setupBirthdateValidation();
    setupContactNumberValidation();
    setupEmailValidation();
    setupPasswordValidation();
    setupFileUploadValidation();
    setupFormSubmitValidation();
    setupAlertNotification();
    setupRealTimeValidation();
}

// Image Preview and Modal
function previewImage(input) {
    if (input.files && input.files[0]) {
        const file = input.files[0];

        // Validate file size (max 5MB)
        if (file.size > 5 * 1024 * 1024) {
            showError('Profile image must be less than 5MB');
            input.value = '';
            return;
        }

        // Validate file type
        const allowedTypes = ['image/jpeg', 'image/jpg', 'image/png', 'image/gif', 'image/webp'];
        if (!allowedTypes.includes(file.type)) {
            showError('Only JPG, PNG, GIF, or WEBP formats are allowed');
            input.value = '';
            return;
        }

        const reader = new FileReader();
        reader.onload = function (e) {
            const preview = document.getElementById('imgPreview');
            const modalImg = document.getElementById('modalImgPreview');
            preview.src = e.target.result;
            modalImg.src = e.target.result;
        };
        reader.readAsDataURL(file);
    }
}

function setupImageModal() {
    const preview = document.getElementById('imgPreview');
    const modalImg = document.getElementById('modalImgPreview');

    if (preview && modalImg) {
        preview.addEventListener('click', function () {
            modalImg.src = this.src;
        });
    }
}

// Birthdate and Age Validation
function setupBirthdateValidation() {
    const birthdateInput = document.getElementById('birthdate');
    const ageInput = document.getElementById('age');

    if (birthdateInput) {
        // Set max date to today
        const today = new Date().toISOString().split('T')[0];
        birthdateInput.max = today;

        // Set min date to 100 years ago
        const minDate = new Date();
        minDate.setFullYear(minDate.getFullYear() - 100);
        birthdateInput.min = minDate.toISOString().split('T')[0];

        birthdateInput.addEventListener('change', function () {
            const birthDate = new Date(this.value);
            const today = new Date();

            if (birthDate > today) {
                showError('Birthdate cannot be in the future');
                this.value = '';
                ageInput.value = '';
                this.classList.add('error');
                return;
            }

            let age = today.getFullYear() - birthDate.getFullYear();
            const monthDiff = today.getMonth() - birthDate.getMonth();

            if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < birthDate.getDate())) {
                age--;
            }

            if (age < 18) {
                showError('You must be at least 18 years old');
                this.classList.add('error');
            } else {
                this.classList.remove('error');
                clearError(this);
            }

            if (ageInput) {
                ageInput.value = age;
            }
        });

        // Calculate age on page load if birthdate exists
        if (birthdateInput.value) {
            birthdateInput.dispatchEvent(new Event('change'));
        }
    }
}

// Contact Number Validation
function setupContactNumberValidation() {
    const contactInput = document.querySelector('input[name="ContactNumber"]');

    if (contactInput) {
        contactInput.addEventListener('input', function () {
            let value = this.value.replace(/\D/g, ''); // Remove non-digits

            // Limit to 11 digits
            if (value.length > 11) {
                value = value.substring(0, 11);
            }

            // Ensure it starts with 09
            if (value.length >= 2 && !value.startsWith('09')) {
                if (value.startsWith('9')) {
                    value = '0' + value;
                } else {
                    value = '09' + value.substring(2);
                }
            }

            this.value = value;

            // Validate format
            const phoneRegex = /^09\d{9}$/;
            if (value && !phoneRegex.test(value)) {
                this.classList.add('error');
                showFieldError(this, 'Contact number must be 11 digits starting with 09');
            } else {
                this.classList.remove('error');
                clearFieldError(this);
            }
        });

        // Prevent non-numeric input
        contactInput.addEventListener('keypress', function (e) {
            if (!/\d/.test(e.key) && !['Backspace', 'Delete', 'Tab', 'Enter'].includes(e.key)) {
                e.preventDefault();
            }
        });
    }
}

// Email Validation
function setupEmailValidation() {
    const emailInput = document.querySelector('input[name="Email"]');

    if (emailInput) {
        emailInput.addEventListener('blur', function () {
            const email = this.value.trim();
            const emailRegex = /^[\w\.-]+@(gmail\.com|ims\.com)$/;

            if (email && !emailRegex.test(email)) {
                this.classList.add('error');
                showFieldError(this, 'Email must be a valid Gmail or IMS email address');
            } else {
                this.classList.remove('error');
                clearFieldError(this);
            }
        });
    }
}

// Password Validation
function setupPasswordValidation() {
    const newPasswordInput = document.getElementById('NewPassword');
    const confirmPasswordInput = document.getElementById('ConfirmNewPassword');
    const strengthBar = document.getElementById('strengthBar');

    if (newPasswordInput) {
        newPasswordInput.addEventListener('input', function () {
            const password = this.value;
            updatePasswordStrength(password, strengthBar);

            if (confirmPasswordInput.value) {
                validatePasswordMatch();
            }
        });
    }

    if (confirmPasswordInput) {
        confirmPasswordInput.addEventListener('input', validatePasswordMatch);
    }

    function validatePasswordMatch() {
        const newPassword = newPasswordInput.value;
        const confirmPassword = confirmPasswordInput.value;

        if (confirmPassword && newPassword !== confirmPassword) {
            confirmPasswordInput.classList.add('error');
            showFieldError(confirmPasswordInput, 'Passwords do not match');
        } else {
            confirmPasswordInput.classList.remove('error');
            clearFieldError(confirmPasswordInput);
        }
    }
}

function updatePasswordStrength(password, strengthBar) {
    if (!strengthBar) return;

    let strength = 0;
    let strengthText = '';
    let strengthClass = '';

    if (password.length >= 8) strength++;
    if (/[a-z]/.test(password)) strength++;
    if (/[A-Z]/.test(password)) strength++;
    if (/\d/.test(password)) strength++;
    if (/[!@#$%^&*(),.?":{}|<>]/.test(password)) strength++;

    switch (strength) {
        case 0:
        case 1:
            strengthText = 'Very Weak';
            strengthClass = 'very-weak';
            break;
        case 2:
            strengthText = 'Weak';
            strengthClass = 'weak';
            break;
        case 3:
            strengthText = 'Fair';
            strengthClass = 'fair';
            break;
        case 4:
            strengthText = 'Good';
            strengthClass = 'good';
            break;
        case 5:
            strengthText = 'Strong';
            strengthClass = 'strong';
            break;
    }

    strengthBar.className = 'strength-bar ' + strengthClass;
    strengthBar.style.width = (strength * 20) + '%';
    strengthBar.setAttribute('data-strength', strengthText);
}

// File Upload Validation
function setupFileUploadValidation() {
    const fileInput = document.querySelector('input[name="ProfileImage"]');

    if (fileInput) {
        fileInput.addEventListener('change', function () {
            if (this.files && this.files[0]) {
                const file = this.files[0];

                // Validate file size
                if (file.size > 5 * 1024 * 1024) {
                    showError('Profile image must be less than 5MB');
                    this.value = '';
                    return;
                }

                // Validate file type
                const allowedTypes = ['image/jpeg', 'image/jpg', 'image/png', 'image/gif', 'image/webp'];
                if (!allowedTypes.includes(file.type)) {
                    showError('Only JPG, PNG, GIF, or WEBP formats are allowed');
                    this.value = '';
                    return;
                }
            }
        });
    }
}

// Form Submit Validation
function setupFormSubmitValidation() {
    const form = document.getElementById('profileForm');

    if (form) {
        form.addEventListener('submit', function (e) {
            let hasErrors = false;

            // Validate required fields
            const requiredFields = form.querySelectorAll('input[required], select[required]');
            requiredFields.forEach(field => {
                if (!field.value.trim()) {
                    field.classList.add('error');
                    hasErrors = true;
                }
            });

            // Validate age
            const birthdateInput = document.getElementById('birthdate');
            if (birthdateInput && birthdateInput.value) {
                const birthDate = new Date(birthdateInput.value);
                const today = new Date();
                let age = today.getFullYear() - birthDate.getFullYear();
                const monthDiff = today.getMonth() - birthDate.getMonth();

                if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < birthDate.getDate())) {
                    age--;
                }

                if (age < 18) {
                    hasErrors = true;
                    showError('You must be at least 18 years old');
                }
            }

            // Validate password fields if any are filled
            const currentPassword = document.querySelector('input[name="CurrentPassword"]');
            const newPassword = document.querySelector('input[name="NewPassword"]');
            const confirmPassword = document.querySelector('input[name="ConfirmNewPassword"]');

            if (currentPassword && newPassword && confirmPassword) {
                const hasCurrentPwd = currentPassword.value.trim();
                const hasNewPwd = newPassword.value.trim();
                const hasConfirmPwd = confirmPassword.value.trim();

                if (hasCurrentPwd || hasNewPwd || hasConfirmPwd) {
                    if (!hasCurrentPwd || !hasNewPwd || !hasConfirmPwd) {
                        hasErrors = true;
                        showError('All password fields are required when changing password');
                    } else if (hasNewPwd !== hasConfirmPwd) {
                        hasErrors = true;
                        showError('New password and confirmation do not match');
                    } else if (hasNewPwd.length < 8) {
                        hasErrors = true;
                        showError('New password must be at least 8 characters long');
                    }
                }
            }

            if (hasErrors) {
                e.preventDefault();
                scrollToFirstError();
                console.log('Form submission prevented due to client-side validation errors');
            } else {
                console.log('Form validation passed, submitting...');
            }
        });
    }
}

// Real-time Validation
function setupRealTimeValidation() {
    const inputs = document.querySelectorAll('input, select');

    inputs.forEach(input => {
        input.addEventListener('input', function () {
            if (this.classList.contains('error')) {
                validateField(this);
            }
        });

        input.addEventListener('blur', function () {
            validateField(this);
        });
    });
}

function validateField(field) {
    const fieldName = field.name || field.id;
    let isValid = true;

    switch (fieldName) {
        case 'Email':
            const emailRegex = /^[\w\.-]+@(gmail\.com|ims\.com)$/;
            isValid = !field.value || emailRegex.test(field.value);
            break;
        case 'ContactNumber':
            const phoneRegex = /^09\d{9}$/;
            isValid = !field.value || phoneRegex.test(field.value);
            break;
        case 'FirstName':
        case 'LastName':
        case 'Gender':
        case 'Course':
        case 'School':
            isValid = field.value.trim() !== '';
            break;
        case 'HoursToRender':
            isValid = !field.value || (parseInt(field.value) > 0 && parseInt(field.value) <= 1000);
            break;
    }

    if (isValid) {
        field.classList.remove('error');
        clearFieldError(field);
    } else {
        field.classList.add('error');
    }
}

// Alert Notification
function setupAlertNotification() {
    const alert = document.getElementById('customAlert');
    if (alert) {
        setTimeout(() => {
            alert.style.opacity = '0';
            setTimeout(() => {
                alert.style.display = 'none';
            }, 300);
        }, 5000);

        // Allow manual close
        alert.addEventListener('click', function () {
            this.style.opacity = '0';
            setTimeout(() => {
                this.style.display = 'none';
            }, 300);
        });
    }
}

// Utility Functions
function showError(message) {
    // Create or update error notification
    let errorDiv = document.querySelector('.error-notification');
    if (!errorDiv) {
        errorDiv = document.createElement('div');
        errorDiv.className = 'error-notification';
        document.body.appendChild(errorDiv);
    }

    errorDiv.innerHTML = `<i class="fas fa-exclamation-circle"></i><span>${message}</span>`;
    errorDiv.style.display = 'block';

    setTimeout(() => {
        errorDiv.style.opacity = '0';
        setTimeout(() => {
            errorDiv.style.display = 'none';
        }, 300);
    }, 5000);
}

function showFieldError(field, message) {
    const errorSpan = field.parentNode.parentNode.querySelector('.error-text');
    if (errorSpan) {
        errorSpan.textContent = message;
        errorSpan.style.display = 'block';
    }
}

function clearFieldError(field) {
    const errorSpan = field.parentNode.parentNode.querySelector('.error-text');
    if (errorSpan) {
        errorSpan.textContent = '';
        errorSpan.style.display = 'none';
    }
}

function clearError(field) {
    clearFieldError(field);
}

function scrollToFirstError() {
    const firstError = document.querySelector('.error, .error-text:not(:empty)');
    if (firstError) {
        firstError.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
}

// Initialize password strength on page load
document.addEventListener('DOMContentLoaded', function () {
    const newPasswordInput = document.getElementById('NewPassword');
    const strengthBar = document.getElementById('strengthBar');

    if (newPasswordInput && newPasswordInput.value) {
        updatePasswordStrength(newPasswordInput.value, strengthBar);
    }
});