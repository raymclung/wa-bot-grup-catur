'use strict';

/*
 * WhatsApp Gateway (Baileys) \u2014 bagian "tipis" dari arsitektur hybrid.
 *
 * Tanggung jawab:
 *   1. Menjaga koneksi WhatsApp (scan QR, auto-reconnect).
 *   2. Meneruskan SETIAP pesan grup masuk ke "brain" C# (webhook POST).
 *   3. Menyediakan perintah HTTP untuk brain: POST /send dan POST /delete.
 *
 * TIDAK ada logika moderasi di sini \u2014 semua keputusan ada di brain (C#).
 */

const path = require('path');
const fs = require('fs');
const express = require('express');
const QRCode = require('qrcode');
const {
  default: makeWASocket,
  useMultiFileAuthState,
  fetchLatestBaileysVersion,
  DisconnectReason,
  downloadMediaMessage,
} = require('@whiskeysockets/baileys');
const { Boom } = require('@hapi/boom');
const qrcode = require('qrcode-terminal');
const pino = require('pino');

const config = require('../config.json');

const AUTH_DIR = path.join(__dirname, '..', 'auth');
const logger = pino({ level: config.logLevel || 'info' });

// Jaring pengaman: error/Promise yang tak tertangani JANGAN sampai mematikan gateway
// (daemon jangka panjang). Cukup dicatat; wrapper-loop tetap hidup, tak flap.
process.on('unhandledRejection', (e) => logger.error({ err: e && (e.message || e) }, 'unhandledRejection (ditangkap, proses tetap hidup)'));
process.on('uncaughtException', (e) => logger.error({ err: e && (e.stack || e.message || e) }, 'uncaughtException (ditangkap, proses tetap hidup)'));

/** Referensi socket aktif (diganti saat reconnect). */
let sock = null;

/** State pemantau "bot mati". */
let everConnected = false;      // sudah pernah konek WA (agar tak alert saat start awal)
let waReady = false;            // koneksi WA BENAR-BENAR terbuka (untuk /health jujur + alarm logout)
let waLoggedOut = false;        // sesi ter-logout (401) -> butuh re-link (scan QR)
let waDisconnectedAt = null;    // kapan WA mulai putus (untuk rekap downtime)
let waBackoffMs = 0;            // jeda reconnect bertahap (anti-hajar WhatsApp saat throttle)
let profileDone = false;        // profil (nama/bio) sudah diset sekali per proses
let brainDown = false;          // status brain saat ini (down/up)
let brainFails = 0;             // hitung gagal cek berturut-turut
let ollamaDown = false;         // status Ollama (AI) saat ini
let ollamaFails = 0;            // hitung gagal cek Ollama berturut-turut
// Antrean pesan masuk yang GAGAL terkirim ke brain (saat brain mati/restart ~5 dtk).
// Dikirim ulang otomatis saat brain hidup -> pesan (jawaban puzzle, pertanyaan) tetap dibalas.
let _incomingRetry = [];
const INCOMING_RETRY_MAX = 200;
const INCOMING_RETRY_MAXAGE = 5 * 60 * 1000;   // buang yang lebih tua dari 5 menit (anti balas basi)

/** Util: ambil digit dari sebuah JID/string. */
const digits = (s) => (s || '').split('@')[0].split(':')[0].replace(/\D/g, '');

function privateChatAllowed(jid) {
  const raw = String(jid || '');
  if (raw.endsWith('@g.us')) return true;
  if (!config.allowPrivateChat) return false;
  const allowed = config.privateChatAllowNumbers || config.privateChatAllowedNumbers || [];
  if (!Array.isArray(allowed) || allowed.length === 0) return true;
  const n = raw.endsWith('@lid') ? (resolvePhone(raw) || digits(raw)) : digits(raw);
  return allowed.some((x) => digits(x) === n);
}

// ===== Resolusi LID -> nomor HP =====
// WhatsApp modern memakai LID (123@lid) yang BUKAN nomor telepon. Kita bangun peta
// LID->nomor dari metadata grup (peserta punya .lid dan .jid) agar pencocokan admin/
// exempt berbasis nomor HP bekerja.
const lidToPhone = new Map();

const LID_MAP_MAX = 20000;
// Set dengan batas: buang entri TERLAMA saat peta penuh (FIFO; Map jaga urutan sisip).
// Cegah lidToPhone tumbuh tanpa batas pada proses 24/7 dengan banyak grup besar.
function setLid(lid, pn) {
  if (!lid || !pn) return;
  if (!lidToPhone.has(lid) && lidToPhone.size >= LID_MAP_MAX) {
    lidToPhone.delete(lidToPhone.keys().next().value);
  }
  lidToPhone.set(lid, pn);
}

// Cache pesan masuk terakhir (id -> objek WAMessage) agar /send & /send-image bisa
// ME-REPLY (quote) pesan tertentu. Berguna saat balasan tak berurutan (mis. jawaban
// salah yang lambat dijawab via AI) -> tetap jelas pesan mana yang dibalas. FIFO cap.
const MSG_CACHE_MAX = 400;
const msgCache = new Map();
function cacheMsg(m) {
  const id = m && m.key && m.key.id;
  if (!id) return;
  if (!msgCache.has(id) && msgCache.size >= MSG_CACHE_MAX) msgCache.delete(msgCache.keys().next().value);
  msgCache.set(id, m);
}

// ===== Log chat (SEMUA grup) =====
// Rekam tiap pesan masuk + balasan bot + penghapusan, ke file HARIAN (chat-YYYY-MM-DD.log).
// Retensi berbasis WAKTU: file lebih tua dari chatLogRetentionDays (default 14) dihapus otomatis.
// Karena pesan dicatat saat tiba, pesan yang NANTI dihapus tetap tersimpan di sini. Lokal saja.
const CHAT_LOG_DIR = path.join(__dirname, '..', 'logs');
const CHAT_RETENTION_DAYS = (config.chatLogRetentionDays && config.chatLogRetentionDays > 0) ? config.chatLogRetentionDays : 14;
try { fs.mkdirSync(CHAT_LOG_DIR, { recursive: true }); } catch {}
let _chatAppends = 0;
function cleanOldChatLogs() {
  try {
    const cutoff = Date.now() - CHAT_RETENTION_DAYS * 86400000;
    fs.readdir(CHAT_LOG_DIR, (err, files) => {
      if (err) return;
      for (const f of files) {
        const m = /^chat-(\d{4}-\d{2}-\d{2})\.log$/.exec(f);
        if (!m) continue;
        const d = Date.parse(m[1] + 'T00:00:00Z');
        if (!isNaN(d) && d < cutoff) fs.unlink(path.join(CHAT_LOG_DIR, f), () => {});
      }
    });
  } catch {}
}
function chatLog(kind, jid, who, text) {
  try {
    const now = new Date();
    const ts = now.toISOString().replace('T', ' ').slice(0, 19);
    const day = now.toISOString().slice(0, 10);   // YYYY-MM-DD -> satu file per hari
    const file = path.join(CHAT_LOG_DIR, `chat-${day}.log`);
    const g = String(jid || '').replace('@g.us', '').replace('@s.whatsapp.net', '').replace('@lid', '');
    const t = String(text == null ? '' : text).replace(/[\r\n]+/g, ' ').slice(0, 600);
    fs.appendFile(file, `${ts} | ${kind} | ${g} | ${who} | ${t}\n`, () => {});
    if (++_chatAppends % 200 === 0) cleanOldChatLogs(); // buang file > retensi, berkala
  } catch {}
}
cleanOldChatLogs(); // bersihkan sekali saat start juga
// Nama tampil untuk log: "PushName(nomor)".
function chatWho(m) {
  const k = (m && m.key) || {};
  const part = k.participant || k.remoteJid || '';
  const num = resolvePhone(part) || digits(part) || '?';
  const nm = (m && m.pushName) || '';
  return nm ? `${nm}(${num})` : num;
}

