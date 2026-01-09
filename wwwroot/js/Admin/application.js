document.addEventListener("DOMContentLoaded", () => {
    // Elements
    const tableSelect = document.getElementById("tableSelect");
    const searchInput = document.getElementById("searchInput");
    const dateFilter = document.getElementById("dateFilter");
    const applicantsTable = document.getElementById("applicantsTable");
    const documentsTable = document.getElementById("documentsTable");
    const sidebarToggleBtn = document.getElementById("sidebarToggle");
    const sidebar = document.querySelector(".sidebar");
    const contentWrapper = document.querySelector(".content-wrapper");

    const pdfViewer = document.getElementById("fileViewerPdf");
    const imageViewer = document.getElementById("fileViewerImage");
    const modalElement = document.getElementById("pdfModal");
    const modal = new bootstrap.Modal(modalElement);

    // Preview File Function
    window.previewFile = function (fileUrl) {
        if (!fileUrl) {
            alert("File not found.");
            return;
        }

        const ext = fileUrl.split('.').pop().toLowerCase();
        resetPreview();

        if (ext === "pdf") {
            pdfViewer.src = fileUrl;
            pdfViewer.style.display = "block";
        } else if (["jpg", "jpeg", "png", "gif", "bmp", "webp"].includes(ext)) {
            imageViewer.src = fileUrl;
            imageViewer.style.display = "block";
        } else {
            alert("Unsupported file type.");
            return;
        }

        modal.show();
    };

    // Reset preview modal
    function resetPreview() {
        pdfViewer.src = "";
        imageViewer.src = "";
        pdfViewer.style.display = "none";
        imageViewer.style.display = "none";
    }

    // Sidebar toggle
    function toggleSidebar() {
        sidebar?.classList.toggle("collapsed");
        contentWrapper?.classList.toggle("expanded");
    }

    // Table View Switching
    function showTable() {
        const selected = tableSelect.value;
        const statusFilterContainer = document.getElementById("statusFilterContainer");

        applicantsTable.classList.toggle("d-none", selected !== "applicants");
        documentsTable.classList.toggle("d-none", selected !== "documents");
        statusFilterContainer.classList.toggle("d-none", selected !== "documents");

        filterRows();
    }


    // Search & Filter Function
    function filterRows() {
        const keyword = searchInput.value.toLowerCase().trim();
        const selectedDate = dateFilter.value;
        const selected = tableSelect.value;
        const selectedStatus = document.getElementById("statusFilter")?.value || "";

        const rows = (selected === "applicants")
            ? applicantsTable.querySelectorAll(".app-row")
            : documentsTable.querySelectorAll(".doc-row");

        rows.forEach(row => {
            const name = row.dataset.name?.toLowerCase() || "";
            const date = row.dataset.date || "";
            const type = row.dataset.type?.toLowerCase() || "";
            const uploader = row.dataset.uploader?.toLowerCase() || "";
            const status = row.dataset.status?.toLowerCase() || "";

            const matchesKeyword = !keyword || name.includes(keyword) || type.includes(keyword) || uploader.includes(keyword);
            const matchesDate = !selectedDate || date === selectedDate;
            const matchesStatus = !selectedStatus || status === selectedStatus;

            row.style.display = (matchesKeyword && matchesDate && matchesStatus) ? "" : "none";
        });
    }


    // Event Listeners
    tableSelect?.addEventListener("change", showTable);
    searchInput?.addEventListener("input", filterRows);
    dateFilter?.addEventListener("change", filterRows);
    sidebarToggleBtn?.addEventListener("click", toggleSidebar);
    document.getElementById("statusFilter")?.addEventListener("change", filterRows);

    // Initial render
    showTable();
});
