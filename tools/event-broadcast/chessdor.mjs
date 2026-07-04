/*
 * ============================================================
 *  PENJADWAL PROMO EVENT — Nobar Naroditsky Memorial (ChessDOR!)
 * ============================================================
 *  Kirim pesan per-babak ke SEMUA grup pemain di jam terjadwal (WIB).
 *  Anti-ban: jeda antar grup, pesan beda tiap ronde, skip grup console.
 *
 *  PAKAI:
 *    node chessdor.mjs            -> PREVIEW (dry-run, TIDAK kirim) + cek jadwal
 *    node chessdor.mjs --send     -> AKTIF: kirim beneran di jam masing-masing
 *
 *  Biarkan proses ini JALAN (background) sampai jadwal terakhir selesai.
 *  Mulai idealnya sore/petang hari-H (3 Juli) biar nggak jalan kelamaan.
 * ============================================================
 */

const GATEWAY = 'http://127.0.0.1:3211';
const SEND = process.argv.includes('--send');
// Kirim SATU babak sekarang juga (abaikan jadwal):  --now R3
const _iNow = process.argv.indexOf('--now');
const NOW_LABEL = _iNow >= 0 ? (process.argv[_iNow + 1] || '') : '';

// Grup yang DILEWATI (console/plumbing bot). Cocokkan by substring nama.
const EXCLUDE = ['Konsol Bot', 'Judit Polica WAG'];

// Jeda antar grup saat broadcast satu pesan (anti-ban). 25 detik.
const GAP_MS = 25_000;

const LINK = 'https://chessdor.com';

// Poster event (sama dgn send-poster.mjs). Reminder = poster + caption.
const IMG_PATH = 'C:\\Users\\dev8\\Downloads\\WhatsApp Image 2026-07-03 at 7.03.45 PM.jpeg';

// Caption lengkap (kayak poster jam 12:22), hanya baris headline yg ganti tiap ronde.
function cap(headline) {
  return `🏆 *2026 Naroditsky Memorial Rapid* (7 babak)
🔗 https://chessDOR.com

${headline}

Nobar sambil prediksi lebih asyik 🎯
_Perhatikan jam main tiap babak!_

🎁 *Hadiah utama* (akumulasi semua babak):
🥇 Podium I  : 25K
🥈 Podium II : 20K
🥉 Podium III: 15K
   Podium IV : 10K
   Podium V  : 5K

🎁 *Hadiah tiap babak* (Podium 1/2/3): 10K / 7K / 5K

💰 *TOTAL = Rp229.000*`;
}

// Jadwal: waktu WIB (UTC+7) + teks. Urut waktu.
const SCHEDULE = [
  {
    label: 'PEMBUKA',
    at: '2026-07-03T21:30:00+07:00',
    text:
`🏆 *NOBAR NARODITSKY MEMORIAL — RAPID* 🏆
Nonton bareng + prediksi seru bareng *ChessDOR!* 🎯
Pasang tebakanmu tiap ronde, menangin bagian dari *Rp229.000*!

📅 Malam ini, mulai *22:00 WIB* (7 ronde, sampai dini hari)
🔗 ${LINK}
_Predict • Enjoy • Win_ ♟️`,
  },
  { label: 'R1', at: '2026-07-03T22:00:00+07:00', text:
`♟️ *RONDE 1 GAS!* (22:00 WIB)
Prediksi pertamamu apa? Siapa menang ronde ini? 🎯
👉 ${LINK}` },
  { label: 'R2', at: '2026-07-03T22:50:00+07:00', text:
`♟️ *RONDE 2!* (22:50 WIB)
Udah bener tebakan ronde 1? 😏 Lanjut, poin masih jalan!
🎯 ${LINK}` },
  { label: 'R3', at: '2026-07-03T23:40:00+07:00', text:
`♟️ *RONDE 3!* (23:40 WIB)
Makin malam makin panas 🔥 Amankan prediksimu!
🎯 ${LINK}` },
  { label: 'R4', at: '2026-07-04T01:05:00+07:00', img: true, text:
    cap('⏰ *RONDE 4* mulai *01:20 WIB* — 15 menit lagi!') },
  { label: 'R5', at: '2026-07-04T01:55:00+07:00', img: true, text:
    cap('⏰ *RONDE 5* mulai *02:10 WIB* — 15 menit lagi!') },
  { label: 'R6', at: '2026-07-04T02:45:00+07:00', img: true, text:
    cap('⏰ *RONDE 6* mulai *03:00 WIB* — 15 menit lagi!') },
  { label: 'R7', at: '2026-07-04T03:35:00+07:00', img: true, text:
    cap('🏁 *RONDE 7 (TERAKHIR)* mulai *03:50 WIB* — 15 menit lagi!') },
  { label: 'PENUTUP', at: '2026-07-04T05:00:00+07:00', text:
`🎉 *Selesai! Terima kasih sudah nobar & prediksi bareng ChessDOR!*
Cek hasil prediksi & pemenang di 🔗 ${LINK} ♟️🏆` },
];

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function targetGroups() {
  const res = await fetch(`${GATEWAY}/groups`);
  const j = await res.json();
  return (j.groups || []).filter((g) => !EXCLUDE.some((x) => (g.subject || '').includes(x)));
}