function indexParticipants(participants) {
  for (const p of participants || []) {
    const lid = p.lid ? digits(p.lid) : (String(p.id || '').endsWith('@lid') ? digits(p.id) : '');
    const pn = p.jid ? digits(p.jid) : (String(p.id || '').endsWith('@s.whatsapp.net') ? digits(p.id) : '');
    if (lid && pn) setLid(lid, pn);
  }
}

/** Nomor HP dari participant JID (LID atau phone JID). '' jika belum diketahui.
 *  Baileys kadang memberi OBJEK ({id,jid,lid}) bukan string -> normalkan dulu agar
 *  tak jadi "[object Object]" -> '' (yang diam-diam merusak peta welcome/admin). */
function resolvePhone(participant) {
  const raw = String(
    (participant && typeof participant === 'object')
      ? (participant.jid || participant.id || '')
      : (participant || '')
  );
  if (raw.endsWith('@s.whatsapp.net')) return digits(raw);
  if (raw.endsWith('@lid')) return lidToPhone.get(digits(raw)) || '';
  return digits(raw);
}

// Jadikan jid bisa-dikirim: DM yang datang sebagai @lid (ID internal) sering GAGAL saat sendMessage.
// Ubah ke nomor @s.whatsapp.net via peta lidToPhone. Grup (@g.us) & nomor biasa dibiarkan apa adanya.
function toSendableJid(jid) {
  // DM yang datang sebagai @lid \u2192 ubah ke nomor @s.whatsapp.net (alamat Signal yang stabil
  // untuk membangun sesi enkripsi). Grup (@g.us) & nomor biasa dibiarkan.
  const raw = String(jid || '');
  if (raw.endsWith('@lid')) {
    const pn = lidToPhone.get(digits(raw));
    if (pn) return pn + '@s.whatsapp.net';
  }
  return jid;
}

// Paksa bangun sesi enkripsi 1-1 sebelum kirim DM. Companion (perangkat tertaut) sering "ok" tapi
// tak terkirim ke kontak yang belum punya sesi; assertSessions mengambil prekey & membangunnya.
// Grup (@g.us) dilewati (pakai sender-key, tak perlu ini).
async function ensureSession(jid) {
  try {
    if (sock && jid && !String(jid).endsWith('@g.us') && typeof sock.assertSessions === 'function') {
      await sock.assertSessions([jid], true);
    }
  } catch (e) { logger.warn({ err: e.message, jid }, 'assertSessions gagal (lanjut kirim)'); }
}

async function refreshGroupLidMap(jid) {
  try { const meta = await sock.groupMetadata(jid); indexParticipants(meta.participants); }
  catch (err) { logger.warn({ err: err.message, jid }, 'Gagal segarkan peta LID grup'); }
}

async function refreshAllLidMaps() {
  try {
    const groups = await sock.groupFetchAllParticipating();
    for (const g of Object.values(groups)) indexParticipants(g.participants);
    logger.info({ count: lidToPhone.size }, 'Peta LID->nomor disegarkan');
  } catch (err) { logger.warn({ err: err.message }, 'Gagal segarkan semua peta LID'); }
}

async function refreshAllowedPrivateChatIds() {
  try {
    if (!(sock && sock.user && typeof sock.onWhatsApp === 'function')) return;
    const allowed = config.privateChatAllowNumbers || config.privateChatAllowedNumbers || [];
    const nums = Array.isArray(allowed) ? [...new Set(allowed.map(digits).filter(Boolean))] : [];
    if (nums.length === 0) return;
    const results = await sock.onWhatsApp(...nums.map((n) => n + '@s.whatsapp.net'));
    let mapped = 0;
    for (const r of results || []) {
      const phone = digits(r.jid || r.id);
      const lid = digits(r.lid);
      if (phone && lid) { setLid(lid, phone); mapped++; }
    }
    logger.info({ requested: nums.length, mapped }, 'Private chat allowlist LID disegarkan');
  } catch (err) { logger.warn({ err: err.message }, 'Gagal resolve allowlist private chat'); }
}

// Bersihkan karakter tak terlihat yang kadang membuat kata pendek tampak terbelah,
// misalnya "limit" terlihat seperti "lim it" di WhatsApp atau hasil copy-paste model.
function sanitizeOutgoingText(s) {
  return String(s || '')
    .normalize('NFC')
    .replace(/(?<=\p{L})[\u00AD\u200B\u200C\u200D\u200E\u200F\u2060\uFEFF](?=\p{L})/gu, '')
    .replace(/(?<=\p{L})[\u00A0\u2000-\u200A\u202F\u205F\u3000](?=\p{L})/gu, '')
    .replace(/[\u00AD\u2060\uFEFF]/g, '')
    .replace(/[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]/g, '');
}

function sanitizeContent(content) {
  if (!content || typeof content !== 'object') return content;
  const c = { ...content };
  if (typeof c.text === 'string') c.text = sanitizeOutgoingText(c.text);
  if (typeof c.caption === 'string') c.caption = sanitizeOutgoingText(c.caption);
  return c;
}
// ===== Antrian kirim global =====
// Serialisasi semua pengiriman + jeda minimal antar-kirim. Mencegah pola "kirim
// beruntun secepat kilat" yang memicu deteksi spam WhatsApp (anti-ban).
let _sendGate = Promise.resolve();
let _lastSendAt = 0;
let _sendTimes = [];   // timestamp kirim dalam 60 dtk terakhir (untuk rate-limit per menit)
// Berapa ms harus ditunggu agar tidak melewati maxPerMinute (sliding window 60 dtk). 0 = boleh kirim.
function rateLimitWaitMs() {
  const rl = config.rateLimit;
  if (!rl || !rl.enabled) return 0;
  const max = rl.maxPerMinute > 0 ? rl.maxPerMinute : 20;
  const now = Date.now();
  _sendTimes = _sendTimes.filter((t) => now - t < 60000);
  if (_sendTimes.length < max) return 0;
  return Math.max(0, 60000 - (now - _sendTimes[0]) + 50);
}
function safeSend(jid, content, options) {
  const gap = Math.max(0, config.sendMinGapMs != null ? config.sendMinGapMs : 700);
  const run = _sendGate.then(async () => {
    const wait = Math.max(0, _lastSendAt + gap - Date.now());
    if (wait) await new Promise((r) => setTimeout(r, wait));
    // Batas pesan/menit (sliding window): kalau mentok, tahan sampai slot kosong (anti-burst, anti-ban).
    // DIBATASI total tunggu 45 dtk: kalau backlog terlalu panjang, TOLAK (jangan menggantung
    // selamanya -> handler HTTP brain dapat error & lanjut, bukan timeout menumpuk).
    let rl = rateLimitWaitMs();
    let waited = 0;
    while (rl > 0) {
      if (waited >= 45000) throw new Error('rate-limit backlog (>45s) - kirim ditolak');
      const step = Math.min(rl, 45000 - waited);
      logger.warn({ waitMs: step, dalam60s: _sendTimes.length }, '[RATE] batas pesan/menit tercapai, menahan kirim');
      await new Promise((r) => setTimeout(r, step));
      waited += step;
      rl = rateLimitWaitMs();
    }
    // Cek kesiapan socket DULU, sebelum memesan slot rate-limit / memajukan jam jeda.
    // Kalau ditolak setelah memesan slot, kiriman gagal saat reconnect akan "memakan"
    // kuota per-menit & jeda -> menghambat kiriman asli yang seharusnya lolos.
    if (!(sock && sock.user)) throw new Error('socket WA belum siap - kirim ditolak');
    _lastSendAt = Date.now();
    _sendTimes.push(Date.now());
    // Timeout guard: satu kirim yang menggantung (mis. jid tak valid) JANGAN sampai
    // menyumbat seluruh antrian. Tolak setelah 25 dtk agar rantai lanjut.
    return Promise.race([
      sock.sendMessage(jid, sanitizeContent(content), options),
      new Promise((_, rej) => setTimeout(() => rej(new Error('send timeout 25s')), 25000)),
    ]);
  });
  _sendGate = run.then(() => {}, () => {}); // rantai tetap jalan walau ada error
  return run;
}

