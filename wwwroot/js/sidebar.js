
    function toggleSidebar() {
        const sidebar = document.getElementById("sidebar");
    const mainContent = document.querySelector(".main-content");

    sidebar.classList.toggle("expanded");

    if (sidebar.classList.contains("expanded")) {
        mainContent.style.marginLeft = "220px";
        } else {
        mainContent.style.marginLeft = "60px";
        }
    }