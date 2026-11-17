"use strict";
// Optional browser enhancement. The server independently validates seats, prices, and availability.
const bookingForm = document.getElementById("booking-form");
if (bookingForm) {
  const inputs = [...bookingForm.querySelectorAll('input[name="SeatIds"]')];
  const money = new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND", minimumFractionDigits: 0, maximumFractionDigits: 0 });
  const announcement = document.getElementById("seat-announcement");
  function updateSeats() {
    const selected = inputs.filter(input => input.checked && !input.disabled);
    document.getElementById("seat-count").textContent = `${selected.length} selected`;
    document.getElementById("booking-total").textContent = money.format(selected.length * Number(bookingForm.dataset.priceVnd));
    const container = document.getElementById("selected-seats");
    const badges = selected.map(input => { const badge = document.createElement("span"); badge.className = "seat-tag"; badge.textContent = input.dataset.seatLabel; return badge; });
    container.replaceChildren(...badges);
    if (!badges.length) { const text = document.createElement("span"); text.className = "muted"; text.textContent = "None yet"; container.append(text); }
    return selected.length;
  }
  bookingForm.addEventListener("change", event => {
    if (event.target.name !== "SeatIds") return;
    const count = inputs.filter(input => input.checked).length;
    if (count > 8) { event.target.checked = false; announcement.textContent = "You can select up to 8 seats per booking."; }
    else announcement.textContent = count ? `${count} seat${count === 1 ? "" : "s"} selected. Ready when you are.` : "Select seats on the map.";
    updateSeats();
  });
  bookingForm.addEventListener("submit", event => {
    if (!updateSeats()) { event.preventDefault(); announcement.textContent = "Select at least one available seat before confirming."; const first = inputs.find(input => !input.disabled); if (first) first.focus(); }
  });
  updateSeats();
}
document.querySelectorAll("[data-print]").forEach(button => button.addEventListener("click", () => window.print()));
// Live poster preview on the movie form.
const posterInput = document.querySelector("[data-poster-input]");
const posterPreview = document.querySelector("[data-poster-preview]");
if (posterInput && posterPreview) {
  const img = posterPreview.querySelector("img");
  img.addEventListener("load", () => { img.style.display = ""; posterPreview.classList.remove("no-img"); });
  img.addEventListener("error", () => { img.style.display = "none"; posterPreview.classList.add("no-img"); });
  posterInput.addEventListener("input", () => {
    const url = posterInput.value.trim();
    if (/^https?:\/\//i.test(url)) img.src = url; else { img.removeAttribute("src"); img.style.display = "none"; posterPreview.classList.add("no-img"); }
  });
}
// Filter bars apply instantly: dropdowns/dates on change, search boxes after a short pause while typing.
document.querySelectorAll("form.filter-bar").forEach(form => {
  let timer;
  const submit = () => { clearTimeout(timer); form.requestSubmit(); };
  form.querySelectorAll("select, input[type=date]").forEach(field => field.addEventListener("change", submit));
  form.querySelectorAll("input[type=search], input[name=search]").forEach(field => {
    let last = field.value;
    field.addEventListener("input", () => {
      clearTimeout(timer);
      if (field.value.trim() === last.trim()) return;
      timer = setTimeout(() => { last = field.value; sessionStorageSet("refocus", field.name); submit(); }, 450);
    });
  });
});
function sessionStorageSet(key, value) { try { sessionStorage.setItem(key, value); } catch { } }
// After a live-search reload, put the cursor back at the end of the search box so typing can continue.
try {
  const name = sessionStorage.getItem("refocus");
  sessionStorage.removeItem("refocus");
  const field = name && document.querySelector(`form.filter-bar [name="${name}"]`);
  if (field) { field.focus(); field.setSelectionRange(field.value.length, field.value.length); }
} catch { }
// Movie form: the end date can't be before the start date.
const runStart = document.querySelector("[data-run-start]");
const runEnd = document.querySelector("[data-run-end]");
if (runStart && runEnd) {
  const sync = () => { runEnd.min = runStart.value; if (runEnd.value && runEnd.value < runStart.value) runEnd.value = runStart.value; };
  runStart.addEventListener("change", sync);
  sync();
}
