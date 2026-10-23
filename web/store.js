// 백엔드 없는 웹 버전의 데이터·규칙 계층. 데이터는 브라우저 localStorage에만 저장된다(기기·브라우저별 독립).
// 데스크톱 앱(AppService)과 같은 권한 규칙을 따른다.

export const LOGIN_FAIL = '아이디 또는 비밀번호가 올바르지 않습니다';
export const VERSION = '1.0.0';

const KEY = 'devmanager.db.v1';
const CATEGORIES = ['차세대 통합 플랫폼', 'AI 업무 자동화', '데이터 허브 구축', '클라우드 전환', '보안 통합 관제'];
const JOB_TITLES = ['사원', '주임', '대리', '과장', '차장', '부장', '이사', '상무', '전무', '대표이사'];
const STATUS_LABEL = { registered: '등록', inProgress: '진행', review: '검토', done: '개발완료', closed: '종료' };

const enc = new TextEncoder();
const b64 = (buf) => btoa(String.fromCharCode(...new Uint8Array(buf)));
const unb64 = (s) => Uint8Array.from(atob(s), (c) => c.charCodeAt(0));

async function derive(password, salt, iterations) {
  const key = await crypto.subtle.importKey('raw', enc.encode(password), 'PBKDF2', false, ['deriveBits']);
  return crypto.subtle.deriveBits({ name: 'PBKDF2', hash: 'SHA-256', salt, iterations }, key, 256);
}

export async function hashPassword(password) {
  const salt = crypto.getRandomValues(new Uint8Array(16));
  return `pbkdf2$150000$${b64(salt)}$${b64(await derive(password, salt, 150000))}`;
}

export async function verifyPassword(password, stored) {
  const [alg, iter, salt, hash] = String(stored).split('$');
  if (alg !== 'pbkdf2') return false;
  const got = b64(await derive(password, unb64(salt), Number(iter)));
  let diff = got.length ^ hash.length;
  for (let i = 0; i < Math.min(got.length, hash.length); i++) diff |= got.charCodeAt(i) ^ hash.charCodeAt(i);
  return diff === 0;
}

export function validatePassword(password, loginId) {
  if (password.length < 10) return '비밀번호는 10자 이상이어야 합니다';
  if (!/[A-Za-z가-힣]/.test(password) || !/\d/.test(password)) return '비밀번호는 영문과 숫자를 모두 포함해야 합니다';
  if (password.toLowerCase() === String(loginId).toLowerCase()) return '비밀번호는 아이디와 같을 수 없습니다';
  return null;
}

export class Store {
  constructor(storage = globalThis.localStorage) {
    this.storage = storage;
    const db = this.#read();
    this.#seedCodes(db);
    this.#write(db);
  }