/** Humanisasi: tampilkan "sedang mengetik..." lalu jeda sesuai panjang teks, agar
 * balasan terasa manusiawi (anti-deteksi bot) sekaligus jadi rate-limit alami (anti-ban).
 * Aman: tidak melempar error; dilewati kalau dimatikan atau WA belum siap. */
async function humanTyping(jid, text) {
  const h = config.humanize;
  if (!h || !h.enabled || !(sock && sock.user)) return;
  try { await sock.sendPresenceUpdate('composing', jid); } catch {}
  const cps = h.charsPerSec > 0 ? h.charsPerSec : 7;
  const len = (text || '').length;
  let ms = (h.readDelayMs != null ? h.readDelayMs : 800) + (len / cps) * 1000;
  ms = Math.min(Math.max(ms, h.minMs != null ? h.minMs : 1200), h.maxMs != null ? h.maxMs : 6000);
  ms = Math.round(ms * (0.85 + Math.random() * 0.3));   // jitter +-15% SETELAH clamp -> pesan panjang pun bervariasi
  await new Promise((r) => setTimeout(r, ms));
  try { await sock.sendPresenceUpdate('paused', jid); } catch {}
}

/** Info media: tipe + status forward (untuk moderasi gambar/forward di brain). */
function mediaInfo(msg) {
  const m = (msg && msg.message) || {};
  let type = '';
  let ctx = null;
  if (m.imageMessage) { type = 'image'; ctx = m.imageMessage.contextInfo; }
  else if (m.videoMessage) { type = 'video'; ctx = m.videoMessage.contextInfo; }
  else if (m.stickerMessage) { type = 'sticker'; ctx = m.stickerMessage.contextInfo; }
  else if (m.documentMessage) { type = 'document'; ctx = m.documentMessage.contextInfo; }
  else if (m.audioMessage) { type = 'audio'; ctx = m.audioMessage.contextInfo; }
  else if (m.extendedTextMessage) { ctx = m.extendedTextMessage.contextInfo; }
  const score = (ctx && Number(ctx.forwardingScore)) || 0;
  const isForwarded = !!(ctx && (ctx.isForwarded || score > 0));
  return { type, isForwarded, forwardScore: score };
}

/** Jika pesan adalah hasil EDIT, kembalikan pseudo-msg berisi konten BARU + key asli. */
function extractEdit(msg) {
  const m = (msg && msg.message) || {};
  const pm = m.protocolMessage;
  if (pm && pm.editedMessage && pm.key && (pm.type === 14 || pm.type === 'MESSAGE_EDIT')) {
    return { key: pm.key, message: pm.editedMessage, pushName: msg.pushName, messageTimestamp: msg.messageTimestamp };
  }
  if (m.editedMessage && m.editedMessage.message) {
    return { key: msg.key, message: m.editedMessage.message, pushName: msg.pushName, messageTimestamp: msg.messageTimestamp };
  }
  return null;
}

/** Kirim peringatan ke admin via WhatsApp (butuh WA tersambung). dryRun: hanya log. */
async function alertAdmin(text) {
  const m = config.monitor;
  if (!m || !m.enabled) return;
  // Banyak penerima: alertJids (array) diprioritaskan; fallback ke alertJid tunggal.
  const targets = (Array.isArray(m.alertJids) && m.alertJids.length)
    ? m.alertJids
    : (m.alertJid ? [m.alertJid] : []);
  if (targets.length === 0) return;
  if (m.dryRun) {
    logger.warn({ alert: text }, '[MONITOR dryRun] alert TIDAK dikirim (uji)');
    return;
  }
  if (!(sock && sock.user)) {
    logger.warn('[MONITOR] WA belum tersambung, alert ditunda');
    return;
  }
  for (const jid of targets) {
    try {
      await safeSend(jid, { text });
    } catch (err) {
      logger.error({ err: err.message, jid }, '[MONITOR] gagal kirim alert');
    }
  }
}

/** Cek kesehatan brain (C#); alert sekali saat down, sekali saat pulih. */
async function checkBrainHealth() {
  const m = config.monitor;
  if (!m || !m.enabled) return;
  if (!(sock && sock.user)) return; // WA putus -> tak bisa alert, lewati
  let ok = false;
  try {
    const r = await fetch(`${config.brainUrl}/health`, { signal: AbortSignal.timeout(8000) });
    ok = r.ok;
  } catch {
    ok = false;
  }
  if (ok) {
    if (brainDown) {
      brainDown = false;
      await alertAdmin('\u2705 *Otak bot (brain) pulih.* Moderasi, AI, jadwal & hasil otomatis aktif kembali.');
    }
    brainFails = 0;
  } else {
    brainFails++;
    if (!brainDown && brainFails >= (m.failThreshold || 2)) {
      brainDown = true;
      await alertAdmin('\u26A0\uFE0F *Bot bermasalah:* otak bot (brain) tidak merespons.\nModerasi, AI, jadwal, & hasil otomatis kemungkinan BERHENTI. Mohon cek / nyalakan ulang server bot.');
    }
  }
}

/** Cek kesehatan Ollama (AI); alert sekali saat down, sekali saat pulih. */
async function checkOllama() {
  const m = config.monitor;
  if (!m || !m.enabled || !m.ollamaUrl) return;
  if (!(sock && sock.user)) return; // WA putus -> tak bisa alert, lewati
  let ok = false;
  try {
    const r = await fetch(`${m.ollamaUrl}/api/version`, { signal: AbortSignal.timeout(8000) });
    ok = r.ok;
  } catch {
    ok = false;
  }
  if (ok) {
    if (ollamaDown) {
      ollamaDown = false;
      await alertAdmin('\u2705 *AI (Ollama) pulih.* Fitur tanya/AI aktif kembali.');
    }
    ollamaFails = 0;
  } else {
    ollamaFails++;
    if (!ollamaDown && ollamaFails >= (m.failThreshold || 2)) {
      ollamaDown = true;
      await alertAdmin('\u26A0\uFE0F *AI (Ollama) tidak merespons.*\nFitur !tanya / tag-AI mungkin mati. Moderasi, jadwal, & hasil tetap jalan. Mohon cek Ollama di server.');
    }
  }
}

/** Dead-man's switch: ping healthchecks.io HANYA saat WA tersambung. Kalau ping berhenti
 * (WA mati / gateway / mesin mati), healthchecks alert via email/SMS - menutup celah saat
 * monitor WA sendiri tak bisa mengirim (karena WA-nya yang mati). */