async function broadcast(entry) {
  const groups = await targetGroups();
  console.log(`\n[${new Date().toISOString()}] KIRIM "${entry.label}" -> ${groups.length} grup`);
  for (const g of groups) {
    if (SEND) {
      try {
        const url = entry.img ? `${GATEWAY}/send-image` : `${GATEWAY}/send`;
        const body = entry.img
          ? { jid: g.jid, path: IMG_PATH, caption: entry.text }
          : { jid: g.jid, text: entry.text };
        const r = await fetch(url, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(body),
        });
        const jr = await r.json();
        console.log(`   ${jr.ok ? 'OK ' : 'GAGAL'} ${g.subject}${jr.ok ? '' : ' — ' + (jr.error || '')}`);
      } catch (e) {
        console.log(`   ERR ${g.subject}: ${e.message}`);
      }
    } else {
      console.log(`   [dry]${entry.img ? ' [poster]' : ''} ${g.subject}`);
    }
    await sleep(SEND ? GAP_MS : 50);
  }
}

async function main() {
  console.log(SEND ? '=== MODE KIRIM (AKTIF) ===' : '=== MODE PREVIEW (dry-run, tidak kirim) ===');
  const now = Date.now();
  const groups = await targetGroups().catch(() => []);
  console.log(`Grup tujuan (${groups.length}, sudah skip console):`);
  groups.forEach((g) => console.log(`   - ${g.subject} (${g.size || '?'} anggota)`));

  // Mode "kirim 1 babak sekarang" (--now R3): langsung broadcast, abaikan jadwal.
  if (NOW_LABEL) {
    const e = SCHEDULE.find((x) => x.label.toUpperCase() === NOW_LABEL.toUpperCase());
    if (!e) {
      console.log('Label tidak ada:', NOW_LABEL, '(pilih: ' + SCHEDULE.map((x) => x.label).join(', ') + ')');
      return;
    }
    console.log(`\nKirim SEKARANG: ${e.label} ${SEND ? '(AKTIF)' : '(dry-run)'}`);
    await broadcast(e);
    console.log('\nSelesai. ✅');
    return;
  }

  const pending = [];
  for (const e of SCHEDULE) {
    const t = new Date(e.at).getTime();
    const inMs = t - now;
    const when = new Date(e.at).toLocaleString('id-ID', { timeZone: 'Asia/Jakarta' });
    if (inMs < -60_000) {
      console.log(`\n[LEWAT] ${e.label} @ ${when} WIB (sudah lewat, dilewati)`);
      continue;
    }
    console.log(`\n[DIJADWALKAN] ${e.label} @ ${when} WIB (${Math.round(inMs / 60000)} menit lagi)`);
    pending.push({ e, inMs: Math.max(0, inMs) });
  }

  if (!SEND) {
    console.log('\n(Preview saja. Jalankan dengan --send untuk mengaktifkan.)');
    return;
  }
  if (pending.length === 0) {
    console.log('\nTidak ada jadwal tersisa. Selesai.');
    return;
  }
  console.log(`\nAKTIF. Menunggu ${pending.length} jadwal... (biarkan proses ini jalan)`);
  for (const p of pending) {
    // Tunggu sampai WAKTU ABSOLUT tiap jadwal (hitung ulang dari SEKARANG),
    // supaya tidak numpuk/ngaret walau broadcast sebelumnya makan waktu.
    const waitMs = new Date(p.e.at).getTime() - Date.now();
    if (waitMs > 0) {
      await sleep(waitMs);
    }
    await broadcast(p.e);
  }
  console.log('\nSemua jadwal selesai. ✅');
}

main().catch((e) => console.error('FATAL:', e));
