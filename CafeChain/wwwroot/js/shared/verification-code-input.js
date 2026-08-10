(function () {
    "use strict";
    const OTP_ALPHABET = new Set("ABCDEFGHJKLMNPQRSTUVWXYZ23456789".split(""));
    let groupSequence = 0;

    function enhance(input) {
        if (!input || input.dataset.codeEnhanced === "true") return;
        input.dataset.codeEnhanced = "true";
        const required = input.required;
        const groupId = `verification-code-${++groupSequence}`;
        const pin = input.dataset.verificationMode === "pin";
        const allowed = pin ? new Set("0123456789".split("")) : OTP_ALPHABET;
        const group = document.createElement("span");
        group.className = "verification-code-input";
        group.setAttribute("role", "group");
        group.setAttribute("aria-required", required ? "true" : "false");
        group.setAttribute("aria-label", input.getAttribute("aria-label") || (pin ? "PIN 6 chữ số" : "OTP 6 ký tự"));
        const cells = [];

        function normalize(raw) {
            return Array.from(String(raw || "").toUpperCase()).filter(char => allowed.has(char)).slice(0, 6);
        }
        function sync(values, emitChange) {
            input.value = values.join("");
            cells.forEach((cell, index) => { cell.value = values[index] || ""; });
            input.dispatchEvent(new Event("input", { bubbles: true }));
            if (emitChange === true) {
                input.dispatchEvent(new Event("change", { bubbles: true }));
            }
        }
        function setInvalid(invalid) {
            if (invalid) input.setAttribute("aria-invalid", "true");
            else input.removeAttribute("aria-invalid");
            syncState();
        }
        function isComplete() {
            return cells.every(cell => cell.value.length === 1)
                && normalize(input.value).length === 6;
        }
        function focusFirstIncomplete() {
            const target = cells.find(cell => !cell.value) || cells[0];
            target?.focus();
        }
        function write(index, raw) {
            const incoming = normalize(raw);
            const values = cells.map(cell => cell.value);
            if (!incoming.length) {
                values[index] = "";
                sync(values);
                return;
            }
            incoming.forEach((char, offset) => { if (index + offset < 6) values[index + offset] = char; });
            setInvalid(false);
            sync(values);
            cells[Math.min(5, index + incoming.length)]?.focus();
        }

        for (let index = 0; index < 6; index += 1) {
            const cell = document.createElement("input");
            cell.type = pin ? "password" : "text";
            cell.inputMode = pin ? "numeric" : "text";
            cell.maxLength = 1;
            cell.autocomplete = index === 0 && !pin ? "one-time-code" : "off";
            cell.id = `${groupId}-cell-${index + 1}`;
            cell.required = required;
            cell.setAttribute("aria-label", `${pin ? "PIN" : "OTP"}, ô ${index + 1}`);
            cell.addEventListener("focus", () => cell.select());
            cell.addEventListener("input", () => {
                if (!cell.value) {
                    const values = cells.map(item => item.value);
                    values[index] = "";
                    setInvalid(false);
                    sync(values);
                    return;
                }
                write(index, cell.value);
            });
            cell.addEventListener("keydown", event => {
                if (event.key === "Backspace" && !cell.value && index > 0) {
                    event.preventDefault();
                    const values = cells.map(item => item.value);
                    values[index - 1] = "";
                    setInvalid(false);
                    sync(values);
                    cells[index - 1].focus();
                }
            });
            cell.addEventListener("paste", event => {
                event.preventDefault();
                write(index, event.clipboardData.getData("text"));
            });
            cells.push(cell);
            group.appendChild(cell);
        }

        input.classList.add("verification-code-source");
        // Constraint validation must target the visible cells. Keeping `required`
        // on this visually hidden source makes global form guards report an
        // invisible missing field before the page's active validation can run.
        input.required = false;
        input.tabIndex = -1;
        input.insertAdjacentElement("afterend", group);
        const syncState = () => cells.forEach(cell => {
            cell.disabled = input.disabled;
            cell.setAttribute("aria-invalid", input.getAttribute("aria-invalid") || "false");
        });
        input._verificationCodeController = {
            setValue(value) { sync(normalize(value), true); },
            isComplete,
            focusFirstIncomplete,
            setInvalid
        };
        new MutationObserver(syncState).observe(input, {
            attributes: true,
            attributeFilter: ["disabled", "aria-invalid"]
        });
        sync(normalize(input.value));
        syncState();
    }

    function scan(root) {
        root.querySelectorAll?.("[data-verification-code-input]").forEach(enhance);
    }
    scan(document);
    new MutationObserver(records => records.forEach(record => record.addedNodes.forEach(node => {
        if (node.nodeType === Node.ELEMENT_NODE) {
            if (node.matches?.("[data-verification-code-input]")) enhance(node);
            scan(node);
        }
    }))).observe(document.documentElement, { childList: true, subtree: true });
    window.VerificationCodeInput = {
        enhance,
        scan,
        setValue(input, value) {
            if (!input) return;
            if (input._verificationCodeController) input._verificationCodeController.setValue(value);
            else {
                input.value = value;
                input.dispatchEvent(new Event("input", { bubbles: true }));
                input.dispatchEvent(new Event("change", { bubbles: true }));
            }
        },
        isComplete(input) {
            if (!input) return false;
            return input._verificationCodeController?.isComplete() === true;
        },
        focusFirstIncomplete(input) {
            input?._verificationCodeController?.focusFirstIncomplete();
        },
        setInvalid(input, invalid) {
            if (!input) return;
            if (input._verificationCodeController) input._verificationCodeController.setInvalid(invalid === true);
            else if (invalid) input.setAttribute("aria-invalid", "true");
            else input.removeAttribute("aria-invalid");
        }
    };
}());