async function heartbeatPing() {
  const m = config.monitor;
  if (!m || !m.heartbeatUrl) return;
  if (!(sock && sock.user)) return;  // WA putus -> JANGAN ping, biar healthchecks sadar ada masalah
  try { await fetch(m.heartbeatUrl, { signal: AbortSignal.timeout(8000) }); }
  catch (err) { logger.warn({ err: err.message }, '[MONITOR] heartbeat ping gagal'); }
}

/** Ambil teks dari berbagai jenis pesan (teks biasa + caption media). */
function extractText(msg) {
  const m = msg.message;
  if (!m) return '';
  return (
    m.conversation ||
    (m.extendedTextMessage && m.extendedTextMessage.text) ||
    (m.imageMessage && m.imageMessage.caption) ||
    (m.videoMessage && m.videoMessage.caption) ||
    (m.documentMessage && m.documentMessage.caption) ||
    ''
  );
}

/** Ambil config relay dari brain (di-cache 30 detik). */
let _relayCache = null;
let _relayCacheAt = 0;
async function getRelayConfig() {
  const now = Date.now();
  if (_relayCache && now - _relayCacheAt < 30000) return _relayCache;
  try {
    const r = await fetch(`${config.brainUrl}/relay-config`, { signal: AbortSignal.timeout(8000) });
    if (r.ok) {
      _relayCache = await r.json();
      _relayCacheAt = now;
    }
  } catch (err) {
    logger.warn({ err: err.message }, 'Gagal ambil relay-config dari brain');
  }
  return _relayCache;
}

/**
 * Sebar GAMBAR dari grup hub: jika pesan adalah gambar di grup hub dengan caption
 * "!sebar <teks>", unduh gambarnya lalu kirim ulang ke semua grup tujuan.
 * Mengembalikan true jika ditangani (agar tidak diteruskan ke brain).
 */
async function maybeRelayMedia(msg, jid, caption) {
  if (!msg.message || !msg.message.imageMessage) return false;
  const rc = await getRelayConfig();
  if (!rc || !rc.enabled || jid !== rc.hubGroupJid) return false;

  const cap = (caption || '').replace(/^\s+/, '');
  if (!cap.toLowerCase().startsWith((rc.prefix + rc.command).toLowerCase())) return false;

  // Allowlist admin: jika adminNumbers diisi, hanya admin yang boleh sebar poster.
  const admins = rc.adminNumbers || [];
  if (admins.length > 0) {
    const senderLid = digits(msg.key.participant || msg.key.remoteJid);
    const senderPhone = resolvePhone(msg.key.participant || msg.key.remoteJid);
    const isAdmin = admins.some((a) => {
      const an = String(a).replace(/\D/g, '');
      return an === senderLid || (senderPhone && an === senderPhone);
    });
    if (!isAdmin) {
      await safeSend(jid, { text: 'Maaf, hanya admin yang boleh memakai !sebar.' });
      return true;
    }
  }

  const text = cap.slice((rc.prefix + rc.command).length).trim();
  const targets = rc.targetGroups || [];
  if (targets.length === 0) {
    await safeSend(jid, { text: 'Belum ada grup tujuan (relay.targetGroups kosong).' });
    return true;
  }

  let buffer;
  try {
    buffer = await downloadMediaMessage(msg, 'buffer', {}, { logger, reuploadRequest: sock.updateMediaMessage });
  } catch (err) {
    await safeSend(jid, { text: 'Gagal mengunduh gambar: ' + err.message });
    return true;
  }

  const finalCaption = rc.footer ? (text ? text + '\n\n' + rc.footer : rc.footer) : text;
  const throttleMs = Math.max(0, rc.throttleSeconds || 4) * 1000;
  await safeSend(jid, { text: `Menyebar gambar ke ${targets.length} grup (jeda ${rc.throttleSeconds || 4} dtk)...` });

  (async () => {
    let ok = 0;
    for (const t of targets) {
      try {
        await safeSend(t, { image: buffer, caption: finalCaption });
        ok++;
      } catch (err) {
        logger.error({ err: err.message, t }, 'Relay gambar gagal');
      }
      if (throttleMs > 0) await new Promise((r) => setTimeout(r, throttleMs));
    }
    try {
      await safeSend(jid, { text: `Selesai menyebar gambar ke ${ok}/${targets.length} grup.` });
    } catch (err) {
      logger.error({ err: err.message }, 'Relay: gagal kirim ringkasan');
    }
  })().catch((e) => logger.error({ err: e && e.message }, 'Relay IIFE (ditangkap)'));

  return true;
}

