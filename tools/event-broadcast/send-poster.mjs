/*
 * Kirim POSTER + caption promo Naroditsky Memorial ke semua grup pemain.
 *   node send-poster.mjs         -> preview (tidak kirim)
 *   node send-poster.mjs --send  -> kirim beneran (jeda anti-ban, skip console)
 */

const GATEWAY = 'http://127.0.0.1:3211';
const SEND = process.argv.includes('--send');
const EXCLUDE = ['Konsol Bot', 'Judit Polica WAG'];
const GAP_MS = 25_000;

const IMG_PATH = 'C:\\Users\\dev8\\Downloads\\WhatsApp Image 2026-07-03 at 7.03.45 PM.jpeg';

const CAPTION =
`🏆 *2026 Naroditsky Memorial Rapid* (7 babak)
🔗 https://chessDOR.com — *malam ini!*
⏰ Mulai *22:00 WIB!*

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

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function main() {
  console.log(SEND ? '=== KIRIM POSTER (AKTIF) ===' : '=== PREVIEW (dry-run) ===');
  const res = await fetch(`${GATEWAY}/groups`);
  const j = await res.json();
  const groups = (j.groups || []).filter((g) => !EXCLUDE.some((x) => (g.subject || '').includes(x)));
  console.log(`Poster: ${IMG_PATH}`);
  console.log(`Grup tujuan (${groups.length}, console di-skip):`);
  groups.forEach((g) => console.log(`   - ${g.subject}`));
  if (!SEND) {
    console.log('\n--- CAPTION ---\n' + CAPTION + '\n\n(Preview saja. Tambah --send untuk kirim.)');
    return;
  }
  console.log('');
  for (const g of groups) {
    try {
      const r = await fetch(`${GATEWAY}/send-image`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ jid: g.jid, path: IMG_PATH, caption: CAPTION }),
      });
      const jr = await r.json();
      console.log(`${jr.ok ? 'OK ' : 'GAGAL'} ${g.subject}${jr.ok ? '' : ' — ' + (jr.error || '')}`);
    } catch (e) {
      console.log(`ERR ${g.subject}: ${e.message}`);
    }
    await sleep(GAP_MS);
  }
  console.log('\nSelesai. ✅');
}

main().catch((e) => console.error('FATAL:', e));
