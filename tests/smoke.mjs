// ============================================================
//  Smoke test terisolasi untuk BRAIN.
//  - Menyalakan MOCK GATEWAY (menangkap semua /send /delete dll).
//  - Menyalakan INSTANCE BRAIN UJI sendiri (port 5099, config & data terpisah,
//    gatewayUrl -> mock). Bot live (5050) & WhatsApp nyata TIDAK tersentuh.
//  - Menjalankan assertion untuk endpoint penting, lalu bersih-bersih.
//
//  Jalankan:  node tests/smoke.mjs       (butuh brain sudah di-build: dotnet build brain)
//  Exit 0 = semua lulus, 1 = ada yang gagal.
// ============================================================
import http from 'node:http';
import { spawn } from 'node:child_process';
import { mkdirSync, writeFileSync, copyFileSync, rmSync, existsSync, readFileSync, cpSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join, resolve } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(__dirname, '..');
const DLL = join(ROOT, 'brain', 'bin', 'Debug', 'net10.0', 'WaBot.dll');
const TESTROOT = join(__dirname, '.smoke');
const BRAIN_PORT = 5099, MOCK_PORT = 4099;
const BRAIN = `http://127.0.0.1:${BRAIN_PORT}`;

if (!existsSync(DLL)) { console.error('FATAL: brain belum di-build:', DLL, '\nJalankan: dotnet build brain'); process.exit(2); }

// ---------- siapkan content-root uji ----------
rmSync(TESTROOT, { recursive: true, force: true });
mkdirSync(join(TESTROOT, 'config'), { recursive: true });
mkdirSync(join(TESTROOT, 'data'), { recursive: true });
mkdirSync(join(TESTROOT, 'logs'), { recursive: true });
copyFileSync(join(ROOT, 'brain', 'config', 'rules.json'), join(TESTROOT, 'config', 'rules.json'));
// Pool puzzle kecil (2 dari pool asli) + aset bidak — agar uji puzzle/reveal merender gambar betulan.
try {
  const pool = JSON.parse(readFileSync(join(ROOT, 'brain', 'data', 'puzzles.json'), 'utf8'));
  writeFileSync(join(TESTROOT, 'data', 'puzzles.json'), JSON.stringify(pool.slice(0, 2)));
} catch { writeFileSync(join(TESTROOT, 'data', 'puzzles.json'), '[]'); }
try { cpSync(join(ROOT, 'brain', 'assets', 'pieces'), join(TESTROOT, 'assets', 'pieces'), { recursive: true }); } catch {}

const testConfig = {
  listenUrl: BRAIN,
  gatewayUrl: `http://127.0.0.1:${MOCK_PORT}`,
  manageAllGroups: false,
  moderationEnabled: true,
  commandsEnabled: true,
  commandPrefix: '!',
  commandCooldownSeconds: 0,
  floodEnabled: true,
  broadcastToken: 'smoke-token',
  adminApiToken: 'smoke-admin-tok',
  adminNumbers: ['628111'],
  rulesText: 'Aturan grup test: dilarang judi & spam.',
  warningMessage: '@user dihapus: {reason} (peringatan ke-{count})',
  welcomeEnabled: true,
  welcomeMessage: 'Selamat datang @user di grup test!',
  groups: {
    'testgrp@g.us': { label: 'Smoke Test Group' },
    'hub@g.us': { label: 'Smoke Hub' },
  },
  relay: { enabled: true, hubGroupJid: 'hub@g.us', throttleSeconds: 0 },
  faq: { enabled: true, requireMention: false, entries: [{ id: 'jadwaltest', pattern: 'jadwaltest', reply: 'Balasan FAQ test' }] },
  // ai wajib ada (diakses tiap pesan); tak akan dipanggil ke Ollama di test ini.
  ai: { enabled: true, url: 'http://127.0.0.1:11434', model: 'dummy', requireMention: true, systemPrompt: 'test', maxOutputChars: 500, numPredict: 16, timeoutSeconds: 5, keepAlive: '1m' },
  // puzzle on-demand aktif; groupJids kosong supaya loop harian tak ikut posting saat test.
  puzzle: { enabled: true, commandEnabled: true, command: 'puzzle', solveCommand: 'solusi', solveAfterMinutes: 0, revealMinutes: 60, dailyHour: 8, groupJids: [] },
  privateChat: { enabled: true, persona: 'test dm', allowedNumbers: ['628111'], consoleGroupJids: [] },
};
writeFileSync(join(TESTROOT, 'config', 'config.json'), JSON.stringify(testConfig, null, 2));