/** Kirim payload ke salah satu endpoint brain (C#). Gagal-diam agar loop tetap jalan. */
async function postBrain(endpointPath, payload, isReplay = false) {
  try {
    const res = await fetch(`${config.brainUrl}${endpointPath}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
      // Timeout WAJIB: brain yang menggantung (deadlock/GC) jangan sampai membekukan
      // loop messages.upsert (yang await per pesan secara berurutan).
      signal: AbortSignal.timeout((config.brainTimeoutMs && config.brainTimeoutMs > 0) ? config.brainTimeoutMs : 20000),
    });
    const bodyText = await res.text().catch(() => '');
    let body = null;
    try { body = bodyText ? JSON.parse(bodyText) : null; } catch {}
    if (!res.ok) {
      logger.warn({ status: res.status, endpointPath }, 'Brain membalas non-OK');
    }
    return body;
  } catch (err) {
    // Brain tak terjangkau (mati/restart). Simpan pesan MASUK untuk dikirim ulang saat brain hidup,
    // supaya pesan yang datang pas brain restart tetap dibalas. Hanya error KONEKSI (bukan timeout:
    // timeout bisa berarti brain sempat memproses -> jangan sampai dobel).
    // AbortSignal.timeout() melempar 'TimeoutError' (Node), manual abort 'AbortError'. Keduanya =
    // mungkin brain SEMPAT memproses -> JANGAN antre ulang (cegah balasan/aksi DOBEL).
    const isTimeout = err && (err.name === 'TimeoutError' || err.name === 'AbortError');
    if (endpointPath === '/incoming' && !isReplay && !isTimeout && _incomingRetry.length < INCOMING_RETRY_MAX) {
      _incomingRetry.push({ payload, at: Date.now() });
    }
    logger.error({ err: err.message, endpointPath }, 'Gagal menghubungi brain (C#) \u2014 apakah brain sudah jalan?');
  }
}

// Kirim ulang pesan tertunda saat brain hidup lagi (dipanggil berkala). Buang yang terlalu basi.
async function flushIncomingRetry() {
  if (_incomingRetry.length === 0) return;
  let up = false;
  try { const r = await fetch(`${config.brainUrl}/health`, { signal: AbortSignal.timeout(5000) }); up = r.ok; } catch {}
  if (!up) return;                                  // brain belum hidup -> tunggu putaran berikutnya
  const items = _incomingRetry; _incomingRetry = [];
  let n = 0;
  for (const it of items) {
    if (Date.now() - it.at > INCOMING_RETRY_MAXAGE) continue; // basi -> buang (cek per-item, bukan snapshot)
    try { await postBrain('/incoming', it.payload, true); n++; } catch {}
    await new Promise((r) => setTimeout(r, 300));            // pelan, anti-burst
  }
  if (n) logger.info({ replayed: n }, 'pesan tertunda (saat brain mati) dikirim ulang ke brain');
}
setInterval(() => { flushIncomingRetry().catch(() => {}); }, 4000);

async function startSocket() {
  const { state, saveCreds } = await useMultiFileAuthState(AUTH_DIR);
  const { version } = await fetchLatestBaileysVersion();

  // Presence manusiawi: jangan tampil "online 24 jam" (ciri bot). appearOffline (default true) =>
  // bot tampak OFFLINE; tetap menerima & membalas pesan, hanya status online-nya tak disiarkan terus.
  const appearOffline = !config.humanize || config.humanize.appearOffline !== false;
  sock = makeWASocket({
    version,
    auth: state,
    logger,
    printQRInTerminal: false,
    qrTimeout: 180000, // QR bertahan ~3 menit per sesi (stabil, tak sering ganti)
    markOnlineOnConnect: !appearOffline, // false = tidak menyiarkan "online" saat konek (lebih manusiawi)
  });

  sock.ev.on('creds.update', saveCreds);

  sock.ev.on('connection.update', async (update) => {
    const { connection, lastDisconnect, qr } = update;

    if (qr) {
      console.log('\n\uD83D\uDCF1 Scan QR berikut dengan WhatsApp di NOMOR BOT (Perangkat Tertaut):\n');
      qrcode.generate(qr, { small: true });
      // Simpan juga sebagai file gambar untuk discan dari layar/browser.
      try {
        const qrPng = path.join(__dirname, '..', 'qr.png');
        const qrHtml = path.join(__dirname, '..', 'qr.html');
        await QRCode.toFile(qrPng, qr, { width: 400, margin: 2 });
        const dataUrl = await QRCode.toDataURL(qr, { width: 400, margin: 2 });
        fs.writeFileSync(qrHtml,
          '<html><meta http-equiv="refresh" content="20"><body style="text-align:center;font-family:sans-serif">' +
          '<h3>Scan dengan WhatsApp NOMOR BOT (Perangkat Tertaut)</h3>' +
          '<img src="' + dataUrl + '">' +
          '<p>QR berlaku ~3 menit \u2014 halaman ini auto-refresh. Scan yang sedang tampil.</p></body></html>');
        console.log('\uD83D\uDCBE QR juga disimpan: gateway/qr.png dan gateway/qr.html');
      } catch (e) {
        logger.warn({ err: e.message }, 'Gagal menyimpan file QR');
      }
    }

    if (connection === 'open') {
      console.log('\u2705 Gateway tersambung ke WhatsApp. Jadikan bot ADMIN di grup yang dimoderasi.');
      refreshAllLidMaps(); // bangun peta LID->nomor untuk semua grup (best-effort)
      refreshAllowedPrivateChatIds(); // map nomor DM allowlist ke LID WhatsApp
      // Sambung-ulang setelah putus \u2192 HANYA alert kalau putusnya cukup lama
      // (blip jaringan 1-2 menit wajar & self-heal, tidak perlu spam admin).
      if (everConnected && waDisconnectedAt) {
        const mins = Math.max(1, Math.round((Date.now() - waDisconnectedAt) / 60000));
        waDisconnectedAt = null;
        // reconnectAlertMinMinutes: 0 = MATIKAN alert reconnect (blip pendek tak ada yang sadar).
        // > 0 = hanya alert kalau putus selama itu (mis. 10 menit).
        const minAlert = (config.monitor && config.monitor.reconnectAlertMinMinutes) || 0;
        if (minAlert > 0 && mins >= minAlert) {
          setTimeout(() => {
            alertAdmin(`\u2705 *Bot tersambung lagi ke WhatsApp* setelah sempat terputus ~${mins} menit.\nMohon cek apakah ada turnamen/pesan yang terlewat selama itu.`);
          }, 3000); // beri jeda agar socket benar-benar siap mengirim
        } else {
          logger.info({ mins }, '[MONITOR] reconnect \u2014 tidak alert');
        }
      }
      everConnected = true;
      waReady = true; waLoggedOut = false;   // koneksi hidup -> /health jujur + alarm aman
      waBackoffMs = 0;   // sukses konek -> reset backoff reconnect
      if (appearOffline) { try { await sock.sendPresenceUpdate('unavailable'); } catch {} } // tampil offline
      // Profil bot (sekali per proses): nama & bio konsisten (juga otomatis setelah swap akun).
      if (!profileDone) {
        profileDone = true;
        const p = config.profile || {};
        if (p.name) { try { await sock.updateProfileName(p.name); console.log('Profil: nama diset ->', p.name); } catch (e) { logger.warn({ err: e.message }, 'gagal set nama profil'); } }
        if (p.about) { try { await sock.updateProfileStatus(p.about); } catch {} }
        if (p.picture) {
          try {
            const picPath = path.isAbsolute(p.picture) ? p.picture : path.join(__dirname, '..', '..', p.picture);
            await sock.updateProfilePicture(sock.user.id, { url: picPath });
            console.log('Profil: foto diset ->', picPath);
          } catch (e) { logger.warn({ err: e.message }, 'gagal set foto profil'); }
        }
      }
    }

    if (connection === 'close') {
      waReady = false;   // koneksi tutup -> /health jujur (jangan lapor connected padahal mati)
      const code = lastDisconnect && lastDisconnect.error instanceof Boom
        ? lastDisconnect.error.output.statusCode
        : null;

      // DIAGNOSTIK: tulis alasan disconnect ke file agar bisa dibaca dari luar (console tak terakses).
      try {
        fs.writeFileSync(path.join(__dirname, '..', 'last-disconnect.json'), JSON.stringify({
          code: code,
          loggedOut: code === DisconnectReason.loggedOut,
          reason: (lastDisconnect && lastDisconnect.error && lastDisconnect.error.message) || '',
          at: new Date().toISOString(),
        }, null, 2));
      } catch (e) {}

      if (code === DisconnectReason.loggedOut) {
        waLoggedOut = true;   // tandai: butuh re-link (dipakai /health & alarm)
        console.log('\u26D4 Sesi logout. Hapus isi folder gateway/auth/ lalu jalankan ulang untuk scan QR baru.');
      } else {
        if (everConnected && !waDisconnectedAt) waDisconnectedAt = Date.now(); // tandai mulai putus
        // Backoff: naikkan jeda tiap putus beruntun (2s,4s,...,maks 60s) supaya tidak menghajar
        // WhatsApp saat sesi dibatasi/throttle \u2014 agar throttle cepat lepas & koneksi stabil.
        waBackoffMs = Math.min(waBackoffMs ? waBackoffMs * 2 : 5000, 300000);
        console.log(`\uD83D\uDD04 Koneksi terputus, menyambung ulang dalam ${Math.round(waBackoffMs / 1000)}s...`);
        setTimeout(() => startSocket(), waBackoffMs);
      }
    }
  });

  sock.ev.on('messages.upsert', async ({ messages, type }) => {
    if (type !== 'notify') return;
    for (const msg of messages) {
      const jid = msg.key && msg.key.remoteJid;
      if (!jid) continue;

      // Pesan DIHAPUS (revoke "hapus untuk semua"): catat isi aslinya dari cache sebelum hilang.
      // (bot-delete sendiri = fromMe, dicatat di /delete; di sini hanya hapus oleh pengguna).
      const pm = msg.message && msg.message.protocolMessage;
      if (pm && (pm.type === 0 || pm.type === 'REVOKE')) {
        if (!msg.key.fromMe) {
          const orig = (pm.key && pm.key.id) ? msgCache.get(pm.key.id) : null;
          chatLog('HAPUS', jid, orig ? chatWho(orig) : '?', orig ? (extractText(orig) || '[media]') : '[tak ada di cache]');
        }
        continue;
      }
      // Rekam SEMUA pesan masuk (termasuk yang NANTI dihapus -> sudah tercatat di sini).
      if (!msg.key.fromMe) {
        const _m = mediaInfo(msg);
        chatLog('IN', jid, chatWho(msg), extractText(msg) || (_m.type ? `[${_m.type}]` : ''));
      }

      // DM (non-grup) dilewati, kecuali private chat reaktif aktif dan nomornya diizinkan.
      if (config.moderateGroupsOnly !== false && !jid.endsWith('@g.us')) {
        if (String(jid).endsWith('@lid') && config.allowPrivateChat && !resolvePhone(jid)) {
          await refreshAllowedPrivateChatIds();
          if (!resolvePhone(jid)) await refreshAllLidMaps();
        }
        if (!privateChatAllowed(jid)) {
          logger.info({ jid }, 'DM dilewati: tidak ada di allowlist private chat');
          continue;
        }
      }
      if (msg.key.fromMe) continue;

      // Pesan yang masuk saat bot offline akan dikirim WhatsApp saat reconnect (type 'notify')
      // \u2192 bot tetap menjawabnya. TAPI batasi umur: jangan balas borongan pesan basi kalau bot
      // offline lama (anti-ban). maxMessageAgeMinutes: 0 = tanpa batas.
      const maxAgeMin = (config.maxMessageAgeMinutes != null) ? config.maxMessageAgeMinutes : 60;
      const tsSec = Number((msg.messageTimestamp && msg.messageTimestamp.toString)
        ? msg.messageTimestamp.toString() : msg.messageTimestamp) || 0;
      if (maxAgeMin > 0 && tsSec > 0 && (Date.now() / 1000 - tsSec) / 60 > maxAgeMin) {
        logger.info({ jid, ageMin: Math.round((Date.now() / 1000 - tsSec) / 60) }, 'pesan terlalu lama, dilewati');
        continue;
      }

      // Tangani relay GAMBAR (!sebar) pada pesan ASLI lebih dulu.
      const origText = extractText(msg);
      if (await maybeRelayMedia(msg, jid, origText)) continue;

      // Jika ini hasil EDIT, moderasi konten BARU-nya (pakai key asli agar bisa dihapus).
      const editInfo = extractEdit(msg);
      const src = editInfo || msg;
      cacheMsg(src); // simpan agar brain bisa minta bot me-reply (quote) pesan ini



      const text = extractText(src);
      const media = mediaInfo(src);

      // Lewati hanya jika benar-benar kosong (tak ada teks DAN bukan media).
      if (!text && !media.type) continue;

      // Resolusi nomor HP pengirim (LID -> nomor). Lazy-refresh kalau LID belum dikenal.
      const part = src.key.participant || jid;
      let participantPhone = resolvePhone(part);
      if (!participantPhone && String(part).endsWith('@lid')) {
        await refreshGroupLidMap(jid);
        participantPhone = resolvePhone(part);
      }

      // Deteksi apakah bot di-tag (pemicu AI). WhatsApp pakai LID -> cocokkan nomor & LID bot.
      const botIds = [];
      if (sock.user && sock.user.id) botIds.push(digits(sock.user.id));
      if (sock.user && sock.user.lid) botIds.push(digits(sock.user.lid));
      const ci =
        (src.message &&
          src.message.extendedTextMessage &&
          src.message.extendedTextMessage.contextInfo) || {};
      const ctxMentions = ci.mentionedJid || [];
      const mentionedBot =
        botIds.length > 0 &&
        (ctxMentions.some((j) => botIds.includes(digits(j))) ||
          botIds.some((n) => n && text.includes('@' + n)));

      // Mention -> {lid, phone}. Lazy-refresh peta LID grup kalau ada tag yang
      // nomornya belum dikenal (mirip resolusi nomor pengirim). Penting untuk
      // perintah yang andalkan nomor pemain (mis. @bot turnamen @A @B @C @D).
      const mentionJids = (ctxMentions || []).filter((j) => !botIds.includes(digits(j)));
      const mentionPairs = [];
      for (const mj of mentionJids) {
        let phone = resolvePhone(mj) || '';
        // Grup LID-only tak punya nomor di metadata. Baileys 7 menyimpan peta
        // LID<->nomor yang PERSISTEN (dari riwayat pesan) -> pakai getPNForLID.
        if (!phone) {
          try {
            const lm = sock && sock.signalRepository && sock.signalRepository.lidMapping;
            if (lm && typeof lm.getPNForLID === 'function') {
              const pn = await lm.getPNForLID(digits(mj) + '@lid');
              if (pn) {
                phone = digits(pn);
                setLid(digits(mj), phone);
              }
            }
          } catch (e) {
            /* abaikan: biarkan phone kosong */
          }
        }
        mentionPairs.push({ lid: digits(mj), phone });
      }

      // Pesan yang di-reply (untuk !lapor): teks + penulis pesan yang dikutip.
      let quotedText = '';
      let quotedAuthor = '';
      if (ci.quotedMessage) {
        const qm = ci.quotedMessage;
        quotedText =
          qm.conversation ||
          (qm.extendedTextMessage && qm.extendedTextMessage.text) ||
          (qm.imageMessage && qm.imageMessage.caption) ||
          (qm.videoMessage && qm.videoMessage.caption) ||
          '';
        quotedAuthor = ci.participant || '';
      }
      const quotedId = ci.stanzaId || ''; // id pesan yang di-reply (untuk targetkan puzzle tertentu)

      const brainResult = await postBrain('/incoming', {
        jid,
        participant: part,
        participantPhone,
        pushName: src.pushName || msg.pushName || '',
        text,
        mediaType: media.type,
        isForwarded: media.isForwarded,
        forwardScore: media.forwardScore,
        edited: !!editInfo,
        key: src.key, // key konten yang dimoderasi (asli atau target edit)
        mentionedBot,
        mentions: mentionPairs,
        quotedText,
        quotedAuthor,
        quotedId,
        channel: 'whatsapp', // fondasi agnostic: adapter ini = WhatsApp
      });
    }
  });

  // Member baru join / keluar grup \u2192 teruskan ke brain (untuk pesan sambutan).
  sock.ev.on('group-participants.update', async (update) => {
    // Segarkan peta LID setiap ada perubahan keanggotaan (add/remove/promote).
    await refreshGroupLidMap(update.id);
    if (update.action !== 'add') return; // sambutan hanya untuk member baru
    let groupName = '';
    try {
      const meta = await sock.groupMetadata(update.id);
      groupName = (meta && meta.subject) || '';
    } catch (err) {
      logger.warn({ err: err.message }, 'Gagal ambil nama grup (lanjut tanpa nama)');
    }
    const participants = update.participants || [];
    await postBrain('/member-joined', {
      jid: update.id,
      groupName,
      participants,
      participantsPhone: participants.map((p) => resolvePhone(p)),
    });
  });
}

function startServer() {
  const app = express();
  app.use(express.json({ limit: '1mb' }));

  app.get('/health', (req, res) => {
    res.json({ ok: true, connected: waReady, ready: waReady, loggedOut: waLoggedOut });
  });

  // Indikator "sedang mengetik" SEGERA (dipanggil brain begitu pesan masuk, selama AI berpikir).
  // Ringan: hanya kirim presence, tanpa jeda/teks. WA otomatis hilang saat pesan tiba / ~25 dtk.
  app.post('/typing', async (req, res) => {
    let { jid, state } = req.body || {};
    if (!(sock && sock.user) || !jid) return res.json({ ok: false });
    jid = toSendableJid(jid); // DM @lid -> nomor agar presence ke alamat yang benar
    try { await sock.sendPresenceUpdate(state === 'paused' ? 'paused' : 'composing', jid); } catch {}
    res.json({ ok: true });
  });

  // Restart gateway: keluar \u2192 wrapper loop (run-gateway.cmd) jalankan node lagi = kode terbaru.
  // Token = config.adminApiToken (localhost-only). Dipakai brain /admin/restart maupun manual.
  app.post('/admin/restart', (req, res) => {
    if (!config.adminApiToken) return res.status(403).json({ ok: false, error: 'endpoint mati (set adminApiToken)' });
    const token = (req.query && req.query.token) || (req.body && req.body.token);
    if (token !== config.adminApiToken) return res.status(401).json({ ok: false, error: 'token salah' });
    res.json({ ok: true, restarting: 'gateway' });
    setTimeout(() => { logger.warn('Restart gateway via /admin/restart'); process.exit(0); }, 800);
  });

  // Daftar grup yang diikuti bot (jid + nama) \u2014 untuk pengaturan per-grup.
  app.get('/groups', async (req, res) => {
    if (!(sock && sock.user)) return res.status(503).json({ ok: false, error: 'socket belum siap' });
    try {
      const groups = await sock.groupFetchAllParticipating();
      const list = Object.values(groups).map((g) => ({
        jid: g.id,
        subject: g.subject,
        size: g.participants ? g.participants.length : 0,
      }));
      res.json({ ok: true, count: list.length, groups: list });
    } catch (err) {
      res.status(500).json({ ok: false, error: err.message });
    }
  });

  // DEBUG: cek resolusi LID -> nomor (peta in-memory vs store persisten Baileys).
  app.get('/debug/lid', async (req, res) => {
    try {
      const lid = digits(req.query.lid || '');
      if (!lid) return res.status(400).json({ ok: false, error: 'lid wajib' });
      const fromMap = lidToPhone.get(lid) || '';
      let fromBaileys = '';
      try {
        const lm = sock && sock.signalRepository && sock.signalRepository.lidMapping;
        if (lm && typeof lm.getPNForLID === 'function') {
          const pn = await lm.getPNForLID(lid + '@lid');
          fromBaileys = pn ? digits(pn) : '(null)';
        } else {
          fromBaileys = '(API tidak ada)';
        }
      } catch (e) {
        fromBaileys = 'ERR:' + e.message;
      }
      res.json({ ok: true, lid, fromMap, fromBaileys });
    } catch (err) {
      res.status(500).json({ ok: false, error: err.message });
    }
  });

  // Daftar anggota satu grup (jid + nomor) \u2014 untuk mengisi adminNumbers dari grup admin.
  app.get('/group-members', async (req, res) => {
    const jid = req.query.jid;
    if (!(sock && sock.user) || !jid) return res.status(503).json({ ok: false, error: 'jid wajib & socket siap' });
    try {
      const meta = await sock.groupMetadata(jid);
      indexParticipants(meta.participants); // sekalian segarkan peta LID->nomor
      const lm = sock && sock.signalRepository && sock.signalRepository.lidMapping;
      const members = [];
      for (const p of (meta.participants || [])) {
        let phone = p.jid ? digits(p.jid) : (String(p.id || '').endsWith('@s.whatsapp.net') ? digits(p.id) : '');
        // Peta in-memory (lidToPhone) hilang tiap restart -> untuk peserta @lid yang phone-nya kosong,
        // pakai peta PERSISTEN Baileys (getPNForLID, dari riwayat pesan) supaya tag by-nama tetap jalan.
        if (!phone && String(p.id || '').endsWith('@lid')) {
          const lid = digits(p.id);
          phone = lidToPhone.get(lid) || '';
          if (!phone && lm && typeof lm.getPNForLID === 'function') {
            try {
              const pn = await lm.getPNForLID(lid + '@lid');
              if (pn) { phone = digits(pn); setLid(lid, phone); }
            } catch (e) { /* biarkan kosong kalau belum ada mapping */ }
          }
        }
        members.push({ jid: p.id, number: digits(p.id), phone, admin: p.admin || null });
      }
      res.json({ ok: true, subject: meta.subject, count: members.length, members });
    } catch (err) {
      res.status(500).json({ ok: false, error: err.message });
    }
  });

  // Batasi mention per pesan (anti mass-tag). Kalau lebih dari batas,
  // kirim tag lanjutan per batch agar semua tetap terpanggil tanpa >5 tag/pesan.
  function mentionBatches(mentions, jid) {
    const arr = mentions || [];
    const max = config.maxMentions != null ? config.maxMentions : 5;
    if (max <= 0 || arr.length <= max) return [arr];
    logger.warn({ jid, count: arr.length, max }, '[ANTI-BAN] mention dipecah per batch');
    const batches = [];
    for (let i = 0; i < arr.length; i += max) batches.push(arr.slice(i, i + max));
    return batches;
  }

  function mentionText(batch) {
    return batch.map((j) => '@' + digits(j)).join(' ');
  }

  // Tag "menyala": WhatsApp pasca-migrasi LID hanya me-highlight mention kalau mentionedJid = NOMOR
  // (@s.whatsapp.net), bukan @lid. Jadi tiap mention @lid diubah -> nomor, sekaligus token @<digit>
  // di teks/caption diselaraskan. Pakai peta persisten (getPNForLID) agar tetap jalan walau gateway
  // habis restart (peta in-memory kosong). Matikan dgn config.mentionForm = 'lid'.
  async function mentionsToPhone(text, mentions) {
    if (!Array.isArray(mentions) || mentions.length === 0 || config.mentionForm === 'lid') {
      return { text, mentions: mentions || [] };
    }
    const lm = sock && sock.signalRepository && sock.signalRepository.lidMapping;
    let out = String(text || '');
    const newMentions = [];
    for (const m of mentions) {
      const raw = String(m || '');
      if (raw.endsWith('@lid')) {
        const lid = digits(raw);
        let phone = lidToPhone.get(lid) || '';
        if (!phone && lm && typeof lm.getPNForLID === 'function') {
          try { const pn = await lm.getPNForLID(lid + '@lid'); if (pn) { phone = digits(pn); setLid(lid, phone); } } catch (e) { /* belum ada mapping */ }
        }
        if (phone) {
          out = out.split('@' + lid).join('@' + phone); // token teks ikut jadi nomor
          newMentions.push(phone + '@s.whatsapp.net');
          continue;
        }
      }
      newMentions.push(raw); // bukan @lid, atau nomor tak diketahui -> biarkan
    }
    return { text: out, mentions: newMentions };
  }

  // Brain memerintahkan kirim pesan (mis. peringatan).
  app.post('/send', async (req, res) => {
    let { jid, text, mentions, replyToId } = req.body || {};
    if (!(sock && sock.user) || !jid || !text) {
      return res.status(400).json({ ok: false, error: 'jid & text wajib, dan socket harus siap' });
    }
    jid = toSendableJid(jid); // DM @lid -> nomor agar pesan benar-benar terkirim
    // Grup: ubah mention @lid -> nomor supaya tag benar-benar menyala (highlight + notif).
    if (String(jid).endsWith('@g.us')) {
      const mp = await mentionsToPhone(text, mentions);
      text = mp.text; mentions = mp.mentions;
    }
    // Quote pesan yang dibalas (kalau ada di cache) -> jelas pesan mana yang dijawab.
    const quoted = replyToId ? msgCache.get(replyToId) : null;
    try {
      // Penanda \x1F memecah satu /send jadi BEBERAPA pesan terpisah (mis. info + "X menit lagi"
      // dipisah agar saat diforward, waktu relatif tak ikut & bikin bingung). mentions hanya di pesan pertama.
      const parts = String(text).split('\x1F').map(s => s.trim()).filter(s => s.length > 0);
      let lastId = '';
      for (let i = 0; i < parts.length; i++) {
        await ensureSession(jid);
        await humanTyping(jid, parts[i]);   // "sedang mengetik..." + jeda manusiawi sebelum tiap pesan
        const batches = i === 0 ? mentionBatches(mentions, jid) : [[]];
        // quote hanya di pesan pertama (balasan utama)
        const sent = await safeSend(jid, { text: parts[i], mentions: batches[0] || [] }, (i === 0 && quoted) ? { quoted } : undefined);
        chatLog('BOT', jid, 'BOT', parts[i]);
        for (const batch of batches.slice(1)) {
          const tagText = mentionText(batch);
          if (tagText) await safeSend(jid, { text: 'Tag lanjutan: ' + tagText, mentions: batch });
        }
        lastId = (sent && sent.key && sent.key.id) || lastId;
      }
      res.json({ ok: true, id: lastId });
    } catch (err) {
      logger.error({ err: err.message }, 'Gagal /send');
      res.status(500).json({ ok: false, error: err.message });
    }
  });

  // Brain memerintahkan REAKSI emoji ke sebuah pesan (lebih manusiawi + hemat pesan).
  app.post('/react', async (req, res) => {
    let { jid, key, emoji } = req.body || {};
    if (!(sock && sock.user) || !jid || !key || !emoji) {
      return res.status(400).json({ ok: false, error: 'jid, key, emoji wajib & socket siap' });
    }
    jid = toSendableJid(jid);
    try {
      await safeSend(jid, { react: { text: emoji, key } }); // lewat antrian/rate-limit, tanpa typing
      res.json({ ok: true });
    } catch (err) {
      logger.error({ err: err.message }, 'Gagal /react');
      res.status(500).json({ ok: false, error: err.message });
    }
  });

  // Brain memerintahkan kirim GAMBAR (mis. papan puzzle) dari file PNG lokal.
  app.post('/send-image', async (req, res) => {
    let { jid, path: imgPath, caption, mentions, replyToId } = req.body || {};
    if (!(sock && sock.user) || !jid || !imgPath) {
      return res.status(400).json({ ok: false, error: 'jid & path wajib, dan socket harus siap' });
    }
    jid = toSendableJid(jid);
    if (String(jid).endsWith('@g.us')) {
      const mp = await mentionsToPhone(caption, mentions);
      caption = mp.text; mentions = mp.mentions;
    }
    const quoted = replyToId ? msgCache.get(replyToId) : null;
    try {
      const buf = await fs.promises.readFile(imgPath);  // async: jangan blokir event-loop
      const batches = mentionBatches(mentions, jid);
      const sent = await safeSend(jid, { image: buf, caption: caption || '', mentions: batches[0] || [] }, quoted ? { quoted } : undefined);
      chatLog('BOT-IMG', jid, 'BOT', caption || '[gambar]');
      for (const batch of batches.slice(1)) {
        const tagText = mentionText(batch);
        if (tagText) await safeSend(jid, { text: 'Tag lanjutan: ' + tagText, mentions: batch });
      }
      res.json({ ok: true, id: (sent && sent.key && sent.key.id) || '' });
    } catch (err) {
      logger.error({ err: err.message }, 'Gagal /send-image');
      res.status(500).json({ ok: false, error: err.message });
    }
  });

  // Brain meminta pesan terakhir non-bot di grup untuk aksi admin yang dikonfirmasi.
  app.get('/last-message', async (req, res) => {
    const jid = req.query.jid;
    if (!jid) return res.status(400).json({ ok: false, error: 'jid wajib' });
    let found = null;
    for (const m of Array.from(msgCache.values()).reverse()) {
      if (!m || !m.key || m.key.remoteJid !== jid || m.key.fromMe) continue;
      const text = extractText(m) || (mediaInfo(m).type ? `[${mediaInfo(m).type}]` : '');
      found = { key: m.key, id: m.key.id || '', who: chatWho(m), text };
      break;
    }
    if (!found) return res.status(404).json({ ok: false, error: 'pesan terakhir tidak ada di cache' });
    res.json({ ok: true, message: found });
  });
  // Brain memerintahkan hapus pesan (butuh bot = admin grup).
  app.post('/delete', async (req, res) => {
    let { jid, key } = req.body || {};
    if (!(sock && sock.user) || !jid || !key) {
      return res.status(400).json({ ok: false, error: 'jid & key wajib' });
    }
    jid = toSendableJid(jid);
    try {
      const orig = (key && key.id) ? msgCache.get(key.id) : null;
      chatLog('BOT-HAPUS', jid, orig ? chatWho(orig) : '?', orig ? (extractText(orig) || '[media]') : '[tak ada di cache]');
      await safeSend(jid, { delete: key });
      res.json({ ok: true });
    } catch (err) {
      logger.error({ err: err.message }, 'Gagal /delete \u2014 pastikan bot admin grup');
      res.status(500).json({ ok: false, error: err.message });
    }
  });

  // Brain minta isi GAMBAR sebuah pesan (dari cache) -> base64. Untuk analisa papan dari foto.
  app.post('/get-media', async (req, res) => {
    const { id } = req.body || {};
    const m = id ? msgCache.get(id) : null;
    if (!m) return res.status(404).json({ ok: false, error: 'pesan tak ada di cache' });
    try {
      const buf = await downloadMediaMessage(m, 'buffer', {}, { logger, reuploadRequest: sock.updateMediaMessage });
      res.json({ ok: true, base64: Buffer.from(buf).toString('base64') });
    } catch (err) {
      logger.error({ err: err.message }, 'Gagal /get-media');
      res.status(500).json({ ok: false, error: err.message });
    }
  });

  app.listen(config.port, '127.0.0.1', () => {
    console.log(`\uD83C\uDF10 Gateway API: http://127.0.0.1:${config.port} (/send, /delete, /health)`);
  });

  // Pemantau "bot mati": cek brain berkala, alert ke admin saat down/pulih.
  if (config.monitor && config.monitor.enabled) {
    const sec = Math.max(20, config.monitor.checkSeconds || 60);
    setInterval(() => { checkBrainHealth(); checkOllama(); }, sec * 1000);
    const ollamaNote = config.monitor.ollamaUrl ? ' + Ollama' : '';
    console.log(`\uD83E\uDE7A Monitor brain${ollamaNote} aktif tiap ${sec} dtk \u2192 alert ke ${config.monitor.alertJid}${config.monitor.dryRun ? ' (dryRun)' : ''}`);
    // Dead-man's switch eksternal (healthchecks.io): ping berkala saat WA tersambung.
    if (config.monitor.heartbeatUrl) {
      const hbMin = Math.max(1, config.monitor.heartbeatMinutes || 5);
      heartbeatPing();
      setInterval(heartbeatPing, hbMin * 60 * 1000);
      console.log(`\uD83D\uDC93 Heartbeat eksternal tiap ${hbMin} mnt \u2192 ${config.monitor.heartbeatUrl.replace(/\/[^/]+$/, '/****')}`);
    }
  }
}

startSocket()
  .then(startServer)
  .catch((err) => {
    console.error('Gagal memulai gateway:', err);
    process.exit(1);
  });
