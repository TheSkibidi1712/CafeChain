window.toast = function (msg, type = "success") {

    if (type !== "success" && type !== "error") {
        type = "success";
    }

    const t = document.createElement("div");
    t.className = `toast-item ${type}`;

    t.innerHTML = `
        <div class="toast-content">
            <div class="toast-icon">
                <i class="fa ${type === "success" ? "fa-check-circle" : "fa-times-circle"}"></i>
            </div>
            <div class="toast-text">${msg}</div>
        </div>
        <div class="toast-progress"></div>
    `;

    const container = document.getElementById("toast-container");
    if (!container) return;

    container.appendChild(t);

    setTimeout(() => {
        t.classList.add("fade-out");
        setTimeout(() => t.remove(), 300);
    }, 3000);
};