// ---------- mock gateway ----------
const captured = [];
const readBody = (req) => new Promise((r) => { let b = ''; req.on('data', (d) => (b += d)); req.on('end', () => r(b)); });
let failOnceFor = null;      // jid yang harus GAGAL sekali (untuk uji retry queue)
let failAlwaysFor = null;    // jid yang GAGAL terus (untuk uji persistensi antrean)
const failedSeen = new Set();
const mock = http.createServer(async (req, res) => {
  const body = await readBody(req);
  let parsed = null; try { parsed = body ? JSON.parse(body) : null; } catch {}
  captured.push({ url: req.url, body: parsed });
  if (req.url === '/health') { res.writeHead(200); return res.end(JSON.stringify({ ok: true, connected: true })); }
  if (req.url.startsWith('/group-members')) { res.writeHead(200); return res.end(JSON.stringify({ ok: true, members: [] })); }
  // Gagal TERUS untuk jid tertentu (uji item bertahan di disk lintas restart).
  if (req.url === '/send' && parsed?.jid === failAlwaysFor) { res.writeHead(503); return res.end('{"ok":false,"error":"down"}'); }
  // Simulasi gateway reconnect: gagal SEKALI untuk jid tertentu, lalu sukses.
  if (req.url === '/send' && parsed?.jid === failOnceFor && !failedSeen.has(failOnceFor)) {
    failedSeen.add(failOnceFor);
    res.writeHead(503); return res.end('{"ok":false,"error":"reconnecting"}');
  }
  res.writeHead(200, { 'Content-Type': 'application/json' });
  res.end('{"ok":true,"id":"mock-msg-id"}');
});
await new Promise((r) => mock.listen(MOCK_PORT, '127.0.0.1', r));

