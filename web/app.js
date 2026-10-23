import { Store, VERSION } from './store.js';

const store = new Store();
const root = document.getElementById('app');
const SAVED_ID_KEY = 'devmanager.savedLoginId';

let me = null;
let selectedId = null;
let pollTimer = null;

const $ = (sel, el = document) => el.querySelector(sel);
const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const opts = (list, selected, placeholder) =>
  (placeholder ? `<option value="">${esc(placeholder)}</option>` : '') +
  list.map((o) => `<option value="${o.id}" ${Number(selected) === o.id ? 'selected' : ''}>${esc(o.name)}</option>`).join('');
const val = (id) => $(`#${id}`)?.value ?? '';
const ICON_X = '<svg width="10" height="10" viewBox="0 0 10 10" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" aria-hidden="true"><path d="M0 0L10 10M10 0L0 10"/></svg>';

function readSavedId() {
  try { return localStorage.getItem(SAVED_ID_KEY) ?? ''; } catch { return ''; }
}

function writeSavedId(id) {
  try { id ? localStorage.setItem(SAVED_ID_KEY, id) : localStorage.removeItem(SAVED_ID_KEY); } catch { /* 저장소를 못 써도 로그인은 가능해야 한다 */ }
}

// ───────── 초기 설정 / 로그인 ─────────
function renderSetup() {
  root.innerHTML = `
  <div class="center"><form class="login" id="setup">
    <img class="brand-icon" src="favicon.png" alt=""><h1>초기 설정</h1><div class="hint">마스터(관리자) 계정을 만듭니다</div>
    <input type="text" id="s-name" placeholder="이름" autocomplete="off">
    <select id="s-job">${opts(store.jobTitles(), null, '직급 선택')}</select>
    <input type="text" id="s-id" placeholder="아이디" autocomplete="off">
    <input type="password" id="s-pw" placeholder="비밀번호 (10자 이상, 영문+숫자)" autocomplete="new-password">
    <input type="password" id="s-pw2" placeholder="비밀번호 확인" autocomplete="new-password">
    <div class="error" id="s-err"></div>
    <button class="btn primary block" type="submit">계정 만들기</button>
  </form></div>`;
  $('#s-name').focus();
  $('#setup').addEventListener('submit', async (e) => {
    e.preventDefault();
    const err = await store.createAdmin(val('s-id'), val('s-name'), val('s-job'), val('s-pw'), val('s-pw2'));
    if (err) { $('#s-err').textContent = err; return; }
    renderLogin(val('s-id'));
  });
}

function renderLogin(prefillId) {
  const saved = prefillId ?? readSavedId();
  root.innerHTML = `
  <div class="center"><form class="login" id="login">
    <img class="brand-icon" src="favicon.png" alt=""><h1>개발관리</h1><div class="hint">로그인</div>
    <input type="text" id="l-id" placeholder="아이디" autocomplete="username" value="${esc(saved)}">
    <input type="password" id="l-pw" placeholder="비밀번호" autocomplete="current-password">
    <label class="check"><input type="checkbox" id="l-save" ${readSavedId() ? 'checked' : ''}> 아이디 저장</label>
    <div class="error" id="l-err"></div>
    <button class="btn primary block" type="submit">로그인</button>
  </form></div>`;
  (saved ? $('#l-pw') : $('#l-id')).focus();
  $('#login').addEventListener('submit', async (e) => {
    e.preventDefault();
    const { user, error } = await store.login(val('l-id'), val('l-pw'));
    if (error) { $('#l-err').textContent = error; return; }
    writeSavedId($('#l-save').checked ? user.loginId : '');
    me = user;
    renderMain();
  });
}

