// Adapter Telegram â€” menghubungkan Telegram ke brain (channel-agnostic).
// Brain tidak tahu ini Telegram; ia hanya bicara kontrak /incoming + /send (lihat adapters/README.md).
//
// Jalankan:  TELEGRAM_BOT_TOKEN=123:abc node index.js
// Lalu di brain/config/config.json tambahkan:  "channels": { "telegram": "http://127.0.0.1:3310" }  + POST /reload
//
// ENV:
//   TELEGRAM_BOT_TOKEN  (wajib)  token dari @BotFather
//   BRAIN_URL           (opsional, default http://127.0.0.1:5050)
//   PORT                (opsional, default 3310)

const http = require('http');
const fs = require('fs');

const TOKEN = process.env.TELEGRAM_BOT_TOKEN || '';
const BRAIN = process.env.BRAIN_URL || 'http://127.0.0.1:5050';
const PORT = parseInt(process.env.PORT || '3310', 10);
const API = `https://api.telegram.org/bot${TOKEN}`;

if (!TOKEN) { console.error('FATAL: TELEGRAM_BOT_TOKEN belum diset.'); process.exit(1); }

let botUsername = '';
let botId = 0;

async function tg(method, body) {
  const r = await fetch(`${API}/${method}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body || {}),
  });
  const json = await r.json().catch(() => ({}));
  if (!r.ok || json.ok === false) {
    throw new Error(`${method} gagal: HTTP ${r.status} ${JSON.stringify(json)}`);
  }
  return json;
}

// â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ KELUAR: brain â†’ adapter (HTTP server) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
function readBody(req) {
  return new Promise((resolve) => { let b = ''; req.on('data', (d) => (b += d)); req.on('end', () => resolve(b)); });
}

http.createServer(async (req, res) => {
  try {
    if (req.method === 'GET' && req.url === '/health') {
      res.writeHead(200, { 'Content-Type': 'application/json' });
      return res.end(JSON.stringify({ ok: true, connected: !!botId }));
    }
    const body = await readBody(req);
    const o = body ? JSON.parse(body) : {};

    if (req.url === '/send') {
      await tg('sendMessage', { chat_id: o.jid, text: o.text || '' });
    } else if (req.url === '/send-image') {
      const form = new FormData();
      form.append('chat_id', String(o.jid));
      if (o.caption) form.append('caption', o.caption);
      form.append('photo', new Blob([fs.readFileSync(o.path)]), 'image.png');
      const r = await fetch(`${API}/sendPhoto`, { method: 'POST', body: form });
      const json = await r.json().catch(() => ({}));
      if (!r.ok || json.ok === false) throw new Error(`sendPhoto gagal: HTTP ${r.status} ${JSON.stringify(json)}`);
    } else if (req.url === '/delete') {
      await tg('deleteMessage', { chat_id: o.jid, message_id: o.key && o.key.id });
    }
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end('{"ok":true}');
  } catch (e) {
    res.writeHead(502, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ ok: false, error: e.message }));
  }
}).listen(PORT, '127.0.0.1', () => console.log(`Adapter Telegram aktif di http://127.0.0.1:${PORT}`));

// â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ MASUK: Telegram â†’ brain (long polling) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
async function forward(m, mentioned) {
  await fetch(`${BRAIN}/incoming`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      channel: 'telegram',
      jid: String(m.chat.id),                 // ID percakapan = chat id
      participant: String(m.from.id),         // ID pengirim = user id
      pushName: m.from.first_name || m.from.username || '',
      text: m.text || '',
      key: { id: String(m.message_id) },      // untuk /delete
      mentionedBot: mentioned,
    }),
  }).catch(() => {});
}

async function poll() {
  let me;
  try { me = await tg('getMe', {}); }
  catch (e) { console.error('getMe gagal - token salah?', e.message); process.exit(1); }
  botUsername = me.result.username;
  botId = me.result.id;
  console.log('Terhubung sebagai @' + botUsername);

  let offset = 0;
  while (true) {
    try {
      const r = await tg('getUpdates', { offset, timeout: 50 });
      if (r.ok) for (const u of r.result) {
        offset = u.update_id + 1;
        const m = u.message;
        if (!m || !m.text) continue;
        // bot dianggap "dipanggil" bila di-tag @username atau pesan me-reply pesan bot
        const mentioned =
          m.text.includes('@' + botUsername) ||
          (m.reply_to_message && m.reply_to_message.from && m.reply_to_message.from.id === botId);
        await forward(m, !!mentioned);
      }
    } catch (e) {
      await new Promise((r) => setTimeout(r, 2000));
    }
  }
}
poll();