// ---------- spawn brain uji ----------
let brainLog = '';
function startBrain() {
  const p = spawn('dotnet', [DLL], {
    cwd: TESTROOT,
    env: {
      ...process.env,
      ASPNETCORE_CONTENTROOT: TESTROOT,
      ASPNETCORE_ENVIRONMENT: 'Production',
      DOTNET_NOLOGO: '1',
      // Windows Event Log sering butuh hak admin. Smoke test harus bisa jalan tanpa itu.
      Logging__EventLog__LogLevel__Default: 'None',
    },
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  p.stdout.on('data', (d) => (brainLog += d));
  p.stderr.on('data', (d) => (brainLog += d));
  return p;
}
let brain = startBrain();

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
async function jget(path) { const r = await fetch(BRAIN + path); return { status: r.status, json: await r.json().catch(() => null) }; }
async function jpost(path, body) {
  const r = await fetch(BRAIN + path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
  return { status: r.status, json: await r.json().catch(() => null) };
}

// tunggu brain siap
let up = false;
for (let i = 0; i < 40; i++) { await sleep(500); try { const h = await jget('/health'); if (h.json?.ok) { up = true; break; } } catch {} }
if (!up) { console.error('FATAL: brain uji tidak siap.\n--- log brain ---\n' + brainLog.slice(-2000)); await cleanup(); process.exit(2); }

// ---------- test cases ----------
let pass = 0, fail = 0;
function check(name, cond, detail = '') { if (cond) { pass++; console.log('  PASS  ' + name); } else { fail++; console.log('  FAIL  ' + name + (detail ? '  -> ' + detail : '')); } }
const inc = (o) => jpost('/incoming', { channel: 'whatsapp', mentionedBot: false, key: { id: 'k' + Math.floor(performance.now()) }, ...o });
const sentTo = (jid, sub) => captured.some((c) => c.url === '/send' && c.body?.jid === jid && (!sub || (c.body?.text || '').includes(sub)));
const deletedIn = (jid) => captured.some((c) => c.url === '/delete' && c.body?.jid === jid);

console.log('\n=== SMOKE TEST (brain uji :' + BRAIN_PORT + ' -> mock :' + MOCK_PORT + ') ===');

// 1. health
const h = await jget('/health'); check('health brain ok', h.json?.ok === true, JSON.stringify(h.json));

// 2. unmanaged (grup tak terdaftar)
let r = await inc({ jid: 'random@g.us', participant: '628222@s.whatsapp.net', text: 'halo' });
check('unmanaged group -> unmanaged', r.json?.action === 'unmanaged', r.json?.action);

// 3. clean (grup dikelola, pesan bersih)
r = await inc({ jid: 'testgrp@g.us', participant: '628222@s.whatsapp.net', text: 'halo semua apa kabar' });
check('managed clean -> clean', r.json?.action === 'clean', r.json?.action);

// 4. command !rules
r = await inc({ jid: 'testgrp@g.us', participant: '628222@s.whatsapp.net', text: '!rules' });
check('command !rules -> command', r.json?.action === 'command', r.json?.action);

// 5. FAQ
r = await inc({ jid: 'testgrp@g.us', participant: '628222@s.whatsapp.net', text: 'cek jadwaltest dong' });
check('faq keyword -> faq', r.json?.action === 'faq', r.json?.action);

// 6. moderation (judi) + delete tertangkap
captured.length = 0;
r = await inc({ jid: 'testgrp@g.us', participant: '628222@s.whatsapp.net', text: 'ayo main slot gacor maxwin' });
check('judi -> moderated', r.json?.action === 'moderated', r.json?.action);
await sleep(300);
check('moderation kirim /delete ke grup', deletedIn('testgrp@g.us'));

// 7. relay !sebar dari hub
r = await inc({ jid: 'hub@g.us', participant: '628111@s.whatsapp.net', text: '!sebar' });
check('!sebar dari hub (admin) -> relay-start', r.json?.action === 'relay-start', r.json?.action);

// 8. relay !sebar dari non-admin di hub -> denied
r = await inc({ jid: 'hub@g.us', participant: '628999@s.whatsapp.net', text: '!sebar' });
check('!sebar non-admin -> relay-denied', r.json?.action === 'relay-denied', r.json?.action);

// 9. /broadcast
captured.length = 0;
r = await jpost('/broadcast', { token: 'smoke-token', jid: 'testgrp@g.us', text: 'pengumuman uji' });
await sleep(300);
check('broadcast token benar -> kirim ke grup', sentTo('testgrp@g.us', 'pengumuman uji'), JSON.stringify(r.json));
r = await jpost('/broadcast', { token: 'salah', jid: 'testgrp@g.us', text: 'x' });
check('broadcast token salah -> 401', r.status === 401, 'status ' + r.status);

// 10. /member-joined -> welcome
captured.length = 0;
r = await jpost('/member-joined', { jid: 'testgrp@g.us', groupName: 'Smoke Test Group', participants: ['628333@s.whatsapp.net'] });
await sleep(300);
check('member-joined -> kirim sambutan', sentTo('testgrp@g.us'), JSON.stringify(r.json));

// 11. retry queue: broadcast gagal sekali (503) -> harus dicoba ulang & akhirnya sukses
failOnceFor = 'testgrp@g.us';
captured.length = 0;
await jpost('/broadcast', { token: 'smoke-token', jid: 'testgrp@g.us', text: 'pesan penting retry' });
await sleep(8000); // tunggu loop retry (5 dtk) + backoff
const sendsToGrp = captured.filter((c) => c.url === '/send' && c.body?.jid === 'testgrp@g.us' && (c.body?.text || '').includes('pesan penting retry'));
check('retry queue: kirim ulang setelah gagal (>=2 percobaan)', sendsToGrp.length >= 2, sendsToGrp.length + ' percobaan');

// 12. typing: permintaan AI memicu indikator /typing SEGERA
captured.length = 0;
r = await inc({ jid: 'testgrp@g.us', participant: '628222@s.whatsapp.net', text: '!tanya' });
await sleep(400);
check('AI -> kirim indikator /typing', captured.some((c) => c.url === '/typing' && c.body?.jid === 'testgrp@g.us'), 'action=' + r.json?.action);

// 13. private chat: nomor allowlist dijawab, nomor lain ditolak
captured.length = 0;
r = await inc({ jid: '628111@s.whatsapp.net', participant: '628111@s.whatsapp.net', participantPhone: '628111', text: 'jadwal' });
await sleep(500);
check('DM admin allowlist -> dm-chat', r.json?.action === 'dm-chat', r.json?.action);
check('DM admin allowlist -> kirim balasan', sentTo('628111@s.whatsapp.net'), JSON.stringify(captured));
r = await inc({ jid: '628999@s.whatsapp.net', participant: '628999@s.whatsapp.net', participantPhone: '628999', text: 'jadwal' });
check('DM non-allowlist -> dm-not-allowed', r.json?.action === 'dm-not-allowed', r.json?.action);

// 14. puzzle on-demand -> render & kirim gambar papan
captured.length = 0;
r = await inc({ jid: 'testgrp@g.us', participant: '628222@s.whatsapp.net', text: '!puzzle' });
await sleep(800);
check('!puzzle -> action puzzle', r.json?.action === 'puzzle', r.json?.action);
check('puzzle terkirim sebagai gambar (/send-image)', captured.some((c) => c.url === '/send-image' && c.body?.jid === 'testgrp@g.us'));

// 15. reveal solusi (solveAfterMinutes=0 -> boleh langsung)
captured.length = 0;
r = await inc({ jid: 'testgrp@g.us', participant: '628222@s.whatsapp.net', text: '!solusi' });
await sleep(800);
check('!solusi -> action solusi', r.json?.action === 'solusi', r.json?.action);
check('reveal solusi terkirim (gambar/teks)', captured.some((c) => (c.url === '/send-image' || c.url === '/send') && c.body?.jid === 'testgrp@g.us'));

// 16. admin panel auth (Basic; password = adminApiToken)
let ra = await fetch(BRAIN + '/admin');
check('admin panel tanpa auth -> 401', ra.status === 401, 'status ' + ra.status);
ra = await fetch(BRAIN + '/admin', { headers: { Authorization: 'Basic ' + Buffer.from('admin:smoke-admin-tok').toString('base64') } });
check('admin panel auth benar -> 200', ra.status === 200, 'status ' + ra.status);
ra = await fetch(BRAIN + '/admin', { headers: { Authorization: 'Basic ' + Buffer.from('admin:salah').toString('base64') } });
check('admin panel auth salah -> 401', ra.status === 401, 'status ' + ra.status);

// 17. retry queue PERSISTEN: item gagal-terus tersimpan ke disk & selamat saat brain restart
failAlwaysFor = 'persist@g.us';
await jpost('/broadcast', { token: 'smoke-token', jid: 'persist@g.us', text: 'harus selamat restart' });
await sleep(1500);
const qfile = join(TESTROOT, 'data', 'retry-queue.json');
check('retry queue: tersimpan ke disk', existsSync(qfile) && readFileSync(qfile, 'utf8').includes('persist@g.us'));
brain.kill('SIGKILL'); await sleep(900);     // restart brain uji
brain = startBrain();
let back = false;
for (let i = 0; i < 40; i++) { await sleep(500); try { const hh = await jget('/health'); if (hh.json?.ok) { back = true; break; } } catch {} }
const stt = await jget('/stats');
check('retry queue: termuat lagi setelah restart (>=1)', back && (stt.json?.retryQueue ?? 0) >= 1, 'retryQueue=' + stt.json?.retryQueue);

console.log(`\n=== HASIL: ${pass} lulus, ${fail} gagal ===`);
await cleanup();
process.exit(fail === 0 ? 0 : 1);

async function cleanup() {
  try { brain.kill('SIGKILL'); } catch {}
  try { mock.close(); } catch {}
  await sleep(300);
  try { rmSync(TESTROOT, { recursive: true, force: true }); } catch {}
}

