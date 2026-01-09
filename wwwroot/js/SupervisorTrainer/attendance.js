function filterAttendance() {
    const input = document.getElementById("attendanceSearch");
    const filter = input.value.toLowerCase();
    const table = document.getElementById("attendanceTable");
    const rows = table.getElementsByTagName("tr");

    for (let i = 1; i < rows.length; i++) {
        const row = rows[i];
        const rowText = row.innerText.toLowerCase();
        row.style.display = rowText.includes(filter) ? "" : "none";
    }
}
