document.addEventListener("DOMContentLoaded", () => {
  const prompt = document.querySelector("#aiPrompt");
  const charCount = document.querySelector("#charCount");
  const aiForm = document.querySelector("#aiForm");
  const analyzeButton = document.querySelector("#analyzeButton");
  const answerPanel = document.querySelector("#answerPanel");

  if (prompt && charCount) {
    const updateCount = () => { charCount.textContent = prompt.value.length; };
    prompt.addEventListener("input", updateCount);
    document.querySelectorAll(".suggestion").forEach((button) => {
      button.addEventListener("click", () => {
        prompt.value = button.textContent.trim();
        updateCount();
        prompt.focus();
      });
    });
  }

  if (aiForm && analyzeButton && answerPanel) {
    const runAnalysis = () => {
      const label = analyzeButton.querySelector("span");
      const original = label.textContent;
      label.textContent = "Đang đối chiếu dữ liệu...";
      analyzeButton.disabled = true;
      window.setTimeout(() => {
        label.textContent = original;
        analyzeButton.disabled = false;
        answerPanel.classList.add("show");
        answerPanel.scrollIntoView({ behavior: "smooth", block: "nearest" });
      }, 650);
    };
    aiForm.addEventListener("submit", (event) => {
      event.preventDefault();
      runAnalysis();
    });
    prompt.addEventListener("keydown", (event) => {
      if ((event.ctrlKey || event.metaKey) && event.key === "Enter") {
        event.preventDefault();
        aiForm.requestSubmit();
      }
    });
  }

  document.querySelectorAll(".guide-card-head").forEach((button) => {
    button.addEventListener("click", () => {
      const card = button.closest(".guide-card");
      const closed = card.classList.toggle("closed");
      button.setAttribute("aria-expanded", String(!closed));
    });
  });

  const search = document.querySelector("#guideSearch");
  const cards = [...document.querySelectorAll(".guide-card")];
  const noResults = document.querySelector("#noResults");
  if (search && cards.length) {
    search.addEventListener("input", () => {
      const query = search.value.trim().toLocaleLowerCase("vi");
      let shown = 0;
      cards.forEach((card) => {
        const text = `${card.textContent} ${card.dataset.search || ""}`.toLocaleLowerCase("vi");
        const match = !query || text.includes(query);
        card.hidden = !match;
        if (match) {
          shown += 1;
          if (query) {
            card.classList.remove("closed");
            card.querySelector(".guide-card-head").setAttribute("aria-expanded", "true");
          }
        }
      });
      if (noResults) noResults.style.display = shown ? "none" : "block";
    });
  }

  document.querySelectorAll(".guide-link").forEach((link) => {
    link.addEventListener("click", () => {
      document.querySelectorAll(".guide-link").forEach((item) => item.classList.remove("active"));
      link.classList.add("active");
    });
  });
});
