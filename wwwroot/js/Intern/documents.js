function showUploadModal() {
    document.getElementById("uploadModal").style.display = "flex";
}

function hideUploadModal() {
    document.getElementById("uploadModal").style.display = "none";
}

function handleTypeChange(value) {
    const otherGroup = document.getElementById("otherTypeGroup");
    otherGroup.style.display = value === "Other" ? "block" : "none";
}

// Search filter
document.getElementById("searchInput").addEventListener("keyup", function () {
    const searchTerm = this.value.toLowerCase();
    const rows = document.querySelectorAll("#docTable tbody tr");

    rows.forEach(row => {
        const fileName = row.children[0].innerText.toLowerCase();
        const docType = row.children[1].innerText.toLowerCase();
        row.style.display = fileName.includes(searchTerm) || docType.includes(searchTerm) ? "" : "none";
    });
});