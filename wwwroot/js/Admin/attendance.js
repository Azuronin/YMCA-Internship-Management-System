function filterAttendance() {
    let input = document.getElementById("attendanceSearch").value.toLowerCase();
    let table = document.getElementById("attendanceTable");
    let rows = table.getElementsByTagName("tr");

    for (let i = 1; i < rows.length; i++) {
        let rowText = rows[i].textContent.toLowerCase();
        rows[i].style.display = rowText.includes(input) ? "" : "none";
    }
}