  #read() {
    const raw = this.storage.getItem(KEY);
    if (raw) return JSON.parse(raw);
    return { seq: {}, users: [], jobTitles: [], categories: [], requirements: [], assignments: [], notifications: [], audit: [], docSeq: {} };
  }

  #write(db) {
    this.storage.setItem(KEY, JSON.stringify(db));
  }

  #id(db, name) {
    db.seq[name] = (db.seq[name] ?? 0) + 1;
    return db.seq[name];
  }

  // 카테고리·직급은 코드값으로 관리한다. 코드가 없는 행은 삭제하지 않고 비활성화한다.
  #seedCodes(db) {
    const upsert = (list, seqName, items, prefix, pad) => {
      items.forEach((name, i) => {
        const code = pad ? `${prefix}${String(i + 1).padStart(2, '0')}` : `${prefix}${String.fromCharCode(65 + i)}`;
        let row = list.find((x) => x.code === code) ?? list.find((x) => x.name === name);
        if (!row) { row = { id: this.#id(db, seqName) }; list.push(row); }
        Object.assign(row, { code, name, sortOrder: i, isActive: true });
      });
      list.filter((x) => !x.code).forEach((x) => { x.isActive = false; });
    };
    upsert(db.categories, 'category', CATEGORIES, 'CAT_', false);
    upsert(db.jobTitles, 'jobTitle', JOB_TITLES, 'JT', true);
  }

  #isAdmin(db, userId) {
    return db.users.some((u) => u.id === userId && u.role === 'admin' && u.isActive);
  }

  #notify(db, userId, type, req, actorId) {
    db.notifications.push({
      id: this.#id(db, 'notification'), userId, type, entity: 'requirement', entityId: req.id,
      title: `${req.docNo} ${req.title}`, createdById: actorId, isRead: false, createdAt: new Date().toISOString()
    });
  }

  hasAdmin() { return this.#read().users.some((u) => u.role === 'admin'); }
  jobTitles() { return this.#read().jobTitles.filter((x) => x.isActive).sort((a, b) => a.sortOrder - b.sortOrder); }
  categories() { return this.#read().categories.filter((x) => x.isActive).sort((a, b) => a.sortOrder - b.sortOrder); }

  activeUsers() {
    const db = this.#read();
    return db.users.filter((u) => u.isActive).sort((a, b) => a.name.localeCompare(b.name, 'ko'))
      .map((u) => ({ id: u.id, name: this.#displayName(db, u) }));
  }

  #displayName(db, u) {
    const jt = db.jobTitles.find((j) => j.id === u.jobTitleId);
    return jt ? `${u.name} ${jt.name}` : u.name;
  }

  async #addUser(db, loginId, name, jobTitleId, password, confirm, role) {
    loginId = String(loginId).trim();
    name = String(name).trim();
    if (!loginId || !name) return '아이디와 이름을 입력하세요';
    if (password !== confirm) return '비밀번호 확인이 일치하지 않습니다';
    const policy = validatePassword(password, loginId);
    if (policy) return policy;
    if (db.users.some((u) => u.loginId === loginId)) return '이미 사용 중인 아이디입니다';
    db.users.push({
      id: this.#id(db, 'user'), loginId, name, jobTitleId: jobTitleId ? Number(jobTitleId) : null,
      passwordHash: await hashPassword(password), role, isActive: true, failedCount: 0, lockedUntil: null, createdAt: new Date().toISOString()
    });
    return null;
  }

  async createAdmin(loginId, name, jobTitleId, password, confirm) {
    const db = this.#read();
    if (db.users.some((u) => u.role === 'admin')) return '이미 관리자 계정이 있습니다';
    const err = await this.#addUser(db, loginId, name, jobTitleId, password, confirm, 'admin');
    if (!err) this.#write(db);
    return err;
  }

  async createUser(actorId, loginId, name, jobTitleId, password) {
    const db = this.#read();
    if (!this.#isAdmin(db, actorId)) return '권한이 없습니다';
    const err = await this.#addUser(db, loginId, name, jobTitleId, password, password, 'member');
    if (!err) this.#write(db);
    return err;
  }

  async login(loginId, password) {
    const db = this.#read();
    const u = db.users.find((x) => x.loginId === String(loginId).trim());
    if (!u || !u.isActive) return { error: LOGIN_FAIL };
    if (u.lockedUntil && new Date(u.lockedUntil) > new Date()) return { error: '로그인 시도가 너무 많습니다. 잠시 후 다시 시도하세요' };
    if (!(await verifyPassword(password, u.passwordHash))) {
      u.failedCount += 1;
      if (u.failedCount >= 5) { u.lockedUntil = new Date(Date.now() + 15 * 60000).toISOString(); u.failedCount = 0; }
      this.#write(db);
      return { error: LOGIN_FAIL };
    }
    u.failedCount = 0;
    u.lockedUntil = null;
    this.#write(db);
    const jt = db.jobTitles.find((j) => j.id === u.jobTitleId);
    return { user: { id: u.id, loginId: u.loginId, name: u.name, role: u.role, jobTitle: jt?.name ?? '' } };
  }

  #validate(i) {
    if (!String(i.title ?? '').trim()) return '개발 제목을 입력하세요';
    if (i.title.length > 200) return '제목은 200자 이내로 입력하세요';
    if (!String(i.description ?? '').trim()) return '내용을 입력하세요';
    if (!i.categoryId) return '카테고리를 선택하세요';
    if (i.devStartDate && i.devEndDate && i.devEndDate < i.devStartDate) return '개발 종료일은 시작일보다 앞설 수 없습니다';
    return null;
  }

  // 제목·카테고리는 마스터만, 개발 시작·종료일은 담당자만, 배포일은 마스터만 반영된다.
  #apply(r, i, { canCore, canDev, canDeploy }) {
    if (canCore) { r.title = i.title.trim(); r.categoryId = Number(i.categoryId); }
    r.description = i.description.trim();
    r.requesterUserId = Number(i.requesterUserId);
    r.dueDate = i.dueDate || null;
    if (canDev) { r.devStartDate = i.devStartDate || null; r.devEndDate = i.devEndDate || null; }
    if (canDeploy) r.deployDate = i.deployDate || null;
  }

  #snapshot(r) {
    const { title, description, categoryId, requesterUserId, dueDate, devStartDate, devEndDate, deployDate, assigneeId } = r;
    return JSON.stringify({ title, description, categoryId, requesterUserId, dueDate, devStartDate, devEndDate, deployDate, assigneeId });
  }

  register(actorId, input) {
    const err = this.#validate(input);
    if (err) return { error: err };
    const db = this.#read();
    if (!db.users.some((u) => u.id === Number(input.requesterUserId) && u.isActive)) return { error: '요청자를 선택하세요' };
    const year = new Date().getFullYear();
    db.docSeq[year] = (db.docSeq[year] ?? 0) + 1;
    const r = {
      id: this.#id(db, 'requirement'), docNo: `DEV-${year}-${String(db.docSeq[year]).padStart(4, '0')}`,
      status: 'registered', priority: 'normal', assigneeId: null, version: 0, createdById: actorId,
      createdAt: new Date().toISOString(), completedModules: null, completedAt: null,
      devStartDate: null, devEndDate: null, deployDate: null
    };
    this.#apply(r, input, { canCore: true, canDev: false, canDeploy: this.#isAdmin(db, actorId) });
    db.requirements.push(r);
    db.audit.push({ entity: 'requirement', entityId: r.id, actorId, action: 'create', after: this.#snapshot(r), at: new Date().toISOString() });
    this.#write(db);
    return { requirement: r };
  }

  update(actorId, id, version, input) {
    const err = this.#validate(input);
    if (err) return err;
    const db = this.#read();
    const r = db.requirements.find((x) => x.id === id);
    if (!r) return '개발 등록건을 찾을 수 없습니다';
    const admin = this.#isAdmin(db, actorId);
    if (!(admin || r.createdById === actorId || r.assigneeId === actorId)) return '수정 권한이 없습니다';
    if (r.version !== version) return '다른 사용자가 먼저 수정했습니다. 새로고침한 뒤 다시 시도하세요';
    if (!db.users.some((u) => u.id === Number(input.requesterUserId) && u.isActive)) return '요청자를 선택하세요';
    const before = this.#snapshot(r);
    this.#apply(r, input, { canCore: admin, canDev: r.assigneeId === actorId, canDeploy: admin });
    r.version += 1;
    db.audit.push({ entity: 'requirement', entityId: r.id, actorId, action: 'update', before, after: this.#snapshot(r), at: new Date().toISOString() });
    if (r.assigneeId && r.assigneeId !== actorId) this.#notify(db, r.assigneeId, 'updated', r, actorId);
    this.#write(db);
    return null;
  }

  complete(actorId, id, version, modules) {
    if (!String(modules ?? '').trim()) return '개발완료 모듈을 입력하세요';
    const db = this.#read();
    const r = db.requirements.find((x) => x.id === id);
    if (!r) return '개발 등록건을 찾을 수 없습니다';
    if (r.assigneeId !== actorId) return '배정받은 담당자만 개발 완료를 기록할 수 있습니다';
    if (r.version !== version) return '다른 사용자가 먼저 수정했습니다. 새로고침한 뒤 다시 시도하세요';
    const before = r.status;
    r.status = 'done';
    r.completedAt = new Date().toISOString();
    r.completedModules = modules.trim();
    r.version += 1;
    db.users.filter((u) => u.role === 'admin' && u.isActive && u.id !== actorId).forEach((u) => this.#notify(db, u.id, 'status', r, actorId));
    db.audit.push({ entity: 'requirement', entityId: r.id, actorId, action: 'complete', before, after: r.completedModules, at: new Date().toISOString() });
    this.#write(db);
    return null;
  }

  // 담당자 배정·변경·해제는 마스터만 가능하다.
  assign(actorId, id, toUserId) {
    const db = this.#read();
    if (!this.#isAdmin(db, actorId)) return '담당자 배정은 마스터만 할 수 있습니다';
    const r = db.requirements.find((x) => x.id === id);
    if (!r) return '개발 등록건을 찾을 수 없습니다';
    const to = toUserId ? Number(toUserId) : null;
    if (r.assigneeId === to) return null;
    const old = r.assigneeId;
    r.assigneeId = to;
    r.version += 1;
    db.assignments.push({ requirementId: r.id, fromUserId: old, toUserId: to, assignedById: actorId, at: new Date().toISOString() });
    if (to) this.#notify(db, to, 'assigned', r, actorId);
    if (old) this.#notify(db, old, 'unassigned', r, actorId);
    db.audit.push({ entity: 'requirement', entityId: r.id, actorId, action: 'assign', before: String(old), after: String(to), at: new Date().toISOString() });
    this.#write(db);
    return null;
  }

  list(userId, all) {
    const db = this.#read();
    const admin = this.#isAdmin(db, userId);
    const unread = new Set(db.notifications.filter((n) => n.userId === userId && !n.isRead && n.entity === 'requirement').map((n) => n.entityId));
    return db.requirements
      .filter((r) => (all && admin) || r.assigneeId === userId)
      .sort((a, b) => b.id - a.id)
      .map((r) => {
        const assignee = db.users.find((u) => u.id === r.assigneeId);
        const acks = db.notifications.filter((n) => n.type === 'assigned' && n.entityId === r.id && n.userId === r.assigneeId);
        const latest = acks.length ? acks.reduce((a, b) => (a.id > b.id ? a : b)) : null;
        const unassigned = !r.assigneeId;
        return {
          id: r.id, docNo: r.docNo, title: r.title, categoryId: r.categoryId, isNew: unread.has(r.id),
          assigneeName: assignee ? this.#displayName(db, assignee) : '',
          isUnassigned: unassigned, isUnconfirmed: !unassigned && !latest?.isRead, isConfirmed: !unassigned && !!latest?.isRead
        };
      });
  }

  detail(userId, id) {
    const db = this.#read();
    const r = db.requirements.find((x) => x.id === id);
    if (!r) return null;
    if (!(this.#isAdmin(db, userId) || r.assigneeId === userId || r.createdById === userId)) return null;
    const user = (uid) => { const u = db.users.find((x) => x.id === uid); return u ? this.#displayName(db, u) : '-'; };
    return {
      ...r,
      categoryName: db.categories.find((c) => c.id === r.categoryId)?.name ?? '',
      requesterName: user(r.requesterUserId),
      assigneeName: r.assigneeId ? user(r.assigneeId) : '미배정',
      statusLabel: STATUS_LABEL[r.status] ?? '종료'
    };
  }

  unreadCount(userId) {
    return this.#read().notifications.filter((n) => n.userId === userId && !n.isRead).length;
  }

  // 나에게 배정됐는데 아직 열어보지 않은 건의 id (오래된 순)
  unconfirmedAssignedIds(userId) {
    const db = this.#read();
    const ids = new Set(db.notifications
      .filter((n) => n.userId === userId && n.type === 'assigned' && !n.isRead).map((n) => n.entityId));
    return db.requirements.filter((r) => r.assigneeId === userId && ids.has(r.id)).map((r) => r.id).sort((x, y) => x - y);
  }

  unconfirmedAssigned(userId) {
    return this.unconfirmedAssignedIds(userId).length;
  }

  markRead(userId, requirementId) {
    const db = this.#read();
    let changed = false;
    db.notifications.forEach((n) => {
      if (n.userId === userId && n.entity === 'requirement' && n.entityId === requirementId && !n.isRead) { n.isRead = true; changed = true; }
    });
    if (changed) this.#write(db);
  }
}