// ───────── 메인(메신저) ─────────
function renderMain() {
  const admin = me.role === 'admin';
  root.innerHTML = `
  <div class="app">
    <header class="topbar">
      <div class="brand"><img src="favicon.png" alt=""><span>개발관리</span></div>
      <div class="topbar-right">
        <span class="badge" id="badge" hidden></span>
        <div class="me">
          <div class="avatar">${esc(me.name.slice(0, 1))}</div>
          <div class="who"><div class="name">${esc(me.name)}</div><div class="rank">${esc(me.jobTitle)}</div></div>
          <button class="pill" id="unconfirmed" type="button" hidden></button>
        </div>
        <button class="btn small" id="logout">로그아웃</button>
      </div>
    </header>
    <main class="workspace" id="workspace">
      <aside class="listpane">
        <div class="listtools">
          <div class="listtitle" id="listHeader"></div>
          <div class="actions">
            <button class="btn primary small" id="newReq">+ 개발 등록</button>
            ${admin ? '<button class="btn small" id="newUser">계정 추가</button>' : ''}
            <button class="btn small" id="refresh">새로고침</button>
          </div>
          <select id="catFilter" aria-label="카테고리">${opts(store.categories(), null, '전체 카테고리')}</select>
          <input type="text" id="q" placeholder="문서번호·제목·담당자 검색" autocomplete="off">
        </div>
        <div class="list" id="list"></div>
      </aside>
      <section class="detailpane" id="panel"></section>
    </main>
    <footer class="foot">DB 연결됨 (버전 : ${VERSION})</footer>
  </div>`;
  $('#logout').onclick = logout;
  $('#unconfirmed').onclick = () => {
    const [first] = store.unconfirmedAssignedIds(me.id);
    if (first) openDetail(first);
  };
  $('#newReq').onclick = () => openForm(null);
  $('#refresh').onclick = refreshList;
  $('#q').addEventListener('input', refreshList);
  $('#catFilter').addEventListener('change', refreshList);
  if (admin) $('#newUser').onclick = openUserForm;
  $('#list').addEventListener('click', (e) => {
    const row = e.target.closest('[data-id]');
    if (row) openDetail(Number(row.dataset.id));
  });
  renderEmptyPanel();
  refreshList();
  clearInterval(pollTimer);
  pollTimer = setInterval(refreshList, 10000);
}

function logout() {
  clearInterval(pollTimer);
  me = null;
  selectedId = null;
  renderLogin();
}

function refreshList() {
  if (!me || !$('#list')) return;
  const admin = me.role === 'admin';
  const all = store.list(me.id, admin);
  const q = ($('#q')?.value ?? '').trim().toLowerCase();
  const cat = Number($('#catFilter')?.value) || 0;
  const rows = all.filter((r) => (!cat || r.categoryId === cat)
    && (!q || `${r.docNo} ${r.title} ${r.assigneeName}`.toLowerCase().includes(q)));
  $('#listHeader').textContent = admin ? `전체 개발건 ${all.length}건` : `내게 배정된 개발건 ${all.length}건`;
  $('#list').innerHTML = rows.length
    ? rows.map((r) => `
      <button class="row ${r.id === selectedId ? 'selected' : ''}" data-id="${r.id}">
        <span class="no">${esc(r.docNo)}${r.isNew ? '<span class="new">NEW</span>' : ''}</span>
        <span class="title">${esc(r.title)}</span>
        <span class="meta">
          ${r.assigneeName ? `<span>${esc(r.assigneeName)}</span>` : ''}
          ${r.isUnassigned ? '<span>미배정</span>' : ''}
          ${r.isUnconfirmed ? '<span class="unconfirmed">· 미확인</span>' : ''}
          ${r.isConfirmed ? '<span class="confirmed">· 확인</span>' : ''}
        </span>
      </button>`).join('')
    : `<div class="empty">${q || cat ? '조건에 맞는 개발건이 없습니다' : '표시할 개발건이 없습니다'}</div>`;
  const pending = store.unconfirmedAssigned(me.id);
  const pill = $('#unconfirmed');
  pill.hidden = pending === 0;
  pill.textContent = `미확인 배정 ${pending}`;
  pill.title = '클릭하면 확인하지 않은 배정 건을 엽니다';
  const unread = store.unreadCount(me.id);
  const badge = $('#badge');
  badge.hidden = unread === 0;
  badge.textContent = `알림 ${unread}`;
}

// ───────── 오른쪽 패널(상세·등록·계정 추가·개발 완료) ─────────
function renderEmptyPanel() {
  $('#panel').innerHTML = '<div class="placeholder"><img src="favicon.png" alt=""><div>목록에서 개발건을 선택하세요</div></div>';
  $('#workspace').classList.remove('has-panel');
}

