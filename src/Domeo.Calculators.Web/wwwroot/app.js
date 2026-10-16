// Движок опросника. Одна страница — одно описание в JSON; сам файл общий
// для всех бланков, поэтому пути и имя страницы приходят из data-атрибутов
// тега <script>, а не зашиты здесь.
const config = (document.currentScript || {}).dataset || {};
const FORM_URL = config.form || "form.json";
const API_URL = config.api || "api/report/pdf";
const PAGE_KEY = config.page || "";

let schema = null;
const state = {};

// ---------- условный показ ----------
function visible(field) {
  const rule = field.showIf;
  if (!rule) return true;

  const value = state[rule.field];
  if (rule.has) return Array.isArray(value) && value.indexOf(rule.has) !== -1;
  return value === rule.equals;
}

// ---------- простые поля ----------
function textNode(field, id, value, onInput) {
  const wrap = document.createElement("div");
  wrap.className = "field";

  const label = document.createElement("label");
  label.setAttribute("for", id);
  label.innerHTML = field.label + hintHtml(field);
  wrap.appendChild(label);

  const input = document.createElement(field.type === "textarea" ? "textarea" : "input");
  if (field.type !== "textarea") {
    input.type = "text";
    if (field.short) input.className = "short";
  }
  input.id = id;
  if (field.placeholder) input.placeholder = field.placeholder;
  input.value = value || "";
  input.addEventListener("input", function () {
    onInput(input.value);
    if (field.type === "textarea") grow(input);
  });

  wrap.appendChild(input);
  if (field.type === "textarea") requestAnimationFrame(function () { grow(input); });
  return wrap;
}

function hintHtml(field) {
  return field.hint ? " <span class='hint'>— " + field.hint + "</span>" : "";
}

// ---------- варианты ----------
function optionsNode(field) {
  const wrap = document.createElement("div");
  wrap.className = "field";

  const legend = document.createElement("span");
  legend.className = "glabel";
  legend.innerHTML = field.label + hintHtml(field);
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

  // Варианты вида «Под замену (бренд, коллекция)» — уточнение живёт
  // отдельной строкой и появляется только у выбранного варианта.
  for (const option of list) {
    const placeholder = noteFor(field, option);
    if (!placeholder || !isPicked(field, option)) continue;

    const key = field.id + "Note:" + option;
    const note = document.createElement("input");
    note.type = "text";
    note.className = "other-note";
    note.placeholder = placeholder;
    note.value = state[key] || "";
    note.addEventListener("input", function () { state[key] = note.value; });
    wrap.appendChild(note);
  }

  return wrap;
}

function noteFor(field, option) {
  if (field.notes && field.notes[option]) return field.notes[option];
  if (field.other && option === "другой вариант") return "Что именно";
  return null;
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

// ---------- повторяющийся блок ----------
function repeatNode(field) {
  const wrap = document.createElement("div");
  wrap.className = "repeat";

  const items = Array.isArray(state[field.id]) ? state[field.id] : [{}];
  state[field.id] = items;

  items.forEach(function (item, index) {
    const block = document.createElement("div");
    block.className = "repeat-item";

    const head = document.createElement("div");
    head.className = "repeat-head";

    const title = document.createElement("span");
    title.className = "repeat-title";
    title.textContent = (field.itemLabel || "Позиция") + " " + (index + 1);
    head.appendChild(title);

    if (items.length > 1) {
      const remove = document.createElement("button");
      remove.type = "button";
      remove.className = "link";
      remove.textContent = "Убрать";
      remove.addEventListener("click", function () {
        items.splice(index, 1);
        render();
      });
      head.appendChild(remove);
    }

    block.appendChild(head);

    const fields = document.createElement("div");
    fields.className = "fields";
    for (const sub of field.fields) {
      const id = "f-" + field.id + "-" + index + "-" + sub.id;
      fields.appendChild(textNode(sub, id, item[sub.id], function (value) { item[sub.id] = value; }));
    }
    block.appendChild(fields);

    wrap.appendChild(block);
  });

  const add = document.createElement("button");
  add.type = "button";
  add.className = "link add";
  add.textContent = "+ " + (field.addLabel || "Добавить");
  add.addEventListener("click", function () {
    items.push({});
    render();
  });
  wrap.appendChild(add);

  return wrap;
}

function fieldNode(field) {
  if (field.type === "repeat") return repeatNode(field);
  if (field.type === "text" || field.type === "textarea") {
    return textNode(field, "f-" + field.id, state[field.id], function (value) { state[field.id] = value; });
  }
  return optionsNode(field);
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
    const shown = section.fields.filter(visible);
    if (shown.length === 0) continue;

    const block = document.createElement("section");
    block.className = "section";

    const heading = document.createElement("h2");
    heading.textContent = section.title;
    if (section.hint) {
      const hint = document.createElement("span");
      hint.className = "hint";
      hint.textContent = " — " + section.hint;
      heading.appendChild(hint);
    }
    block.appendChild(heading);

    const fields = document.createElement("div");
    fields.className = "fields";
    for (const field of shown) fields.appendChild(fieldNode(field));
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
    const res = await fetch(API_URL, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ page: PAGE_KEY, answers: state })
    });
    if (!res.ok) throw new Error("HTTP " + res.status);

    const blob = await res.blob();
    const match = (res.headers.get("Content-Disposition") || "").match(/filename=\"?([^\";]+)/);
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = match ? match[1] : "oprosnik.pdf";
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
    const res = await fetch(FORM_URL);
    if (!res.ok) throw new Error("HTTP " + res.status);
    schema = await res.json();
  } catch (err) {
    document.getElementById("form").textContent = "Не удалось загрузить опросник: " + err.message;
    return;
  }

  document.title = schema.title;
  document.getElementById("title").textContent = schema.title;
  document.getElementById("lede").textContent = schema.lede;
  document.getElementById("foot").textContent = schema.source;
  document.getElementById("btn-pdf").addEventListener("click", downloadPdf);

  applyPrintState();
  render();
}

boot();
