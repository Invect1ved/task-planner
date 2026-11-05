const state = {
  mode: "login",
  filter: "all",
  sort: "due",
  direction: "asc",
  tasks: [],
  editingId: null
};

const authScreen = document.querySelector("#auth-screen");
const appScreen = document.querySelector("#app-screen");
const authForm = document.querySelector("#auth-form");
const authError = document.querySelector("#auth-error");
const taskList = document.querySelector("#task-list");
const notices = document.querySelector("#notices");
const dialog = document.querySelector("#task-dialog");
const taskForm = document.querySelector("#task-form");

function session() {
  return {
    accessToken: sessionStorage.getItem("accessToken") || "",
    refreshToken: sessionStorage.getItem("refreshToken") || "",
    userId: Number(sessionStorage.getItem("userId") || 0),
    email: sessionStorage.getItem("email") || "",
    name: sessionStorage.getItem("name") || ""
  };
}

function saveSession(tokens) {
  sessionStorage.setItem("accessToken", tokens.accessToken);
  sessionStorage.setItem("refreshToken", tokens.refreshToken);
  sessionStorage.setItem("userId", String(tokens.userId));
  sessionStorage.setItem("email", tokens.email);
  sessionStorage.setItem("name", tokens.name);
}

function clearSession() {
  sessionStorage.clear();
}

function decodePayload(token) {
  const part = token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/");
  const padded = part + "=".repeat((4 - part.length % 4) % 4);
  const bytes = Uint8Array.from(atob(padded), char => char.charCodeAt(0));
  return JSON.parse(new TextDecoder().decode(bytes));
}

function verifyToken(token) {
  if (!token || token.split(".").length !== 3) {
    return { ok: false, reason: "Токен имеет неверную форму." };
  }
  try {
    const payload = decodePayload(token);
    if (payload.typ !== "access") {
      return { ok: false, reason: "Ожидался access token." };
    }
    if (!payload.exp || payload.exp * 1000 <= Date.now()) {
      return { ok: false, reason: "Срок access token истёк." };
    }
    return { ok: true, payload };
  } catch {
    return { ok: false, reason: "Токен не удалось прочитать." };
  }
}

async function ensureAccess() {
  const current = session();
  if (verifyToken(current.accessToken).ok) {
    return current.accessToken;
  }
  if (!current.refreshToken || !current.userId) {
    throw new Error("Сессия не найдена. Войдите снова.");
  }
  const response = await fetch("/api/auth/refresh", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ userId: current.userId, refreshToken: current.refreshToken })
  });
  if (!response.ok) {
    clearSession();
    throw new Error("Сессия истекла. Войдите снова.");
  }
  const tokens = await response.json();
  saveSession(tokens);
  const check = verifyToken(tokens.accessToken);
  if (!check.ok) {
    throw new Error(check.reason);
  }
  return tokens.accessToken;
}

async function api(path, options = {}) {
  const token = await ensureAccess();
  const headers = Object.assign({ Authorization: "Bearer " + token }, options.headers || {});
  if (options.body && !headers["Content-Type"]) {
    headers["Content-Type"] = "application/json";
  }
  const response = await fetch(path, Object.assign({}, options, { headers }));
  if (response.status === 401) {
    clearSession();
    showAuth();
    throw new Error("Сервер отклонил токен.");
  }
  return response;
}

function showAuth() {
  authScreen.hidden = false;
  appScreen.hidden = true;
}

function showApp() {
  authScreen.hidden = true;
  appScreen.hidden = false;
  document.querySelector("#user-label").textContent = session().name + " · " + session().email;
}

function formatDate(value) {
  const date = new Date(value);
  return date.toLocaleString("ru-RU", { day: "2-digit", month: "short", hour: "2-digit", minute: "2-digit" });
}

function dueClass(task) {
  if (task.isCompleted) return "done";
  const due = new Date(task.dueAt).getTime();
  const now = Date.now();
  if (due < now) return "late";
  if (due - now < 2 * 60 * 60 * 1000) return "soon";
  return "";
}

function renderTasks() {
  if (state.tasks.length === 0) {
    taskList.innerHTML = "<p class='empty'>В этом списке пока пусто.</p>";
    return;
  }
  taskList.innerHTML = state.tasks.map(task => `
    <article class="task ${dueClass(task)}">
      <input type="checkbox" data-complete="${task.id}" ${task.isCompleted ? "checked" : ""} />
      <div>
        <div class="title">${escapeHtml(task.title)}</div>
        <div class="meta">
          ${task.isOwner ? "" : `<span class="badge">от ${escapeHtml(task.ownerName)}</span>`}
          ${task.sharedWith.length ? `<span class="badge">доступ: ${escapeHtml(task.sharedWith.join(", "))}</span>` : ""}
          <div>${task.description ? escapeHtml(task.description) : "Без описания"}</div>
          <div>Создана ${formatDate(task.createdAt)} · срок ${formatDate(task.dueAt)}</div>
        </div>
        ${task.isOwner ? `<div class="share-row"><input data-share-email="${task.id}" type="email" placeholder="почта коллеги" /><button type="button" data-share="${task.id}">Открыть доступ</button></div>` : ""}
      </div>
      <div class="row-actions">
        ${task.isOwner ? `<button type="button" data-edit="${task.id}">Изменить</button><button type="button" data-delete="${task.id}">Удалить</button>` : ""}
      </div>
    </article>
  `).join("");
}