function showPanel(html) {
  const p = $('#panel');
  p.innerHTML = `<div class="panel-inner"><button class="close" id="pclose" aria-label="닫기">${ICON_X}</button><div class="panel-body">${html}</div></div>`;
  $('#workspace').classList.add('has-panel');
  $('#pclose').onclick = closePanel;
  return p;
}

function closePanel() {
  selectedId = null;
  if ($('#panel')) renderEmptyPanel();
  refreshList();
}

function openDetail(id) {
  const d = store.detail(me.id, id);
  if (!d) { closePanel(); return; }
  selectedId = id;
  store.markRead(me.id, id);
  refreshList();
  const admin = me.role === 'admin';
  const isAssignee = d.assigneeId === me.id;
  const canEdit = admin || isAssignee || d.createdById === me.id;
  const canComplete = isAssignee && d.status !== 'done';
  const date = (v) => v || '-';
  const p = showPanel(`
    <div class="docno">${esc(d.docNo)}</div>
    <h1 class="ptitle">${esc(d.title)}</h1>
    <div class="grid2">
      <div class="field"><label>카테고리</label>${esc(d.categoryName)}</div>
      <div class="field"><label>요청자</label>${esc(d.requesterName)}</div>
      <div class="field"><label>완료 요청일</label>${esc(date(d.dueDate))}</div>
      <div class="field"><label>배포일</label>${esc(date(d.deployDate))}</div>
      <div class="field"><label>개발 시작일</label>${esc(date(d.devStartDate))}</div>
      <div class="field"><label>개발 종료일</label>${esc(date(d.devEndDate))}</div>
      <div class="field"><label>담당자</label>${esc(d.assigneeName)}</div>
      <div class="field"><label>상태</label>${esc(d.statusLabel)}</div>
    </div>
    <div class="field"><label>내용</label><div class="text">${esc(d.description)}</div></div>
    ${d.completedModules ? `<div class="field"><label>개발완료 모듈</label><div class="text">${esc(d.completedModules)}</div>
      <div class="lbl" style="margin-top:4px">완료 ${esc(new Date(d.completedAt).toLocaleString('ko-KR'))}</div></div>` : ''}
    ${admin ? `<div class="field"><label>담당자 배정 (마스터 전용)</label><div class="inline">
      <select id="assignee">${opts([{ id: 0, name: '미배정' }, ...store.activeUsers()], d.assigneeId ?? 0)}</select>
      <button class="btn small" id="assign">배정</button></div></div>` : ''}
    <div class="ok" id="msg"></div>
    <div class="inline">
      ${canEdit ? '<button class="btn primary small" id="edit">수정</button>' : ''}
      ${canComplete ? '<button class="btn small" id="complete">개발 완료</button>' : ''}
    </div>`);
  if (admin) {
    $('#assign', p).onclick = () => {
      const err = store.assign(me.id, id, Number($('#assignee', p).value) || null);
      if (err) { $('#msg', p).style.color = 'var(--danger)'; $('#msg', p).textContent = err; return; }
      openDetail(id);
      $('#msg').textContent = '배정했습니다';
    };
  }
  if (canEdit) $('#edit', p).onclick = () => openForm(id);
  if (canComplete) $('#complete', p).onclick = () => openComplete(id, d.version);
}

