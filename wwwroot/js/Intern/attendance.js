
function showTimeInModal() {
    new bootstrap.Modal(document.getElementById('timeInModal')).show();
}

function logTimeOut() {
    if (confirm("Are you sure you want to time out now?")) {
        fetch('/Intern/TimeOut', {
            method: 'POST',
            headers: { 'RequestVerificationToken': getCsrfToken() }
        })
            .then(res => location.reload());
    }
}

function openRemarksModal(attendanceId, remarks, proofImageUrl) {
    document.getElementById('remarksAttendanceId').value = attendanceId;
    document.getElementById('remarksInput').value = remarks || '';

    const previewDiv = document.getElementById('existingImagePreview');
    const previewImage = document.getElementById('previewImage');

    if (proofImageUrl) {
        previewImage.src = proofImageUrl;
        previewDiv.classList.remove('d-none');
    } else {
        previewDiv.classList.add('d-none');
    }

    new bootstrap.Modal(document.getElementById('remarksModal')).show();
}

function removeImage() {
    document.getElementById('previewImage').src = '';
    document.getElementById('existingImagePreview').classList.add('d-none');
    // Optionally, you could add a hidden input to mark deletion in POST
}

function getCsrfToken() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value;
}