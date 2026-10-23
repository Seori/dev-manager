import test from 'node:test';
import assert from 'node:assert/strict';
import { Store, LOGIN_FAIL } from './store.js';

class MemoryStorage {
  data = new Map();
  getItem(k) { return this.data.has(k) ? this.data.get(k) : null; }
  setItem(k, v) { this.data.set(k, String(v)); }
}

async function setup() {
  const s = new Store(new MemoryStorage());
  assert.equal(await s.createAdmin('boss', '마스터', 1, 'password123', 'password123'), null);
  assert.equal(await s.createUser(1, 'dev1', '개발자', 3, 'password123'), null);
  const boss = (await s.login('boss', 'password123')).user;
  const dev = (await s.login('dev1', 'password123')).user;
  return { s, boss, dev };
}

const input = (o = {}) => ({ title: '제목', description: '내용', categoryId: 1, requesterUserId: 1, ...o });

test('로그인 실패 메시지는 동일하다', async () => {
  const { s } = await setup();
  assert.equal((await s.login('boss', 'wrong')).error, LOGIN_FAIL);
  assert.equal((await s.login('nobody', 'x')).error, LOGIN_FAIL);
});

test('비밀번호 정책을 검사한다', async () => {
  const s = new Store(new MemoryStorage());
  assert.match(await s.createAdmin('a', 'a', null, 'short1', 'short1'), /10자/);
});

test('문서번호는 연도별 순번으로 채번된다', async () => {
  const { s, boss } = await setup();
  const y = new Date().getFullYear();
  assert.equal(s.register(boss.id, input()).requirement.docNo, `DEV-${y}-0001`);
  assert.equal(s.register(boss.id, input()).requirement.docNo, `DEV-${y}-0002`);
});

test('목록 행에 카테고리가 포함된다', async () => {
  const { s, boss } = await setup();
  s.register(boss.id, input({ categoryId: 2 }));
  assert.equal(s.list(boss.id, true)[0].categoryId, 2);
});

test('필수값과 날짜 검증', async () => {
  const { s, boss } = await setup();
  assert.ok(s.register(boss.id, input({ title: '' })).error);
  assert.ok(s.register(boss.id, input({ requesterUserId: 0 })).error);
  assert.ok(s.register(boss.id, input({ devStartDate: '2026-11-05', devEndDate: '2026-11-01' })).error);
});

test('배정은 마스터만, 담당자에게 알림이 가고 확인 상태가 표시된다', async () => {
  const { s, boss, dev } = await setup();
  const r = s.register(dev.id, input()).requirement;
  assert.ok(s.assign(dev.id, r.id, dev.id));
  assert.equal(s.list(dev.id, false).length, 0);

  assert.equal(s.assign(boss.id, r.id, dev.id), null);
  assert.equal(s.list(dev.id, false).length, 1);
  assert.equal(s.unreadCount(dev.id), 1);
  const row = s.list(boss.id, true)[0];
  assert.ok(row.isUnconfirmed);
  s.markRead(dev.id, r.id);
  assert.ok(s.list(boss.id, true)[0].isConfirmed);
  assert.equal(s.unreadCount(dev.id), 0);
});

test('배정됐지만 확인 안 한 건 수를 센다', async () => {
  const { s, boss, dev } = await setup();
  const a = s.register(boss.id, input()).requirement;
  const b = s.register(boss.id, input()).requirement;
  s.assign(boss.id, a.id, dev.id);
  s.assign(boss.id, b.id, dev.id);
  assert.equal(s.unconfirmedAssigned(dev.id), 2);
  assert.deepEqual(s.unconfirmedAssignedIds(dev.id), [a.id, b.id]);
  assert.equal(s.unconfirmedAssigned(boss.id), 0);
  s.markRead(dev.id, a.id);
  assert.equal(s.unconfirmedAssigned(dev.id), 1);
});

test('일정·제목·카테고리 수정 권한', async () => {
  const { s, boss, dev } = await setup();
  const r = s.register(boss.id, input({ deployDate: '2026-11-05' })).requirement;
  assert.equal(r.deployDate, '2026-11-05');
  s.assign(boss.id, r.id, dev.id);

  const dates = input({ title: '바뀐 제목', categoryId: 2, devStartDate: '2026-11-01', devEndDate: '2026-11-03', deployDate: '2026-12-01' });
  // 마스터: 제목·카테고리·배포일 O, 개발 시작·종료일 X
  assert.equal(s.update(boss.id, r.id, s.detail(boss.id, r.id).version, dates), null);
  let d = s.detail(boss.id, r.id);
  assert.equal(d.title, '바뀐 제목');
  assert.equal(d.categoryId, 2);
  assert.equal(d.deployDate, '2026-12-01');
  assert.equal(d.devStartDate, null);

  // 담당자: 개발 시작·종료일 O, 제목·카테고리·배포일 X
  const mine = input({ title: '또 바뀜', categoryId: 3, devStartDate: '2026-11-01', devEndDate: '2026-11-03', deployDate: '2027-01-01' });
  assert.equal(s.update(dev.id, r.id, d.version, mine), null);
  d = s.detail(dev.id, r.id);
  assert.equal(d.title, '바뀐 제목');
  assert.equal(d.deployDate, '2026-12-01');
  assert.equal(d.devStartDate, '2026-11-01');
  assert.equal(d.devEndDate, '2026-11-03');
});

test('동시 수정은 버전으로 막는다', async () => {
  const { s, boss } = await setup();
  const r = s.register(boss.id, input()).requirement;
  assert.equal(s.update(boss.id, r.id, 0, input({ description: 'a' })), null);
  assert.match(s.update(boss.id, r.id, 0, input({ description: 'b' })), /먼저 수정/);
});

test('개발 완료 모듈은 담당자만 기록하고 마스터에게 알림이 간다', async () => {
  const { s, boss, dev } = await setup();
  const r = s.register(boss.id, input()).requirement;
  s.assign(boss.id, r.id, dev.id);
  assert.ok(s.complete(boss.id, r.id, s.detail(boss.id, r.id).version, '모듈A'));
  assert.ok(s.complete(dev.id, r.id, s.detail(dev.id, r.id).version, '  '));
  assert.equal(s.complete(dev.id, r.id, s.detail(dev.id, r.id).version, '로그인\n알림'), null);
  const d = s.detail(boss.id, r.id);
  assert.equal(d.status, 'done');
  assert.match(d.completedModules, /알림/);
  assert.equal(s.unreadCount(boss.id), 1);
});

test('다른 사람의 건은 조회할 수 없고 일반 사용자는 계정을 만들 수 없다', async () => {
  const { s, boss, dev } = await setup();
  const r = s.register(boss.id, input()).requirement;
  assert.equal(s.detail(dev.id, r.id), null);
  assert.ok(await s.createUser(dev.id, 'x', 'x', null, 'password123'));
});
