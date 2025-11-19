const apiBase = '';

function setMsg(el, txt, isError = false) {
  el.innerText = txt;
  el.className = isError ? 'text-danger' : 'text-success';
}

function getToken() {
  return localStorage.getItem('ds_token');
}

function setUser(email) {
  const nav = document.getElementById('nav-user');
  if (email) nav.innerText = email; else nav.innerText = 'Not signed';
}

async function register() {
  const email = document.getElementById('reg-email').value;
  const password = document.getElementById('reg-password').value;
  const msg = document.getElementById('auth-msg');
  try {
    const res = await fetch(apiBase + '/api/auth/register', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify({ email, password }) });
    const data = await res.json();
    if (!res.ok) throw new Error(data?.Error || JSON.stringify(data));
    localStorage.setItem('ds_token', data.token);
    setUser(data.email);
    setMsg(msg, 'Registered and logged in as ' + data.email);
  } catch (e) { setMsg(msg, e.toString(), true); }
}

async function login() {
  const email = document.getElementById('login-email').value;
  const password = document.getElementById('login-password').value;
  const msg = document.getElementById('auth-msg');
  try {
    const res = await fetch(apiBase + '/api/auth/login', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify({ email, password }) });
    const data = await res.json();
    if (!res.ok) throw new Error(data?.Error || JSON.stringify(data));
    localStorage.setItem('ds_token', data.token);
    setUser(data.email);
    setMsg(msg, 'Logged in as ' + data.email);
  } catch (e) { setMsg(msg, e.toString(), true); }
}

function logout() {
  localStorage.removeItem('ds_token');
  setUser(null);
  setMsg(document.getElementById('auth-msg'), 'Logged out');
}

async function submitTask() {
  const token = getToken();
  const out = document.getElementById('submit-result');
  if (!token) { setMsg(out, 'Login required', true); return; }
  const size = parseInt(document.getElementById('matrix-size').value || '0');
  let matrixA, vectorB;
  try { matrixA = JSON.parse(document.getElementById('matrix-a').value); vectorB = JSON.parse(document.getElementById('vector-b').value); } catch (e) { setMsg(out, 'Invalid JSON', true); return; }
  try {
    const res = await fetch(apiBase + '/api/task/submit', { method: 'POST', headers: {'Content-Type':'application/json', 'Authorization':'Bearer ' + token}, body: JSON.stringify({ MatrixSize: size, MatrixA: matrixA, VectorB: vectorB }) });
    const data = await res.json();
    if (!res.ok) throw new Error(data?.Error || JSON.stringify(data));
    out.innerHTML = `<div class="alert alert-success">Task submitted: <strong>${data.taskId}</strong></div>`;
    document.getElementById('status-id').value = data.taskId;
  } catch (e) { out.innerHTML = `<div class="alert alert-danger">${e}</div>`; }
}

async function getStatus() {
  const token = getToken();
  const id = document.getElementById('status-id').value.trim();
  const out = document.getElementById('status-result');
  if (!id) { out.innerText = 'Provide TaskId'; return; }
  try {
    const res = await fetch(apiBase + '/api/task/' + id, { headers: { 'Authorization': 'Bearer ' + token } });
    const data = await res.json();
    if (!res.ok) throw new Error(JSON.stringify(data));
    out.innerText = JSON.stringify(data, null, 2);
  } catch (e) { out.innerText = e.toString(); }
}

async function loadHistory() {
  const token = getToken();
  const out = document.getElementById('history-list');
  try {
    const res = await fetch(apiBase + '/api/task/history', { headers: { 'Authorization': 'Bearer ' + token } });
    const data = await res.json();
    if (!res.ok) throw new Error(JSON.stringify(data));
    if (!data || data.length === 0) { out.innerHTML = '<div class="text-muted">No tasks</div>'; return; }
    out.innerHTML = data.map(t => `<div class="card mb-2"><div class="card-body"><b>${t.id}</b> — ${t.status} — ${t.progressPercent}% — ${t.matrixSize} — <small>${t.timeCreated}</small></div></div>`).join('');
  } catch (e) { out.innerHTML = `<div class="text-danger">${e}</div>`; }
}

document.addEventListener('DOMContentLoaded', () => {
  document.getElementById('btn-register').onclick = register;
  document.getElementById('btn-login').onclick = login;
  document.getElementById('btn-logout').onclick = logout;
  document.getElementById('btn-submit').onclick = submitTask;
  document.getElementById('btn-status').onclick = getStatus;
  document.getElementById('btn-history').onclick = loadHistory;
  const token = getToken();
  setUser(token ? 'Signed' : null);
});