function openForm(id) {
  const admin = me.role === 'admin';
  const d = id ? store.detail(me.id, id) : null;
  const editing = !!d;
  const users = store.activeUsers();
  const p = showPanel(`
    <h2>${editing ? `${esc(d.docNo)} 수정` : '개발 등록 (문서번호는 저장 시 자동 부여)'}</h2>
    <input type="text" id="f-title" placeholder="개발 제목 *" value="${esc(d?.title)}" ${editing && !admin ? 'disabled' : ''}>
    <select id="f-cat" ${editing && !admin ? 'disabled' : ''}>${opts(store.categories(), d?.categoryId, '카테고리 선택 *')}</select>
    <textarea id="f-desc" placeholder="내용 *">${esc(d?.description)}</textarea>
    <select id="f-req">${opts(users, d?.requesterUserId ?? me.id, '요청자 선택 *')}</select>
    <div class="grid2">
      <div class="field"><label>완료 요청일</label><input type="date" id="f-due" value="${esc(d?.dueDate)}"></div>
      <div class="field"><label>배포일 (마스터)</label><input type="date" id="f-deploy" value="${esc(d?.deployDate)}" ${admin ? '' : 'disabled'}></div>
      <div class="field"><label>개발 시작일 (담당자)</label><input type="date" id="f-start" value="${esc(d?.devStartDate)}" ${editing && d.assigneeId === me.id ? '' : 'disabled'}></div>
      <div class="field"><label>개발 종료일 (담당자)</label><input type="date" id="f-end" value="${esc(d?.devEndDate)}" ${editing && d.assigneeId === me.id ? '' : 'disabled'}></div>
    </div>
    <div class="error" id="f-err"></div>
    <div class="inline"><button class="btn primary small" id="save">저장</button><button class="btn small" id="cancel">취소</button></div>`);
  selectedId = id ?? null;
  $('#cancel', p).onclick = () => (editing ? openDetail(id) : closePanel());
  $('#save', p).onclick = () => {
    const input = {
      title: val('f-title'), description: val('f-desc'), categoryId: Number(val('f-cat')) || 0,
      requesterUserId: Number(val('f-req')) || 0, dueDate: val('f-due'),
      devStartDate: val('f-start'), devEndDate: val('f-end'), deployDate: val('f-deploy')
    };
    if (editing) {
      const err = store.update(me.id, id, d.version, input);
      if (err) { $('#f-err', p).textContent = err; return; }
      openDetail(id);
    } else {
      const { error } = store.register(me.id, input);
      if (error) { $('#f-err', p).textContent = error; return; }
      closePanel(); // 신규 등록은 저장 후 창을 닫는다.
    }
  };
  $('#f-title', p).focus();
}

function openComplete(id, version) {
  const p = showPanel(`
    <h2>개발 완료</h2>
    <div class="lbl">개발완료 모듈 *</div>
    <textarea id="c-modules" placeholder="완료한 모듈을 한 줄에 하나씩 입력" style="min-height:160px"></textarea>
    <div class="error" id="c-err"></div>
    <div class="inline"><button class="btn primary small" id="c-save">완료 처리</button><button class="btn small" id="c-cancel">취소</button></div>`);
  $('#c-cancel', p).onclick = () => openDetail(id);
  $('#c-save', p).onclick = () => {
    const err = store.complete(me.id, id, version, val('c-modules'));
    if (err) { $('#c-err', p).textContent = err; return; }
    openDetail(id);
  };
  $('#c-modules', p).focus();
}

function openUserForm() {
  const p = showPanel(`
    <h2>계정 추가</h2>
    <input type="text" id="u-name" placeholder="이름" autocomplete="off">
    <select id="u-job">${opts(store.jobTitles(), null, '직급 선택')}</select>
    <input type="text" id="u-id" placeholder="아이디" autocomplete="off">
    <input type="password" id="u-pw" placeholder="비밀번호 (10자 이상, 영문+숫자)" autocomplete="new-password">
    <div class="error" id="u-msg"></div>
    <div class="inline"><button class="btn primary small" id="u-save">만들기</button><button class="btn small" id="u-close">닫기</button></div>`);
  $('#u-close', p).onclick = closePanel;
  $('#u-save', p).onclick = async () => {
    const err = await store.createUser(me.id, val('u-id'), val('u-name'), val('u-job'), val('u-pw'));
    const msg = $('#u-msg', p);
    msg.className = err ? 'error' : 'ok';
    msg.textContent = err ?? '계정을 만들었습니다';
    if (!err) ['u-name', 'u-id', 'u-pw'].forEach((x) => { $(`#${x}`, p).value = ''; });
  };
  $('#u-name', p).focus();
}

// 다른 탭에서 데이터가 바뀌면 목록을 갱신한다.
window.addEventListener('storage', () => refreshList());

if (store.hasAdmin()) renderLogin(); else renderSetup();
