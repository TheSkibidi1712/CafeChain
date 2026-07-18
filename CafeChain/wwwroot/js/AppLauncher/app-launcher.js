(() => {
    const root = document.querySelector("#appLauncher");
    const posCard = root?.querySelector('[data-requires-launch="true"]');
    const statusBox = document.querySelector("#posLaunchStatus");
    if (!root || !posCard || !statusBox) return;

    let inFlight = false;

    const token = () => root.querySelector('input[name="__RequestVerificationToken"]')?.value || "";
    const headers = () => ({ "RequestVerificationToken": token(), "Accept": "application/json" });

    const showStatus = (message, isError = false) => {
        statusBox.hidden = false;
        statusBox.textContent = message;
        statusBox.classList.toggle("is-error", isError);
    };

    const readJson = async response => {
        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
            throw new Error(payload.message || "Không thể khởi chạy POS.");
        }
        return payload;
    };

    const issuePosToken = async () => readJson(await fetch(root.dataset.posTokenUrl, {
        method: "POST",
        headers: headers(),
        credentials: "same-origin"
    }));

    const pollStatus = async () => {
        try {
            const response = await fetch(root.dataset.posStatusUrl, {
                headers: { "Accept": "application/json" },
                credentials: "same-origin",
                cache: "no-store"
            });
            if (!response.ok) return;
            const payload = await response.json();
            if (payload.message) showStatus(payload.message, payload.state === 6 || payload.state === "Failed");
        } catch {
            // Launch request remains authoritative; polling is only progressive feedback.
        }
    };

    posCard.addEventListener("click", async event => {
        event.preventDefault();
        if (inFlight) return;

        inFlight = true;
        posCard.classList.add("is-launching");
        posCard.setAttribute("aria-disabled", "true");
        const newTab = window.open("about:blank", "_blank");
        if (newTab) newTab.document.title = "Đang khởi chạy CafeChain POS...";

        showStatus("Đang kiểm tra CafeChain.PrintBridge...");
        const pollTimer = window.setInterval(pollStatus, 700);

        try {
            const launch = await readJson(await fetch(root.dataset.launchPosUrl, {
                method: "POST",
                headers: headers(),
                credentials: "same-origin"
            }));
            if (!launch.isReady) throw new Error(launch.message || "POS chưa sẵn sàng.");

            showStatus("POS đã sẵn sàng. Đang tạo phiên đăng nhập...");
            const auth = await issuePosToken();
            const target = new URL(auth.posUrl);
            target.hash = `pos_token=${encodeURIComponent(auth.token)}`;

            if (newTab) newTab.location.replace(target.toString());
            else window.location.assign(target.toString());
            showStatus("POS đã sẵn sàng.");
        } catch (error) {
            if (newTab && !newTab.closed) newTab.close();
            showStatus(error.message || "Không thể khởi chạy POS.", true);
        } finally {
            window.clearInterval(pollTimer);
            inFlight = false;
            posCard.classList.remove("is-launching");
            posCard.removeAttribute("aria-disabled");
        }
    });
})();