function escapeHtml(value) {
  return String(value).replace(/[&<>"']/g, char => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[char]));
}

async function loadTasks() {
  const params = new URLSearchParams({ sort: state.sort, direction: state.direction, filter: state.filter });
  const response = await api("/api/tasks?" + params.toString());
  state.tasks = await response.json();
  renderTasks();
  await loadNotices();
}

async function loadNotices() {
  const response = await api("/api/tasks/upcoming");
  const upcoming = await response.json();
  notices.innerHTML = upcoming.map(task => `<div class="notice">Срок близко: «${escapeHtml(task.title)}» — ${formatDate(task.dueAt)}</div>`).join("");
  if (Notification.permission === "granted") {
    const seen = JSON.parse(sessionStorage.getItem("seenNotices") || "[]");
    for (const task of upcoming) {
      if (!seen.includes(task.id)) {
        new Notification("Планировщик", { body: task.title + " — " + formatDate(task.dueAt) });
        seen.push(task.id);
      }
    }
    sessionStorage.setItem("seenNotices", JSON.stringify(seen));
  }
}

document.querySelector("#tab-login").onclick = () => setMode("login");
document.querySelector("#tab-register").onclick = () => setMode("register");

function setMode(mode) {
  state.mode = mode;
  document.querySelector("#tab-login").classList.toggle("active", mode === "login");
  document.querySelector("#tab-register").classList.toggle("active", mode === "register");
  document.querySelector("#name-field").hidden = mode === "login";
  authForm.querySelector("button[type=submit]").textContent = mode === "login" ? "Войти" : "Создать аккаунт";
}

authForm.onsubmit = async event => {
  event.preventDefault();
  authError.hidden = true;
  const data = Object.fromEntries(new FormData(authForm));
  if (state.mode === "register" && !data.displayName.trim()) {
    authError.hidden = false;
    authError.textContent = "Укажите имя.";
    return;
  }
  const path = state.mode === "login" ? "/api/auth/makeAuthRequest" : "/api/auth/register";
  const response = await fetch(path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data)
  });
  const body = await response.json();
  if (!response.ok) {
    authError.hidden = false;
    authError.textContent = body.error || "Не удалось войти.";
    return;
  }
  const check = verifyToken(body.accessToken);
  if (!check.ok) {
    authError.hidden = false;
    authError.textContent = check.reason;
    return;
  }
  saveSession(body);
  showApp();
  await loadTasks();
};

document.querySelector("#filters").onclick = async event => {
  const button = event.target.closest("button[data-filter]");
  if (!button) return;
  state.filter = button.dataset.filter;
  document.querySelectorAll("#filters button").forEach(item => item.classList.toggle("active", item === button));
  await loadTasks();
};

document.querySelector("#sort").onchange = async event => { state.sort = event.target.value; await loadTasks(); };
document.querySelector("#direction").onchange = async event => { state.direction = event.target.value; await loadTasks(); };

document.querySelector("#new-task").onclick = () => openDialog(null);
document.querySelector("#cancel-dialog").onclick = () => dialog.close();

function openDialog(task) {
  state.editingId = task ? task.id : null;
  document.querySelector("#dialog-title").textContent = task ? "Изменить задачу" : "Новая задача";
  taskForm.title.value = task ? task.title : "";
  taskForm.description.value = task ? (task.description || "") : "";
  taskForm.dueAt.value = task ? toLocalInput(task.dueAt) : toLocalInput(new Date(Date.now() + 60 * 60 * 1000).toISOString());
  taskForm.isCompleted.checked = task ? task.isCompleted : false;
  document.querySelector("#form-error").hidden = true;
  dialog.showModal();
}

function toLocalInput(value) {
  const date = new Date(value);
  const pad = number => String(number).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

taskForm.onsubmit = async event => {
  event.preventDefault();
  const payload = {
    title: taskForm.title.value,
    description: taskForm.description.value,
    dueAt: taskForm.dueAt.value,
    isCompleted: taskForm.isCompleted.checked
  };
  const path = state.editingId ? "/api/tasks/" + state.editingId : "/api/tasks";
  const response = await api(path, { method: state.editingId ? "PUT" : "POST", body: JSON.stringify(payload) });
  if (!response.ok) {
    const body = await response.json();
    const error = document.querySelector("#form-error");
    error.hidden = false;
    error.textContent = body.error || "Не удалось сохранить.";
    return;
  }
  dialog.close();
  await loadTasks();
};

taskList.onclick = async event => {
  const complete = event.target.dataset.complete;
  const edit = event.target.dataset.edit;
  const remove = event.target.dataset.delete;
  const share = event.target.dataset.share;
  if (complete) {
    await api("/api/tasks/" + complete + "/complete", { method: "POST" });
    await loadTasks();
  }
  if (edit) {
    openDialog(state.tasks.find(task => task.id === Number(edit)));
  }
  if (remove && confirm("Удалить задачу?")) {
    await api("/api/tasks/" + remove, { method: "DELETE" });
    await loadTasks();
  }
  if (share) {
    const email = taskList.querySelector(`[data-share-email="${share}"]`).value;
    const response = await api("/api/tasks/" + share + "/share", { method: "POST", body: JSON.stringify({ email }) });
    if (!response.ok) {
      const body = await response.json();
      alert(body.error || "Не удалось открыть доступ.");
      return;
    }
    await loadTasks();
  }
};

document.querySelector("#logout-btn").onclick = async () => {
  try { await api("/api/auth/logout", { method: "POST" }); } catch { /* сессия уже закрыта */ }
  clearSession();
  showAuth();
};

document.querySelector("#notify-btn").onclick = async () => {
  const permission = await Notification.requestPermission();
  if (permission === "granted") await loadNotices();
};

setInterval(() => {
  if (!appScreen.hidden) loadNotices().catch(() => {});
}, 30000);

if (verifyToken(session().accessToken).ok || session().refreshToken) {
  showApp();
  loadTasks().catch(() => showAuth());
}
