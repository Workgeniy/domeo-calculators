let schema = null;
const state = {};

// ---------- сборка формы из описания ----------
function fieldNode(field) {
  const wrap = document.createElement("div");
  wrap.className = "field";

  const hint = field.hint ? " <span class='hint'>— " + field.hint + "</span>" : "";

  if (field.type === "text" || field.type === "textarea") {
    const id = "f-" + field.id;
    const label = document.createElement("label");
    label.setAttribute("for", id);
    label.innerHTML = field.label + hint;
    wrap.appendChild(label);

    const input = document.createElement(field.type === "textarea" ? "textarea" : "input");
    if (field.type === "text") {
      input.type = "text";
      if (field.short) input.className = "short";
    }
    input.id = id;
    input.value = state[field.id] || "";
    input.addEventListener("input", function () {
      state[field.id] = input.value;
      if (field.type === "textarea") grow(input);
    });
    wrap.appendChild(input);
    if (field.type === "textarea") requestAnimationFrame(function () { grow(input); });
    return wrap;
  }

  const legend = document.createElement("span");
  legend.className = "glabel";
  legend.innerHTML = field.label + hint;
  wrap.appendChild(legend);

  const options = document.createElement("div");
  options.className = "options";
  options.setAttribute("role", "group");
  options.setAttribute("aria-label", field.label);

  const multi = field.type === "multi";
  const list = field.other ? field.options.concat(["другой вариант"]) : field.options;

  for (const option of list) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "opt";
    button.setAttribute("aria-pressed", String(isPicked(field, option)));

    const mark = document.createElement("span");
    mark.className = "mark " + (multi ? "box" : "dot");
    mark.textContent = multi ? "✓" : "●";
    button.appendChild(mark);
    button.appendChild(document.createTextNode(option));

    button.addEventListener("click", function () {
      pick(field, option);
      render();
    });

    options.appendChild(button);
  }

  wrap.appendChild(options);

  // «Другой вариант» без поля для текста бесполезен — открываем строку.
  if (field.other && isPicked(field, "другой вариант")) {
    const note = document.createElement("input");
    note.type = "text";
    note.className = "other-note";
    note.placeholder = "Что именно";
    note.value = state[field.id + "Other"] || "";
    note.addEventListener("input", function () { state[field.id + "Other"] = note.value; });
    wrap.appendChild(note);
  }

  return wrap;
}

function isPicked(field, option) {
  const value = state[field.id];
  if (field.type === "multi") return Array.isArray(value) && value.indexOf(option) !== -1;
  return value === option;
}

function pick(field, option) {
  if (field.type !== "multi") {
    state[field.id] = state[field.id] === option ? null : option;   // повторный клик снимает выбор
    return;
  }

  const current = Array.isArray(state[field.id]) ? state[field.id].slice() : [];
  const at = current.indexOf(option);
  if (at === -1) current.push(option);
  else current.splice(at, 1);
  state[field.id] = current;
}

function grow(textarea) {
  textarea.style.height = "auto";
  textarea.style.height = textarea.scrollHeight + 2 + "px";
}

function render() {
  const host = document.getElementById("form");
  const focused = document.activeElement ? document.activeElement.id : null;
  host.innerHTML = "";

  for (const section of schema.sections) {
    const block = document.createElement("section");
    block.className = "section";

    const heading = document.createElement("h2");
    heading.textContent = section.title;
    block.appendChild(heading);

    const fields = document.createElement("div");
    fields.className = "fields";
    for (const field of section.fields) fields.appendChild(fieldNode(field));
    block.appendChild(fields);

    host.appendChild(block);
  }

  if (focused) {
    const restored = document.getElementById(focused);
    if (restored) restored.focus();
  }
}

// ---------- печать: состояние приходит в адресе ----------
function applyPrintState() {
  const raw = new URLSearchParams(location.search).get("print");
  if (!raw) return;

  try {
    const bytes = Uint8Array.from(atob(raw), function (c) { return c.charCodeAt(0); });
    Object.assign(state, JSON.parse(new TextDecoder().decode(bytes)));
    document.documentElement.setAttribute("data-theme", "light");
  } catch (err) {
    /* битое состояние — печатаем пустой бланк */
  }
}

async function downloadPdf() {
  const button = document.getElementById("btn-pdf");
  const label = button.textContent;
  button.disabled = true;
  button.textContent = "Готовлю файл…";

  try {
    const res = await fetch("api/report/pdf", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(state)
    });
    if (!res.ok) throw new Error("HTTP " + res.status);

    const blob = await res.blob();
    const match = (res.headers.get("Content-Disposition") || "").match(/filename=\"?([^\";]+)/);
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = match ? match[1] : "peredacha-v-komplektaciyu.pdf";
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  } catch (err) {
    button.textContent = "Не получилось: " + err.message;
    setTimeout(function () { button.textContent = label; button.disabled = false; }, 4000);
    return;
  }

  button.textContent = label;
  button.disabled = false;
}

async function boot() {
  try {
    const res = await fetch("form.json");
    if (!res.ok) throw new Error("HTTP " + res.status);
    schema = await res.json();
  } catch (err) {
    document.getElementById("form").textContent = "Не удалось загрузить опросник: " + err.message;
    return;
  }

  document.getElementById("lede").textContent = schema.lede;
  document.getElementById("foot").textContent = schema.source;
  document.getElementById("btn-pdf").addEventListener("click", downloadPdf);

  applyPrintState();
  render();
}

boot();
