"use strict";
// Add showings: several dates × several start times, each time with its own price.
// The server re-checks everything (prices, overlaps, past times) when the form is submitted.
const batchForm = document.getElementById("batch-form");
if (batchForm) {
  const pricing = JSON.parse(batchForm.dataset.pricing);
  const vnd = new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND", maximumFractionDigits: 0 });
  const slots = document.getElementById("slots");
  const template = document.getElementById("slot-template");
  const movie = batchForm.querySelector('select[name="MovieId"]');
  const hall = batchForm.querySelector('select[name="HallId"]');
  const minutes = t => { const [h, m] = t.split(":").map(Number); return h * 60 + m; };
  const hhmm = total => `${String(Math.floor(total / 60) % 24).padStart(2, "0")}:${String(total % 60).padStart(2, "0")}`;
  const duration = () => Number(movie.selectedOptions[0]?.dataset.duration || 0);
  const isImax = () => hall.selectedOptions[0]?.dataset.imax === "1";
  const tier = t => minutes(t) < minutes(pricing.afternoonFrom) ? "Morning" : minutes(t) < minutes(pricing.eveningFrom) ? "Afternoon" : "Evening";
  const suggest = t => ({ Morning: pricing.morning, Afternoon: pricing.afternoon, Evening: pricing.evening })[tier(t)] + (isImax() ? pricing.imax : 0);
  const rows = () => [...slots.querySelectorAll(".slot-row")];
  const timeOf = row => row.querySelector('input[type="time"]').value;
  const priceOf = row => row.querySelector('input[name="Prices"]');

  function refreshRow(row, resetPrice) {
    const time = timeOf(row);
    const price = priceOf(row);
    const badge = row.querySelector(".slot-tier");
    const end = row.querySelector(".slot-end");
    if (!time) { badge.textContent = ""; badge.className = "badge badge-outline slot-tier"; end.textContent = ""; return; }
    const name = tier(time);
    badge.textContent = name;
    badge.className = `badge badge-outline slot-tier ${name.toLowerCase()}`;
    const d = duration();
    const finish = minutes(time) + d;
    end.textContent = d ? `ends ${hhmm(finish)}${finish >= 1440 ? " (+1 day)" : ""}` : "choose a movie";
    // Keep prices typed by hand; refill suggested ones when the time or hall changes.
    if (resetPrice || !price.classList.contains("manual") || !price.value) {
      price.value = suggest(time);
      price.classList.remove("manual");
    }
  }

  function refreshAll(resetPrices) { rows().forEach(r => refreshRow(r, resetPrices)); updateSummary(); }

  function addRow(time) {
    const row = template.content.firstElementChild.cloneNode(true);
    slots.append(row);
    if (time) row.querySelector('input[type="time"]').value = time;
    refreshRow(row, true);
    return row;
  }

  function updateSummary() {
    const dates = batchForm.querySelectorAll('input[name="Dates"]:checked').length;
    const times = rows().filter(timeOf).length;
    document.getElementById("date-count").textContent = dates;
    const total = dates * times;
    const prices = rows().map(r => Number(priceOf(r).value)).filter(Boolean);
    const low = Math.min(...prices), high = Math.max(...prices);
    const range = !prices.length ? "" : low === high ? vnd.format(low) : `${vnd.format(low)} – ${vnd.format(high)}`;
    const summary = document.getElementById("batch-summary");
    summary.replaceChildren();
    const strong = text => { const s = document.createElement("strong"); s.textContent = text; return s; };
    summary.append(strong(dates), " date(s) × ", strong(times), " time(s) = ", strong(total), ` showing(s)${range ? " · " + range : ""}`);
    const submit = document.getElementById("batch-submit");
    submit.textContent = total === 1 ? "Add 1 showing" : `Add ${total} showings`;
    submit.disabled = total === 0;
  }

  // Rows from first load or a post-back: a price that differs from the suggestion was typed by hand.
  rows().forEach(row => {
    const time = timeOf(row), price = priceOf(row);
    if (time && price.value && Number(price.value) !== suggest(time)) price.classList.add("manual");
    refreshRow(row, false);
  });
  if (!rows().length) addRow("18:30");

  slots.addEventListener("input", e => {
    if (e.target.name === "Prices") e.target.classList.add("manual");
    if (e.target.type === "time") refreshRow(e.target.closest(".slot-row"), false);
    updateSummary();
  });
  slots.addEventListener("click", e => {
    const remove = e.target.closest(".slot-remove");
    if (!remove) return;
    remove.closest(".slot-row").remove();
    updateSummary();
  });
  document.getElementById("add-slot").addEventListener("click", () => {
    // Suggest the next start: after the latest slot's movie ends + 20 min cleaning, rounded up to 15 minutes.
    const last = rows().map(timeOf).filter(Boolean).sort().pop();
    const next = last ? Math.ceil((minutes(last) + (duration() || 120) + 20) / 15) * 15 : 18 * 60 + 30;
    addRow(next < 1440 ? hhmm(next) : "").querySelector('input[type="time"]').focus();
    updateSummary();
  });
  document.getElementById("fill-day").addEventListener("click", () => {
    const d = duration();
    if (!d) { movie.reportValidity(); movie.focus(); return; }
    rows().forEach(r => r.remove());
    for (let t = 9 * 60 + 30; t <= 22 * 60 + 30; t = Math.ceil((t + d + 20) / 15) * 15) addRow(hhmm(t));
    updateSummary();
  });
  document.getElementById("reset-prices").addEventListener("click", () => refreshAll(true));
  movie.addEventListener("change", () => refreshAll(false));
  hall.addEventListener("change", () => refreshAll(false));

  // ----- Dates -----
  const dayBoxes = () => [...batchForm.querySelectorAll('input[name="Dates"]')];
  const otherDate = document.getElementById("other-date");
  const today = otherDate.min;
  const fmt = iso => new Date(iso + "T00:00:00").toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" });
  // Only days inside the selected movie's showing period can be picked.
  function applyRun() {
    const option = movie.selectedOptions[0];
    const start = option?.dataset.start, end = option?.dataset.end;
    const note = document.getElementById("run-note");
    dayBoxes().forEach(box => {
      const outside = !!start && (box.value < start || box.value > end);
      box.disabled = outside;
      if (outside) box.checked = false;
      box.closest(".day-chip").title = outside ? "Outside this movie's showing period" : "";
    });
    otherDate.min = start && start > today ? start : today;
    otherDate.max = end || "";
    note.replaceChildren();
    if (start) {
      const strong = document.createElement("strong");
      strong.textContent = `${fmt(start)} – ${fmt(end)}`;
      note.append(`${option.textContent.replace(/ \(.*$/, "")} is showing `, strong, ". Days outside this period are greyed out.");
    }
    updateSummary();
  }
  movie.addEventListener("change", applyRun);
  batchForm.addEventListener("change", e => { if (e.target.name === "Dates") updateSummary(); });
  batchForm.querySelectorAll("[data-dates]").forEach(button => button.addEventListener("click", () => {
    const mode = button.dataset.dates;
    dayBoxes().forEach(box => {
      const dow = Number(box.dataset.dow), offset = Number(box.dataset.offset);
      if (box.disabled) return;
      box.checked = mode === "clear" ? false
        : mode === "week" ? offset < 7
        : mode === "weekdays" ? offset < 14 && dow >= 1 && dow <= 5
        : offset < 14 && (dow === 0 || dow === 6);
    });
    updateSummary();
  }));
  document.getElementById("add-date").addEventListener("click", () => {
    const input = document.getElementById("other-date");
    if (!input.value) { input.focus(); return; }
    if (!input.checkValidity()) { input.reportValidity(); return; }
    const existing = dayBoxes().find(b => b.value === input.value);
    if (existing) { existing.checked = true; input.value = ""; updateSummary(); return; }
    const date = new Date(input.value + "T00:00:00");
    const label = document.createElement("label");
    label.className = "day-chip";
    const box = document.createElement("input");
    Object.assign(box, { type: "checkbox", name: "Dates", value: input.value, checked: true });
    box.dataset.dow = date.getDay();
    box.dataset.offset = 99;
    const span = document.createElement("span");
    const day = document.createElement("small"), num = document.createElement("strong"), mon = document.createElement("small");
    day.textContent = date.toLocaleDateString("en-GB", { weekday: "short" });
    num.textContent = date.getDate();
    mon.textContent = date.toLocaleDateString("en-GB", { month: "short" });
    span.append(day, num, mon);
    label.append(box, span);
    document.getElementById("day-grid").append(label);
    input.value = "";
    updateSummary();
  });

  applyRun();
}
