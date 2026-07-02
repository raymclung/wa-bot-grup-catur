// ============================================================================
//  WA Bot - BRAIN (moderasi + puzzle + AI). ASP.NET minimal API, listen :5050.
//
//  CATATAN: file ini DIPULIHKAN dengan men-decompile DLL live pada 2026-06-28
//  (lihat docs/INSIDEN-2026-06-28.md). Komentar asli hilang saat decompile.
//  Identifier sudah di-rapikan sejauh aman:
//    - fungsi lokal  -> nama asli (PostJson, RevealPuzzleAsync, PostPuzzleAsync, ...)
//    - cl_<N>        = closure (capture class) hasil compiler  (dulu CS_cl8_<N>)
//    - DC_<N>_<M>    = tipe display/closure CLASS
//    - lam_<N>       = method lambda
//  PENTING: decompiler meng-INLINE beberapa fungsi lokal jadi DUA salinan
//  (mis. logika handler /incoming ada di lambda 'lam_21' DAN handler kedua yang
//  memakai cl_472). Tiap ubah handler puzzle/moderasi, terapkan ke KEDUA salinan,
//  lalu build-verify. Usahakan tetap ASCII saat menambah string.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Chess;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using Svg.Skia;

public class Program
{
	private static readonly object DmAnnounceLock = new object();

	private static readonly Dictionary<string, DmAnnouncementPending> DmAnnouncePending = new Dictionary<string, DmAnnouncementPending>();

	private sealed class DmAnnouncementPending
	{
		public string TargetJid { get; set; } = "";

		public string TargetName { get; set; } = "";

		public string Text { get; set; } = "";

		public string Kind { get; set; } = "text";

		public string Level { get; set; } = "";

		public JsonElement DeleteKey { get; set; }

		public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
	}

	[CompilerGenerated]
	private sealed class DC_0_0
	{
		public FileSystemWatcher configWatcher;

		public AppConfig config;

		public HttpClient http;

		public string puzzleDailyStatePath;

		public List<PuzzleItem> puzzlePool;

		public object puzzleLock;

		public Dictionary<string, ActivePuzzle> activePuzzles;

		public WebApplication app;

		public List<Rule> rules;

		public HashSet<string> exempt;

		public WarningStore warnings;

		public string pieceAssetsDir;

		public AuditLog audit;

		public DateTime startedAt;

		public string puzzleCacheDir;

		public CooldownTracker cmdCooldown;

		public SessionStore sessions;

		public JoinStore joins;

		public Dictionary<string, ActivePuzzle> puzzleByMsg;

		public FloodTracker floodTracker;

		public string activePuzzlePath;

		public object reloadLock;

		public string configDir;

		public Func<Task?> cd9_48;

		public Func<GroupOption, bool> cd9_56;

		internal void lam_0()
		{
			configWatcher.Dispose();
		}

		internal AppConfig lam_1()
		{
			return config;
		}

		internal AppConfig lam_2()
		{
			return config;
		}

		internal AppConfig lam_3()
		{
			return config;
		}

		internal AppConfig lam_4()
		{
			return config;
		}

		internal async Task<bool> PostImportant(string url, object body)
		{
			bool ok = await PostJson(http, url, body);
			if (!ok && !Sleeper.Asleep)
			{
				RetryQueue.Enqueue(url, body);
			}
			return ok;
		}

		internal void SendTyping(string jid, string channel)
		{
			if (Sleeper.Asleep)
			{
				return;
			}
			try
			{
				string requestUri = ChannelRoute.Base(config, channel) + "/typing";
				http.PostAsync(requestUri, new StringContent(JsonSerializer.Serialize(new
				{
					jid = jid,
					state = "composing"
				}), Encoding.UTF8, "application/json"));
			}
			catch
			{
			}
		}

		internal async Task? lam_7()
		{
			HashSet<string> sentSlots = LoadPuzzleDailyState(puzzleDailyStatePath);
			while (true)
			{
				try
				{
					PuzzleConfig pc = config.Puzzle;
					if (pc != null && pc.Enabled && puzzlePool.Count > 0)
					{
						DC_0_1 cl_7 = new DC_0_1
						{
							nowLocal = DateTime.UtcNow.AddHours(pc.TimezoneOffsetHours)
						};
						string today = cl_7.nowLocal.ToString("yyyy-MM-dd");
						string[] groupJids = pc.GroupJids;
						List<string> dailyTargets = ((groupJids != null && groupJids.Length > 0) ? pc.GroupJids.Where((string s) => !string.IsNullOrWhiteSpace(s)).ToList() : (string.IsNullOrWhiteSpace(pc.GroupJid) ? new List<string>() : new List<string> { pc.GroupJid }));
						PuzzleDailySlot[] dailySlots = pc.DailySlots;
						PuzzleDailySlot[] dailySlots2 = ((dailySlots != null && dailySlots.Length > 0) ? pc.DailySlots : new PuzzleDailySlot[1]
						{
							new PuzzleDailySlot
							{
								Hour = pc.DailyHour,
								RevealMinutes = pc.RevealMinutes,
								MinRating = 0,
								MaxRating = 9999,
								Label = "Harian"
							}
						});
						foreach (PuzzleDailySlot slot in dailySlots2.Where((PuzzleDailySlot s) => s.Hour == cl_7.nowLocal.Hour))
						{
							if (dailyTargets.Count == 0)
							{
								continue;
							}
							HashSet<int> usedIdx = new HashSet<int>();
							using List<string>.Enumerator enumerator2 = dailyTargets.GetEnumerator();
							while (enumerator2.MoveNext())
							{
								DC_0_2 cl_8 = new DC_0_2
								{
									gj = enumerator2.Current
								};
								HashSet<string> activeIds;
								lock (puzzleLock)
								{
									activeIds = (from a in activePuzzles.Values
										where !a.Revealed && a.Jid != cl_8.gj
										select a.Puzzle.Id).ToHashSet();
								}
								string slotKey = $"{today}|{slot.Hour}|{slot.Label}|{cl_8.gj}";
								if (!sentSlots.Contains(slotKey))
								{
									ActivePuzzle curp;
									lock (puzzleLock)
									{
										activePuzzles.TryGetValue(cl_8.gj, out curp);
									}
									if (curp != null && !curp.Revealed)
									{
										await RevealPuzzleAsync(cl_8.gj, curp, true);
									}
									PuzzleItem puzzle = PickPuzzleForSlot(puzzlePool, slot, usedIdx, activeIds);
									await PostPuzzleAsync(cl_8.gj, true, puzzle, slot);
									sentSlots.Add(slotKey);
									SavePuzzleDailyState(puzzleDailyStatePath, sentSlots, today);
									activeIds = null;
									curp = null;
								}
							}
						}
						List<(string jid, ActivePuzzle ap)> due = new List<(string, ActivePuzzle)>();
						lock (puzzleLock)
						{
							foreach (KeyValuePair<string, ActivePuzzle> kv in activePuzzles)
							{
								if (!kv.Value.Revealed && DateTimeOffset.UtcNow.UtcDateTime >= kv.Value.RevealAt)
								{
									due.Add((kv.Key, kv.Value));
								}
							}
						}
						foreach (var (jid, ap) in due)
						{
							await RevealPuzzleAsync(jid, ap, true);
						}
					}
				}
				catch (Exception ex)
				{
					Exception ex2 = ex;
					app.Logger.LogError("Puzzle loop error: {Msg}", ex2.Message);
				}
				await Task.Delay(TimeSpan.FromSeconds(30L));
			}
		}

		internal IResult lam_8()
		{
			return Results.Json(new
			{
				ok = true,
				rules = rules.Count,
				exempt = exempt.Count,
				warned = warnings.Count
			});
		}

		internal async Task<IResult> lam_9(string? q)
		{
			ChessAnalysis.Output o = await ChessAnalysis.Run(q ?? "", config.Ai, http, app.Logger);
			return Results.Json(new
			{
				ok = true,
				engine = StockfishEngine.Available,
				text = o?.Text,
				fen = o?.Fen
			});
		}

		internal async Task<IResult> lam_10(string url, int? flip)
		{
			try
			{
				string fen = BoardVision.RecognizeFen(await http.GetByteArrayAsync(url), pieceAssetsDir, flip == 1);
				return Results.Json(new
				{
					ok = true,
					fen = fen
				});
			}
			catch (Exception ex)
			{
				Exception e = ex;
				return Results.Json(new
				{
					ok = false,
					error = e.Message
				});
			}
		}

		internal async Task<IResult> lam_11()
		{
			bool gw = false;
			try
			{
				gw = (await http.GetStringAsync(config.GatewayUrl + "/health")).Contains("\"connected\":true");
			}
			catch
			{
			}
			int modToday = audit.LinesSince(DateTime.Now.Date).Count((string l) => l.Contains("| HAPUS |") && !l.Contains("aturan=SHADOW:"));
			return Results.Json(new
			{
				ok = true,
				uptimeMinutes = (int)(DateTime.UtcNow - startedAt).TotalMinutes,
				gatewayConnected = gw,
				asleep = Sleeper.Asleep,
				sent = SendLog.Sent,
				failed = SendLog.Failed,
				retryQueue = RetryQueue.Count,
				puzzlePool = puzzlePool.Count,
				activePuzzles = activePuzzles.Count,
				warningsTracked = warnings.Count,
				moderatedToday = modToday,
				rules = rules.Count,
				managedGroups = (config.ManageAllGroups ? (-1) : config.Groups.Count)
			});
		}

		internal bool PanelAuthOk(HttpContext c)
		{
			string adminApiToken = config.AdminApiToken;
			if (string.IsNullOrEmpty(adminApiToken))
			{
				return true;
			}
			string text = c.Request.Headers.Authorization.ToString();
			if (text.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					string text2 = Encoding.UTF8.GetString(Convert.FromBase64String(text.Substring(6).Trim()));
					int num = text2.IndexOf(':');
					string text3 = ((num >= 0) ? text2.Substring(num + 1) : text2);
					if (text3 == adminApiToken)
					{
						return true;
					}
				}
				catch
				{
				}
			}
			return false;
		}

		internal IResult lam_14(HttpContext c)
		{
			return PanelAuthOk(c) ? Results.Json(new
			{
				ok = true,
				manageAll = config.ManageAllGroups,
				groups = Enumerable.Select(config.Groups, (KeyValuePair<string, GroupConfig> kv) => new
				{
					jid = kv.Key,
					label = kv.Value.Label
				}).ToList()
			}) : PanelDeny(c);
		}

		internal IResult lam_15(HttpContext c, int? n)
		{
			IResult result;
			if (!PanelAuthOk(c))
			{
				result = PanelDeny(c);
			}
			else
			{
				AuditLog auditLog = audit;
				int n2;
				switch (n)
				{
				default:
					n2 = 15;
					break;
				case 1:
				case 2:
				case 3:
				case 4:
				case 5:
				case 6:
				case 7:
				case 8:
				case 9:
				case 10:
				case 11:
				case 12:
				case 13:
				case 14:
				case 15:
				case 16:
				case 17:
				case 18:
				case 19:
				case 20:
				case 21:
				case 22:
				case 23:
				case 24:
				case 25:
				case 26:
				case 27:
				case 28:
				case 29:
				case 30:
				case 31:
				case 32:
				case 33:
				case 34:
				case 35:
				case 36:
				case 37:
				case 38:
				case 39:
				case 40:
				case 41:
				case 42:
				case 43:
				case 44:
				case 45:
				case 46:
				case 47:
				case 48:
				case 49:
				case 50:
				case 51:
				case 52:
				case 53:
				case 54:
				case 55:
				case 56:
				case 57:
				case 58:
				case 59:
				case 60:
				case 61:
				case 62:
				case 63:
				case 64:
				case 65:
				case 66:
				case 67:
				case 68:
				case 69:
				case 70:
				case 71:
				case 72:
				case 73:
				case 74:
				case 75:
				case 76:
				case 77:
				case 78:
				case 79:
				case 80:
				case 81:
				case 82:
				case 83:
				case 84:
				case 85:
				case 86:
				case 87:
				case 88:
				case 89:
				case 90:
				case 91:
				case 92:
				case 93:
				case 94:
				case 95:
				case 96:
				case 97:
				case 98:
				case 99:
				case 100:
					n2 = n.Value;
					break;
				}
				result = Results.Json(new
				{
					ok = true,
					lines = auditLog.Tail(n2)
				});
			}
			return result;
		}

		internal IResult lam_16(HttpContext c)
		{
			return PanelAuthOk(c) ? Results.Content("<!doctype html><html lang=id><head><meta charset=utf-8><meta name=viewport content=\"width=device-width,initial-scale=1\">\r\n<title>WA Bot \\u2014 Admin</title><style>\r\nbody{font-family:system-ui,sans-serif;margin:0;background:#0f1216;color:#e6e6e6}\r\nheader{background:#16a34a;color:#fff;padding:12px 16px;font-weight:700}\r\n.wrap{padding:16px;max-width:900px;margin:auto}\r\n.card{background:#1a1f26;border:1px solid #2a2f37;border-radius:10px;padding:14px;margin:12px 0}\r\n.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(120px,1fr));gap:10px}\r\n.metric{background:#11151a;border-radius:8px;padding:10px;text-align:center}\r\n.metric b{display:block;font-size:1.4rem;color:#16a34a}.metric span{font-size:.78rem;color:#9aa4af}\r\nbutton{background:#16a34a;color:#fff;border:0;border-radius:8px;padding:8px 12px;cursor:pointer;font-weight:600;margin:2px}\r\nbutton.warn{background:#b45309}button.alt{background:#374151}\r\ninput,textarea{width:100%;box-sizing:border-box;background:#11151a;border:1px solid #2a2f37;color:#e6e6e6;border-radius:8px;padding:8px;margin:4px 0}\r\ntable{width:100%;border-collapse:collapse;font-size:.82rem}td,th{border-bottom:1px solid #2a2f37;padding:6px;text-align:left}\r\n.ok{color:#16a34a}.bad{color:#ef4444}pre{white-space:pre-wrap;font-size:.78rem;color:#9aa4af;max-height:240px;overflow:auto}\r\nh3{margin:.2rem 0 .6rem}</style></head><body>\r\n<header>\\U0001F916 WA Bot \\u2014 Panel Admin (lokal)</header><div class=wrap>\r\n<div class=card><h3>Status</h3><div class=grid id=metrics>memuat\\u2026</div></div>\r\n<div class=card><h3>Aksi cepat</h3>\r\n<input id=token placeholder=\"Token admin (untuk restart/broadcast)\">\r\n<div><button onclick=reload()>Reload config</button>\r\n<button class=alt onclick=\"restart('brain')\">Restart brain</button>\r\n<button class=alt onclick=\"restart('gateway')\">Restart gateway</button>\r\n<button class=warn onclick=\"restart('both')\">Restart both</button></div>\r\n<div id=msg></div></div>\r\n<div class=card><h3>Broadcast</h3>\r\n<input id=bjid placeholder=\"JID grup tujuan (mis. 1203...@g.us)\">\r\n<textarea id=btext placeholder=\"Isi pesan\"></textarea>\r\n<button onclick=broadcast()>Kirim broadcast</button></div>\r\n<div class=card><h3>Grup dikelola</h3><div id=groups>memuat\\u2026</div></div>\r\n<div class=card><h3>Audit moderasi terbaru</h3><pre id=audit>memuat\\u2026</pre></div></div>\r\n<script>\r\nconst $=s=>document.querySelector(s);\r\n$('#token').value=localStorage.getItem('wabotToken')||'';\r\n$('#token').oninput=e=>localStorage.setItem('wabotToken',e.target.value);\r\nasync function j(u,o){const r=await fetch(u,o);try{return await r.json()}catch{return{status:r.status}}}\r\nasync function refresh(){\r\n const s=await j('/stats');if(s.ok){$('#metrics').innerHTML=[\r\n  ['Gateway',s.gatewayConnected?'<span class=ok>OK</span>':'<span class=bad>OFF</span>'],\r\n  ['Terkirim',s.sent],['Gagal',s.failed],['Antre ulang',s.retryQueue],\r\n  ['Puzzle aktif',s.activePuzzles],['Moderasi/hari',s.moderatedToday],\r\n  ['Peringatan',s.warningsTracked],['Grup',s.managedGroups],['Aturan',s.rules],\r\n  ['Uptime(m)',s.uptimeMinutes],['Tidur',s.asleep?'ya':'tidak']\r\n ].map(m=>`<div class=metric><b>${m[1]}</b><span>${m[0]}</span></div>`).join('')}\r\n const g=await j('/admin/groups');$('#groups').innerHTML='<table><tr><th>Label</th><th>JID</th></tr>'+(g.groups||[]).map(x=>`<tr><td>${x.label||''}</td><td>${x.jid}</td></tr>`).join('')+'</table>';\r\n const a=await j('/admin/audit?n=15');$('#audit').textContent=(a.lines||[]).join('\\n')||'(belum ada)';}\r\nfunction note(t,ok){$('#msg').innerHTML=`<p class=\"${ok?'ok':'bad'}\">${t}</p>`}\r\nasync function reload(){const r=await j('/reload',{method:'POST'});note('Reload: '+(r.ok?'OK':'gagal'),r.ok);refresh()}\r\nasync function restart(t){const k=$('#token').value;if(!k)return note('Isi token dulu',false);note('Mengirim restart '+t+'\\u2026',true);await fetch(`/admin/restart?token=${encodeURIComponent(k)}&target=${t}`,{method:'POST'});setTimeout(()=>{note('Perintah restart '+t+' terkirim.',true);refresh()},1800)}\r\nasync function broadcast(){const k=$('#token').value,jid=$('#bjid').value,text=$('#btext').value;if(!k||!jid||!text)return note('Token, JID, teks wajib diisi',false);const r=await j('/broadcast',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({token:k,jid,text})});note('Broadcast: '+(r.ok?'terkirim \\u2705':('gagal \\u2014 '+(r.error||''))),r.ok)}\r\nrefresh();setInterval(refresh,5000);\r\n</script></body></html>", "text/html; charset=utf-8") : PanelDeny(c);
		}

		internal IResult lam_17()
		{
			ReloadRuntimeConfig("manual /reload");
			return Results.Json(new
			{
				ok = true,
				rules = rules.Count,
				exempt = exempt.Count
			});
		}

		internal async Task<IResult> lam_18(HttpContext ctx)
		{
			if (string.IsNullOrWhiteSpace(config.AdminApiToken))
			{
				return Results.Json(new
				{
					ok = false,
					error = "endpoint mati (set adminApiToken)"
				}, (JsonSerializerOptions?)null, (string?)null, (int?)403);
			}
			if ((string?)ctx.Request.Query["token"] != config.AdminApiToken)
			{
				return Results.Json(new
				{
					ok = false,
					error = "token salah"
				}, (JsonSerializerOptions?)null, (string?)null, (int?)401);
			}
			string target = ctx.Request.Query["target"].ToString();
			if (string.IsNullOrWhiteSpace(target))
			{
				target = "both";
			}
			string text = target;
			bool flag = ((text == "gateway" || text == "both") ? true : false);
			bool doGw = flag;
			text = target;
			flag = ((text == "brain" || text == "both") ? true : false);
			bool doBrain = flag;
			if (doGw)
			{
				try
				{
					await http.PostAsync(config.GatewayUrl + "/admin/restart?token=" + Uri.EscapeDataString(config.AdminApiToken), null);
				}
				catch (Exception ex)
				{
					Exception ex2 = ex;
					app.Logger.LogWarning("Gagal minta gateway restart: {Msg}", ex2.Message);
				}
			}
			if (doBrain)
			{
				Task.Run(async delegate
				{
					await Task.Delay(800);
					app.Logger.LogWarning("Restart brain via /admin/restart");
					Environment.Exit(0);
				});
			}
			return Results.Json(new
			{
				ok = true,
				restarting = new
				{
					brain = doBrain,
					gateway = doGw
				}
			});
		}

		internal async Task? lam_48()
		{
			await Task.Delay(800);
			app.Logger.LogWarning("Restart brain via /admin/restart");
			Environment.Exit(0);
		}

		internal IResult lam_19()
		{
			RelayConfig relay = config.Relay;
			return Results.Json(new
			{
				enabled = (relay?.Enabled ?? false),
				hubGroupJid = (relay?.HubGroupJid ?? ""),
				command = (relay?.Command ?? "sebar"),
				prefix = config.CommandPrefix,
				targetGroups = (relay?.TargetGroups ?? Array.Empty<string>()),
				throttleSeconds = (relay?.ThrottleSeconds ?? 4),
				footer = (relay?.Footer ?? ""),
				adminNumbers = AdminSync.Effective(config)
			});
		}

		internal async Task<IResult> lam_20()
		{
			DC_0_3 cl_13 = new DC_0_3
			{
				cl_1 = this
			};
			AnnouncerConfig announcer = config.Announcer;
			if (announcer == null || !announcer.Enabled || string.IsNullOrWhiteSpace(config.Announcer.TeamId))
			{
				return Results.Json(new
				{
					ok = false,
					error = "announcer tidak aktif / teamId kosong"
				});
			}
			List<SwissItem> list = await Announcer.Fetch(config, http, app.Logger);
			cl_13.now = DateTimeOffset.UtcNow;
			cl_13.reminders = config.Announcer.RemindersMinutes;
			var preview = list.OrderBy((SwissItem t) => t.StartsAt).Select(delegate(SwissItem t)
			{
				DC_0_4 cl_15 = new DC_0_4
				{
					cl_2 = cl_13,
					t = t
				};
				return new
				{
					Name = cl_15.t.Name,
					Id = cl_15.t.Id,
					minutesUntilStart = (int)(cl_15.t.StartsAt - cl_13.now).TotalMinutes,
					dueNow = cl_13.reminders.Where(delegate(int T)
					{
						double totalMinutes = (cl_15.t.StartsAt - cl_15.cl_2.now).TotalMinutes;
						return totalMinutes > 0.0 && totalMinutes <= (double)T && totalMinutes >= (double)(T - 60);
					}).ToArray(),
					sample = Announcer.BuildText(cl_13.cl_1.config, cl_15.t, (cl_13.reminders.Length != 0) ? cl_13.reminders[0] : 300)
				};
			});
			return Results.Json(new
			{
				ok = true,
				count = list.Count,
				tournaments = preview
			});
		}

		internal async Task<IResult> lam_21(IncomingMessage msg)
		{
			DC_0_5 cl_278 = new DC_0_5
			{
				cl_3 = this,
				msg = msg
			};
			if (string.IsNullOrWhiteSpace(cl_278.msg.Text))
			{
				return Results.Json(new
				{
					ok = true,
					action = "ignored"
				});
			}
			if (string.IsNullOrWhiteSpace(cl_278.msg.Jid))
			{
				return Results.Json(new
				{
					ok = true,
					action = "ignored"
				});
			}
			config.Groups.TryGetValue(cl_278.msg.Jid, out cl_278.g);
			bool isPrivate = cl_278.msg.Channel == "whatsapp" && !cl_278.msg.Jid.EndsWith("@g.us");
			PrivateChatConfig? privateChat = config.PrivateChat;
			bool? obj;
			if (privateChat == null)
			{
				obj = null;
			}
			else
			{
				string[] consoleGroupJids = privateChat.ConsoleGroupJids;
				obj = ((consoleGroupJids != null) ? new bool?(((ReadOnlySpan<string>)consoleGroupJids).Contains(cl_278.msg.Jid)) : ((bool?)null));
			}
			bool? flag = obj;
			bool isConsole = flag == true;
			int num;
			if (isPrivate || isConsole)
			{
				PrivateChatConfig privateChat2 = config.PrivateChat;
				if (privateChat2 != null)
				{
					bool enabled = privateChat2.Enabled;
					num = (enabled ? 1 : 0);
				}
				else
				{
					num = 0;
				}
			}
			else
			{
				num = 0;
			}
			bool dmAllowed = (byte)num != 0;
			if (!config.ManageAllGroups && cl_278.g == null && cl_278.msg.Channel == "whatsapp" && !dmAllowed)
			{
				return Results.Json(new
				{
					ok = true,
					action = "unmanaged"
				});
			}
			bool eCommands = cl_278.g?.CommandsEnabled ?? config.CommandsEnabled;
			bool eFlood = cl_278.g?.FloodEnabled ?? config.FloodEnabled;
			bool eModeration = cl_278.g?.ModerationEnabled ?? config.ModerationEnabled;
			string trimmedText = cl_278.msg.Text.TrimStart();
			string cmdText = Regex.Replace(trimmedText, "^(\\s*@\\d+\\s*)+", "").TrimStart();
			bool isCommand = cmdText.StartsWith(config.CommandPrefix);
			string cmdName = (isCommand ? cmdText.Substring(config.CommandPrefix.Length).TrimStart().Split(' ', 2)[0].ToLowerInvariant() : "");
			if (!isCommand && (cl_278.g?.CommandsEnabled ?? config.CommandsEnabled))
			{
				string natCmd = NaturalIntent.Detect(config, cmdText, cl_278.msg.MentionedBot);
				if (natCmd != null)
				{
					isCommand = true;
					cmdText = config.CommandPrefix + natCmd;
					cmdName = natCmd.Split(' ', 2)[0].ToLowerInvariant();
				}
			}
			string senderNum = NumberUtil.Normalize(cl_278.msg.Participant);
			string senderPhone = NumberUtil.Normalize(cl_278.msg.ParticipantPhone);
			HashSet<string> groupExemptSet = (from text5 in (cl_278.g?.ExemptNumbers ?? Array.Empty<string>()).Select(NumberUtil.Normalize)
				where text5.Length > 0
				select text5).ToHashSet();
			bool senderExempt = ModUtil.IdInSet(exempt, senderPhone, senderNum) || ModUtil.IdInSet(groupExemptSet, senderPhone, senderNum);
			QuietHoursConfig quietCfg = cl_278.g?.QuietHours ?? config.QuietHours;
			bool quietNow = QuietHours.IsActive(quietCfg, DateTimeOffset.UtcNow);
			ConvContext ctx = new ConvContext
			{
				ConversationId = cl_278.msg.Jid,
				SenderId = cl_278.msg.Participant,
				SenderNum = senderNum,
				Channel = cl_278.msg.Channel,
				Caps = Caps.Of(cl_278.msg.Channel),
				IsExempt = senderExempt,
				QuietNow = quietNow,
				GroupLabel = (cl_278.g?.Label ?? ""),
				WorkspaceName = (config.Workspace?.Name ?? ""),
				Topic = TopicStore.Get(cl_278.msg.Jid)
			};
			string outBase = ChannelRoute.Base(config, ctx.Channel);
			if (isCommand && (cmdName == "sleep" || cmdName == "wake"))
			{
				if (cmdName == "wake")
				{
					if (!AdminSync.IsAllowed(config, senderNum, senderPhone))
					{
						return Results.Json(new
						{
							ok = true,
							action = "wake-denied"
						});
					}
					Sleeper.Set(false);
					await PostJson(http, outBase + "/send", new
					{
						jid = cl_278.msg.Jid,
						text = "Judit Polica aktif lagi. Siap bertugas."
					});
					return Results.Json(new
					{
						ok = true,
						action = "wake"
					});
				}
				await PostJson(http, outBase + "/send", new
				{
					jid = cl_278.msg.Jid,
					text = "Baik, saya istirahat dulu. Admin bisa membangunkan dengan *!wake*."
				});
				Sleeper.Set(true);
				return Results.Json(new
				{
					ok = true,
					action = "sleep"
				});
			}
			if (Sleeper.Asleep)
			{
				return Results.Json(new
				{
					ok = true,
					action = "asleep"
				});
			}
			string paKey = cl_278.msg.Jid + "|" + senderNum;
			if (PendingAnalysis.Has(paKey))
			{
				string lowPaRaw = cl_278.msg.Text.Trim().ToLowerInvariant();
				bool flipPa = lowPaRaw.Contains("balik") || lowPaRaw.Contains("terbalik") || lowPaRaw.Contains("flip");
				string lowPa = lowPaRaw.Replace("terbalik", "").Replace("balik", "").Replace("flip", "").Trim();
				bool enabled;
				switch (lowPa)
				{
				case "putih":
				case "white":
				case "w":
					enabled = true;
					break;
				default:
					enabled = false;
					break;
				}
				bool? flag2;
				if (enabled)
				{
					flag2 = true;
				}
				else
				{
					bool flag3;
					switch (lowPa)
					{
					case "hitam":
					case "black":
					case "b":
						flag3 = true;
						break;
					default:
						flag3 = false;
						break;
					}
					flag2 = (flag3 ? new bool?(false) : ((bool?)null));
				}
				bool? whitePa = flag2;
				if (whitePa.HasValue)
				{
					string placePa = PendingAnalysis.Take(paKey);
					if (placePa != null)
					{
						if (flipPa) placePa = BoardVision.FlipPlacement(placePa); // sisi Hitam -> putar 180
						SendTyping(cl_278.msg.Jid, ctx.Channel);
						string fenPa = BoardVision.BuildFullFen(placePa, whitePa.Value);
						ChessAnalysis.Output oPa = (await ChessAnalysis.Run(fenPa, config.Ai, http, app.Logger)) ?? new ChessAnalysis.Output("Gagal menganalisa.", "");
						string imgPa = null;
						if (oPa.Fen.Length > 0)
						{
							try
							{
								imgPa = BoardRenderer.Render(oPa.Fen, !oPa.Fen.Contains(" w "), puzzleCacheDir, pieceAssetsDir);
							}
							catch
							{
							}
						}
						if (imgPa == null)
						{
							await PostJson(http, outBase + "/send", new
							{
								jid = cl_278.msg.Jid,
								text = "\ud83d\udcf7 " + oPa.Text
							});
						}
						else
						{
							await PostJson(http, outBase + "/send-image", new
							{
								jid = cl_278.msg.Jid,
								path = imgPa,
								caption = "\ud83d\udcf7 " + oPa.Text
							});
						}
						return Results.Json(new
						{
							ok = true,
							action = "analisa-answered"
						});
					}
				}
			}
			int cooldownSec = cl_278.g?.CommandCooldownSeconds ?? config.CommandCooldownSeconds;
			int num2;
			AiConfig ai;
			if (cooldownSec > 0 && !senderExempt)
			{
				if (!isCommand)
				{
					ai = config.Ai;
					if (ai != null && ai.Enabled && ai.RequireMention)
					{
						num2 = (cl_278.msg.MentionedBot ? 1 : 0);
						goto IL_117f;
					}
				}
				num2 = 0;
				goto IL_117f;
			}
			goto IL_12be;
			IL_ad48:
			object obj3;
			string ftmpl = (string)obj3;
			string number;
			int fcount;
			string ftext = ftmpl.Replace("@user", "@" + number).Replace("{count}", fcount.ToString());
			await PostJson(http, outBase + "/send", new
			{
				jid = cl_278.msg.Jid,
				text = ftext,
				mentions = new string[1] { cl_278.msg.Participant }
			});
			audit.Write(cl_278.msg.Jid, cl_278.msg.Participant, cl_278.msg.PushName, "flood", fcount, cl_278.msg.Text);
			app.Logger.LogInformation("FLOOD dari {Number}, peringatan ke-{Count}", number, fcount);
			return Results.Json(new
			{
				ok = true,
				action = "flood",
				warned = true,
				warnCount = fcount
			});
			IL_117f:
			bool aiMention = (byte)num2 != 0;
			if (isCommand && cmdName != "batal" && !cmdCooldown.Allow($"{cl_278.msg.Jid}|{senderNum}|{cmdName}", cooldownSec))
			{
				return Results.Json(new
				{
					ok = true,
					action = "cooldown",
					cmd = cmdName
				});
			}
			if (aiMention && !cmdCooldown.Allow(cl_278.msg.Jid + "|" + senderNum + "|@ai", cooldownSec))
			{
				return Results.Json(new
				{
					ok = true,
					action = "cooldown",
					cmd = "ai"
				});
			}
			goto IL_12be;
			IL_12be:
			if (!isCommand && Regex.IsMatch(cl_278.msg.Text, "\\b(report|lapor|blokir|ban)\\b.*\\b(bot|nomor|number|wa)\\b|\\b(bot|nomor|number|wa)\\b.*\\b(report|lapor|blokir|ban)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
			{
				if (quietNow)
				{
					return Results.Json(new
					{
						ok = true,
						action = "quiet-antireport"
					});
				}
				await PostJson(http, outBase + "/send", new
				{
					jid = cl_278.msg.Jid,
					text = "Bot bermasalah? Jangan report nomor. Ketik " + config.CommandPrefix + "admin <kendala>."
				});
				return Results.Json(new
				{
					ok = true,
					action = "anti-report"
				});
			}
			PrivateChatConfig pcDM = default(PrivateChatConfig);
			int num3;
			if (isPrivate || isConsole)
			{
				pcDM = config.PrivateChat;
				if (pcDM != null && pcDM.Enabled)
				{
					ai = config.Ai;
					if (ai != null)
					{
						bool flag3 = ai.Enabled;
						num3 = (flag3 ? 1 : 0);
					}
					else
					{
						num3 = 0;
					}
					goto IL_1457;
				}
			}
			num3 = 0;
			goto IL_1457;
			IL_a94f:
			object obj4;
			string warnTmpl = (string)obj4;
			Rule matched;
			int count;
			string warnText = warnTmpl.Replace("@user", "@" + number).Replace("{reason}", matched.Reason ?? matched.Name ?? "aturan grup").Replace("{count}", count.ToString());
			await PostJson(http, outBase + "/send", new
			{
				jid = cl_278.msg.Jid,
				text = warnText,
				mentions = new string[1] { cl_278.msg.Participant }
			});
			goto IL_aa9d;
			IL_195b:
			object obj5;
			string consoleJid = (string)obj5;
			string replyDM;
			if (isConsole)
			{
				await PostJson(http, outBase + "/send", new
				{
					jid = cl_278.msg.Jid,
					text = replyDM
				});
				return Results.Json(new
				{
					ok = true,
					action = "console-chat"
				});
			}
			string qDM;
			string replyJidDM = ((senderPhone.Length > 0) ? (senderPhone + "@s.whatsapp.net") : cl_278.msg.Jid);
			bool directSentDM = await PostJson(http, outBase + "/send", new
			{
				jid = replyJidDM,
				text = replyDM
			});
			if (!string.IsNullOrWhiteSpace(consoleJid))
			{
				string who = ((!string.IsNullOrWhiteSpace(cl_278.msg.PushName)) ? cl_278.msg.PushName : ("@" + senderNum));
				string head = ((qDM.Length > 0) ? $"\ud83d\udce9 *DM dari {who}:* {qDM}\n\n" : ("\ud83d\udce9 *DM dari " + who + "*\n\n"));
				await PostJson(http, outBase + "/send", new
				{
					jid = consoleJid,
					text = head + replyDM
				});
				return Results.Json(new
				{
					ok = true,
					action = "dm-chat-console-copy",
					replyJid = replyJidDM,
					directSent = directSentDM,
					consoleJid = consoleJid
				});
			}
			return Results.Json(new
			{
				ok = true,
				action = "dm-chat",
				replyJid = replyJidDM,
				directSent = directSentDM
			});
			IL_aa9d:
			audit.Write(cl_278.msg.Jid, cl_278.msg.Participant, cl_278.msg.PushName, matched.Id, count, cl_278.msg.Text);
			app.Logger.LogInformation("HAPUS dari {Number} (aturan {Rule}), peringatan ke-{Count}{Quiet}", number, matched.Id, count, quietNow ? " [jam tenang]" : "");
			return Results.Json(new
			{
				ok = true,
				action = "moderated",
				rule = matched.Id,
				warnCount = count,
				quiet = quietNow
			});
			IL_1457:
			if (num3 != 0)
			{
				if (!PrivateChatAccess.IsAllowed(config, pcDM, senderNum, senderPhone))
				{
					return Results.Json(new
					{
						ok = true,
						action = "dm-not-allowed"
					});
				}
				if (cooldownSec > 0 && !cmdCooldown.Allow(cl_278.msg.Jid + "|dm", Math.Max(cooldownSec, 3)))
				{
					return Results.Json(new
					{
						ok = true,
						action = "dm-cooldown"
					});
				}
				if (quietNow)
				{
					return Results.Json(new
					{
						ok = true,
						action = "dm-quiet"
					});
				}
				SendTyping(cl_278.msg.Jid, ctx.Channel);
				string memKeyDM = cl_278.msg.Jid + "|dm";
				string convDM = ConvMemory.Recent(memKeyDM);
				string personaDM = (string.IsNullOrWhiteSpace(pcDM.Persona) ? "" : pcDM.Persona);
				qDM = cmdText.Trim();
				string? adminDmReply = await TryHandleDmAnnouncement(config, http, cl_278.msg, senderNum, senderPhone, qDM, app.Logger, audit, config.Puzzle.RevealMinutes, puzzlePool.Count, async (jid, level) => { await PostPuzzleAsync(jid, false, null, PuzzleMove.DifficultySlot("puzzle " + level, config.Puzzle.RevealMinutes)); return true; }, async (jid) => { ActivePuzzle ap; lock (puzzleLock) { activePuzzles.TryGetValue(jid, out ap); } if (ap == null) return false; await RevealPuzzleAsync(jid, ap, false); return true; }, () => { lock (puzzleLock) { return BuildActivePuzzleSummary(activePuzzles); } });
				if (adminDmReply != null)
				{
					replyDM = adminDmReply;
				}
				else
				{
					switch ((qDM.Length != 0) ? ChatIntents.Classify(qDM) : ChatIntent.Empty)
					{
					case ChatIntent.Schedule:
						replyDM = await CommandHandler.BuildSchedule(config, http, app.Logger);
						break;
					case ChatIntent.Result:
						replyDM = await CommandHandler.BuildLatestResult(config, http, app.Logger);
						break;
					default:
					{
						string ansDM = await Ai.Ask(config.Ai, http, (qDM.Length == 0) ? "Halo" : qDM, app.Logger, personaDM, convDM);
						replyDM = (string.IsNullOrWhiteSpace(ansDM) ? "Maaf, aku lagi belum bisa menjawab. Coba lagi sebentar ya." : ansDM);
						if (replyDM.Length > config.Ai.MaxOutputChars)
						{
							replyDM = replyDM.Substring(0, config.Ai.MaxOutputChars) + "…";
						}
						break;
					}
					}
				}
				if (qDM.Length > 0)
				{
					ConvMemory.Append(memKeyDM, "user", qDM);
					ConvMemory.Append(memKeyDM, "assistant", replyDM);
				}
				string[] cgs = config.PrivateChat?.ConsoleGroupJids;
				if (cgs != null)
				{
					int num4 = cgs.Length;
					if (num4 > 0 && !string.IsNullOrWhiteSpace(cgs[0]))
					{
						obj5 = cgs[0];
						goto IL_195b;
					}
				}
				obj5 = config.AdminSyncGroupJid;
				goto IL_195b;
			}
			RelayConfig relay = config.Relay;
			if (relay != null && relay.Enabled && cl_278.msg.Jid == config.Relay.HubGroupJid)
			{
				string sessKey = cl_278.msg.Participant;
				BroadcastSession sess;
				lock (sessions.BroadcastLock)
				{
					sessions.Broadcast.TryGetValue(sessKey, out sess);
					if (sess != null && (DateTimeOffset.UtcNow - sess.CreatedAt).TotalMinutes > 5.0)
					{
						sessions.Broadcast.Remove(sessKey);
						sess = null;
					}
				}
				string firstWord = (isCommand ? cmdText.Substring(config.CommandPrefix.Length).Split(' ', 2)[0].ToLowerInvariant() : "");
				if (isCommand && firstWord == "batal")
				{
					bool had;
					lock (sessions.BroadcastLock)
					{
						had = sessions.Broadcast.Remove(sessKey);
					}
					if (had)
					{
						await PostJson(http, outBase + "/send", new
						{
							jid = cl_278.msg.Jid,
							text = "Siap, proses sebar saya batalkan."
						});
					}
					return Results.Json(new
					{
						ok = true,
						action = "relay-cancel"
					});
				}
				if (isCommand && (firstWord == config.Relay.Command.ToLowerInvariant() || firstWord == "announcement" || firstWord == "umumkan"))
				{
					if (!AdminSync.IsAllowed(config, senderNum, senderPhone))
					{
						await PostJson(http, outBase + "/send", new
						{
							jid = cl_278.msg.Jid,
							text = "Fitur sebar khusus admin."
						});
						return Results.Json(new
						{
							ok = true,
							action = "relay-denied"
						});
					}
					string[] cmdParts = cmdText.Substring(config.CommandPrefix.Length).Split(' ', 2);
					string inlineText = ((cmdParts.Length > 1) ? cmdParts[1].Trim() : "");
					if ((firstWord == "announcement" || firstWord == "umumkan") && inlineText.Length > 0)
					{
						DC_0_6 cl_261 = new DC_0_6
						{
							cl_4 = cl_278,
							targets = (config.Relay.TargetGroups ?? Array.Empty<string>()).Where((string value) => !string.IsNullOrWhiteSpace(value)).ToList()
						};
						if (cl_261.targets.Count == 0)
						{
							await PostJson(http, outBase + "/send", new
							{
								jid = cl_261.cl_4.msg.Jid,
								text = "Belum ada grup tujuan."
							});
							return Results.Json(new
							{
								ok = true,
								action = "announcement-notarget"
							});
						}
						cl_261.outText = (string.IsNullOrWhiteSpace(config.Relay.Footer) ? inlineText : (inlineText + "\n\n" + config.Relay.Footer));
						cl_261.throttleMs = Math.Max(0, config.Relay.ThrottleSeconds) * 1000;
						cl_261.hubJid = cl_261.cl_4.msg.Jid;
						Task.Run(async delegate
						{
							int okCount = 0;
							foreach (string tj in cl_261.targets)
							{
								try
								{
									if (await PostJson(cl_261.cl_4.cl_3.http, ChannelRoute.BaseForJid(cl_261.cl_4.cl_3.config, tj) + "/send", new
									{
										jid = tj,
										text = cl_261.outText
									}))
									{
										okCount++;
									}
								}
								catch (Exception ex)
								{
									cl_261.cl_4.cl_3.app.Logger.LogError("Announcement gagal ke {Jid}: {Msg}", tj, ex.Message);
								}
								if (cl_261.throttleMs > 0)
								{
									await Task.Delay(cl_261.throttleMs);
								}
							}
							await PostJson(cl_261.cl_4.cl_3.http, ChannelRoute.BaseForJid(cl_261.cl_4.cl_3.config, cl_261.hubJid) + "/send", new
							{
								jid = cl_261.hubJid,
								text = $"Announcement terkirim ke {okCount}/{cl_261.targets.Count} grup."
							});
						});
						await PostJson(http, outBase + "/send", new
						{
							jid = cl_261.cl_4.msg.Jid,
							text = $"Mengirim announcement ke {cl_261.targets.Count} grup..."
						});
						return Results.Json(new
						{
							ok = true,
							action = "announcement-send",
							targets = cl_261.targets.Count
						});
					}
					lock (sessions.BroadcastLock)
					{
						sessions.Broadcast[sessKey] = new BroadcastSession
						{
							Stage = "text"
						};
					}
					await PostJson(http, outBase + "/send", new
					{
						jid = cl_278.msg.Jid,
						text = "Mau sebar pesan apa? Ketik pesannya. (!batal untuk batal)"
					});
					return Results.Json(new
					{
						ok = true,
						action = "relay-start"
					});
				}
				if (sess != null && !isCommand)
				{
					if (sess.Stage == "text")
					{
						List<GroupOption> opts = (await FetchGroups(config.GatewayUrl, http)).Where((GroupOption o) => o.Jid != config.Relay.HubGroupJid && o.Jid.Length > 0).ToList();
						lock (sessions.BroadcastLock)
						{
							sess.Text = cl_278.msg.Text.Trim();
							sess.Options = opts;
							sess.Stage = "targets";
							sess.CreatedAt = DateTimeOffset.UtcNow;
						}
						await PostJson(http, outBase + "/send", new
						{
							jid = cl_278.msg.Jid,
							text = TargetPrompt(opts)
						});
						return Results.Json(new
						{
							ok = true,
							action = "relay-text"
						});
					}
					if (sess.Stage == "targets")
					{
						DC_0_7 cl_274 = new DC_0_7
						{
							cl_5 = cl_278
						};
						List<GroupOption> chosen = ParseSelection(cl_274.cl_5.msg.Text, sess.Options);
						if (chosen.Count == 0)
						{
							await PostJson(http, outBase + "/send", new
							{
								jid = cl_274.cl_5.msg.Jid,
								text = "Saya belum menangkap pilihan. Balas nomor, semua, atau !batal."
							});
							return Results.Json(new
							{
								ok = true,
								action = "relay-retry"
							});
						}
						string textToSend = sess.Text;
						lock (sessions.BroadcastLock)
						{
							sessions.Broadcast.Remove(sessKey);
						}
						cl_274.outText = (string.IsNullOrWhiteSpace(config.Relay.Footer) ? textToSend : (textToSend + "\n\n" + config.Relay.Footer));
						cl_274.hubJid = cl_274.cl_5.msg.Jid;
						cl_274.throttleMs = Math.Max(0, config.Relay.ThrottleSeconds) * 1000;
						cl_274.targetJids = chosen.Select((GroupOption groupOption) => groupOption.Jid).ToList();
						Task.Run(async delegate
						{
							int okCount = 0;
							foreach (string tj in cl_274.targetJids)
							{
								try
								{
									if (await PostJson(cl_274.cl_5.cl_3.http, ChannelRoute.BaseForJid(cl_274.cl_5.cl_3.config, tj) + "/send", new
									{
										jid = tj,
										text = cl_274.outText
									}))
									{
										okCount++;
									}
									else
									{
										cl_274.cl_5.cl_3.app.Logger.LogWarning("Relay gagal (gateway tolak) ke {Jid}", tj);
									}
								}
								catch (Exception ex)
								{
									cl_274.cl_5.cl_3.app.Logger.LogError("Relay gagal ke {Jid}: {Msg}", tj, ex.Message);
								}
								if (cl_274.throttleMs > 0)
								{
									await Task.Delay(cl_274.throttleMs);
								}
							}
							await PostJson(cl_274.cl_5.cl_3.http, ChannelRoute.BaseForJid(cl_274.cl_5.cl_3.config, cl_274.hubJid) + "/send", new
							{
								jid = cl_274.hubJid,
								text = $"Selesai menyebar ke {okCount}/{cl_274.targetJids.Count} grup."
							});
						});
						await PostJson(http, outBase + "/send", new
						{
							jid = cl_274.cl_5.msg.Jid,
							text = $"Menyebar ke {cl_274.targetJids.Count} grup (jeda {config.Relay.ThrottleSeconds} dtk)..."
						});
						return Results.Json(new
						{
							ok = true,
							action = "relay-send",
							targets = cl_274.targetJids.Count
						});
					}
				}
			}
			string ssKey = cl_278.msg.Jid + "|" + cl_278.msg.Participant;
			StandingsSession ss;
			lock (sessions.StandingsLock)
			{
				sessions.Standings.TryGetValue(ssKey, out ss);
				if (ss != null && (DateTimeOffset.UtcNow - ss.CreatedAt).TotalMinutes > 3.0)
				{
					sessions.Standings.Remove(ssKey);
					ss = null;
				}
			}
			string sFirst = (isCommand ? cmdText.Substring(config.CommandPrefix.Length).Split(' ', 2)[0].ToLowerInvariant() : "");
			if (eCommands && isCommand && (sFirst == "standings" || sFirst == "klasemen"))
			{
				string[] sp = cmdText.Substring(config.CommandPrefix.Length).Split(' ', 2);
				if (sp.Length > 1 && int.TryParse(sp[1].Trim(), out var sid))
				{
					string r = await CommandHandler.BuildStandings(sid, http, app.Logger);
					await PostJson(http, outBase + "/send", new
					{
						jid = cl_278.msg.Jid,
						text = r
					});
					return Results.Json(new
					{
						ok = true,
						action = "standings"
					});
				}
				List<(string url, string swiss, string name, string date)> recent = await CommandHandler.GetRecentTournaments(http, app.Logger, 5);
				if (recent.Count == 0)
				{
					await PostJson(http, outBase + "/send", new
					{
						jid = cl_278.msg.Jid,
						text = "Daftar belum terbaca. Coba " + config.CommandPrefix + "standings <id>."
					});
					return Results.Json(new
					{
						ok = true,
						action = "standings-nolist"
					});
				}
				lock (sessions.StandingsLock)
				{
					sessions.Standings[ssKey] = new StandingsSession
					{
						Options = recent
					};
				}
				StringBuilder sb = new StringBuilder();
				sb.AppendLine("Klasemen turnamen mana? Balas nomornya:");
				for (int i = 0; i < recent.Count; i++)
				{
					StringBuilder stringBuilder = sb;
					StringBuilder stringBuilder2 = stringBuilder;
					StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(2, 3, stringBuilder);
					handler.AppendFormatted(i + 1);
					handler.AppendLiteral(". ");
					handler.AppendFormatted(recent[i].name);
					handler.AppendFormatted(string.IsNullOrEmpty(recent[i].date) ? "" : (" (" + recent[i].date + ")"));
					stringBuilder2.AppendLine(ref handler);
				}
				sb.Append("(ketik !batal untuk membatalkan)");
				await PostJson(http, outBase + "/send", new
				{
					jid = cl_278.msg.Jid,
					text = sb.ToString()
				});
				return Results.Json(new
				{
					ok = true,
					action = "standings-list"
				});
			}
			if (ss != null && isCommand && sFirst == "batal")
			{
				lock (sessions.StandingsLock)
				{
					sessions.Standings.Remove(ssKey);
				}
				await PostJson(http, outBase + "/send", new
				{
					jid = cl_278.msg.Jid,
					text = "Siap, saya batalkan."
				});
				return Results.Json(new
				{
					ok = true,
					action = "standings-cancel"
				});
			}
			if (ss != null && !isCommand && int.TryParse(cl_278.msg.Text.Trim(), out var pick) && pick >= 1 && pick <= ss.Options.Count)
			{
				(string url, string swiss, string name, string date) chosen2 = ss.Options[pick - 1];
				lock (sessions.StandingsLock)
				{
					sessions.Standings.Remove(ssKey);
				}
				string r2 = await CommandHandler.BuildStandingsSmart(chosen2.url, chosen2.swiss, chosen2.name, http, app.Logger);
				await PostJson(http, outBase + "/send", new
				{
					jid = cl_278.msg.Jid,
					text = r2
				});
				return Results.Json(new
				{
					ok = true,
					action = "standings-pick"
				});
			}
			CclConfig ccl = config.Ccl;
			int num5;
			if (ccl != null)
			{
				bool flag3 = ccl.Enabled;
				num5 = (flag3 ? 1 : 0);
			}
			else
			{
				num5 = 0;
			}
			if (num5 != 0)
			{
				string csKey = cl_278.msg.Jid + "|" + cl_278.msg.Participant;
				CclSession cs;
				lock (sessions.CclLock)
				{
					sessions.Ccl.TryGetValue(csKey, out cs);
					if (cs != null && (DateTimeOffset.UtcNow - cs.CreatedAt).TotalMinutes > 3.0)
					{
						sessions.Ccl.Remove(csKey);
						cs = null;
					}
				}
				string cFirst = (isCommand ? cmdText.Substring(config.CommandPrefix.Length).Split(' ', 2)[0].ToLowerInvariant() : "");
				string cclCmd = (string.IsNullOrWhiteSpace(config.Ccl.Command) ? "events" : config.Ccl.Command.ToLowerInvariant());
				if (eCommands && isCommand && (cFirst == cclCmd || cFirst == "events" || cFirst == "ccl"))
				{
					(List<CclEvent> upcoming, List<CclEvent> past) tuple = await Ccl.GetEvents(config.Ccl, http, app.Logger);
					List<CclEvent> up = tuple.upcoming;
					List<CclEvent> past = tuple.past;
					List<CclEvent> opts2 = new List<CclEvent>();
					opts2.AddRange(up.OrderBy((CclEvent e) => e.Start).Take(8));
					opts2.AddRange(past.Take(8));
					if (opts2.Count == 0)
					{
						await PostJson(http, outBase + "/send", new
						{
							jid = cl_278.msg.Jid,
							text = "Daftar event belum bisa saya ambil sekarang. Silakan coba lagi nanti ya."
						});
						return Results.Json(new
						{
							ok = true,
							action = "ccl-nolist"
						});
					}
					lock (sessions.CclLock)
					{
						sessions.Ccl[csKey] = new CclSession
						{
							Options = opts2
						};
					}
					await PostJson(http, outBase + "/send", new
					{
						jid = cl_278.msg.Jid,
						text = Ccl.BuildList(config.Ccl, opts2)
					});
					return Results.Json(new
					{
						ok = true,
						action = "ccl-list"
					});
				}
				if (cs != null && isCommand && cFirst == "batal")
				{
					lock (sessions.CclLock)
					{
						sessions.Ccl.Remove(csKey);
					}
					await PostJson(http, outBase + "/send", new
					{
						jid = cl_278.msg.Jid,
						text = "Siap, saya batalkan."
					});
					return Results.Json(new
					{
						ok = true,
						action = "ccl-cancel"
					});
				}
				if (cs != null && !isCommand && int.TryParse(cl_278.msg.Text.Trim(), out var cpick) && cpick >= 1 && cpick <= cs.Options.Count)
				{
					CclEvent chosen3 = cs.Options[cpick - 1];
					lock (sessions.CclLock)
					{
						sessions.Ccl.Remove(csKey);
					}
					string r3 = await Ccl.BuildView(config.Ccl, chosen3, http, app.Logger);
					await PostJson(http, outBase + "/send", new
					{
						jid = cl_278.msg.Jid,
						text = r3
					});
					return Results.Json(new
					{
						ok = true,
						action = "ccl-pick"
					});
				}
			}
			ai = config.Ai;
			int num6;
			if (ai != null)
			{
				bool flag3 = ai.Enabled;
				num6 = (flag3 ? 1 : 0);
			}
			else
			{
				num6 = 0;
			}
			if (num6 != 0)
			{
				string aiQuestion = null;
				if (isCommand)
				{
					string[] parts = cmdText.Substring(config.CommandPrefix.Length).Split(' ', 2);
					if (((ReadOnlySpan<string>)config.Ai.Commands).Contains(parts[0].ToLowerInvariant()))
					{
						aiQuestion = ((parts.Length > 1) ? parts[1].Trim() : "");
					}
				}
				else if (config.Ai.RequireMention && cl_278.msg.MentionedBot)
				{
					aiQuestion = cmdText.Trim();
				}
				if (aiQuestion != null)
				{
					if (quietNow)
					{
						if (!string.IsNullOrWhiteSpace(config.QuietHours?.Notice))
						{
							await PostJson(http, outBase + "/send", new
							{
								jid = cl_278.msg.Jid,
								text = config.QuietHours.Notice
							});
						}
						return Results.Json(new
						{
							ok = true,
							action = "quiet-ai"
						});
					}
					string asker = NumberUtil.Normalize(cl_278.msg.Participant);
					SendTyping(cl_278.msg.Jid, ctx.Channel);
					string memKey = cl_278.msg.Jid + "|" + asker;
					string convHistory = ConvMemory.Recent(memKey);
					string reply;
					switch ((aiQuestion.Length != 0) ? ChatIntents.Classify(aiQuestion) : ChatIntent.Empty)
					{
					case ChatIntent.Empty:
						reply = "Bisa. Format: !tanya <pertanyaan>.";
						break;
					case ChatIntent.Result:
						reply = await CommandHandler.BuildLatestResult(config, http, app.Logger);
						break;
					case ChatIntent.Schedule:
					{
						string sched = await CommandHandler.BuildSchedule(config, http, app.Logger);
						string hint = cl_278.g?.EventsHint ?? "Info & hasil turnamen: https://ligacatur.com/";
						reply = sched + (string.IsNullOrWhiteSpace(hint) ? "" : ("\n\n" + hint));
						break;
					}
					default:
					{
						WorkspaceConfig ws = config.Workspace;
						string wsSuffix = ((ws != null && !string.IsNullOrWhiteSpace(ws.Scope)) ? ("[Workspace: " + ws.Name + "] " + ws.Scope) : "");
						if (!string.IsNullOrWhiteSpace(ctx.Topic))
						{
							wsSuffix = wsSuffix + "\n\nKonteks: percakapan terakhir di chat ini bertema \"" + ctx.Topic + "\". Jaga kesinambungan bila relevan.";
						}
						string ans = await Ai.Ask(config.Ai, http, aiQuestion, app.Logger, wsSuffix, convHistory);
						reply = (string.IsNullOrWhiteSpace(ans) ? "Maaf, saya belum bisa menjawab dengan baik sekarang. Silakan coba lagi sebentar lagi, atau ketik !help untuk daftar perintah." : ans);
						if (reply.Length > config.Ai.MaxOutputChars)
						{
							reply = reply.Substring(0, config.Ai.MaxOutputChars) + "…";
						}
						break;
					}
					}
					if (aiQuestion.Length > 0)
					{
						ConvMemory.Append(memKey, "user", aiQuestion);
						ConvMemory.Append(memKey, "assistant", reply);
					}
					string jid = cl_278.msg.Jid;
					ChatIntent chatIntent = ChatIntents.Classify(aiQuestion);
					if (1 == 0)
					{
					}
					string topic = chatIntent switch
					{
						ChatIntent.Result => "hasil", 
						ChatIntent.Schedule => "jadwal", 
						ChatIntent.Empty => ctx.Topic, 
						_ => "obrolan-catur", 
					};
					if (1 == 0)
					{
					}
					TopicStore.Set(jid, topic);
					await PostJson(http, outBase + "/send", new
					{
						jid = cl_278.msg.Jid,
						text = "@" + asker + " " + reply,
						mentions = new string[1] { cl_278.msg.Participant }
					});
					return Results.Json(new
					{
						ok = true,
						action = "ai"
					});
				}
			}
			if (eCommands && isCommand && cmdName == "status")
			{
				if (!AdminSync.IsAllowed(config, senderNum, senderPhone))
				{
					return Results.Json(new
					{
						ok = true,
						action = "status-denied"
					});
				}
				string gw;
				try
				{
					gw = ((await http.GetStringAsync(config.GatewayUrl + "/health")).Contains("\"connected\":true") ? "tersambung ✅" : "TIDAK tersambung ⚠\ufe0f");
				}
				catch
				{
					gw = "TIDAK responsif ⚠\ufe0f";
				}
				StringBuilder st = new StringBuilder();
				st.AppendLine("\ud83e\ude7a *Status Bot*");
				StringBuilder stringBuilder = st;
				StringBuilder stringBuilder3 = stringBuilder;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(47, 2, stringBuilder);
				handler.AppendLiteral("• Brain: hidup ✅ (");
				handler.AppendFormatted(rules.Count);
				handler.AppendLiteral(" aturan, ");
				handler.AppendFormatted(warnings.Count);
				handler.AppendLiteral(" riwayat peringatan)");
				stringBuilder3.AppendLine(ref handler);
				stringBuilder = st;
				StringBuilder stringBuilder4 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder);
				handler.AppendLiteral("• Gateway/WhatsApp: ");
				handler.AppendFormatted(gw);
				stringBuilder4.AppendLine(ref handler);
				stringBuilder = st;
				StringBuilder stringBuilder5 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder);
				handler.AppendLiteral("• Reminder Lichess: ");
				AnnouncerConfig announcer = config.Announcer;
				handler.AppendFormatted((announcer != null && announcer.Enabled) ? "aktif" : "mati");
				stringBuilder5.AppendLine(ref handler);
				stringBuilder = st;
				StringBuilder stringBuilder6 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(23, 1, stringBuilder);
				handler.AppendLiteral("• Event chess.college: ");
				ccl = config.Ccl;
				handler.AppendFormatted((ccl != null && ccl.Enabled) ? "aktif" : "mati");
				stringBuilder6.AppendLine(ref handler);
				stringBuilder = st;
				StringBuilder stringBuilder7 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(23, 1, stringBuilder);
				handler.AppendLiteral("• Jam tenang sekarang: ");
				handler.AppendFormatted(quietNow ? "AKTIF" : "tidak");
				stringBuilder7.AppendLine(ref handler);
				stringBuilder = st;
				StringBuilder stringBuilder8 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder);
				handler.AppendLiteral("• Grup dikelola: ");
				handler.AppendFormatted(config.ManageAllGroups ? "semua" : config.Groups.Count.ToString());
				stringBuilder8.AppendLine(ref handler);
				int modTodaySt = audit.LinesSince(DateTime.Now.Date).Count((string l) => l.Contains("| HAPUS |") && !l.Contains("aturan=SHADOW:"));
				stringBuilder = st;
				StringBuilder stringBuilder9 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(23, 1, stringBuilder);
				handler.AppendLiteral("• Dimoderasi hari ini: ");
				handler.AppendFormatted(modTodaySt);
				stringBuilder9.AppendLine(ref handler);
				st.AppendLine($"• Kirim: {SendLog.Sent} ok / {SendLog.Failed} gagal" + ((RetryQueue.Count > 0) ? $" (antre ulang: {RetryQueue.Count})" : ""));
				stringBuilder = st;
				StringBuilder stringBuilder10 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(16, 1, stringBuilder);
				handler.AppendLiteral("• Puzzle aktif: ");
				handler.AppendFormatted(activePuzzles.Count);
				stringBuilder10.AppendLine(ref handler);
				stringBuilder = st;
				StringBuilder stringBuilder11 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(28, 1, stringBuilder);
				handler.AppendLiteral("• Admin terdaftar (!sebar): ");
				handler.AppendFormatted(config.AdminNumbers.Length);
				stringBuilder11.Append(ref handler);
				await PostJson(http, outBase + "/send", new
				{
					jid = cl_278.msg.Jid,
					text = st.ToString()
				});
				return Results.Json(new
				{
					ok = true,
					action = "status"
				});
			}
			if (eCommands && isCommand && (cmdName == "warnings" || cmdName == "pelanggar"))
			{
				if (!AdminSync.IsAllowed(config, senderNum, senderPhone))
				{
					return Results.Json(new
					{
						ok = true,
						action = "warnings-denied"
					});
				}
				List<(string num, int count)> top = warnings.TopForGroup(cl_278.msg.Jid, 10);
				StringBuilder wb = new StringBuilder();
				wb.AppendLine("*Catatan moderasi terbanyak (grup ini)*");
				StringBuilder stringBuilder;
				StringBuilder.AppendInterpolatedStringHandler handler;
				if (top.Count == 0)
				{
					wb.AppendLine("Belum ada catatan moderasi.");
				}
				else
				{
					int i2 = 1;
					foreach (var item in top)
					{
						string num7 = item.num;
						int c = item.count;
						stringBuilder = wb;
						StringBuilder stringBuilder12 = stringBuilder;
						handler = new StringBuilder.AppendInterpolatedStringHandler(6, 3, stringBuilder);
						handler.AppendFormatted(i2++);
						handler.AppendLiteral(". ");
						handler.AppendFormatted(num7);
						handler.AppendLiteral(" — ");
						handler.AppendFormatted(c);
						handler.AppendLiteral("×");
						stringBuilder12.AppendLine(ref handler);
					}
				}
				wb.AppendLine();
				stringBuilder = wb;
				StringBuilder stringBuilder13 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(141, 3, stringBuilder);
				handler.AppendLiteral("Total catatan tersimpan (semua grup): ");
				handler.AppendFormatted(warnings.Count);
				handler.AppendLiteral(". Ketik ");
				handler.AppendFormatted(config.CommandPrefix);
				handler.AppendLiteral("audit untuk 10 tindakan terakhir, atau ");
				handler.AppendFormatted(config.CommandPrefix);
				handler.AppendLiteral("percaya (balas pesan) untuk membuka akses/reset catatan.");
				stringBuilder13.Append(ref handler);
				await PostJson(http, outBase + "/send", new
				{
					jid = cl_278.msg.Jid,
					text = wb.ToString()
				});
				return Results.Json(new
				{
					ok = true,
					action = "warnings"
				});
			}
			if (eCommands && isCommand && cmdName == "audit")
			{
				if (!AdminSync.IsAllowed(config, senderNum, senderPhone))
				{
					return Results.Json(new
					{
						ok = true,
						action = "audit-denied"
					});
				}
				List<string> lines = audit.Tail(10);
				string body = ((lines.Count == 0) ? "Belum ada catatan audit." : string.Join("\n", lines));
				if (body.Length > 3500)
				{
					body = body.Substring(body.Length - 3500);
				}
				await PostJson(http, outBase + "/send", new
				{
					jid = cl_278.msg.Jid,
					text = "\ud83d\udcdc *Audit moderasi (terbaru)*\n" + body
				});
				return Results.Json(new
				{
					ok = true,
					action = "audit"
				});
			}
			if (eCommands && isCommand && cmdName == "modreport")
			{
				if (!AdminSync.IsAllowed(config, senderNum, senderPhone))
				{
					return Results.Json(new
					{
						ok = true,
						action = "modreport-denied"
					});
				}
				string rep = ModerationReport.Build(audit, config, DateTime.Now.AddHours(-24.0));
				await PostJson(http, outBase + "/send", new
				{
					jid = cl_278.msg.Jid,
					text = rep
				});
				return Results.Json(new
				{
					ok = true,
					action = "modreport"
				});
			}
			if (eCommands && isCommand && cmdName == "percaya")
			{
				if (!AdminSync.IsAllowed(config, senderNum, senderPhone))
				{
					return Results.Json(new
					{
						ok = true,
						action = "percaya-denied"
					});
				}
				if (string.IsNullOrWhiteSpace(cl_278.msg.QuotedAuthor))
				{
					await PostJson(http, outBase + "/send", new
					{
						jid = cl_278.msg.Jid,
						text = "Cara pakai: balas (reply) pesan anggota yang ingin dibuka aksesnya, lalu ketik " + config.CommandPrefix + "percaya. Bot akan membuka akses awalnya dan merapikan catatan moderasinya."
					});
					return Results.Json(new
					{
						ok = true,
						action = "percaya-noquote"
					});
				}
				string targetNum = NumberUtil.Normalize(cl_278.msg.QuotedAuthor);
				bool wasProbation = joins.Clear(targetNum);
				bool hadWarn = warnings.Reset(cl_278.msg.Jid + "|" + cl_278.msg.QuotedAuthor);
				await PostJson(http, outBase + "/send", new
				{
					jid = cl_278.msg.Jid,
					text = $"{targetNum} sudah ditandai aman - {(wasProbation ? "akses awal dibuka" : "akses sudah normal")}, {(hadWarn ? "catatan moderasi direset" : "tidak ada catatan moderasi")}.",
					mentions = new string[1] { cl_278.msg.QuotedAuthor }
				});
				app.Logger.LogInformation("PERCAYA {Target} oleh admin {Admin}", targetNum, senderNum);
				return Results.Json(new
				{
					ok = true,
					action = "percaya",
					target = targetNum
				});
			}
			if (eCommands && isCommand && (cmdName == "lapor" || cmdName == "admin"))
			{
				string laporTo = ((!string.IsNullOrWhiteSpace(config.LaporGroupJid)) ? config.LaporGroupJid : (config.Relay?.HubGroupJid ?? ""));
				if (string.IsNullOrWhiteSpace(laporTo))
				{
					await PostJson(http, outBase + "/send", new
					{
						jid = cl_278.msg.Jid,
						text = "Fitur lapor belum siap karena grup admin tujuan belum diset."
					});
					return Results.Json(new
					{
						ok = true,
						action = "lapor-noconfig"
					});
				}
				string[] lp = cmdText.Substring(config.CommandPrefix.Length).Split(' ', 2);
				string note = ((lp.Length > 1) ? lp[1].Trim() : "");
				StringBuilder stringBuilder;
				StringBuilder.AppendInterpolatedStringHandler handler;
				if (string.IsNullOrWhiteSpace(cl_278.msg.QuotedText))
				{
					if (cmdName == "admin")
					{
						string reporter0 = NumberUtil.Normalize(cl_278.msg.Participant);
						string grpLabel0 = cl_278.g?.Label ?? cl_278.msg.Jid;
						StringBuilder call = new StringBuilder();
						call.AppendLine("\ud83d\udea8 *Admin dipanggil anggota*");
						stringBuilder = call;
						StringBuilder stringBuilder14 = stringBuilder;
						handler = new StringBuilder.AppendInterpolatedStringHandler(6, 1, stringBuilder);
						handler.AppendLiteral("Grup: ");
						handler.AppendFormatted(grpLabel0);
						stringBuilder14.AppendLine(ref handler);
						stringBuilder = call;
						StringBuilder stringBuilder15 = stringBuilder;
						handler = new StringBuilder.AppendInterpolatedStringHandler(14, 2, stringBuilder);
						handler.AppendLiteral("Pemanggil: ");
						handler.AppendFormatted(cl_278.msg.PushName);
						handler.AppendLiteral(" (");
						handler.AppendFormatted(reporter0);
						handler.AppendLiteral(")");
						stringBuilder15.AppendLine(ref handler);
						if (note.Length > 0)
						{
							stringBuilder = call;
							StringBuilder stringBuilder16 = stringBuilder;
							handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder);
							handler.AppendLiteral("Catatan: ");
							handler.AppendFormatted(note);
							stringBuilder16.AppendLine(ref handler);
						}
						await PostJson(http, ChannelRoute.BaseForJid(config, laporTo) + "/send", new
						{
							jid = laporTo,
							text = call.ToString()
						});
						await PostJson(http, outBase + "/send", new
						{
							jid = cl_278.msg.Jid,
							text = "Admin sudah saya panggil. Jelaskan singkat ya."
						});
						return Results.Json(new
						{
							ok = true,
							action = "admin-called"
						});
					}
					await PostJson(http, outBase + "/send", new
					{
						jid = cl_278.msg.Jid,
						text = $"Report pesan: reply lalu {config.CommandPrefix}lapor. Panggil admin: {config.CommandPrefix}admin <catatan>."
					});
					return Results.Json(new
					{
						ok = true,
						action = "lapor-noquote"
					});
				}
				string reporter1 = NumberUtil.Normalize(cl_278.msg.Participant);
				string reported = NumberUtil.Normalize(cl_278.msg.QuotedAuthor);
				string snippet = ((cl_278.msg.QuotedText.Length > 400) ? (cl_278.msg.QuotedText.Substring(0, 400) + "…") : cl_278.msg.QuotedText);
				string grpLabel1 = cl_278.g?.Label ?? cl_278.msg.Jid;
				StringBuilder rep2 = new StringBuilder();
				rep2.AppendLine("\ud83d\udea9 *Laporan dari anggota*");
				stringBuilder = rep2;
				StringBuilder stringBuilder17 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(6, 1, stringBuilder);
				handler.AppendLiteral("Grup: ");
				handler.AppendFormatted(grpLabel1);
				stringBuilder17.AppendLine(ref handler);
				stringBuilder = rep2;
				StringBuilder stringBuilder18 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(12, 2, stringBuilder);
				handler.AppendLiteral("Pelapor: ");
				handler.AppendFormatted(cl_278.msg.PushName);
				handler.AppendLiteral(" (");
				handler.AppendFormatted(reporter1);
				handler.AppendLiteral(")");
				stringBuilder18.AppendLine(ref handler);
				if (reported.Length > 0)
				{
					stringBuilder = rep2;
					StringBuilder stringBuilder19 = stringBuilder;
					handler = new StringBuilder.AppendInterpolatedStringHandler(12, 1, stringBuilder);
					handler.AppendLiteral("Dilaporkan: ");
					handler.AppendFormatted(reported);
					stringBuilder19.AppendLine(ref handler);
				}
				if (note.Length > 0)
				{
					stringBuilder = rep2;
					StringBuilder stringBuilder20 = stringBuilder;
					handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder);
					handler.AppendLiteral("Catatan: ");
					handler.AppendFormatted(note);
					stringBuilder20.AppendLine(ref handler);
				}
				rep2.AppendLine("Pesan yang dilaporkan:");
				stringBuilder = rep2;
				StringBuilder stringBuilder21 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(2, 1, stringBuilder);
				handler.AppendLiteral("“");
				handler.AppendFormatted(snippet);
				handler.AppendLiteral("”");
				stringBuilder21.Append(ref handler);
				await PostJson(http, ChannelRoute.BaseForJid(config, laporTo) + "/send", new
				{
					jid = laporTo,
					text = rep2.ToString()
				});
				await PostJson(http, outBase + "/send", new
				{
					jid = cl_278.msg.Jid,
					text = "Terima kasih, laporanmu sudah diteruskan ke admin. \ud83d\ude4f"
				});
				return Results.Json(new
				{
					ok = true,
					action = "lapor"
				});
			}
			PuzzleConfig pzc = config.Puzzle;
			int num8;
			if (pzc != null)
			{
				bool flag3 = pzc.Enabled;
				num8 = (flag3 ? 1 : 0);
			}
			else
			{
				num8 = 0;
			}
			if (((uint)num8 & (eCommands ? 1u : 0u) & (isCommand ? 1u : 0u)) != 0 && cmdName == pzc.Command && (cl_278.g?.PuzzleCommandEnabled ?? pzc.CommandEnabled))
			{
				if (puzzlePool.Count == 0)
				{
					await PostJson(http, outBase + "/send", new
					{
						jid = cl_278.msg.Jid,
						text = "Puzzle belum siap. Coba lagi nanti."
					});
					return Results.Json(new
					{
						ok = true,
						action = "puzzle-nopool"
					});
				}
				ActivePuzzle cur;
				lock (puzzleLock)
				{
					activePuzzles.TryGetValue(cl_278.msg.Jid, out cur);
				}
				if (cur != null && !cur.Revealed)
				{
					await PostJson(http, outBase + "/send", new
					{
						jid = cl_278.msg.Jid,
						text = "Puzzle masih berjalan. Balas langkahmu, atau ketik " + config.CommandPrefix + pzc.SolveCommand + " nanti."
					});
					return Results.Json(new
					{
						ok = true,
						action = "puzzle-busy"
					});
				}
				if (cur != null && cur.Revealed && (DateTime.UtcNow - cur.SolvedAt).TotalSeconds < 12.0)
				{
					await PostJson(http, outBase + "/send", new { jid = cl_278.msg.Jid, text = "Puzzle barusan selesai. Santai dulu sebentar ya — ketik " + config.CommandPrefix + "peringkat untuk papan skor." });
					return Results.Json(new { ok = true, action = "puzzle-cooldown" });
				}
				if (!cmdCooldown.Allow(cl_278.msg.Jid + "|pznew", 12))
				{
					await PostJson(http, outBase + "/send", new { jid = cl_278.msg.Jid, text = "Sabar ya, puzzle baru bisa diminta tiap beberapa detik. Coba lagi sebentar." });
					return Results.Json(new { ok = true, action = "puzzle-ratelimited" });
				}
				await PostPuzzleAsync(cl_278.msg.Jid, false, null, PuzzleMove.DifficultySlot(cmdText, pzc.RevealMinutes));
				return Results.Json(new
				{
					ok = true,
					action = "puzzle"
				});
			}
			PuzzleConfig zc2 = config.Puzzle;
			int num9;
			if (zc2 != null)
			{
				bool flag3 = zc2.Enabled;
				num9 = (flag3 ? 1 : 0);
			}
			else
			{
				num9 = 0;
			}
			if (((uint)num9 & (eCommands ? 1u : 0u) & (isCommand ? 1u : 0u)) != 0 && (cmdName == zc2.SolveCommand || cmdName == "nyerah" || cmdName == "menyerah"))
			{
				ActivePuzzle ap;
				lock (puzzleLock)
				{
					activePuzzles.TryGetValue(cl_278.msg.Jid, out ap);
				}
				if (ap == null)
				{
					await PostJson(http, outBase + "/send", new
					{
						jid = cl_278.msg.Jid,
						text = "Belum ada puzzle aktif. Mulai: " + config.CommandPrefix + zc2.Command + "."
					});
				}
				else if (ap.Revealed || ap.WrongCount >= 6 || !(DateTimeOffset.UtcNow.UtcDateTime < ap.PostedAt.AddMinutes(cl_278.g?.PuzzleSolveAfterMinutes ?? zc2.SolveAfterMinutes)))
				{
					await RevealPuzzleAsync(cl_278.msg.Jid, ap, false);
				}
				else
				{
					await PostJson(http, outBase + "/send", new
					{
						jid = cl_278.msg.Jid,
						text = zc2.TryHarderMessage
					});
				}
				return Results.Json(new
				{
					ok = true,
					action = "solusi"
				});
			}
			PuzzleConfig puzzle = config.Puzzle;
			int num10;
			if (puzzle != null)
			{
				bool flag3 = puzzle.Enabled;
				num10 = (flag3 ? 1 : 0);
			}
			else
			{
				num10 = 0;
			}
			int num11;
			if (((uint)num10 & (eCommands ? 1u : 0u) & (isCommand ? 1u : 0u)) != 0)
			{
				switch (cmdName)
				{
				default:
					num11 = ((cmdName == "leaderboard") ? 1 : 0);
					break;
				case "peringkat":
				case "ranking":
				case "rangking":
				case "rank":
				case "papan":
				case "skor":
					num11 = 1;
					break;
				}
			}
			else
			{
				num11 = 0;
			}
			if (num11 != 0)
			{
				List<PuzzleScoreStore.PlayerScore> top2 = PuzzleScoreStore.Top(cl_278.msg.Jid, 10);
				string text;
				if (top2.Count == 0)
				{
					text = "Belum ada skor puzzle di grup ini. Jawab puzzle harian untuk mulai mengumpulkan poin! \ud83e\udde9";
				}
				else
				{
					StringBuilder sb2 = new StringBuilder();
					sb2.AppendLine("\ud83c\udfc6 *Papan Peringkat Puzzle*");
					string[] medal = new string[3] { "\ud83e\udd47", "\ud83e\udd48", "\ud83e\udd49" };
					for (int i3 = 0; i3 < top2.Count; i3++)
					{
						string pos = ((i3 < 3) ? medal[i3] : $"{i3 + 1}.");
						string nm = (string.IsNullOrWhiteSpace(top2[i3].Name) ? "Pemain" : top2[i3].Name);
						StringBuilder stringBuilder = sb2;
						StringBuilder stringBuilder22 = stringBuilder;
						StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(21, 4, stringBuilder);
						handler.AppendFormatted(pos);
						handler.AppendLiteral(" *");
						handler.AppendFormatted(nm);
						handler.AppendLiteral("* — ");
						handler.AppendFormatted(top2[i3].Points);
						handler.AppendLiteral(" poin · ");
						handler.AppendFormatted(top2[i3].Solves);
						handler.AppendLiteral(" solusi");
						stringBuilder22.AppendLine(ref handler);
					}
					sb2.Append("\nKetik langkah saat puzzle harian untuk naik peringkat ♟\ufe0f");
					text = sb2.ToString();
				}
				await PostJson(http, outBase + "/send", new
				{
					jid = cl_278.msg.Jid,
					text = text
				});
				return Results.Json(new
				{
					ok = true,
					action = "peringkat"
				});
			}
			puzzle = config.Puzzle;
			int num12;
			if (puzzle != null)
			{
				bool flag3 = puzzle.Enabled;
				num12 = (flag3 ? 1 : 0);
			}
			else
			{
				num12 = 0;
			}
			if (((uint)num12 & (eCommands ? 1u : 0u) & (isCommand ? 1u : 0u)) != 0 && cmdName == "resetperingkat")
			{
				if (!AdminSync.IsAllowed(config, senderNum, senderPhone))
				{
					return Results.Json(new
					{
						ok = true,
						action = "reset-denied"
					});
				}
				bool had2 = PuzzleScoreStore.Reset(cl_278.msg.Jid);
				await PostJson(http, outBase + "/send", new
				{
					jid = cl_278.msg.Jid,
					text = (had2 ? "Papan peringkat puzzle grup ini sudah direset. \ud83e\uddf9" : "Belum ada skor untuk direset.")
				});
				return Results.Json(new
				{
					ok = true,
					action = "reset-peringkat"
				});
			}
			int num13;
			if (eCommands && isCommand)
			{
				switch (cmdName)
				{
				default:
					num13 = ((cmdName == "eval") ? 1 : 0);
					break;
				case "analisa":
				case "analisis":
				case "analyze":
					num13 = 1;
					break;
				}
			}
			else
			{
				num13 = 0;
			}
			if (num13 != 0)
			{
				if (!StockfishEngine.Available)
				{
					await PostJson(http, outBase + "/send", new
					{
						jid = cl_278.msg.Jid,
						text = "Engine analisa belum siap di server."
					});
					return Results.Json(new
					{
						ok = true,
						action = "analisa-noengine"
					});
				}
				if (false) // cooldown internal DIMATIKAN: bentrok kunci dgn cooldown command global -> dulu selalu blok !analisa
				{
					return Results.Json(new
					{
						ok = true,
						action = "analisa-cooldown"
					});
				}
				string rawAn = cl_278.msg.Text.TrimStart();
				int spAn = rawAn.IndexOfAny(new char[4] { ' ', '\n', '\r', '\t' });
				string argAn = ((spAn >= 0) ? rawAn.Substring(spAn + 1).Trim() : "");
				string noteAn = "";
				if (!argAn.Contains('/') && argAn.Length <= 15)
				{
					JsonElement kAn;
					string mediaId = ((!(cl_278.msg.MediaType == "image")) ? ((cl_278.msg.QuotedId.Length > 0) ? cl_278.msg.QuotedId : "") : ((cl_278.msg.Key.ValueKind == JsonValueKind.Object && cl_278.msg.Key.TryGetProperty("id", out kAn)) ? (kAn.GetString() ?? "") : ""));
					if (mediaId.Length > 0)
					{
						string al = argAn.ToLowerInvariant();
						bool blackGiven = al.Contains("hitam") || al.Contains("black");
						bool sideGiven = blackGiven || al.Contains("putih") || al.Contains("white");
						byte[] imgBytes = null;
						try
						{
							using HttpResponseMessage rsp = await http.PostAsync(outBase + "/get-media", new StringContent(JsonSerializer.Serialize(new
							{
								id = mediaId
							}), Encoding.UTF8, "application/json"));
							if (rsp.IsSuccessStatusCode)
							{
								using JsonDocument doc = JsonDocument.Parse(await rsp.Content.ReadAsStringAsync());
								string s = default(string);
								int num14;
								if (doc.RootElement.TryGetProperty("base64", out var b64))
								{
									s = b64.GetString();
									num14 = ((s != null) ? 1 : 0);
								}
								else
								{
									num14 = 0;
								}
								if (num14 != 0)
								{
									imgBytes = Convert.FromBase64String(s);
								}
							}
						}
						catch
						{
						}
						if (imgBytes == null)
						{
							await PostJson(http, outBase + "/send", new
							{
								jid = cl_278.msg.Jid,
								text = "Tak bisa ambil gambarnya. Kirim ulang gambar papan + caption !analisa ya."
							});
							return Results.Json(new
							{
								ok = true,
								action = "analisa-nomedia"
							});
						}
						try { File.WriteAllBytes(Path.Combine(puzzleCacheDir, "_last_analisa.png"), imgBytes); } catch { } // DEBUG: tangkap gambar asli utk tuning
						bool autoFlipped;
						string placement = BoardVision.RecognizeFenAuto(imgBytes, pieceAssetsDir, out autoFlipped);
						if (placement == null)
						{
							await PostJson(http, outBase + "/send", new
							{
								jid = cl_278.msg.Jid,
								text = "Gagal membaca papan dari gambar. Pastikan screenshot papan (Lichess/Chess.com) yang jelas, hanya papannya."
							});
							return Results.Json(new
							{
								ok = true,
								action = "analisa-norecog"
							});
						}
						if (!sideGiven)
						{
							PendingAnalysis.Set(cl_278.msg.Jid + "|" + senderNum, placement);
							string imgAsk = null;
							try
							{
								imgAsk = BoardRenderer.Render(BoardVision.BuildFullFen(placement, true), false, puzzleCacheDir, pieceAssetsDir);
							}
							catch
							{
							}
							string ask = (autoFlipped ? "\ud83d\udd04 Papan terdeteksi dari sisi Hitam \u2014 sudah kubalik otomatis.\n" : "") + "\ud83d\udcf7 Ini posisi yang kubaca. *Giliran siapa?* Balas *Putih* atau *Hitam*.\n(kalau orientasi masih salah, balas mis. *hitam balik*. Bidak salah baca? kirim FEN-nya.)";
							if (imgAsk == null)
							{
								await PostJson(http, outBase + "/send", new
								{
									jid = cl_278.msg.Jid,
									text = ask
								});
							}
							else
							{
								await PostJson(http, outBase + "/send-image", new
								{
									jid = cl_278.msg.Jid,
									path = imgAsk,
									caption = ask
								});
							}
							return Results.Json(new
							{
								ok = true,
								action = "analisa-ask-side"
							});
						}
						if (al.Contains("balik") || al.Contains("terbalik") || al.Contains("flip")) placement = BoardVision.FlipPlacement(placement); // papan sisi Hitam -> putar 180
						argAn = BoardVision.BuildFullFen(placement, !blackGiven);
						noteAn = "\ud83d\udcf7 Posisi terbaca dari gambar (" + (blackGiven ? "Hitam" : "Putih") + " jalan). Kalau ada bidak salah baca, kirim FEN-nya ya.\n\n";
					}
					else if (argAn.Length == 0)
					{
						await PostJson(http, outBase + "/send", new
						{
							jid = cl_278.msg.Jid,
							text = "Kirim *FEN*/*PGN*, atau kirim *GAMBAR* papan (screenshot Lichess/Chess.com) dengan caption *!analisa* (tambah 'hitam' kalau giliran Hitam)."
						});
						return Results.Json(new
						{
							ok = true,
							action = "analisa-empty"
						});
					}
				}
				SendTyping(cl_278.msg.Jid, ctx.Channel);
				ChessAnalysis.Output outp = (await ChessAnalysis.Run(argAn, config.Ai, http, app.Logger)) ?? new ChessAnalysis.Output("Gagal menganalisa.", "");
				string capAn = noteAn + outp.Text;
				string imgAn = null;
				if (outp.Fen.Length > 0)
				{
					try
					{
						imgAn = BoardRenderer.Render(outp.Fen, !outp.Fen.Contains(" w "), puzzleCacheDir, pieceAssetsDir);
					}
					catch
					{
					}
				}
				if (imgAn == null)
				{
					await PostJson(http, outBase + "/send", new
					{
						jid = cl_278.msg.Jid,
						text = capAn
					});
				}
				else
				{
					await PostJson(http, outBase + "/send-image", new
					{
						jid = cl_278.msg.Jid,
						path = imgAn,
						caption = capAn
					});
				}
				return Results.Json(new
				{
					ok = true,
					action = "analisa"
				});
			}
			puzzle = config.Puzzle;
			int num15;
			if (puzzle != null)
			{
				bool flag3 = puzzle.Enabled;
				num15 = (flag3 ? 1 : 0);
			}
			else
			{
				num15 = 0;
			}
			if (num15 != 0)
			{
				ActivePuzzle pap;
				lock (puzzleLock)
				{
					if (cl_278.msg.QuotedId.Length > 0 && puzzleByMsg.TryGetValue(cl_278.msg.QuotedId, out ActivePuzzle byMsg))
					{
						pap = byMsg;
					}
					else
					{
						activePuzzles.TryGetValue(cl_278.msg.Jid, out pap);
					}
				}
				JsonElement _idEl;
				string inMsgId = ((cl_278.msg.Key.ValueKind == JsonValueKind.Object && cl_278.msg.Key.TryGetProperty("id", out _idEl)) ? (_idEl.GetString() ?? "") : "");
				if (pap != null && !pap.Revealed && pap.Puzzle.SolutionSan.Length != 0)
				{
					string[] sol = pap.Puzzle.SolutionSan;
					string attempt = PuzzleMove.StripMoveNumber(cmdText.TrimStart('!', ' ').Trim());
					if (PuzzleMove.IsMoveLike(attempt))
					{
						int idx;
						lock (puzzleLock)
						{
							idx = pap.Progress;
						}
						if (idx < sol.Length && (PuzzleMove.Matches(attempt, sol[idx]) || PuzzleMove.MatchesByPosition((idx > 0 && idx - 1 < pap.Puzzle.Fens.Length) ? pap.Puzzle.Fens[idx - 1] : pap.Puzzle.Fen, attempt, sol[idx])))
						{
							string oppMove = null;
							bool done;
							int prog;
							lock (puzzleLock)
							{
								pap.Progress++;
								if (pap.Progress < sol.Length)
								{
									oppMove = sol[pap.Progress];
									pap.Progress++;
								}
								done = pap.Progress >= sol.Length;
								if (done)
								{
									pap.Revealed = true;
									pap.SolvedAt = DateTime.UtcNow;
								}
								prog = pap.Progress;
								if (!pap.SolverNums.Contains(senderNum))
								{
									pap.SolverNums.Add(senderNum);
									pap.SolverJids.Add(cl_278.msg.Participant);
								}
							}
							SaveActivePuzzles();
							int pts = PuzzleScoreStore.Tier(pap.Puzzle.Rating);
							PuzzleScoreStore.Award(cl_278.msg.Jid, senderNum, cl_278.msg.PushName, pts, done);
							try
							{
								await PostJson(http, outBase + "/react", new
								{
									jid = cl_278.msg.Jid,
									key = cl_278.msg.Key,
									emoji = (done ? "\ud83c\udf89" : "✅")
								});
							}
							catch
							{
							}
							if (done)
							{
								List<string> helperNums = new List<string>();
								List<string> mentionList = new List<string> { cl_278.msg.Participant };
								lock (puzzleLock)
								{
									for (int i4 = 0; i4 < pap.SolverNums.Count; i4++)
									{
										if (pap.SolverNums[i4] != senderNum)
										{
											helperNums.Add(pap.SolverNums[i4]);
											mentionList.Add(pap.SolverJids[i4]);
										}
									}
								}
								string credit = ((helperNums.Count > 0) ? ("\nDibantu " + string.Join(" ", helperNums.Select((string h) => "@" + h)) + " \ud83d\udc4f") : "");
								List<PuzzleScoreStore.PlayerScore> topN = PuzzleScoreStore.Top(cl_278.msg.Jid, 3);
								string board = "";
								if (topN.Count > 0)
								{
									string[] md = new string[3] { "\ud83e\udd47", "\ud83e\udd48", "\ud83e\udd49" };
									List<string> parts2 = new List<string>();
									for (int i5 = 0; i5 < topN.Count; i5++)
									{
										string nm2 = (string.IsNullOrWhiteSpace(topN[i5].Name) ? "Pemain" : topN[i5].Name);
										parts2.Add($"{md[i5]} {nm2} ({topN[i5].Points})");
									}
									board = $"\n\n\ud83c\udfc6 *Peringkat:* {string.Join(" · ", parts2)}\n_Ketik {config.CommandPrefix}peringkat untuk lengkap_";
								}
								string t = ((oppMove == null) ? $"✅ *Tepat sekali, @{senderNum}!* \ud83c\udf89 Itu jurus pamungkasnya — puzzle selesai. Keren! (+{pts} poin) ♟\ufe0f{credit}{board}" : $"✅ *Tepat sekali, @{senderNum}!* Lawan terpaksa main *{oppMove}*, dan itu menutup variannya. \ud83c\udf89 Puzzle selesai, mantap! (+{pts} poin) ♟\ufe0f{credit}{board}");
								await PostJson(http, outBase + "/send", new
								{
									jid = cl_278.msg.Jid,
									text = t + PuzzleMove.ThemeNote(pap.Puzzle.Themes),
									mentions = mentionList.ToArray(),
									replyToId = inMsgId
								});
							}
							else
							{
								string cap = $"✅ *Benar, @{senderNum}!* (+{pts} poin) \ud83d\udc4f Lawan membalas *{oppMove}*.\nSekarang giliranmu — langkah terbaik berikutnya apa? \ud83e\udd14";
								string[] fens = pap.Puzzle.Fens;
								string img = null;
								if (prog - 1 >= 0 && prog - 1 < fens.Length)
								{
									try
									{
										img = BoardRenderer.Render(fens[prog - 1], pap.Puzzle.Side == "b", puzzleCacheDir, pieceAssetsDir);
									}
									catch
									{
									}
								}
								if (img == null)
								{
									await PostJson(http, outBase + "/send", new
									{
										jid = cl_278.msg.Jid,
										text = cap,
										mentions = new string[1] { cl_278.msg.Participant },
										replyToId = inMsgId
									});
								}
								else
								{
									await PostJson(http, outBase + "/send-image", new
									{
										jid = cl_278.msg.Jid,
										path = img,
										caption = cap,
										mentions = new string[1] { cl_278.msg.Participant },
										replyToId = inMsgId
									});
								}
							}
							return Results.Json(new
							{
								ok = true,
								action = "puzzle-correct",
								progress = prog
							});
						}
						bool alreadyPlayed = false;
						for (int i6 = 0; i6 < idx && i6 < sol.Length; i6 += 2)
						{
							if (PuzzleMove.Matches(attempt, sol[i6]))
							{
								alreadyPlayed = true;
								break;
							}
						}
						if (alreadyPlayed)
						{
							if (cmdCooldown.Allow(cl_278.msg.Jid + "|" + senderNum + "|pzplayed", 8))
							{
								await PostJson(http, outBase + "/react", new
								{
									jid = cl_278.msg.Jid,
									key = cl_278.msg.Key,
									emoji = "\ud83d\udc4d"
								});
							}
							return Results.Json(new
							{
								ok = true,
								action = "puzzle-already"
							});
						}
						bool isReplyToPuzzle = cl_278.msg.QuotedId.Length > 0 && (cl_278.msg.QuotedId == pap.MsgId || puzzleByMsg.ContainsKey(cl_278.msg.QuotedId));
						bool strongChess = Regex.IsMatch(attempt, "[KQRBNGMx=+#]") || attempt.Contains("O-O") || attempt.Contains("0-0");
						if (!isReplyToPuzzle && !strongChess)
						{
							return Results.Json(new
							{
								ok = true,
								action = "puzzle-maybe-chat"
							});
						}
						pap.WrongCount++;
						if (cmdCooldown.Allow(cl_278.msg.Jid + "|" + senderNum + "|pzwrong", (pap.WrongCount <= 3) ? 10 : 25) && cmdCooldown.Allow(cl_278.msg.Jid + "|pzwrongAny", (pap.WrongCount <= 3) ? 4 : 25))
						{
							// nama tampil lewat mention @senderNum (di-tag agar pemain ke-notify)
							string nextSanW = (idx < sol.Length) ? sol[idx] : "";
							string text2 = (pap.WrongCount <= 3) ? ("Belum pas, @" + senderNum + ". " + PuzzleMove.LocalWrongHint(nextSanW, pap.WrongCount)) : ("Belum pas, @" + senderNum + ".");
							if (pap.WrongCount >= 4 && !pap.SolveHintShown)
							{
								text2 += "\n\nKetik " + config.CommandPrefix + (config.Puzzle?.SolveCommand ?? "solusi") + " untuk lihat jawabannya."; pap.SolveHintShown = true;
							}
							await PostJson(http, outBase + "/send", new
							{
								jid = cl_278.msg.Jid,
								text = text2,
								mentions = new string[1] { cl_278.msg.Participant },
								replyToId = inMsgId
							});
						}
						return Results.Json(new
						{
							ok = true,
							action = "puzzle-wrong"
						});
					}
					ai = config.Ai;
					int num17;
					if (ai != null)
					{
						bool flag3 = ai.Enabled;
						num17 = (flag3 ? 1 : 0);
					}
					else
					{
						num17 = 0;
					}
					if (num17 != 0)
					{
						string low = cl_278.msg.Text.ToLowerInvariant();
						bool otherTopic = Regex.IsMatch(low, "\\b(jadwal|turnamen|tournament|daftar|register|next|help|bantuan|standing|klasemen|pairing|hasil|result|info|kapan|dimana|di mana|harga|biaya|bayar|admin|grup|join|link)\\b");
						bool chessCue = Regex.IsMatch(low, "(langkah|soal|solusi|jawab|posisi|skak|sekak|bidak|menteri|benteng|kuda|gajah|\\braja\\b|pion|puzzle|kenapa|knp|napa|kok|gimana|gmn|gmna|jelas|maksud|salah)");
						bool isReplyToThis = cl_278.msg.QuotedId.Length > 0 && cl_278.msg.QuotedId == pap.MsgId;
						bool relevant = chessCue || isReplyToThis || cl_278.msg.MentionedBot;
						if ((low.Contains('?') || chessCue) && !otherTopic && relevant && cmdCooldown.Allow(cl_278.msg.Jid + "|" + senderNum + "|pzask", 15))
						{
							string sideT2 = ((pap.Puzzle.Side == "w") ? "Putih" : "Hitam");
							string solLine2 = FormatPuzzleSolution(pap.Puzzle);
							string prompt2 = $"Ini puzzle catur yang BELUM diselesaikan pemain. Posisi FEN: {pap.Puzzle.Fen}. {sideT2} yang jalan. Langkah terbaik menurut mesin (RAHASIA, untuk pemahamanmu saja): {solLine2}. Pemain bertanya/berkomentar: \"{cl_278.msg.Text.Trim()}\". " + "Jawab 1-3 kalimat pendek, ramah, Bahasa Indonesia natural. Jelaskan alasan posisi atau konsekuensi dari pertanyaan pemain. Kalau pemain menanyakan langkah yang belum pas, jawab seperti teman latihan: ringan, jelas, dan tidak menggurui. Jangan pakai istilah 'refutasi', 'konkret', 'varian', 'aku belum yakin', atau 'tidak mau asal menebak'. Kalau tidak yakin detailnya, beri arahan umum tanpa mengarang. JANGAN memberi kandidat langkah terbaik untuk pemain. JANGAN sebut atau parafrasekan langkah terbaik/solusi rahasia.";
							string ans3 = await Ai.Ask(config.Ai, http, prompt2, app.Logger);
							string reply2 = (string.IsNullOrWhiteSpace(ans3) ? "Maaf, aku belum bisa menjelaskan dengan baik sekarang. Silakan coba lagi sebentar ya." : PuzzleMove.HumanizeWrongExplanation(PuzzleMove.CleanWrongExplanation(ans3, sol)));
							if (reply2.Length > config.Ai.MaxOutputChars)
							{
								reply2 = reply2.Substring(0, config.Ai.MaxOutputChars) + "…";
							}
							await PostJson(http, outBase + "/send", new
							{
								jid = cl_278.msg.Jid,
								text = "@" + senderNum + " " + reply2,
								mentions = new string[1] { cl_278.msg.Participant },
								replyToId = inMsgId
							});
							return Results.Json(new
							{
								ok = true,
								action = "puzzle-explain"
							});
						}
					}
				}
				else if (pap != null && pap.Revealed && pap.Puzzle.SolutionSan.Length != 0)
				{
					string attempt2 = PuzzleMove.StripMoveNumber(cmdText.TrimStart('!', ' ').Trim());
					bool recent2 = (DateTime.UtcNow - pap.SolvedAt).TotalMinutes <= 3.0 || (cl_278.msg.QuotedId.Length > 0 && cl_278.msg.QuotedId == pap.MsgId);
					if (PuzzleMove.IsMoveLike(attempt2) && recent2 && cmdCooldown.Allow(cl_278.msg.Jid + "|" + senderNum + "|pzdone", 12))
					{
						string[] sol2 = pap.Puzzle.SolutionSan;
						bool wasRight = false;
						for (int i7 = 0; i7 < sol2.Length; i7 += 2)
						{
							if (PuzzleMove.Matches(attempt2, sol2[i7]))
							{
								wasRight = true;
								break;
							}
						}
						string text3 = (wasRight ? ("✅ Betul juga, @" + senderNum + "! \ud83d\udc4f Tapi puzzle ini sudah keburu diselesaikan tadi. Tunggu puzzle berikutnya ya \ud83d\ude42") : ("Puzzle ini sudah selesai, @" + senderNum + ". \ud83d\ude42 Tunggu puzzle berikutnya ya!"));
						await PostJson(http, outBase + "/send", new
						{
							jid = cl_278.msg.Jid,
							text = text3,
							mentions = new string[1] { cl_278.msg.Participant },
							replyToId = inMsgId
						});
						return Results.Json(new
						{
							ok = true,
							action = "puzzle-done-late"
						});
					}
				}
			}
			if (eCommands && isCommand)
			{
				string reply3 = await CommandHandler.Handle(cmdText, config, http, app.Logger);
				if (reply3 != null)
				{
					await PostJson(http, outBase + "/send", new
					{
						jid = cl_278.msg.Jid,
						text = reply3
					});
					TopicStore.Set(cl_278.msg.Jid, cmdName);
				}
				return Results.Json(new
				{
					ok = true,
					action = "command",
					replied = (reply3 != null)
				});
			}
			FaqConfig faq = config.Faq;
			int num18;
			if (faq != null)
			{
				bool flag3 = faq.Enabled;
				num18 = (flag3 ? 1 : 0);
			}
			else
			{
				num18 = 0;
			}
			if (num18 != 0)
			{
				FaqEntry[] entries = config.Faq.Entries;
				foreach (FaqEntry f in entries)
				{
					if (string.IsNullOrEmpty(f.Pattern) || (config.Faq.RequireMention && !cl_278.msg.MentionedBot))
					{
						continue;
					}
					try
					{
						if (Regex.IsMatch(cl_278.msg.Text, f.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
						{
							if (quietNow)
							{
								return Results.Json(new
								{
									ok = true,
									action = "quiet-faq",
									id = f.Id
								});
							}
							if (cooldownSec > 0 && !senderExempt && !cmdCooldown.Allow($"{cl_278.msg.Jid}|{senderNum}|faq:{f.Id}", cooldownSec))
							{
								return Results.Json(new
								{
									ok = true,
									action = "faq-cooldown",
									id = f.Id
								});
							}
							string faqReply = f.Reply;
							if (faqReply.Contains("{schedule}"))
							{
								string text4 = faqReply;
								faqReply = text4.Replace("{schedule}", await CommandHandler.BuildSchedule(config, http, app.Logger));
							}
							if (faqReply.Contains("{rules}"))
							{
								faqReply = faqReply.Replace("{rules}", config.RulesText);
							}
							await PostJson(http, outBase + "/send", new
							{
								jid = cl_278.msg.Jid,
								text = faqReply
							});
							TopicStore.Set(cl_278.msg.Jid, f.Id);
							return Results.Json(new
							{
								ok = true,
								action = "faq",
								id = f.Id
							});
						}
					}
					catch
					{
					}
				}
			}
			number = NumberUtil.Normalize(cl_278.msg.Participant);
			if (senderExempt)
			{
				return Results.Json(new
				{
					ok = true,
					action = "exempt"
				});
			}
			string probationKey = number;
			bool isMedia = !string.IsNullOrEmpty(cl_278.msg.MediaType);
			bool hasUnsafeLink = ModUtil.HasUnsafeLink(cl_278.msg.Text);
			string probeReason = null;
			ProbationConfig pc = config.Probation;
			if (pc != null && pc.Enabled && joins.InProbation(probationKey, pc.Minutes, DateTimeOffset.UtcNow.UtcDateTime))
			{
				if (pc.BlockMedia && isMedia && (!pc.BlockForwardedOnly || cl_278.msg.IsForwarded))
				{
					probeReason = "media (anggota baru)";
				}
				else if (pc.BlockLinks && hasUnsafeLink)
				{
					probeReason = "link (anggota baru)";
				}
			}
			string mediaReason = null;
			int num20;
			if (probeReason == null && isMedia)
			{
				MediaModerationConfig mm = config.MediaModeration;
				if (mm != null && mm.BlockForwardedMedia && cl_278.msg.IsForwarded)
				{
					num20 = ((cl_278.msg.ForwardScore >= Math.Max(1, mm.ForwardScoreThreshold)) ? 1 : 0);
					goto IL_a1ed;
				}
			}
			num20 = 0;
			goto IL_a1ed;
			IL_a1ed:
			if (num20 != 0)
			{
				mediaReason = "media sering diteruskan";
			}
			if (probeReason != null || mediaReason != null)
			{
				if (ctx.Caps.CanDelete)
				{
					await PostJson(http, outBase + "/delete", new
					{
						jid = cl_278.msg.Jid,
						key = cl_278.msg.Key
					});
				}
				int pcount = warnings.Increment(cl_278.msg.Jid + "|" + cl_278.msg.Participant);
				if (!quietNow)
				{
					string tmpl = ((probeReason == null) ? (config.MediaModeration?.Message ?? "@user, media saya rapikan dulu untuk menjaga grup dari spam.") : (config.Probation?.Message ?? "@user, untuk anggota baru, link/media saya tahan sementara agar grup tetap aman."));
					string warnText2 = tmpl.Replace("@user", "@" + number).Replace("{count}", pcount.ToString());
					await PostJson(http, outBase + "/send", new
					{
						jid = cl_278.msg.Jid,
						text = warnText2,
						mentions = new string[1] { cl_278.msg.Participant }
					});
				}
				string tag = ((probeReason != null) ? "probation" : "fwd-media");
				audit.Write(cl_278.msg.Jid, cl_278.msg.Participant, cl_278.msg.PushName, tag, pcount, string.IsNullOrEmpty(cl_278.msg.Text) ? ("[" + cl_278.msg.MediaType + "]") : cl_278.msg.Text);
				app.Logger.LogInformation("HAPUS ({Tag}) dari {Number}, peringatan ke-{Count}", tag, number, pcount);
				return Results.Json(new
				{
					ok = true,
					action = tag,
					warnCount = pcount
				});
			}
			bool isFlood;
			bool shouldWarnFlood;
			if (eFlood)
			{
				(isFlood, shouldWarnFlood) = floodTracker.Check(cl_278.msg.Jid + "|" + cl_278.msg.Participant);
			}
			else
			{
				isFlood = false;
				shouldWarnFlood = false;
			}
			matched = (eModeration ? rules.FirstOrDefault((Rule rule) => RuleActive(rule, cl_278.g) && !rule.Shadow && rule.Compiled.IsMatch(cl_278.msg.Text)) : null);
			if (matched == null && eModeration)
			{
				Rule shadow = rules.FirstOrDefault((Rule rule) => RuleActive(rule, cl_278.g) && rule.Shadow && rule.Compiled.IsMatch(cl_278.msg.Text));
				if (shadow != null)
				{
					audit.Write(cl_278.msg.Jid, cl_278.msg.Participant, cl_278.msg.PushName, "SHADOW:" + shadow.Id, 0, cl_278.msg.Text);
					app.Logger.LogInformation("SHADOW (tidak dihapus) dari {Number} (aturan {Rule})", number, shadow.Id);
				}
			}
			if (matched != null)
			{
				if (ctx.Caps.CanDelete)
				{
					await PostJson(http, outBase + "/delete", new
					{
						jid = cl_278.msg.Jid,
						key = cl_278.msg.Key
					});
				}
				count = warnings.Increment(cl_278.msg.Jid + "|" + cl_278.msg.Participant);
				if (!quietNow)
				{
					string[] wv = config.WarningMessageVariants;
					if (wv != null)
					{
						int num4 = wv.Length;
						if (num4 > 0)
						{
							obj4 = wv[Random.Shared.Next(wv.Length)];
							goto IL_a94f;
						}
					}
					obj4 = config.WarningMessage;
					goto IL_a94f;
				}
				goto IL_aa9d;
			}
			if (isFlood)
			{
				if (ctx.Caps.CanDelete)
				{
					await PostJson(http, outBase + "/delete", new
					{
						jid = cl_278.msg.Jid,
						key = cl_278.msg.Key
					});
				}
				if (shouldWarnFlood && !quietNow)
				{
					fcount = warnings.Increment(cl_278.msg.Jid + "|" + cl_278.msg.Participant);
					string[] fv = config.FloodWarningMessageVariants;
					if (fv != null)
					{
						int num4 = fv.Length;
						if (num4 > 0)
						{
							obj3 = fv[Random.Shared.Next(fv.Length)];
							goto IL_ad48;
						}
					}
					obj3 = config.FloodWarningMessage;
					goto IL_ad48;
				}
				return Results.Json(new
				{
					ok = true,
					action = "flood",
					warned = false
				});
			}
			return Results.Json(new
			{
				ok = true,
				action = "clean"
			});
		}

		internal bool lam_56(GroupOption o)
		{
			return o.Jid != config.Relay.HubGroupJid && o.Jid.Length > 0;
		}

		internal async Task<IResult> lam_22(MemberJoined ev)
		{
			config.Groups.TryGetValue(ev.Jid, out GroupConfig g);
			if (!config.ManageAllGroups && g == null)
			{
				return Results.Json(new
				{
					ok = true,
					action = "unmanaged"
				});
			}
			ProbationConfig probation = config.Probation;
			int num;
			if (probation != null && probation.Enabled)
			{
				string[] participants = ev.Participants;
				num = ((participants != null && participants.Length > 0) ? 1 : 0);
			}
			else
			{
				num = 0;
			}
			if (num != 0)
			{
				DateTime nowJoin = DateTimeOffset.UtcNow.UtcDateTime;
				string[] participants2 = ev.Participants;
				foreach (string pjid in participants2)
				{
					joins.Record(NumberUtil.Normalize(pjid), nowJoin);
				}
			}
			if (!(g?.WelcomeEnabled ?? config.WelcomeEnabled))
			{
				return Results.Json(new
				{
					ok = true,
					action = "welcome-disabled"
				});
			}
			if (QuietHours.IsActive(g?.QuietHours ?? config.QuietHours, DateTimeOffset.UtcNow))
			{
				return Results.Json(new
				{
					ok = true,
					action = "quiet-welcome"
				});
			}
			if (ev.Participants == null || ev.Participants.Length == 0)
			{
				return Results.Json(new
				{
					ok = true,
					action = "no-participants"
				});
			}
			string welcomeMsg = g?.WelcomeMessage ?? config.WelcomeMessage;
			string rulesText = g?.RulesText ?? config.RulesText;
			string[] participants3 = ev.Participants;
			foreach (string p in participants3)
			{
				string number = NumberUtil.Normalize(p);
				string text = welcomeMsg.Replace("@user", "@" + number).Replace("{group}", ev.GroupName ?? "").Replace("{rules}", rulesText);
				await PostImportant(ChannelRoute.BaseForJid(config, ev.Jid) + "/send", new
				{
					jid = ev.Jid,
					text = text,
					mentions = new string[1] { p }
				});
			}
			app.Logger.LogInformation("Sambutan dikirim ke {Count} member baru di {Jid}", ev.Participants.Length, ev.Jid);
			return Results.Json(new
			{
				ok = true,
				action = "welcomed",
				count = ev.Participants.Length
			});
		}

		internal async Task<IResult> lam_23(BroadcastRequest req)
		{
			if (string.IsNullOrWhiteSpace(config.BroadcastToken))
			{
				return Results.Json(new
				{
					ok = false,
					error = "broadcast nonaktif (set broadcastToken di config)"
				}, (JsonSerializerOptions?)null, (string?)null, (int?)403);
			}
			if (req.Token != config.BroadcastToken)
			{
				return Results.Json(new
				{
					ok = false,
					error = "token salah"
				}, (JsonSerializerOptions?)null, (string?)null, (int?)401);
			}
			if (string.IsNullOrWhiteSpace(req.Text))
			{
				return Results.Json(new
				{
					ok = false,
					error = "text wajib diisi"
				}, (JsonSerializerOptions?)null, (string?)null, (int?)400);
			}
			string jid = req.Jid;
			int tid = default(int);
			int num;
			if (string.IsNullOrWhiteSpace(jid))
			{
				int? tournamentId = req.TournamentId;
				if (tournamentId.HasValue)
				{
					tid = tournamentId.GetValueOrDefault();
					num = 1;
				}
				else
				{
					num = 0;
				}
			}
			else
			{
				num = 0;
			}
			if (num != 0)
			{
				config.TournamentGroups.TryGetValue(tid.ToString(), out jid);
			}
			if (string.IsNullOrWhiteSpace(jid))
			{
				return Results.Json(new
				{
					ok = false,
					error = "sertakan 'jid' grup atau 'tournamentId' yang terdaftar di tournamentGroups"
				}, (JsonSerializerOptions?)null, (string?)null, (int?)400);
			}
			string outText = await EnrichBroadcastText(req.Text, http, app.Logger, config);
			if (!(await PostImportant(ChannelRoute.BaseForJid(config, jid) + "/send", new
			{
				jid = jid,
				text = outText,
				mentions = ExtractWhatsAppMentions(outText)
			})))
			{
				app.Logger.LogWarning("Broadcast GAGAL ke {Jid}", jid);
				return Results.Json(new
				{
					ok = false,
					error = "gagal kirim ke gateway/channel",
					jid = jid
				}, (JsonSerializerOptions?)null, (string?)null, (int?)502);
			}
			app.Logger.LogInformation("Broadcast ke {Jid} ({Len} karakter)", jid, outText.Length);
			return Results.Json(new
			{
				ok = true,
				action = "broadcast",
				jid = jid
			});
		}

		internal void SaveActivePuzzles()
		{
			try
			{
				string contents;
				lock (puzzleLock)
				{
					contents = JsonSerializer.Serialize(activePuzzles);
				}
				File.WriteAllText(activePuzzlePath, contents);
			}
			catch
			{
			}
		}

		internal async Task PostPuzzleAsync(string jid, bool isDaily, PuzzleItem? chosen = null, PuzzleDailySlot? slot = null)
		{
			DC_0_10 cl_11 = new DC_0_10
			{
				jid = jid
			};
			PuzzleConfig pc = config.Puzzle;
			if (pc == null || puzzlePool.Count == 0)
			{
				return;
			}
			PuzzleItem puzzle;
			if (chosen != null)
			{
				puzzle = chosen;
			}
			else
			{
				DC_0_11 cl_12 = new DC_0_11();
				lock (puzzleLock)
				{
					cl_12.activeIds = (from a in activePuzzles.Values
						where !a.Revealed && a.Jid != cl_11.jid
						select a.Puzzle.Id).ToHashSet();
				}
				int pzMin = slot?.MinRating ?? 0;
				int pzMax = (slot != null && slot.MaxRating > 0) ? slot.MaxRating : 9999;
				string pzTheme = slot?.Theme ?? "";
				List<PuzzleItem> cand = puzzlePool.Where((PuzzleItem p) => !cl_12.activeIds.Contains(p.Id) && p.Rating >= pzMin && p.Rating <= pzMax && (pzTheme.Length == 0 || p.Themes.IndexOf(pzTheme, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
				if (cand.Count == 0)
				{
					cand = puzzlePool.Where((PuzzleItem p) => p.Rating >= pzMin && p.Rating <= pzMax && (pzTheme.Length == 0 || p.Themes.IndexOf(pzTheme, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
				}
				if (cand.Count == 0 && pzTheme.Length > 0)
				{
					cand = puzzlePool.Where((PuzzleItem p) => p.Themes.IndexOf(pzTheme, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
				}
				if (cand.Count == 0)
				{
					cand = puzzlePool;
				}
				puzzle = cand[Random.Shared.Next(cand.Count)];
			}
			string img;
			try
			{
				img = BoardRenderer.Render(puzzle.Fen, puzzle.Side == "b", puzzleCacheDir, pieceAssetsDir);
			}
			catch (Exception ex)
			{
				app.Logger.LogError("Render puzzle gagal: {Msg}", ex.Message);
				return;
			}
			string sideText = ((puzzle.Side == "w") ? "Putih" : "Hitam");
			string head = (isDaily ? ("\ud83e\udde9 *Puzzle " + ((!string.IsNullOrWhiteSpace(slot?.Label)) ? slot.Label : "Harian") + "*") : "\ud83e\udde9 *Puzzle*");
			config.Groups.TryGetValue(cl_11.jid, out GroupConfig gp);
			int revealMin = gp?.PuzzleRevealMinutes ?? slot?.RevealMinutes ?? pc.RevealMinutes;
			int tierPts = PuzzleScoreStore.Tier(puzzle.Rating);
			string tierLabel = ((tierPts >= 3) ? "sulit" : ((tierPts == 2) ? "menengah" : "mudah"));
			string caption = $"{head} - level {puzzle.Rating} ({tierLabel}, +{tierPts} poin/langkah)\n*{sideText} jalan.* Silakan cari langkah terbaiknya.\n\n" + "Balas (reply) pesan ini dengan notasi langkahmu. Aku bantu cek, dan kalau ada lanjutan kita teruskan bareng.\n" + $"Solusi otomatis dalam {revealMin} menit. Ketik {config.CommandPrefix}peringkat untuk papan skor.";
			string msgId = await PostAndGetId(http, ChannelRoute.BaseForJid(config, cl_11.jid) + "/send-image", new
			{
				jid = cl_11.jid,
				path = img,
				caption = caption
			});
			if (msgId == null)
			{
				app.Logger.LogWarning("Kirim gambar puzzle GAGAL ke {Jid} - puzzle tidak diaktifkan.", cl_11.jid);
				return;
			}
			DateTime nowUtc = DateTimeOffset.UtcNow.UtcDateTime;
			ActivePuzzle ap = new ActivePuzzle
			{
				Puzzle = puzzle,
				RevealAt = nowUtc.AddMinutes(revealMin),
				Revealed = false,
				MsgId = msgId,
				Jid = cl_11.jid,
				PostedAt = nowUtc
			};
			lock (puzzleLock)
			{
				activePuzzles[cl_11.jid] = ap;
				TopicStore.Set(cl_11.jid, "puzzle");
				if (msgId.Length > 0)
				{
					puzzleByMsg[msgId] = ap;
				}
				if (puzzleByMsg.Count > 80)
				{
					foreach (string k in (from kv in puzzleByMsg
						where kv.Value.Revealed
						select kv.Key).ToList())
					{
						puzzleByMsg.Remove(k);
					}
				}
			}
			SaveActivePuzzles();
			app.Logger.LogInformation("Puzzle dikirim ke {Jid} (id {Id}, rating {R}, daily={D})", cl_11.jid, puzzle.Id, puzzle.Rating, isDaily);
		}

		internal async Task RevealPuzzleAsync(string jid, ActivePuzzle ap, bool auto)
		{
			lock (puzzleLock)
			{
				if (ap.Revealed)
				{
					return;
				}
				ap.Revealed = true;
			}
			SaveActivePuzzles();
			string sideTxt = ((ap.Puzzle.Side == "w") ? "Putih" : "Hitam");
			string head = (auto ? "⏰ *Waktunya solusi!*" : "\ud83d\udd11 *Solusi*");
			string caption = $"{head} (puzzle level {ap.Puzzle.Rating}, {sideTxt} jalan)\n\ud83d\udd11 {FormatPuzzleSolution(ap.Puzzle)}";
			if (config.Ai is { Enabled: true })
			{
				try
				{
					string idePrompt = "Ini puzzle catur. FEN: " + ap.Puzzle.Fen + ". Solusi lengkap: " + FormatPuzzleSolution(ap.Puzzle) + ". Jelaskan IDE/taktik utama solusi ini dalam SATU kalimat pendek Bahasa Indonesia (mis. 'korban menteri untuk membuka jalur benteng lalu skakmat'). JANGAN tulis notasi langkah apa pun. Maksimal 16 kata.";
					string ide = await Ai.Ask(config.Ai, http, idePrompt, app.Logger);
					if (!string.IsNullOrWhiteSpace(ide))
					{
						ide = PuzzleMove.StripNotation(ide.Trim());
						if (ide.Length > 160)
						{
							ide = ide.Substring(0, 160) + "\u2026";
						}
						if (ide.Length > 0)
						{
							caption = caption + "\n\U0001F4A1 Ide: " + ide;
						}
					}
				}
				catch
				{
				}
			}
			string img = null;
			try
			{
				img = BoardRenderer.Render(ap.Puzzle.Fen, ap.Puzzle.Side == "b", puzzleCacheDir, pieceAssetsDir);
			}
			catch
			{
			}
			if (img != null)
			{
				await PostImportant(ChannelRoute.BaseForJid(config, jid) + "/send-image", new
				{
					jid = jid,
					path = img,
					caption = caption
				});
			}
			else
			{
				await PostImportant(ChannelRoute.BaseForJid(config, jid) + "/send", new
				{
					jid = jid,
					text = caption
				});
			}
		}

		internal void ReloadRuntimeConfig(string reason)
		{
			lock (reloadLock)
			{
				AppConfig appConfig = ConfigStore.LoadConfig(configDir);
				List<Rule> list = ConfigStore.LoadRules(configDir, app.Logger);
				config = appConfig;
				rules = list;
				exempt = BuildExempt(config);
				floodTracker = new FloodTracker(config.FloodMaxMessages, config.FloodWindowSeconds, config.FloodWarnCooldownSeconds);
				app.Logger.LogInformation("Reload otomatis ({Reason}): {Rules} aturan, {Exempt} exempt", reason, rules.Count, exempt.Count);
			}
		}

		internal FileSystemWatcher StartConfigWatcher()
		{
			DC_0_12 cl_4 = new DC_0_12
			{
				cl_6 = this
			};
			FileSystemWatcher fileSystemWatcher = new FileSystemWatcher(configDir, "*.json")
			{
				IncludeSubdirectories = false,
				NotifyFilter = (NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite),
				EnableRaisingEvents = true
			};
			cl_4.debounceTimer = null;
			fileSystemWatcher.Changed += delegate(object _, FileSystemEventArgs e)
			{
				cl_4.QueueReload(e.FullPath);
			};
			fileSystemWatcher.Created += delegate(object _, FileSystemEventArgs e)
			{
				cl_4.QueueReload(e.FullPath);
			};
			fileSystemWatcher.Renamed += delegate(object _, RenamedEventArgs e)
			{
				cl_4.QueueReload(e.FullPath);
			};
			return fileSystemWatcher;
		}
	}

	[CompilerGenerated]
	private sealed class DC_0_1
	{
		public DateTime nowLocal;

		public Func<PuzzleDailySlot, bool> cd9_43;

		internal bool lam_43(PuzzleDailySlot s)
		{
			return s.Hour == nowLocal.Hour;
		}
	}

	[CompilerGenerated]
	private sealed class DC_0_10
	{
		public string jid;

		internal bool lam_75(ActivePuzzle a)
		{
			return !a.Revealed && a.Jid != jid;
		}
	}

	[CompilerGenerated]
	private sealed class DC_0_11
	{
		public HashSet<string> activeIds;

		internal bool lam_74(PuzzleItem p)
		{
			return !activeIds.Contains(p.Id);
		}
	}

	[CompilerGenerated]
	private sealed class DC_0_12
	{
		public Timer debounceTimer;

		public DC_0_0 cl_6;

		internal void QueueReload(string path)
		{
			DC_0_13 cl_6 = new DC_0_13
			{
				cl_7 = this,
				fileName = Path.GetFileName(path)
			};
			if (!cl_6.fileName.Equals("config.json", StringComparison.OrdinalIgnoreCase) && !cl_6.fileName.Equals("rules.json", StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			debounceTimer?.Dispose();
			debounceTimer = new Timer(delegate
			{
				try
				{
					cl_6.cl_7.cl_6.ReloadRuntimeConfig("file changed: " + cl_6.fileName);
				}
				catch (Exception exception)
				{
					cl_6.cl_7.cl_6.app.Logger.LogError(exception, "Gagal reload otomatis dari {File}", cl_6.fileName);
				}
			}, null, TimeSpan.FromMilliseconds(700L), Timeout.InfiniteTimeSpan);
		}

		internal void lam_80(object _, FileSystemEventArgs e)
		{
			QueueReload(e.FullPath);
		}

		internal void lam_81(object _, FileSystemEventArgs e)
		{
			QueueReload(e.FullPath);
		}

		internal void lam_82(object _, RenamedEventArgs e)
		{
			QueueReload(e.FullPath);
		}
	}

	[CompilerGenerated]
	private sealed class DC_0_13
	{
		public string fileName;

		public DC_0_12 cl_7;

		internal void lam_83(object? _)
		{
			try
			{
				cl_7.cl_6.ReloadRuntimeConfig("file changed: " + fileName);
			}
			catch (Exception exception)
			{
				cl_7.cl_6.app.Logger.LogError(exception, "Gagal reload otomatis dari {File}", fileName);
			}
		}
	}

	[CompilerGenerated]
	private sealed class DC_0_2
	{
		public string gj;

		internal bool lam_44(ActivePuzzle a)
		{
			return !a.Revealed && a.Jid != gj;
		}
	}

	[CompilerGenerated]
	private sealed class DC_0_3
	{
		public DateTimeOffset now;

		public int[] reminders;

		public DC_0_0 cl_1;

		internal object lam_50(SwissItem t)
		{
			DC_0_4 cl_6 = new DC_0_4
			{
				cl_2 = this,
				t = t
			};
			return new
			{
				Name = cl_6.t.Name,
				Id = cl_6.t.Id,
				minutesUntilStart = (int)(cl_6.t.StartsAt - now).TotalMinutes,
				dueNow = reminders.Where(delegate(int T)
				{
					double totalMinutes = (cl_6.t.StartsAt - cl_6.cl_2.now).TotalMinutes;
					return totalMinutes > 0.0 && totalMinutes <= (double)T && totalMinutes >= (double)(T - 60);
				}).ToArray(),
				sample = Announcer.BuildText(cl_1.config, cl_6.t, (reminders.Length != 0) ? reminders[0] : 300)
			};
		}
	}

	[CompilerGenerated]
	private sealed class DC_0_4
	{
		public SwissItem t;

		public DC_0_3 cl_2;

		internal bool lam_51(int T)
		{
			double totalMinutes = (t.StartsAt - cl_2.now).TotalMinutes;
			return totalMinutes > 0.0 && totalMinutes <= (double)T && totalMinutes >= (double)(T - 60);
		}
	}

	[CompilerGenerated]
	private sealed class DC_0_5
	{
		public GroupConfig g;

		public IncomingMessage msg;

		public DC_0_0 cl_3;

		internal bool lam_53(Rule r)
		{
			return RuleActive(r, g) && !r.Shadow && r.Compiled.IsMatch(msg.Text);
		}

		internal bool lam_62(Rule r)
		{
			return RuleActive(r, g) && r.Shadow && r.Compiled.IsMatch(msg.Text);
		}
	}

	[CompilerGenerated]
	private sealed class DC_0_6
	{
		public List<string> targets;

		public string outText;

		public int throttleMs;

		public string hubJid;

		public DC_0_5 cl_4;

		internal async Task? lam_55()
		{
			int okCount = 0;
			foreach (string tj in targets)
			{
				try
				{
					if (await PostJson(cl_4.cl_3.http, ChannelRoute.BaseForJid(cl_4.cl_3.config, tj) + "/send", new
					{
						jid = tj,
						text = outText
					}))
					{
						okCount++;
					}
				}
				catch (Exception ex)
				{
					cl_4.cl_3.app.Logger.LogError("Announcement gagal ke {Jid}: {Msg}", tj, ex.Message);
				}
				if (throttleMs > 0)
				{
					await Task.Delay(throttleMs);
				}
			}
			await PostJson(cl_4.cl_3.http, ChannelRoute.BaseForJid(cl_4.cl_3.config, hubJid) + "/send", new
			{
				jid = hubJid,
				text = $"Announcement terkirim ke {okCount}/{targets.Count} grup."
			});
		}
	}

	[CompilerGenerated]
	private sealed class DC_0_7
	{
		public List<string> targetJids;

		public string outText;

		public int throttleMs;

		public string hubJid;

		public DC_0_5 cl_5;

		internal async Task? lam_58()
		{
			int okCount = 0;
			foreach (string tj in targetJids)
			{
				try
				{
					if (await PostJson(cl_5.cl_3.http, ChannelRoute.BaseForJid(cl_5.cl_3.config, tj) + "/send", new
					{
						jid = tj,
						text = outText
					}))
					{
						okCount++;
					}
					else
					{
						cl_5.cl_3.app.Logger.LogWarning("Relay gagal (gateway tolak) ke {Jid}", tj);
					}
				}
				catch (Exception ex)
				{
					cl_5.cl_3.app.Logger.LogError("Relay gagal ke {Jid}: {Msg}", tj, ex.Message);
				}
				if (throttleMs > 0)
				{
					await Task.Delay(throttleMs);
				}
			}
			await PostJson(cl_5.cl_3.http, ChannelRoute.BaseForJid(cl_5.cl_3.config, hubJid) + "/send", new
			{
				jid = hubJid,
				text = $"Selesai menyebar ke {okCount}/{targetJids.Count} grup."
			});
		}
	}

	[CompilerGenerated]
	private sealed class DC_0_9
	{
		public HashSet<string> excludeIds;

		public HashSet<int> usedIdx;

		public int min;

		public int max;

		internal bool NotActive((PuzzleItem p, int i) x)
		{
			return excludeIds == null || !excludeIds.Contains(x.p.Id);
		}

		internal bool lam_68((PuzzleItem p, int i) x)
		{
			return !usedIdx.Contains(x.i) && NotActive(x) && x.p.Rating >= min && x.p.Rating <= max;
		}

		internal bool lam_70((PuzzleItem p, int i) x)
		{
			return !usedIdx.Contains(x.i) && NotActive(x);
		}

		internal bool lam_72((PuzzleItem p, int i) x)
		{
			return !usedIdx.Contains(x.i);
		}
	}

	private static void Main(string[] args)
	{
		DC_0_0 cl_472 = new DC_0_0();
		WebApplicationBuilder webApplicationBuilder = WebApplication.CreateBuilder(args);
		cl_472.app = webApplicationBuilder.Build();
		SendLog.Logger = cl_472.app.Logger;
		string contentRootPath = webApplicationBuilder.Environment.ContentRootPath;
		cl_472.configDir = Path.Combine(contentRootPath, "config");
		string text = Path.Combine(contentRootPath, "logs");
		string text2 = Path.Combine(contentRootPath, "data");
		Directory.CreateDirectory(text);
		Directory.CreateDirectory(text2);
		cl_472.config = ConfigStore.LoadConfig(cl_472.configDir);
		cl_472.rules = ConfigStore.LoadRules(cl_472.configDir, cl_472.app.Logger);
		cl_472.exempt = BuildExempt(cl_472.config);
		cl_472.audit = new AuditLog(Path.Combine(text, "audit.log"));
		ConvMemory.Init(Path.Combine(text2, "conv-memory.json"));
		RetryQueue.Init(Path.Combine(text2, "retry-queue.json"));
		Sleeper.Init(Path.Combine(text2, "asleep.flag"));
		cl_472.warnings = new WarningStore(Path.Combine(text2, "warnings.json"));
		cl_472.joins = new JoinStore(Path.Combine(text2, "joins.json"));
		cl_472.floodTracker = new FloodTracker(cl_472.config.FloodMaxMessages, cl_472.config.FloodWindowSeconds, cl_472.config.FloodWarnCooldownSeconds);
		cl_472.cmdCooldown = new CooldownTracker();
		cl_472.reloadLock = new object();
		cl_472.app.Logger.LogInformation("Brain siap. {Rules} aturan, {Exempt} nomor dikecualikan, {Warned} riwayat peringatan dimuat.", cl_472.rules.Count, cl_472.exempt.Count, cl_472.warnings.Count);
		cl_472.http = new HttpClient();
		cl_472.startedAt = DateTime.UtcNow;
		cl_472.configWatcher = cl_472.StartConfigWatcher();
		cl_472.app.Lifetime.ApplicationStopping.Register(delegate
		{
			cl_472.configWatcher.Dispose();
		});
		string sentPath = Path.Combine(text2, "announcer-sent.json");
		string resultsPath = Path.Combine(text2, "results-sent.json");
		Announcer.RunLoop(() => cl_472.config, cl_472.http, cl_472.app.Logger, sentPath, resultsPath);
		string sentPath2 = Path.Combine(text2, "ccl-sent.json");
		Ccl.RunLoop(() => cl_472.config, cl_472.http, cl_472.app.Logger, sentPath2);
		AdminSync.RunLoop(() => cl_472.config, cl_472.http, cl_472.app.Logger);
		string statePath = Path.Combine(text2, "modreport-last.txt");
		ModerationReport.RunLoop(() => cl_472.config, cl_472.audit, cl_472.http, cl_472.app.Logger, statePath);
		RetryQueue.RunLoop(cl_472.http, cl_472.app.Logger);
		cl_472.sessions = new SessionStore();
		cl_472.puzzleCacheDir = Path.Combine(text2, "puzzle-cache");
		cl_472.pieceAssetsDir = Path.Combine(contentRootPath, "assets", "pieces");
		cl_472.activePuzzlePath = Path.Combine(text2, "active-puzzles.json");
		cl_472.puzzlePool = LoadPuzzlePool(Path.Combine(text2, "puzzles.json"), cl_472.app.Logger);
			AliasStore.Init(Path.Combine(text2, "aliases.json"));
			TagAliasStore.Init(Path.Combine(text2, "tag-aliases.json"));
			PairingCommand.ResumePoller(cl_472.config, cl_472.http, cl_472.app.Logger);
		cl_472.activePuzzles = LoadActivePuzzles(cl_472.activePuzzlePath);
		cl_472.puzzleByMsg = new Dictionary<string, ActivePuzzle>();
		foreach (ActivePuzzle value in cl_472.activePuzzles.Values)
		{
			if (value.MsgId.Length > 0)
			{
				cl_472.puzzleByMsg[value.MsgId] = value;
			}
		}
		cl_472.puzzleLock = new object();
		cl_472.puzzleDailyStatePath = Path.Combine(text2, "puzzle-daily.json");
		PuzzleScoreStore.Init(Path.Combine(text2, "puzzle-scores.json"));
		StockfishEngine.Init(Path.Combine(contentRootPath, "engine", "stockfish.exe"), 1500);
		cl_472.app.Logger.LogInformation("Stockfish engine: {S}", StockfishEngine.Available ? "siap" : "TAK ADA (engine/stockfish.exe)");
		cl_472.app.Logger.LogInformation("Pool puzzle: {N} dimuat.", cl_472.puzzlePool.Count);
		Task.Run(async delegate
		{
			HashSet<string> sentSlots = LoadPuzzleDailyState(cl_472.puzzleDailyStatePath);
			while (true)
			{
				try
				{
					PuzzleConfig pc = cl_472.config.Puzzle;
					if (pc != null && pc.Enabled && cl_472.puzzlePool.Count > 0)
					{
						DateTime nowLocal = DateTime.UtcNow.AddHours(pc.TimezoneOffsetHours);
						string today = nowLocal.ToString("yyyy-MM-dd");
						string[] groupJids = pc.GroupJids;
						List<string> dailyTargets = ((groupJids != null && groupJids.Length > 0) ? pc.GroupJids.Where((string s) => !string.IsNullOrWhiteSpace(s)).ToList() : (string.IsNullOrWhiteSpace(pc.GroupJid) ? new List<string>() : new List<string> { pc.GroupJid }));
						PuzzleDailySlot[] dailySlots = pc.DailySlots;
						PuzzleDailySlot[] dailySlots2 = ((dailySlots != null && dailySlots.Length > 0) ? pc.DailySlots : new PuzzleDailySlot[1]
						{
							new PuzzleDailySlot
							{
								Hour = pc.DailyHour,
								RevealMinutes = pc.RevealMinutes,
								MinRating = 0,
								MaxRating = 9999,
								Label = "Harian"
							}
						});
						foreach (PuzzleDailySlot slot in dailySlots2.Where((PuzzleDailySlot s) => s.Hour == nowLocal.Hour))
						{
							if (dailyTargets.Count != 0)
							{
								HashSet<int> usedIdx = new HashSet<int>();
								foreach (string gj in dailyTargets)
								{
									HashSet<string> activeIds;
									lock (cl_472.puzzleLock)
									{
										activeIds = (from a in cl_472.activePuzzles.Values
											where !a.Revealed && a.Jid != gj
											select a.Puzzle.Id).ToHashSet();
									}
									string slotKey = $"{today}|{slot.Hour}|{slot.Label}|{gj}";
									if (!sentSlots.Contains(slotKey))
									{
										ActivePuzzle curp;
										lock (cl_472.puzzleLock)
										{
											cl_472.activePuzzles.TryGetValue(gj, out curp);
										}
										if (curp != null && !curp.Revealed)
										{
											await cl_472.RevealPuzzleAsync(gj, curp, true);
										}
										PuzzleItem puzzle = PickPuzzleForSlot(cl_472.puzzlePool, slot, usedIdx, activeIds);
										await cl_472.PostPuzzleAsync(gj, true, puzzle, slot);
										sentSlots.Add(slotKey);
										SavePuzzleDailyState(cl_472.puzzleDailyStatePath, sentSlots, today);
										activeIds = null;
										curp = null;
									}
								}
							}
						}
						List<(string jid, ActivePuzzle ap)> due = new List<(string, ActivePuzzle)>();
						lock (cl_472.puzzleLock)
						{
							foreach (KeyValuePair<string, ActivePuzzle> kv in cl_472.activePuzzles)
							{
								if (!kv.Value.Revealed && DateTimeOffset.UtcNow.UtcDateTime >= kv.Value.RevealAt)
								{
									due.Add((kv.Key, kv.Value));
								}
							}
						}
						foreach (var (jid, ap) in due)
						{
							await cl_472.RevealPuzzleAsync(jid, ap, true);
						}
					}
				}
				catch (Exception ex)
				{
					Exception ex2 = ex;
					cl_472.app.Logger.LogError("Puzzle loop error: {Msg}", ex2.Message);
				}
				await Task.Delay(TimeSpan.FromSeconds(30L));
			}
		});
		cl_472.app.MapGet("/health", (Func<IResult>)(() => Results.Json(new
		{
			ok = true,
			rules = cl_472.rules.Count,
			exempt = cl_472.exempt.Count,
			warned = cl_472.warnings.Count,
			puzzlePool = cl_472.puzzlePool.Count
		})));
		cl_472.app.MapGet("/analyze", (Func<string, Task<IResult>>)async delegate(string? q)
		{
			ChessAnalysis.Output o = await ChessAnalysis.Run(q ?? "", cl_472.config.Ai, cl_472.http, cl_472.app.Logger);
			return Results.Json(new
			{
				ok = true,
				engine = StockfishEngine.Available,
				text = o?.Text,
				fen = o?.Fen
			});
		});
		cl_472.app.MapGet("/recognize", (Func<string, int?, Task<IResult>>)async delegate(string url, int? flip)
		{
			try
			{
				string fen = BoardVision.RecognizeFen(await cl_472.http.GetByteArrayAsync(url), cl_472.pieceAssetsDir, flip == 1);
				return Results.Json(new
				{
					ok = true,
					fen = fen
				});
			}
			catch (Exception ex)
			{
				Exception e = ex;
				return Results.Json(new
				{
					ok = false,
					error = e.Message
				});
			}
		});
		cl_472.app.MapGet("/stats", (Func<Task<IResult>>)async delegate
		{
			bool gw = false;
			try
			{
				gw = (await cl_472.http.GetStringAsync(cl_472.config.GatewayUrl + "/health")).Contains("\"connected\":true");
			}
			catch
			{
			}
			int modToday = cl_472.audit.LinesSince(DateTime.Now.Date).Count((string l) => l.Contains("| HAPUS |") && !l.Contains("aturan=SHADOW:"));
			return Results.Json(new
			{
				ok = true,
				uptimeMinutes = (int)(DateTime.UtcNow - cl_472.startedAt).TotalMinutes,
				gatewayConnected = gw,
				asleep = Sleeper.Asleep,
				sent = SendLog.Sent,
				failed = SendLog.Failed,
				retryQueue = RetryQueue.Count,
				activePuzzles = cl_472.activePuzzles.Count,
				warningsTracked = cl_472.warnings.Count,
				moderatedToday = modToday,
				rules = cl_472.rules.Count,
				managedGroups = (cl_472.config.ManageAllGroups ? (-1) : cl_472.config.Groups.Count)
			});
		});
		cl_472.app.MapGet("/admin/groups", (Func<HttpContext, IResult>)((HttpContext c) => cl_472.PanelAuthOk(c) ? Results.Json(new
		{
			ok = true,
			manageAll = cl_472.config.ManageAllGroups,
			groups = Enumerable.Select(cl_472.config.Groups, (KeyValuePair<string, GroupConfig> kv) => new
			{
				jid = kv.Key,
				label = kv.Value.Label
			}).ToList()
		}) : PanelDeny(c)));
		cl_472.app.MapGet("/admin/audit", (Func<HttpContext, int?, IResult>)delegate(HttpContext c, int? n)
		{
			IResult result;
			if (!cl_472.PanelAuthOk(c))
			{
				result = PanelDeny(c);
			}
			else
			{
				AuditLog auditLog = cl_472.audit;
				int n2;
				switch (n)
				{
				default:
					n2 = 15;
					break;
				case 1:
				case 2:
				case 3:
				case 4:
				case 5:
				case 6:
				case 7:
				case 8:
				case 9:
				case 10:
				case 11:
				case 12:
				case 13:
				case 14:
				case 15:
				case 16:
				case 17:
				case 18:
				case 19:
				case 20:
				case 21:
				case 22:
				case 23:
				case 24:
				case 25:
				case 26:
				case 27:
				case 28:
				case 29:
				case 30:
				case 31:
				case 32:
				case 33:
				case 34:
				case 35:
				case 36:
				case 37:
				case 38:
				case 39:
				case 40:
				case 41:
				case 42:
				case 43:
				case 44:
				case 45:
				case 46:
				case 47:
				case 48:
				case 49:
				case 50:
				case 51:
				case 52:
				case 53:
				case 54:
				case 55:
				case 56:
				case 57:
				case 58:
				case 59:
				case 60:
				case 61:
				case 62:
				case 63:
				case 64:
				case 65:
				case 66:
				case 67:
				case 68:
				case 69:
				case 70:
				case 71:
				case 72:
				case 73:
				case 74:
				case 75:
				case 76:
				case 77:
				case 78:
				case 79:
				case 80:
				case 81:
				case 82:
				case 83:
				case 84:
				case 85:
				case 86:
				case 87:
				case 88:
				case 89:
				case 90:
				case 91:
				case 92:
				case 93:
				case 94:
				case 95:
				case 96:
				case 97:
				case 98:
				case 99:
				case 100:
					n2 = n.Value;
					break;
				}
				result = Results.Json(new
				{
					ok = true,
					lines = auditLog.Tail(n2)
				});
			}
			return result;
		});
		cl_472.app.MapGet("/admin", (Func<HttpContext, IResult>)((HttpContext c) => cl_472.PanelAuthOk(c) ? Results.Content("<!doctype html><html lang=id><head><meta charset=utf-8><meta name=viewport content=\"width=device-width,initial-scale=1\">\r\n<title>WA Bot \\u2014 Admin</title><style>\r\nbody{font-family:system-ui,sans-serif;margin:0;background:#0f1216;color:#e6e6e6}\r\nheader{background:#16a34a;color:#fff;padding:12px 16px;font-weight:700}\r\n.wrap{padding:16px;max-width:900px;margin:auto}\r\n.card{background:#1a1f26;border:1px solid #2a2f37;border-radius:10px;padding:14px;margin:12px 0}\r\n.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(120px,1fr));gap:10px}\r\n.metric{background:#11151a;border-radius:8px;padding:10px;text-align:center}\r\n.metric b{display:block;font-size:1.4rem;color:#16a34a}.metric span{font-size:.78rem;color:#9aa4af}\r\nbutton{background:#16a34a;color:#fff;border:0;border-radius:8px;padding:8px 12px;cursor:pointer;font-weight:600;margin:2px}\r\nbutton.warn{background:#b45309}button.alt{background:#374151}\r\ninput,textarea{width:100%;box-sizing:border-box;background:#11151a;border:1px solid #2a2f37;color:#e6e6e6;border-radius:8px;padding:8px;margin:4px 0}\r\ntable{width:100%;border-collapse:collapse;font-size:.82rem}td,th{border-bottom:1px solid #2a2f37;padding:6px;text-align:left}\r\n.ok{color:#16a34a}.bad{color:#ef4444}pre{white-space:pre-wrap;font-size:.78rem;color:#9aa4af;max-height:240px;overflow:auto}\r\nh3{margin:.2rem 0 .6rem}</style></head><body>\r\n<header>\\U0001F916 WA Bot \\u2014 Panel Admin (lokal)</header><div class=wrap>\r\n<div class=card><h3>Status</h3><div class=grid id=metrics>memuat\\u2026</div></div>\r\n<div class=card><h3>Aksi cepat</h3>\r\n<input id=token placeholder=\"Token admin (untuk restart/broadcast)\">\r\n<div><button onclick=reload()>Reload config</button>\r\n<button class=alt onclick=\"restart('brain')\">Restart brain</button>\r\n<button class=alt onclick=\"restart('gateway')\">Restart gateway</button>\r\n<button class=warn onclick=\"restart('both')\">Restart both</button></div>\r\n<div id=msg></div></div>\r\n<div class=card><h3>Broadcast</h3>\r\n<input id=bjid placeholder=\"JID grup tujuan (mis. 1203...@g.us)\">\r\n<textarea id=btext placeholder=\"Isi pesan\"></textarea>\r\n<button onclick=broadcast()>Kirim broadcast</button></div>\r\n<div class=card><h3>Grup dikelola</h3><div id=groups>memuat\\u2026</div></div>\r\n<div class=card><h3>Audit moderasi terbaru</h3><pre id=audit>memuat\\u2026</pre></div></div>\r\n<script>\r\nconst $=s=>document.querySelector(s);\r\n$('#token').value=localStorage.getItem('wabotToken')||'';\r\n$('#token').oninput=e=>localStorage.setItem('wabotToken',e.target.value);\r\nasync function j(u,o){const r=await fetch(u,o);try{return await r.json()}catch{return{status:r.status}}}\r\nasync function refresh(){\r\n const s=await j('/stats');if(s.ok){$('#metrics').innerHTML=[\r\n  ['Gateway',s.gatewayConnected?'<span class=ok>OK</span>':'<span class=bad>OFF</span>'],\r\n  ['Terkirim',s.sent],['Gagal',s.failed],['Antre ulang',s.retryQueue],\r\n  ['Puzzle aktif',s.activePuzzles],['Moderasi/hari',s.moderatedToday],\r\n  ['Peringatan',s.warningsTracked],['Grup',s.managedGroups],['Aturan',s.rules],\r\n  ['Uptime(m)',s.uptimeMinutes],['Tidur',s.asleep?'ya':'tidak']\r\n ].map(m=>`<div class=metric><b>${m[1]}</b><span>${m[0]}</span></div>`).join('')}\r\n const g=await j('/admin/groups');$('#groups').innerHTML='<table><tr><th>Label</th><th>JID</th></tr>'+(g.groups||[]).map(x=>`<tr><td>${x.label||''}</td><td>${x.jid}</td></tr>`).join('')+'</table>';\r\n const a=await j('/admin/audit?n=15');$('#audit').textContent=(a.lines||[]).join('\\n')||'(belum ada)';}\r\nfunction note(t,ok){$('#msg').innerHTML=`<p class=\"${ok?'ok':'bad'}\">${t}</p>`}\r\nasync function reload(){const r=await j('/reload',{method:'POST'});note('Reload: '+(r.ok?'OK':'gagal'),r.ok);refresh()}\r\nasync function restart(t){const k=$('#token').value;if(!k)return note('Isi token dulu',false);note('Mengirim restart '+t+'\\u2026',true);await fetch(`/admin/restart?token=${encodeURIComponent(k)}&target=${t}`,{method:'POST'});setTimeout(()=>{note('Perintah restart '+t+' terkirim.',true);refresh()},1800)}\r\nasync function broadcast(){const k=$('#token').value,jid=$('#bjid').value,text=$('#btext').value;if(!k||!jid||!text)return note('Token, JID, teks wajib diisi',false);const r=await j('/broadcast',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({token:k,jid,text})});note('Broadcast: '+(r.ok?'terkirim \\u2705':('gagal \\u2014 '+(r.error||''))),r.ok)}\r\nrefresh();setInterval(refresh,5000);\r\n</script></body></html>", "text/html; charset=utf-8") : PanelDeny(c)));
		cl_472.app.MapPost("/reload", (Func<IResult>)delegate
		{
			cl_472.ReloadRuntimeConfig("manual /reload");
			return Results.Json(new
			{
				ok = true,
				rules = cl_472.rules.Count,
				exempt = cl_472.exempt.Count
			});
		});
		cl_472.app.MapGet("/lci/lookup", (Func<HttpContext, Task<IResult>>)async delegate(HttpContext ctxLci)
			{
				if (string.IsNullOrWhiteSpace(cl_472.config.AdminApiToken) || (string?)ctxLci.Request.Query["token"] != cl_472.config.AdminApiToken)
				{
					return Results.Json(new { ok = false, error = "token salah" }, (JsonSerializerOptions?)null, (string?)null, (int?)401);
				}
				string lciPhone = ctxLci.Request.Query["phone"].ToString();
				LciClient.LookupResult lr = await LciClient.LookupByPhone(cl_472.config, cl_472.http, lciPhone, cl_472.app.Logger);
				return Results.Json(new { ok = true, found = lr.Found, verified = lr.Verified, name = lr.FullName, handle = lr.Handle });
			});
			cl_472.app.MapPost("/admin/restart", (Func<HttpContext, Task<IResult>>)async delegate(HttpContext ctx)
		{
			if (string.IsNullOrWhiteSpace(cl_472.config.AdminApiToken))
			{
				return Results.Json(new
				{
					ok = false,
					error = "endpoint mati (set adminApiToken)"
				}, (JsonSerializerOptions?)null, (string?)null, (int?)403);
			}
			if ((string?)ctx.Request.Query["token"] != cl_472.config.AdminApiToken)
			{
				return Results.Json(new
				{
					ok = false,
					error = "token salah"
				}, (JsonSerializerOptions?)null, (string?)null, (int?)401);
			}
			string target = ctx.Request.Query["target"].ToString();
			if (string.IsNullOrWhiteSpace(target))
			{
				target = "both";
			}
			string text3 = target;
			bool flag = ((text3 == "gateway" || text3 == "both") ? true : false);
			bool doGw = flag;
			text3 = target;
			flag = ((text3 == "brain" || text3 == "both") ? true : false);
			bool doBrain = flag;
			if (doGw)
			{
				try
				{
					await cl_472.http.PostAsync(cl_472.config.GatewayUrl + "/admin/restart?token=" + Uri.EscapeDataString(cl_472.config.AdminApiToken), null);
				}
				catch (Exception ex)
				{
					Exception ex2 = ex;
					cl_472.app.Logger.LogWarning("Gagal minta gateway restart: {Msg}", ex2.Message);
				}
			}
			if (doBrain)
			{
				Task.Run(async delegate
				{
					await Task.Delay(800);
					cl_472.app.Logger.LogWarning("Restart brain via /admin/restart");
					Environment.Exit(0);
				});
			}
			return Results.Json(new
			{
				ok = true,
				restarting = new
				{
					brain = doBrain,
					gateway = doGw
				}
			});
		});
		cl_472.app.MapGet("/relay-config", (Func<IResult>)delegate
		{
			RelayConfig relay = cl_472.config.Relay;
			return Results.Json(new
			{
				enabled = (relay?.Enabled ?? false),
				hubGroupJid = (relay?.HubGroupJid ?? ""),
				command = (relay?.Command ?? "sebar"),
				prefix = cl_472.config.CommandPrefix,
				targetGroups = (relay?.TargetGroups ?? Array.Empty<string>()),
				throttleSeconds = (relay?.ThrottleSeconds ?? 4),
				footer = (relay?.Footer ?? ""),
				adminNumbers = AdminSync.Effective(cl_472.config)
			});
		});
		cl_472.app.MapGet("/announcer-preview", (Func<Task<IResult>>)async delegate
		{
			DC_0_0 DCv_0_ = cl_472;
			AnnouncerConfig announcer = cl_472.config.Announcer;
			if (announcer == null || !announcer.Enabled || string.IsNullOrWhiteSpace(cl_472.config.Announcer.TeamId))
			{
				return Results.Json(new
				{
					ok = false,
					error = "announcer tidak aktif / teamId kosong"
				});
			}
			List<SwissItem> list = await Announcer.Fetch(cl_472.config, cl_472.http, cl_472.app.Logger);
			DateTimeOffset now = DateTimeOffset.UtcNow;
			int[] reminders = cl_472.config.Announcer.RemindersMinutes;
			var preview = from t in list
				orderby t.StartsAt
				select new
				{
					Name = t.Name,
					Id = t.Id,
					minutesUntilStart = (int)(t.StartsAt - now).TotalMinutes,
					dueNow = reminders.Where(delegate(int T)
					{
						double totalMinutes = (t.StartsAt - now).TotalMinutes;
						return totalMinutes > 0.0 && totalMinutes <= (double)T && totalMinutes >= (double)(T - 60);
					}).ToArray(),
					sample = Announcer.BuildText(DCv_0_.config, t, (reminders.Length != 0) ? reminders[0] : 300)
				};
			return Results.Json(new
			{
				ok = true,
				count = list.Count,
				tournaments = preview
			});
		});
		cl_472.app.MapPost("/incoming", (Func<IncomingMessage, Task<IResult>>)async delegate(IncomingMessage msg)
		{
			DC_0_0 DCv_0_ = cl_472;
			if (string.IsNullOrWhiteSpace(msg.Text))
			{
				return Results.Json(new
				{
					ok = true,
					action = "ignored"
				});
			}
			if (string.IsNullOrWhiteSpace(msg.Jid))
			{
				return Results.Json(new
				{
					ok = true,
					action = "ignored"
				});
			}
			cl_472.config.Groups.TryGetValue(msg.Jid, out GroupConfig g);
			bool isPrivate = msg.Channel == "whatsapp" && !msg.Jid.EndsWith("@g.us");
			PrivateChatConfig? privateChat = cl_472.config.PrivateChat;
			bool? obj;
			if (privateChat == null)
			{
				obj = null;
			}
			else
			{
				string[] consoleGroupJids = privateChat.ConsoleGroupJids;
				obj = ((consoleGroupJids != null) ? new bool?(((ReadOnlySpan<string>)consoleGroupJids).Contains(msg.Jid)) : ((bool?)null));
			}
			bool? flag = obj;
			bool isConsole = flag == true;
			int num;
			if (isPrivate || isConsole)
			{
				PrivateChatConfig privateChat2 = cl_472.config.PrivateChat;
				if (privateChat2 != null)
				{
					bool enabled = privateChat2.Enabled;
					num = (enabled ? 1 : 0);
				}
				else
				{
					num = 0;
				}
			}
			else
			{
				num = 0;
			}
			bool dmAllowed = (byte)num != 0;
			bool pzlAnswerHere = false;
				if (g == null && msg.Channel == "whatsapp" && !dmAllowed)
				{
					ActivePuzzle apzU;
					lock (cl_472.puzzleLock)
					{
						cl_472.activePuzzles.TryGetValue(msg.Jid, out apzU);
					}
					if (apzU != null && !apzU.Revealed && PuzzleMove.IsMoveLike(PuzzleMove.StripMoveNumber(msg.Text.Trim())))
					{
						pzlAnswerHere = true;
					}
				}
				if (!cl_472.config.ManageAllGroups && g == null && msg.Channel == "whatsapp" && !dmAllowed && !pzlAnswerHere)
			{
				return Results.Json(new
				{
					ok = true,
					action = "unmanaged"
				});
			}
			bool eCommands = g?.CommandsEnabled ?? cl_472.config.CommandsEnabled;
			bool eFlood = g?.FloodEnabled ?? cl_472.config.FloodEnabled;
			bool eModeration = g?.ModerationEnabled ?? cl_472.config.ModerationEnabled;
			string trimmedText = msg.Text.TrimStart();
			string cmdText = Regex.Replace(trimmedText, "^(\\s*@\\d+\\s*)+", "").TrimStart();
			bool isCommand = cmdText.StartsWith(cl_472.config.CommandPrefix);
			string cmdName = (isCommand ? cmdText.Substring(cl_472.config.CommandPrefix.Length).TrimStart().Split(' ', 2)[0].ToLowerInvariant() : "");
			if (!isCommand && (g?.CommandsEnabled ?? cl_472.config.CommandsEnabled))
			{
				string natCmd = NaturalIntent.Detect(cl_472.config, cmdText, msg.MentionedBot);
				if (natCmd != null)
				{
					isCommand = true;
					cmdText = cl_472.config.CommandPrefix + natCmd;
					cmdName = natCmd.Split(' ', 2)[0].ToLowerInvariant();
				}
			}
			string senderNum = NumberUtil.Normalize(msg.Participant);
			string senderPhone = NumberUtil.Normalize(msg.ParticipantPhone);
			HashSet<string> groupExemptSet = (from text7 in (g?.ExemptNumbers ?? Array.Empty<string>()).Select(NumberUtil.Normalize)
				where text7.Length > 0
				select text7).ToHashSet();
			bool senderExempt = ModUtil.IdInSet(cl_472.exempt, senderPhone, senderNum) || ModUtil.IdInSet(groupExemptSet, senderPhone, senderNum);
			QuietHoursConfig quietCfg = g?.QuietHours ?? cl_472.config.QuietHours;
			bool quietNow = QuietHours.IsActive(quietCfg, DateTimeOffset.UtcNow);
			ConvContext ctx = new ConvContext
			{
				ConversationId = msg.Jid,
				SenderId = msg.Participant,
				SenderNum = senderNum,
				Channel = msg.Channel,
				Caps = Caps.Of(msg.Channel),
				IsExempt = senderExempt,
				QuietNow = quietNow,
				GroupLabel = (g?.Label ?? ""),
				WorkspaceName = (cl_472.config.Workspace?.Name ?? ""),
				Topic = TopicStore.Get(msg.Jid)
			};
			string outBase = ChannelRoute.Base(cl_472.config, ctx.Channel);
			if (msg.MentionedBot)
			{
				string? pairAct = await PairingCommand.TryHandle(cl_472.config, cl_472.http, cl_472.app.Logger, outBase, msg, senderNum, senderPhone);
				if (pairAct != null)
				{
					return Results.Json(new { ok = true, action = pairAct });
				}
			}
			if (isCommand && cmdName == "kirimpuzzle" && isConsole)
			{
				if (!AdminSync.IsAllowed(cl_472.config, senderNum, senderPhone))
				{
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "Perintah ini khusus admin." });
					return Results.Json(new { ok = true, action = "kirimpuzzle-denied" });
				}
				string kpArgs = cmdText.Substring(cl_472.config.CommandPrefix.Length).Trim();
				if (kpArgs.Length >= "kirimpuzzle".Length) kpArgs = kpArgs.Substring("kirimpuzzle".Length).Trim();
				string kpLevel = "";
				foreach (string kpLv in new[] { "mudah", "sedang", "sulit" })
				{
					if (kpArgs.EndsWith(kpLv, StringComparison.OrdinalIgnoreCase))
					{
						kpLevel = kpLv;
						kpArgs = kpArgs.Substring(0, kpArgs.Length - kpLv.Length).Trim();
						break;
					}
				}
				string kpName = kpArgs.Trim();
				if (kpName.Length == 0)
				{
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "Format: !kirimpuzzle <nama grup> [mudah|sedang|sulit]" });
					return Results.Json(new { ok = true, action = "kirimpuzzle-usage" });
				}
				string? kpAlias = AliasStore.Get(kpName);
				if (!string.IsNullOrEmpty(kpAlias)) kpName = kpAlias;
				List<GroupOption> kpGroups = await FetchGroups(cl_472.config.GatewayUrl, cl_472.http);
				List<GroupOption> kpMatch = MatchGroups(kpGroups, kpName);
				if (kpMatch.Count == 0)
				{
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "Grup \"" + kpName + "\" tidak ketemu. Cek nama persisnya." });
					return Results.Json(new { ok = true, action = "kirimpuzzle-notfound" });
				}
				if (kpMatch.Count > 1)
				{
					string kpListed = string.Join(", ", kpMatch.Select((GroupOption m) => "\"" + m.Subject + "\""));
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "Lebih dari satu grup cocok: " + kpListed + ". Perjelas namanya." });
					return Results.Json(new { ok = true, action = "kirimpuzzle-ambiguous" });
				}
				GroupOption kpTarget = kpMatch[0];
				await cl_472.PostPuzzleAsync(kpTarget.Jid, false, null, PuzzleMove.DifficultySlot("puzzle " + kpLevel, cl_472.config.Puzzle.RevealMinutes));
				await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "✅ Puzzle terkirim ke \"" + kpTarget.Subject + "\"" + ((kpLevel.Length > 0) ? (" (" + kpLevel + ")") : "") + "." });
				return Results.Json(new { ok = true, action = "kirimpuzzle", target = kpTarget.Jid });
			}
			if (isCommand && cmdName == "kirim" && isConsole)
			{
				if (!AdminSync.IsAllowed(cl_472.config, senderNum, senderPhone))
				{
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "Perintah ini khusus admin." });
					return Results.Json(new { ok = true, action = "kirim-denied" });
				}
				string kmArgs = cmdText.Substring(cl_472.config.CommandPrefix.Length).Trim();
				if (kmArgs.Length >= "kirim".Length) kmArgs = kmArgs.Substring("kirim".Length).Trim();
				string kmGroup = "";
				string kmMsg = "";
				if (kmArgs.StartsWith("\""))
				{
					int kmClose = kmArgs.IndexOf('"', 1);
					if (kmClose > 0)
					{
						kmGroup = kmArgs.Substring(1, kmClose - 1).Trim();
						kmMsg = kmArgs.Substring(kmClose + 1).Trim();
					}
				}
				else
				{
					string kmBestAlias = "";
					foreach (KeyValuePair<string, string> kv in AliasStore.All())
					{
						if (kv.Key.Length > kmBestAlias.Length && kmArgs.Length >= kv.Key.Length && kmArgs.Substring(0, kv.Key.Length).Equals(kv.Key, StringComparison.OrdinalIgnoreCase) && (kmArgs.Length == kv.Key.Length || kmArgs[kv.Key.Length] == ' '))
						{
							kmBestAlias = kv.Key;
						}
					}
					if (kmBestAlias.Length > 0)
					{
						kmGroup = kmBestAlias;
						kmMsg = kmArgs.Substring(kmBestAlias.Length).Trim();
					}
					else
					{
						int kmSp = kmArgs.IndexOf(' ');
						if (kmSp > 0)
						{
							kmGroup = kmArgs.Substring(0, kmSp).Trim();
							kmMsg = kmArgs.Substring(kmSp + 1).Trim();
						}
					}
				}
				if (kmMsg.Length >= 2 && kmMsg.StartsWith("\"") && kmMsg.EndsWith("\""))
				{
					kmMsg = kmMsg.Substring(1, kmMsg.Length - 2);
				}
				if (kmGroup.Length == 0 || kmMsg.Length == 0)
				{
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "Format: !kirim \"nama grup\" \"pesan\"  (atau: !kirim <alias> pesan)" });
					return Results.Json(new { ok = true, action = "kirim-usage" });
				}
				string? kmAlias = AliasStore.Get(kmGroup);
				if (!string.IsNullOrEmpty(kmAlias)) kmGroup = kmAlias;
				List<GroupOption> kmGroups = await FetchGroups(cl_472.config.GatewayUrl, cl_472.http);
				List<GroupOption> kmMatch = MatchGroups(kmGroups, kmGroup);
				if (kmMatch.Count == 0)
				{
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "Grup \"" + kmGroup + "\" tidak ketemu." });
					return Results.Json(new { ok = true, action = "kirim-notfound" });
				}
				if (kmMatch.Count > 1)
				{
					string kmListed = string.Join(", ", kmMatch.Select((GroupOption m) => "\"" + m.Subject + "\""));
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "Lebih dari satu grup cocok: " + kmListed + ". Perjelas namanya." });
					return Results.Json(new { ok = true, action = "kirim-ambiguous" });
				}
				GroupOption kmTarget = kmMatch[0];
				List<string> kmMentions = new List<string>();
				List<(string jid, string number, string phone)> kmMembers = await FetchGroupMembers(cl_472.config.GatewayUrl, cl_472.http, kmTarget.Jid);
				kmMsg = Regex.Replace(kmMsg, "@([A-Za-z0-9]+)", delegate(Match mt)
				{
					string tok = mt.Groups[1].Value;
					string wantPhone = TagAliasStore.Get(tok) ?? "";
					(string jid, string number, string phone) hit = default((string, string, string));
					bool found = false;
					if (wantPhone.Length > 0)
					{
						string wp = NumberUtil.Normalize(wantPhone);
						foreach ((string jid, string number, string phone) mem in kmMembers)
						{
							if (NumberUtil.Normalize(mem.phone) == wp && mem.jid.Length > 0)
							{
								hit = mem;
								found = true;
								break;
							}
						}
					}
					else if (tok.Length >= 3 && tok.All(char.IsDigit))
					{
						List<(string jid, string number, string phone)> sfx = kmMembers.Where(delegate((string jid, string number, string phone) mem)
						{
							string pp = NumberUtil.Normalize(mem.phone);
							return mem.jid.Length > 0 && pp.Length > 0 && (pp == tok || pp.EndsWith(tok));
						}).ToList();
						if (sfx.Count == 1)
						{
							hit = sfx[0];
							found = true;
						}
					}
					if (found)
					{
						kmMentions.Add(hit.jid);
						return "@" + hit.number;
					}
					return mt.Value;
				});
				await PostJson(cl_472.http, outBase + "/send", new { jid = kmTarget.Jid, text = kmMsg, mentions = kmMentions });
				await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "✅ Terkirim ke \"" + kmTarget.Subject + "\": " + kmMsg });
				return Results.Json(new { ok = true, action = "kirim", target = kmTarget.Jid });
			}
			if (isCommand && cmdName == "tag" && isConsole)
			{
				if (!AdminSync.IsAllowed(cl_472.config, senderNum, senderPhone))
				{
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "Perintah ini khusus admin." });
					return Results.Json(new { ok = true, action = "tag-denied" });
				}
				string tgArgs = cmdText.Substring(cl_472.config.CommandPrefix.Length).Trim();
				if (tgArgs.Length >= "tag".Length) tgArgs = tgArgs.Substring("tag".Length).Trim();
				if (tgArgs.Length == 0)
				{
					List<KeyValuePair<string, string>> tgAll = TagAliasStore.All();
					string tgList = (tgAll.Count == 0) ? "Belum ada tag-alias. Set dengan: !tag <nama> = <nomor>" : ("Tag-alias tersimpan:\n" + string.Join("\n", tgAll.Select((KeyValuePair<string, string> kv) => "- @" + kv.Key + " -> " + kv.Value)));
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = tgList });
					return Results.Json(new { ok = true, action = "tag-list" });
				}
				int tgEq = tgArgs.IndexOf('=');
				if (tgEq < 0)
				{
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "Format: !tag <nama> = <nomor>" });
					return Results.Json(new { ok = true, action = "tag-usage" });
				}
				string tgKey = tgArgs.Substring(0, tgEq).Trim().TrimStart('@');
				string tgVal = NumberUtil.Normalize(tgArgs.Substring(tgEq + 1));
				if (tgKey.Length == 0 || tgVal.Length == 0)
				{
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "Format: !tag <nama> = <nomor>" });
					return Results.Json(new { ok = true, action = "tag-usage" });
				}
				TagAliasStore.Set(tgKey, tgVal);
				await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "✅ Oke, @" + tgKey + " = " + tgVal + ". Aku ingat." });
				return Results.Json(new { ok = true, action = "tag-set" });
			}
			if (isCommand && cmdName == "untag" && isConsole)
			{
				if (!AdminSync.IsAllowed(cl_472.config, senderNum, senderPhone))
				{
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "Perintah ini khusus admin." });
					return Results.Json(new { ok = true, action = "untag-denied" });
				}
				string utgArgs = cmdText.Substring(cl_472.config.CommandPrefix.Length).Trim();
				if (utgArgs.Length >= "untag".Length) utgArgs = utgArgs.Substring("untag".Length).Trim();
				utgArgs = utgArgs.TrimStart('@');
				if (utgArgs.Length == 0)
				{
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "Format: !untag <nama>" });
					return Results.Json(new { ok = true, action = "untag-usage" });
				}
				bool utgRemoved = TagAliasStore.Remove(utgArgs);
				await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = utgRemoved ? ("Oke, tag-alias @" + utgArgs + " dihapus.") : ("Tag-alias @" + utgArgs + " tidak ada.") });
				return Results.Json(new { ok = true, action = "untag" });
			}
			if (isCommand && cmdName == "alias" && isConsole)
			{
				if (!AdminSync.IsAllowed(cl_472.config, senderNum, senderPhone))
				{
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "Perintah ini khusus admin." });
					return Results.Json(new { ok = true, action = "alias-denied" });
				}
				string alArgs = cmdText.Substring(cl_472.config.CommandPrefix.Length).Trim();
				if (alArgs.Length >= "alias".Length) alArgs = alArgs.Substring("alias".Length).Trim();
				if (alArgs.Length == 0)
				{
					List<KeyValuePair<string, string>> alAll = AliasStore.All();
					string alList = (alAll.Count == 0) ? "Belum ada alias. Set dengan: !alias <singkatan> = <nama grup>" : ("Alias tersimpan:\n" + string.Join("\n", alAll.Select((KeyValuePair<string, string> kv) => "- " + kv.Key + " -> " + kv.Value)));
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = alList });
					return Results.Json(new { ok = true, action = "alias-list" });
				}
				int alEq = alArgs.IndexOf('=');
				if (alEq < 0)
				{
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "Format: !alias <singkatan> = <nama grup>" });
					return Results.Json(new { ok = true, action = "alias-usage" });
				}
				string alKey = alArgs.Substring(0, alEq).Trim();
				string alVal = alArgs.Substring(alEq + 1).Trim();
				if (alKey.Length == 0 || alVal.Length == 0)
				{
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "Format: !alias <singkatan> = <nama grup>" });
					return Results.Json(new { ok = true, action = "alias-usage" });
				}
				List<GroupOption> alGroups = await FetchGroups(cl_472.config.GatewayUrl, cl_472.http);
				List<GroupOption> alMatch = MatchGroups(alGroups, alVal);
				if (alMatch.Count == 0)
				{
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "Grup \"" + alVal + "\" tidak ketemu. Alias tidak disimpan." });
					return Results.Json(new { ok = true, action = "alias-notfound" });
				}
				if (alMatch.Count > 1)
				{
					string alListed = string.Join(", ", alMatch.Select((GroupOption m) => "\"" + m.Subject + "\""));
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "Lebih dari satu grup cocok: " + alListed + ". Perjelas namanya." });
					return Results.Json(new { ok = true, action = "alias-ambiguous" });
				}
				AliasStore.Set(alKey, alMatch[0].Subject);
				await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "✅ Oke, \"" + alKey + "\" = \"" + alMatch[0].Subject + "\". Aku ingat." });
				return Results.Json(new { ok = true, action = "alias-set" });
			}
			if (isCommand && cmdName == "unalias" && isConsole)
			{
				if (!AdminSync.IsAllowed(cl_472.config, senderNum, senderPhone))
				{
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "Perintah ini khusus admin." });
					return Results.Json(new { ok = true, action = "unalias-denied" });
				}
				string ualArgs = cmdText.Substring(cl_472.config.CommandPrefix.Length).Trim();
				if (ualArgs.Length >= "unalias".Length) ualArgs = ualArgs.Substring("unalias".Length).Trim();
				if (ualArgs.Length == 0)
				{
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "Format: !unalias <singkatan>" });
					return Results.Json(new { ok = true, action = "unalias-usage" });
				}
				bool ualRemoved = AliasStore.Remove(ualArgs);
				await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = ualRemoved ? ("Oke, alias \"" + ualArgs + "\" sudah dihapus.") : ("Alias \"" + ualArgs + "\" tidak ada.") });
				return Results.Json(new { ok = true, action = "unalias" });
			}
			if (isCommand && (cmdName == "sleep" || cmdName == "wake"))
			{
				if (cmdName == "wake")
				{
					if (!AdminSync.IsAllowed(cl_472.config, senderNum, senderPhone))
					{
						return Results.Json(new
						{
							ok = true,
							action = "wake-denied"
						});
					}
					Sleeper.Set(false);
					await PostJson(cl_472.http, outBase + "/send", new
					{
						jid = msg.Jid,
						text = "Judit Polica aktif lagi. Siap bertugas."
					});
					return Results.Json(new
					{
						ok = true,
						action = "wake"
					});
				}
				await PostJson(cl_472.http, outBase + "/send", new
				{
					jid = msg.Jid,
					text = "Baik, saya istirahat dulu. Admin bisa membangunkan dengan *!wake*."
				});
				Sleeper.Set(true);
				return Results.Json(new
				{
					ok = true,
					action = "sleep"
				});
			}
			if (Sleeper.Asleep)
			{
				return Results.Json(new
				{
					ok = true,
					action = "asleep"
				});
			}
			string paKey = msg.Jid + "|" + senderNum;
			if (PendingAnalysis.Has(paKey))
			{
				string lowPaRaw = msg.Text.Trim().ToLowerInvariant();
				bool flipPa = lowPaRaw.Contains("balik") || lowPaRaw.Contains("terbalik") || lowPaRaw.Contains("flip");
				string lowPa = lowPaRaw.Replace("terbalik", "").Replace("balik", "").Replace("flip", "").Trim();
				bool enabled;
				switch (lowPa)
				{
				case "putih":
				case "white":
				case "w":
					enabled = true;
					break;
				default:
					enabled = false;
					break;
				}
				bool? flag2;
				if (enabled)
				{
					flag2 = true;
				}
				else
				{
					bool flag3;
					switch (lowPa)
					{
					case "hitam":
					case "black":
					case "b":
						flag3 = true;
						break;
					default:
						flag3 = false;
						break;
					}
					flag2 = (flag3 ? new bool?(false) : ((bool?)null));
				}
				bool? whitePa = flag2;
				if (whitePa.HasValue)
				{
					string placePa = PendingAnalysis.Take(paKey);
					if (placePa != null)
					{
						if (flipPa) placePa = BoardVision.FlipPlacement(placePa); // sisi Hitam -> putar 180
						cl_472.SendTyping(msg.Jid, ctx.Channel);
						string fenPa = BoardVision.BuildFullFen(placePa, whitePa.Value);
						ChessAnalysis.Output oPa = (await ChessAnalysis.Run(fenPa, cl_472.config.Ai, cl_472.http, cl_472.app.Logger)) ?? new ChessAnalysis.Output("Gagal menganalisa.", "");
						string imgPa = null;
						if (oPa.Fen.Length > 0)
						{
							try
							{
								imgPa = BoardRenderer.Render(oPa.Fen, !oPa.Fen.Contains(" w "), cl_472.puzzleCacheDir, cl_472.pieceAssetsDir);
							}
							catch
							{
							}
						}
						if (imgPa == null)
						{
							await PostJson(cl_472.http, outBase + "/send", new
							{
								jid = msg.Jid,
								text = "\ud83d\udcf7 " + oPa.Text
							});
						}
						else
						{
							await PostJson(cl_472.http, outBase + "/send-image", new
							{
								jid = msg.Jid,
								path = imgPa,
								caption = "\ud83d\udcf7 " + oPa.Text
							});
						}
						return Results.Json(new
						{
							ok = true,
							action = "analisa-answered"
						});
					}
				}
			}
			int cooldownSec = g?.CommandCooldownSeconds ?? cl_472.config.CommandCooldownSeconds;
			int num2;
			AiConfig ai;
			if (cooldownSec > 0 && !senderExempt)
			{
				if (!isCommand)
				{
					ai = cl_472.config.Ai;
					if (ai != null && ai.Enabled && ai.RequireMention)
					{
						num2 = (msg.MentionedBot ? 1 : 0);
						goto IL_117f;
					}
				}
				num2 = 0;
				goto IL_117f;
			}
			goto IL_12be;
			IL_ad48:
			object obj3;
			string ftmpl = (string)obj3;
			string number;
			int fcount;
			string ftext = ftmpl.Replace("@user", "@" + number).Replace("{count}", fcount.ToString());
			await PostJson(cl_472.http, outBase + "/send", new
			{
				jid = msg.Jid,
				text = ftext,
				mentions = new string[1] { msg.Participant }
			});
			cl_472.audit.Write(msg.Jid, msg.Participant, msg.PushName, "flood", fcount, msg.Text);
			cl_472.app.Logger.LogInformation("FLOOD dari {Number}, peringatan ke-{Count}", number, fcount);
			return Results.Json(new
			{
				ok = true,
				action = "flood",
				warned = true,
				warnCount = fcount
			});
			IL_117f:
			bool aiMention = (byte)num2 != 0;
			if (isCommand && cmdName != "batal" && !cl_472.cmdCooldown.Allow($"{msg.Jid}|{senderNum}|{cmdName}", cooldownSec))
			{
				return Results.Json(new
				{
					ok = true,
					action = "cooldown",
					cmd = cmdName
				});
			}
			if (aiMention && !cl_472.cmdCooldown.Allow(msg.Jid + "|" + senderNum + "|@ai", cooldownSec))
			{
				return Results.Json(new
				{
					ok = true,
					action = "cooldown",
					cmd = "ai"
				});
			}
			goto IL_12be;
			IL_12be:
			if (!isCommand && Regex.IsMatch(msg.Text, "\\b(report|lapor|blokir|ban)\\b.*\\b(bot|nomor|number|wa)\\b|\\b(bot|nomor|number|wa)\\b.*\\b(report|lapor|blokir|ban)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
			{
				if (quietNow)
				{
					return Results.Json(new
					{
						ok = true,
						action = "quiet-antireport"
					});
				}
				await PostJson(cl_472.http, outBase + "/send", new
				{
					jid = msg.Jid,
					text = "Bot bermasalah? Jangan report nomor. Ketik " + cl_472.config.CommandPrefix + "admin <kendala>."
				});
				return Results.Json(new
				{
					ok = true,
					action = "anti-report"
				});
			}
			PrivateChatConfig pcDM = default(PrivateChatConfig);
			int num3;
			if (isPrivate || isConsole)
			{
				pcDM = cl_472.config.PrivateChat;
				if (pcDM != null && pcDM.Enabled)
				{
					ai = cl_472.config.Ai;
					if (ai != null)
					{
						bool flag3 = ai.Enabled;
						num3 = (flag3 ? 1 : 0);
					}
					else
					{
						num3 = 0;
					}
					goto IL_1457;
				}
			}
			num3 = 0;
			goto IL_1457;
			IL_a94f:
			object obj4;
			string warnTmpl = (string)obj4;
			Rule matched;
			int count;
			string warnText = warnTmpl.Replace("@user", "@" + number).Replace("{reason}", matched.Reason ?? matched.Name ?? "aturan grup").Replace("{count}", count.ToString());
			await PostJson(cl_472.http, outBase + "/send", new
			{
				jid = msg.Jid,
				text = warnText,
				mentions = new string[1] { msg.Participant }
			});
			goto IL_aa9d;
			IL_195b:
			object obj5;
			string consoleJid = (string)obj5;
			string replyDM;
			if (isConsole)
			{
				await PostJson(cl_472.http, outBase + "/send", new
				{
					jid = msg.Jid,
					text = replyDM
				});
				return Results.Json(new
				{
					ok = true,
					action = "console-chat"
				});
			}
			string qDM;
			string replyJidDM = ((senderPhone.Length > 0) ? (senderPhone + "@s.whatsapp.net") : msg.Jid);
			bool directSentDM = await PostJson(cl_472.http, outBase + "/send", new
			{
				jid = replyJidDM,
				text = replyDM
			});
			if (!directSentDM && !string.IsNullOrWhiteSpace(consoleJid))
			{
				string who = ((!string.IsNullOrWhiteSpace(msg.PushName)) ? msg.PushName : ("@" + senderNum));
				string head = ((qDM.Length > 0) ? $"\ud83d\udce9 *DM dari {who}:* {qDM}\n\n" : ("\ud83d\udce9 *DM dari " + who + "*\n\n"));
				await PostJson(cl_472.http, outBase + "/send", new
				{
					jid = consoleJid,
					text = head + replyDM
				});
				return Results.Json(new
				{
					ok = true,
					action = "dm-chat-console-copy",
					replyJid = replyJidDM,
					directSent = directSentDM,
					consoleJid = consoleJid
				});
			}
			return Results.Json(new
			{
				ok = true,
				action = "dm-chat",
				replyJid = replyJidDM,
				directSent = directSentDM
			});
			IL_aa9d:
			cl_472.audit.Write(msg.Jid, msg.Participant, msg.PushName, matched.Id, count, msg.Text);
			cl_472.app.Logger.LogInformation("HAPUS dari {Number} (aturan {Rule}), peringatan ke-{Count}{Quiet}", number, matched.Id, count, quietNow ? " [jam tenang]" : "");
			return Results.Json(new
			{
				ok = true,
				action = "moderated",
				rule = matched.Id,
				warnCount = count,
				quiet = quietNow
			});
			IL_1457:
			if (num3 != 0)
			{
				if (!PrivateChatAccess.IsAllowed(cl_472.config, pcDM, senderNum, senderPhone))
				{
					return Results.Json(new
					{
						ok = true,
						action = "dm-not-allowed"
					});
				}
				if (cooldownSec > 0 && !cl_472.cmdCooldown.Allow(msg.Jid + "|dm", Math.Max(cooldownSec, 3)))
				{
					return Results.Json(new
					{
						ok = true,
						action = "dm-cooldown"
					});
				}
				if (quietNow)
				{
					return Results.Json(new
					{
						ok = true,
						action = "dm-quiet"
					});
				}
				cl_472.SendTyping(msg.Jid, ctx.Channel);
				string memKeyDM = msg.Jid + "|dm";
				string convDM = ConvMemory.Recent(memKeyDM);
				string personaDM = (string.IsNullOrWhiteSpace(pcDM.Persona) ? "" : pcDM.Persona);
				qDM = cmdText.Trim();
				string? adminDmReply = await TryHandleDmAnnouncement(cl_472.config, cl_472.http, msg, senderNum, senderPhone, qDM, cl_472.app.Logger, cl_472.audit, cl_472.config.Puzzle.RevealMinutes, cl_472.puzzlePool.Count, async (jid, level) => { await cl_472.PostPuzzleAsync(jid, false, null, PuzzleMove.DifficultySlot("puzzle " + level, cl_472.config.Puzzle.RevealMinutes)); return true; }, async (jid) => { ActivePuzzle ap; lock (cl_472.puzzleLock) { cl_472.activePuzzles.TryGetValue(jid, out ap); } if (ap == null) return false; await cl_472.RevealPuzzleAsync(jid, ap, false); return true; }, () => { lock (cl_472.puzzleLock) { return BuildActivePuzzleSummary(cl_472.activePuzzles); } });
				if (adminDmReply != null)
				{
					replyDM = adminDmReply;
				}
				else
				{
					switch ((qDM.Length != 0) ? ChatIntents.Classify(qDM) : ChatIntent.Empty)
					{
					case ChatIntent.Schedule:
						replyDM = await CommandHandler.BuildSchedule(cl_472.config, cl_472.http, cl_472.app.Logger);
						break;
					case ChatIntent.Result:
						replyDM = await CommandHandler.BuildLatestResult(cl_472.config, cl_472.http, cl_472.app.Logger);
						break;
					default:
					{
						string ansDM = await Ai.Ask(cl_472.config.Ai, cl_472.http, (qDM.Length == 0) ? "Halo" : qDM, cl_472.app.Logger, personaDM, convDM);
						replyDM = (string.IsNullOrWhiteSpace(ansDM) ? "Maaf, aku lagi belum bisa menjawab. Coba lagi sebentar ya." : ansDM);
						if (replyDM.Length > cl_472.config.Ai.MaxOutputChars)
						{
							replyDM = replyDM.Substring(0, cl_472.config.Ai.MaxOutputChars) + "…";
						}
						break;
					}
					}
				}
				if (qDM.Length > 0)
				{
					ConvMemory.Append(memKeyDM, "user", qDM);
					ConvMemory.Append(memKeyDM, "assistant", replyDM);
				}
				string[] cgs = cl_472.config.PrivateChat?.ConsoleGroupJids;
				if (cgs != null)
				{
					int num4 = cgs.Length;
					if (num4 > 0 && !string.IsNullOrWhiteSpace(cgs[0]))
					{
						obj5 = cgs[0];
						goto IL_195b;
					}
				}
				obj5 = cl_472.config.AdminSyncGroupJid;
				goto IL_195b;
			}
			RelayConfig relay = cl_472.config.Relay;
			if (relay != null && relay.Enabled && msg.Jid == cl_472.config.Relay.HubGroupJid)
			{
				string sessKey = msg.Participant;
				BroadcastSession sess;
				lock (cl_472.sessions.BroadcastLock)
				{
					cl_472.sessions.Broadcast.TryGetValue(sessKey, out sess);
					if (sess != null && (DateTimeOffset.UtcNow - sess.CreatedAt).TotalMinutes > 5.0)
					{
						cl_472.sessions.Broadcast.Remove(sessKey);
						sess = null;
					}
				}
				string firstWord = (isCommand ? cmdText.Substring(cl_472.config.CommandPrefix.Length).Split(' ', 2)[0].ToLowerInvariant() : "");
				if (isCommand && firstWord == "batal")
				{
					bool had;
					lock (cl_472.sessions.BroadcastLock)
					{
						had = cl_472.sessions.Broadcast.Remove(sessKey);
					}
					if (had)
					{
						await PostJson(cl_472.http, outBase + "/send", new
						{
							jid = msg.Jid,
							text = "Siap, proses sebar saya batalkan."
						});
					}
					return Results.Json(new
					{
						ok = true,
						action = "relay-cancel"
					});
				}
				if (isCommand && (firstWord == cl_472.config.Relay.Command.ToLowerInvariant() || firstWord == "announcement" || firstWord == "umumkan"))
				{
					if (!AdminSync.IsAllowed(cl_472.config, senderNum, senderPhone))
					{
						await PostJson(cl_472.http, outBase + "/send", new
						{
							jid = msg.Jid,
							text = "Fitur sebar khusus admin."
						});
						return Results.Json(new
						{
							ok = true,
							action = "relay-denied"
						});
					}
					string[] cmdParts = cmdText.Substring(cl_472.config.CommandPrefix.Length).Split(' ', 2);
					string inlineText = ((cmdParts.Length > 1) ? cmdParts[1].Trim() : "");
					if ((firstWord == "announcement" || firstWord == "umumkan") && inlineText.Length > 0)
					{
						List<string> targets = (cl_472.config.Relay.TargetGroups ?? Array.Empty<string>()).Where((string value) => !string.IsNullOrWhiteSpace(value)).ToList();
						if (targets.Count == 0)
						{
							await PostJson(cl_472.http, outBase + "/send", new
							{
								jid = msg.Jid,
								text = "Belum ada grup tujuan."
							});
							return Results.Json(new
							{
								ok = true,
								action = "announcement-notarget"
							});
						}
						string outText = (string.IsNullOrWhiteSpace(cl_472.config.Relay.Footer) ? inlineText : (inlineText + "\n\n" + cl_472.config.Relay.Footer));
						int throttleMs = Math.Max(0, cl_472.config.Relay.ThrottleSeconds) * 1000;
						string hubJid = msg.Jid;
						Task.Run(async delegate
						{
							int okCount = 0;
							foreach (string tj in targets)
							{
								try
								{
									if (await PostJson(DCv_0_.http, ChannelRoute.BaseForJid(DCv_0_.config, tj) + "/send", new
									{
										jid = tj,
										text = outText
									}))
									{
										okCount++;
									}
								}
								catch (Exception ex)
								{
									DCv_0_.app.Logger.LogError("Announcement gagal ke {Jid}: {Msg}", tj, ex.Message);
								}
								if (throttleMs > 0)
								{
									await Task.Delay(throttleMs);
								}
							}
							await PostJson(DCv_0_.http, ChannelRoute.BaseForJid(DCv_0_.config, hubJid) + "/send", new
							{
								jid = hubJid,
								text = $"Announcement terkirim ke {okCount}/{targets.Count} grup."
							});
						});
						await PostJson(cl_472.http, outBase + "/send", new
						{
							jid = msg.Jid,
							text = $"Mengirim announcement ke {targets.Count} grup..."
						});
						return Results.Json(new
						{
							ok = true,
							action = "announcement-send",
							targets = targets.Count
						});
					}
					lock (cl_472.sessions.BroadcastLock)
					{
						cl_472.sessions.Broadcast[sessKey] = new BroadcastSession
						{
							Stage = "text"
						};
					}
					await PostJson(cl_472.http, outBase + "/send", new
					{
						jid = msg.Jid,
						text = "Mau sebar pesan apa? Ketik pesannya. (!batal untuk batal)"
					});
					return Results.Json(new
					{
						ok = true,
						action = "relay-start"
					});
				}
				if (sess != null && !isCommand)
				{
					if (sess.Stage == "text")
					{
						List<GroupOption> opts = (await FetchGroups(cl_472.config.GatewayUrl, cl_472.http)).Where((GroupOption o) => o.Jid != cl_472.config.Relay.HubGroupJid && o.Jid.Length > 0).ToList();
						lock (cl_472.sessions.BroadcastLock)
						{
							sess.Text = msg.Text.Trim();
							sess.Options = opts;
							sess.Stage = "targets";
							sess.CreatedAt = DateTimeOffset.UtcNow;
						}
						await PostJson(cl_472.http, outBase + "/send", new
						{
							jid = msg.Jid,
							text = TargetPrompt(opts)
						});
						return Results.Json(new
						{
							ok = true,
							action = "relay-text"
						});
					}
					if (sess.Stage == "targets")
					{
						List<GroupOption> chosen = ParseSelection(msg.Text, sess.Options);
						if (chosen.Count == 0)
						{
							await PostJson(cl_472.http, outBase + "/send", new
							{
								jid = msg.Jid,
								text = "Saya belum menangkap pilihan. Balas nomor, semua, atau !batal."
							});
							return Results.Json(new
							{
								ok = true,
								action = "relay-retry"
							});
						}
						string textToSend = sess.Text;
						lock (cl_472.sessions.BroadcastLock)
						{
							cl_472.sessions.Broadcast.Remove(sessKey);
						}
						string outText2 = (string.IsNullOrWhiteSpace(cl_472.config.Relay.Footer) ? textToSend : (textToSend + "\n\n" + cl_472.config.Relay.Footer));
						string hubJid2 = msg.Jid;
						int throttleMs2 = Math.Max(0, cl_472.config.Relay.ThrottleSeconds) * 1000;
						List<string> targetJids = chosen.Select((GroupOption groupOption) => groupOption.Jid).ToList();
						Task.Run(async delegate
						{
							int okCount = 0;
							foreach (string tj in targetJids)
							{
								try
								{
									if (await PostJson(DCv_0_.http, ChannelRoute.BaseForJid(DCv_0_.config, tj) + "/send", new
									{
										jid = tj,
										text = outText2
									}))
									{
										okCount++;
									}
									else
									{
										DCv_0_.app.Logger.LogWarning("Relay gagal (gateway tolak) ke {Jid}", tj);
									}
								}
								catch (Exception ex)
								{
									DCv_0_.app.Logger.LogError("Relay gagal ke {Jid}: {Msg}", tj, ex.Message);
								}
								if (throttleMs2 > 0)
								{
									await Task.Delay(throttleMs2);
								}
							}
							await PostJson(DCv_0_.http, ChannelRoute.BaseForJid(DCv_0_.config, hubJid2) + "/send", new
							{
								jid = hubJid2,
								text = $"Selesai menyebar ke {okCount}/{targetJids.Count} grup."
							});
						});
						await PostJson(cl_472.http, outBase + "/send", new
						{
							jid = msg.Jid,
							text = $"Menyebar ke {targetJids.Count} grup (jeda {cl_472.config.Relay.ThrottleSeconds} dtk)..."
						});
						return Results.Json(new
						{
							ok = true,
							action = "relay-send",
							targets = targetJids.Count
						});
					}
				}
			}
			string ssKey = msg.Jid + "|" + msg.Participant;
			StandingsSession ss;
			lock (cl_472.sessions.StandingsLock)
			{
				cl_472.sessions.Standings.TryGetValue(ssKey, out ss);
				if (ss != null && (DateTimeOffset.UtcNow - ss.CreatedAt).TotalMinutes > 3.0)
				{
					cl_472.sessions.Standings.Remove(ssKey);
					ss = null;
				}
			}
			string sFirst = (isCommand ? cmdText.Substring(cl_472.config.CommandPrefix.Length).Split(' ', 2)[0].ToLowerInvariant() : "");
			if (eCommands && isCommand && (sFirst == "standings" || sFirst == "klasemen"))
			{
				string[] sp = cmdText.Substring(cl_472.config.CommandPrefix.Length).Split(' ', 2);
				if (sp.Length > 1 && int.TryParse(sp[1].Trim(), out var sid))
				{
					string r = await CommandHandler.BuildStandings(sid, cl_472.http, cl_472.app.Logger);
					await PostJson(cl_472.http, outBase + "/send", new
					{
						jid = msg.Jid,
						text = r
					});
					return Results.Json(new
					{
						ok = true,
						action = "standings"
					});
				}
				List<(string url, string swiss, string name, string date)> recent = await CommandHandler.GetRecentTournaments(cl_472.http, cl_472.app.Logger, 5);
				if (recent.Count == 0)
				{
					await PostJson(cl_472.http, outBase + "/send", new
					{
						jid = msg.Jid,
						text = "Daftar belum terbaca. Coba " + cl_472.config.CommandPrefix + "standings <id>."
					});
					return Results.Json(new
					{
						ok = true,
						action = "standings-nolist"
					});
				}
				lock (cl_472.sessions.StandingsLock)
				{
					cl_472.sessions.Standings[ssKey] = new StandingsSession
					{
						Options = recent
					};
				}
				StringBuilder sb = new StringBuilder();
				sb.AppendLine("Klasemen turnamen mana? Balas nomornya:");
				for (int i = 0; i < recent.Count; i++)
				{
					StringBuilder stringBuilder = sb;
					StringBuilder stringBuilder2 = stringBuilder;
					StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(2, 3, stringBuilder);
					handler.AppendFormatted(i + 1);
					handler.AppendLiteral(". ");
					handler.AppendFormatted(recent[i].name);
					handler.AppendFormatted(string.IsNullOrEmpty(recent[i].date) ? "" : (" (" + recent[i].date + ")"));
					stringBuilder2.AppendLine(ref handler);
				}
				sb.Append("(ketik !batal untuk membatalkan)");
				await PostJson(cl_472.http, outBase + "/send", new
				{
					jid = msg.Jid,
					text = sb.ToString()
				});
				return Results.Json(new
				{
					ok = true,
					action = "standings-list"
				});
			}
			if (ss != null && isCommand && sFirst == "batal")
			{
				lock (cl_472.sessions.StandingsLock)
				{
					cl_472.sessions.Standings.Remove(ssKey);
				}
				await PostJson(cl_472.http, outBase + "/send", new
				{
					jid = msg.Jid,
					text = "Siap, saya batalkan."
				});
				return Results.Json(new
				{
					ok = true,
					action = "standings-cancel"
				});
			}
			if (ss != null && !isCommand && int.TryParse(msg.Text.Trim(), out var pick) && pick >= 1 && pick <= ss.Options.Count)
			{
				(string url, string swiss, string name, string date) chosen2 = ss.Options[pick - 1];
				lock (cl_472.sessions.StandingsLock)
				{
					cl_472.sessions.Standings.Remove(ssKey);
				}
				string r2 = await CommandHandler.BuildStandingsSmart(chosen2.url, chosen2.swiss, chosen2.name, cl_472.http, cl_472.app.Logger);
				await PostJson(cl_472.http, outBase + "/send", new
				{
					jid = msg.Jid,
					text = r2
				});
				return Results.Json(new
				{
					ok = true,
					action = "standings-pick"
				});
			}
			CclConfig ccl = cl_472.config.Ccl;
			int num5;
			if (ccl != null)
			{
				bool flag3 = ccl.Enabled;
				num5 = (flag3 ? 1 : 0);
			}
			else
			{
				num5 = 0;
			}
			if (num5 != 0)
			{
				string csKey = msg.Jid + "|" + msg.Participant;
				CclSession cs;
				lock (cl_472.sessions.CclLock)
				{
					cl_472.sessions.Ccl.TryGetValue(csKey, out cs);
					if (cs != null && (DateTimeOffset.UtcNow - cs.CreatedAt).TotalMinutes > 3.0)
					{
						cl_472.sessions.Ccl.Remove(csKey);
						cs = null;
					}
				}
				string cFirst = (isCommand ? cmdText.Substring(cl_472.config.CommandPrefix.Length).Split(' ', 2)[0].ToLowerInvariant() : "");
				string cclCmd = (string.IsNullOrWhiteSpace(cl_472.config.Ccl.Command) ? "events" : cl_472.config.Ccl.Command.ToLowerInvariant());
				if (eCommands && isCommand && (cFirst == cclCmd || cFirst == "events" || cFirst == "ccl"))
				{
					(List<CclEvent> upcoming, List<CclEvent> past) tuple = await Ccl.GetEvents(cl_472.config.Ccl, cl_472.http, cl_472.app.Logger);
					List<CclEvent> up = tuple.upcoming;
					List<CclEvent> past = tuple.past;
					List<CclEvent> opts2 = new List<CclEvent>();
					opts2.AddRange(up.OrderBy((CclEvent e) => e.Start).Take(8));
					opts2.AddRange(past.Take(8));
					if (opts2.Count == 0)
					{
						await PostJson(cl_472.http, outBase + "/send", new
						{
							jid = msg.Jid,
							text = "Daftar event belum bisa saya ambil sekarang. Silakan coba lagi nanti ya."
						});
						return Results.Json(new
						{
							ok = true,
							action = "ccl-nolist"
						});
					}
					lock (cl_472.sessions.CclLock)
					{
						cl_472.sessions.Ccl[csKey] = new CclSession
						{
							Options = opts2
						};
					}
					await PostJson(cl_472.http, outBase + "/send", new
					{
						jid = msg.Jid,
						text = Ccl.BuildList(cl_472.config.Ccl, opts2)
					});
					return Results.Json(new
					{
						ok = true,
						action = "ccl-list"
					});
				}
				if (cs != null && isCommand && cFirst == "batal")
				{
					lock (cl_472.sessions.CclLock)
					{
						cl_472.sessions.Ccl.Remove(csKey);
					}
					await PostJson(cl_472.http, outBase + "/send", new
					{
						jid = msg.Jid,
						text = "Siap, saya batalkan."
					});
					return Results.Json(new
					{
						ok = true,
						action = "ccl-cancel"
					});
				}
				if (cs != null && !isCommand && int.TryParse(msg.Text.Trim(), out var cpick) && cpick >= 1 && cpick <= cs.Options.Count)
				{
					CclEvent chosen3 = cs.Options[cpick - 1];
					lock (cl_472.sessions.CclLock)
					{
						cl_472.sessions.Ccl.Remove(csKey);
					}
					string r3 = await Ccl.BuildView(cl_472.config.Ccl, chosen3, cl_472.http, cl_472.app.Logger);
					await PostJson(cl_472.http, outBase + "/send", new
					{
						jid = msg.Jid,
						text = r3
					});
					return Results.Json(new
					{
						ok = true,
						action = "ccl-pick"
					});
				}
			}
			ai = cl_472.config.Ai;
			int num6;
			if (ai != null)
			{
				bool flag3 = ai.Enabled;
				num6 = (flag3 ? 1 : 0);
			}
			else
			{
				num6 = 0;
			}
			if (num6 != 0)
			{
				string aiQuestion = null;
				if (isCommand)
				{
					string[] parts = cmdText.Substring(cl_472.config.CommandPrefix.Length).Split(' ', 2);
					if (((ReadOnlySpan<string>)cl_472.config.Ai.Commands).Contains(parts[0].ToLowerInvariant()))
					{
						aiQuestion = ((parts.Length > 1) ? parts[1].Trim() : "");
					}
				}
				else if (cl_472.config.Ai.RequireMention && msg.MentionedBot)
				{
					aiQuestion = cmdText.Trim();
				}
				if (aiQuestion != null)
				{
					if (quietNow)
					{
						if (!string.IsNullOrWhiteSpace(cl_472.config.QuietHours?.Notice))
						{
							await PostJson(cl_472.http, outBase + "/send", new
							{
								jid = msg.Jid,
								text = cl_472.config.QuietHours.Notice
							});
						}
						return Results.Json(new
						{
							ok = true,
							action = "quiet-ai"
						});
					}
					string asker = NumberUtil.Normalize(msg.Participant);
					cl_472.SendTyping(msg.Jid, ctx.Channel);
					string memKey = msg.Jid + "|" + asker;
					string convHistory = ConvMemory.Recent(memKey);
					string reply;
					switch ((aiQuestion.Length != 0) ? ChatIntents.Classify(aiQuestion) : ChatIntent.Empty)
					{
					case ChatIntent.Empty:
						reply = "Bisa. Format: !tanya <pertanyaan>.";
						break;
					case ChatIntent.Result:
						reply = await CommandHandler.BuildLatestResult(cl_472.config, cl_472.http, cl_472.app.Logger);
						break;
					case ChatIntent.Schedule:
					{
						string sched = await CommandHandler.BuildSchedule(cl_472.config, cl_472.http, cl_472.app.Logger);
						string hint = g?.EventsHint ?? "Info & hasil turnamen: https://ligacatur.com/";
						reply = sched + (string.IsNullOrWhiteSpace(hint) ? "" : ("\n\n" + hint));
						break;
					}
					default:
					{
						WorkspaceConfig ws = cl_472.config.Workspace;
						string wsSuffix = ((ws != null && !string.IsNullOrWhiteSpace(ws.Scope)) ? ("[Workspace: " + ws.Name + "] " + ws.Scope) : "");
						if (!string.IsNullOrWhiteSpace(ctx.Topic))
						{
							wsSuffix = wsSuffix + "\n\nKonteks: percakapan terakhir di chat ini bertema \"" + ctx.Topic + "\". Jaga kesinambungan bila relevan.";
						}
						string ans = await Ai.Ask(cl_472.config.Ai, cl_472.http, aiQuestion, cl_472.app.Logger, wsSuffix, convHistory);
						reply = (string.IsNullOrWhiteSpace(ans) ? "Maaf, saya belum bisa menjawab dengan baik sekarang. Silakan coba lagi sebentar lagi, atau ketik !help untuk daftar perintah." : ans);
						if (reply.Length > cl_472.config.Ai.MaxOutputChars)
						{
							reply = reply.Substring(0, cl_472.config.Ai.MaxOutputChars) + "…";
						}
						break;
					}
					}
					if (aiQuestion.Length > 0)
					{
						ConvMemory.Append(memKey, "user", aiQuestion);
						ConvMemory.Append(memKey, "assistant", reply);
					}
					string jid = msg.Jid;
					ChatIntent chatIntent = ChatIntents.Classify(aiQuestion);
					if (1 == 0)
					{
					}
					string topic = chatIntent switch
					{
						ChatIntent.Result => "hasil", 
						ChatIntent.Schedule => "jadwal", 
						ChatIntent.Empty => ctx.Topic, 
						_ => "obrolan-catur", 
					};
					if (1 == 0)
					{
					}
					TopicStore.Set(jid, topic);
					await PostJson(cl_472.http, outBase + "/send", new
					{
						jid = msg.Jid,
						text = "@" + asker + " " + reply,
						mentions = new string[1] { msg.Participant }
					});
					return Results.Json(new
					{
						ok = true,
						action = "ai"
					});
				}
			}
			if (eCommands && isCommand && cmdName == "status")
			{
				if (!AdminSync.IsAllowed(cl_472.config, senderNum, senderPhone))
				{
					return Results.Json(new
					{
						ok = true,
						action = "status-denied"
					});
				}
				string gw;
				try
				{
					gw = ((await cl_472.http.GetStringAsync(cl_472.config.GatewayUrl + "/health")).Contains("\"connected\":true") ? "tersambung ✅" : "TIDAK tersambung ⚠\ufe0f");
				}
				catch
				{
					gw = "TIDAK responsif ⚠\ufe0f";
				}
				StringBuilder st = new StringBuilder();
				st.AppendLine("\ud83e\ude7a *Status Bot*");
				StringBuilder stringBuilder = st;
				StringBuilder stringBuilder3 = stringBuilder;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(47, 2, stringBuilder);
				handler.AppendLiteral("• Brain: hidup ✅ (");
				handler.AppendFormatted(cl_472.rules.Count);
				handler.AppendLiteral(" aturan, ");
				handler.AppendFormatted(cl_472.warnings.Count);
				handler.AppendLiteral(" riwayat peringatan)");
				stringBuilder3.AppendLine(ref handler);
				stringBuilder = st;
				StringBuilder stringBuilder4 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder);
				handler.AppendLiteral("• Gateway/WhatsApp: ");
				handler.AppendFormatted(gw);
				stringBuilder4.AppendLine(ref handler);
				stringBuilder = st;
				StringBuilder stringBuilder5 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder);
				handler.AppendLiteral("• Reminder Lichess: ");
				AnnouncerConfig announcer = cl_472.config.Announcer;
				handler.AppendFormatted((announcer != null && announcer.Enabled) ? "aktif" : "mati");
				stringBuilder5.AppendLine(ref handler);
				stringBuilder = st;
				StringBuilder stringBuilder6 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(23, 1, stringBuilder);
				handler.AppendLiteral("• Event chess.college: ");
				ccl = cl_472.config.Ccl;
				handler.AppendFormatted((ccl != null && ccl.Enabled) ? "aktif" : "mati");
				stringBuilder6.AppendLine(ref handler);
				stringBuilder = st;
				StringBuilder stringBuilder7 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(23, 1, stringBuilder);
				handler.AppendLiteral("• Jam tenang sekarang: ");
				handler.AppendFormatted(quietNow ? "AKTIF" : "tidak");
				stringBuilder7.AppendLine(ref handler);
				stringBuilder = st;
				StringBuilder stringBuilder8 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder);
				handler.AppendLiteral("• Grup dikelola: ");
				handler.AppendFormatted(cl_472.config.ManageAllGroups ? "semua" : cl_472.config.Groups.Count.ToString());
				stringBuilder8.AppendLine(ref handler);
				int modTodaySt = cl_472.audit.LinesSince(DateTime.Now.Date).Count((string l) => l.Contains("| HAPUS |") && !l.Contains("aturan=SHADOW:"));
				stringBuilder = st;
				StringBuilder stringBuilder9 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(23, 1, stringBuilder);
				handler.AppendLiteral("• Dimoderasi hari ini: ");
				handler.AppendFormatted(modTodaySt);
				stringBuilder9.AppendLine(ref handler);
				st.AppendLine($"• Kirim: {SendLog.Sent} ok / {SendLog.Failed} gagal" + ((RetryQueue.Count > 0) ? $" (antre ulang: {RetryQueue.Count})" : ""));
				stringBuilder = st;
				StringBuilder stringBuilder10 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(16, 1, stringBuilder);
				handler.AppendLiteral("• Puzzle aktif: ");
				handler.AppendFormatted(cl_472.activePuzzles.Count);
				stringBuilder10.AppendLine(ref handler);
				stringBuilder = st;
				StringBuilder stringBuilder11 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(28, 1, stringBuilder);
				handler.AppendLiteral("• Admin terdaftar (!sebar): ");
				handler.AppendFormatted(cl_472.config.AdminNumbers.Length);
				stringBuilder11.Append(ref handler);
				await PostJson(cl_472.http, outBase + "/send", new
				{
					jid = msg.Jid,
					text = st.ToString()
				});
				return Results.Json(new
				{
					ok = true,
					action = "status"
				});
			}
			if (eCommands && isCommand && (cmdName == "warnings" || cmdName == "pelanggar"))
			{
				if (!AdminSync.IsAllowed(cl_472.config, senderNum, senderPhone))
				{
					return Results.Json(new
					{
						ok = true,
						action = "warnings-denied"
					});
				}
				List<(string num, int count)> top = cl_472.warnings.TopForGroup(msg.Jid, 10);
				StringBuilder wb = new StringBuilder();
				wb.AppendLine("*Catatan moderasi terbanyak (grup ini)*");
				StringBuilder stringBuilder;
				StringBuilder.AppendInterpolatedStringHandler handler;
				if (top.Count == 0)
				{
					wb.AppendLine("Belum ada catatan moderasi.");
				}
				else
				{
					int i2 = 1;
					foreach (var item in top)
					{
						string num7 = item.num;
						int c = item.count;
						stringBuilder = wb;
						StringBuilder stringBuilder12 = stringBuilder;
						handler = new StringBuilder.AppendInterpolatedStringHandler(6, 3, stringBuilder);
						handler.AppendFormatted(i2++);
						handler.AppendLiteral(". ");
						handler.AppendFormatted(num7);
						handler.AppendLiteral(" — ");
						handler.AppendFormatted(c);
						handler.AppendLiteral("×");
						stringBuilder12.AppendLine(ref handler);
					}
				}
				wb.AppendLine();
				stringBuilder = wb;
				StringBuilder stringBuilder13 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(141, 3, stringBuilder);
				handler.AppendLiteral("Total catatan tersimpan (semua grup): ");
				handler.AppendFormatted(cl_472.warnings.Count);
				handler.AppendLiteral(". Ketik ");
				handler.AppendFormatted(cl_472.config.CommandPrefix);
				handler.AppendLiteral("audit untuk 10 tindakan terakhir, atau ");
				handler.AppendFormatted(cl_472.config.CommandPrefix);
				handler.AppendLiteral("percaya (balas pesan) untuk membuka akses/reset catatan.");
				stringBuilder13.Append(ref handler);
				await PostJson(cl_472.http, outBase + "/send", new
				{
					jid = msg.Jid,
					text = wb.ToString()
				});
				return Results.Json(new
				{
					ok = true,
					action = "warnings"
				});
			}
			if (eCommands && isCommand && cmdName == "audit")
			{
				if (!AdminSync.IsAllowed(cl_472.config, senderNum, senderPhone))
				{
					return Results.Json(new
					{
						ok = true,
						action = "audit-denied"
					});
				}
				List<string> lines = cl_472.audit.Tail(10);
				string body = ((lines.Count == 0) ? "Belum ada catatan audit." : string.Join("\n", lines));
				if (body.Length > 3500)
				{
					body = body.Substring(body.Length - 3500);
				}
				await PostJson(cl_472.http, outBase + "/send", new
				{
					jid = msg.Jid,
					text = "\ud83d\udcdc *Audit moderasi (terbaru)*\n" + body
				});
				return Results.Json(new
				{
					ok = true,
					action = "audit"
				});
			}
			if (eCommands && isCommand && cmdName == "modreport")
			{
				if (!AdminSync.IsAllowed(cl_472.config, senderNum, senderPhone))
				{
					return Results.Json(new
					{
						ok = true,
						action = "modreport-denied"
					});
				}
				string rep = ModerationReport.Build(cl_472.audit, cl_472.config, DateTime.Now.AddHours(-24.0));
				await PostJson(cl_472.http, outBase + "/send", new
				{
					jid = msg.Jid,
					text = rep
				});
				return Results.Json(new
				{
					ok = true,
					action = "modreport"
				});
			}
			if (eCommands && isCommand && cmdName == "percaya")
			{
				if (!AdminSync.IsAllowed(cl_472.config, senderNum, senderPhone))
				{
					return Results.Json(new
					{
						ok = true,
						action = "percaya-denied"
					});
				}
				if (string.IsNullOrWhiteSpace(msg.QuotedAuthor))
				{
					await PostJson(cl_472.http, outBase + "/send", new
					{
						jid = msg.Jid,
						text = "Cara pakai: balas (reply) pesan anggota yang ingin dibuka aksesnya, lalu ketik " + cl_472.config.CommandPrefix + "percaya. Bot akan membuka akses awalnya dan merapikan catatan moderasinya."
					});
					return Results.Json(new
					{
						ok = true,
						action = "percaya-noquote"
					});
				}
				string targetNum = NumberUtil.Normalize(msg.QuotedAuthor);
				bool wasProbation = cl_472.joins.Clear(targetNum);
				bool hadWarn = cl_472.warnings.Reset(msg.Jid + "|" + msg.QuotedAuthor);
				await PostJson(cl_472.http, outBase + "/send", new
				{
					jid = msg.Jid,
					text = $"{targetNum} sudah ditandai aman - {(wasProbation ? "akses awal dibuka" : "akses sudah normal")}, {(hadWarn ? "catatan moderasi direset" : "tidak ada catatan moderasi")}.",
					mentions = new string[1] { msg.QuotedAuthor }
				});
				cl_472.app.Logger.LogInformation("PERCAYA {Target} oleh admin {Admin}", targetNum, senderNum);
				return Results.Json(new
				{
					ok = true,
					action = "percaya",
					target = targetNum
				});
			}
			if (eCommands && isCommand && (cmdName == "lapor" || cmdName == "admin"))
			{
				string laporTo = ((!string.IsNullOrWhiteSpace(cl_472.config.LaporGroupJid)) ? cl_472.config.LaporGroupJid : (cl_472.config.Relay?.HubGroupJid ?? ""));
				if (string.IsNullOrWhiteSpace(laporTo))
				{
					await PostJson(cl_472.http, outBase + "/send", new
					{
						jid = msg.Jid,
						text = "Fitur lapor belum siap karena grup admin tujuan belum diset."
					});
					return Results.Json(new
					{
						ok = true,
						action = "lapor-noconfig"
					});
				}
				string[] lp = cmdText.Substring(cl_472.config.CommandPrefix.Length).Split(' ', 2);
				string note = ((lp.Length > 1) ? lp[1].Trim() : "");
				StringBuilder stringBuilder;
				StringBuilder.AppendInterpolatedStringHandler handler;
				if (string.IsNullOrWhiteSpace(msg.QuotedText))
				{
					if (cmdName == "admin")
					{
						string reporter0 = NumberUtil.Normalize(msg.Participant);
						string grpLabel0 = g?.Label ?? msg.Jid;
						StringBuilder call = new StringBuilder();
						call.AppendLine("\ud83d\udea8 *Admin dipanggil anggota*");
						stringBuilder = call;
						StringBuilder stringBuilder14 = stringBuilder;
						handler = new StringBuilder.AppendInterpolatedStringHandler(6, 1, stringBuilder);
						handler.AppendLiteral("Grup: ");
						handler.AppendFormatted(grpLabel0);
						stringBuilder14.AppendLine(ref handler);
						stringBuilder = call;
						StringBuilder stringBuilder15 = stringBuilder;
						handler = new StringBuilder.AppendInterpolatedStringHandler(14, 2, stringBuilder);
						handler.AppendLiteral("Pemanggil: ");
						handler.AppendFormatted(msg.PushName);
						handler.AppendLiteral(" (");
						handler.AppendFormatted(reporter0);
						handler.AppendLiteral(")");
						stringBuilder15.AppendLine(ref handler);
						if (note.Length > 0)
						{
							stringBuilder = call;
							StringBuilder stringBuilder16 = stringBuilder;
							handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder);
							handler.AppendLiteral("Catatan: ");
							handler.AppendFormatted(note);
							stringBuilder16.AppendLine(ref handler);
						}
						await PostJson(cl_472.http, ChannelRoute.BaseForJid(cl_472.config, laporTo) + "/send", new
						{
							jid = laporTo,
							text = call.ToString()
						});
						await PostJson(cl_472.http, outBase + "/send", new
						{
							jid = msg.Jid,
							text = "Admin sudah saya panggil. Jelaskan singkat ya."
						});
						return Results.Json(new
						{
							ok = true,
							action = "admin-called"
						});
					}
					await PostJson(cl_472.http, outBase + "/send", new
					{
						jid = msg.Jid,
						text = $"Report pesan: reply lalu {cl_472.config.CommandPrefix}lapor. Panggil admin: {cl_472.config.CommandPrefix}admin <catatan>."
					});
					return Results.Json(new
					{
						ok = true,
						action = "lapor-noquote"
					});
				}
				string reporter1 = NumberUtil.Normalize(msg.Participant);
				string reported = NumberUtil.Normalize(msg.QuotedAuthor);
				string snippet = ((msg.QuotedText.Length > 400) ? (msg.QuotedText.Substring(0, 400) + "…") : msg.QuotedText);
				string grpLabel1 = g?.Label ?? msg.Jid;
				StringBuilder rep2 = new StringBuilder();
				rep2.AppendLine("\ud83d\udea9 *Laporan dari anggota*");
				stringBuilder = rep2;
				StringBuilder stringBuilder17 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(6, 1, stringBuilder);
				handler.AppendLiteral("Grup: ");
				handler.AppendFormatted(grpLabel1);
				stringBuilder17.AppendLine(ref handler);
				stringBuilder = rep2;
				StringBuilder stringBuilder18 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(12, 2, stringBuilder);
				handler.AppendLiteral("Pelapor: ");
				handler.AppendFormatted(msg.PushName);
				handler.AppendLiteral(" (");
				handler.AppendFormatted(reporter1);
				handler.AppendLiteral(")");
				stringBuilder18.AppendLine(ref handler);
				if (reported.Length > 0)
				{
					stringBuilder = rep2;
					StringBuilder stringBuilder19 = stringBuilder;
					handler = new StringBuilder.AppendInterpolatedStringHandler(12, 1, stringBuilder);
					handler.AppendLiteral("Dilaporkan: ");
					handler.AppendFormatted(reported);
					stringBuilder19.AppendLine(ref handler);
				}
				if (note.Length > 0)
				{
					stringBuilder = rep2;
					StringBuilder stringBuilder20 = stringBuilder;
					handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder);
					handler.AppendLiteral("Catatan: ");
					handler.AppendFormatted(note);
					stringBuilder20.AppendLine(ref handler);
				}
				rep2.AppendLine("Pesan yang dilaporkan:");
				stringBuilder = rep2;
				StringBuilder stringBuilder21 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(2, 1, stringBuilder);
				handler.AppendLiteral("“");
				handler.AppendFormatted(snippet);
				handler.AppendLiteral("”");
				stringBuilder21.Append(ref handler);
				await PostJson(cl_472.http, ChannelRoute.BaseForJid(cl_472.config, laporTo) + "/send", new
				{
					jid = laporTo,
					text = rep2.ToString()
				});
				await PostJson(cl_472.http, outBase + "/send", new
				{
					jid = msg.Jid,
					text = "Terima kasih, laporanmu sudah diteruskan ke admin. \ud83d\ude4f"
				});
				return Results.Json(new
				{
					ok = true,
					action = "lapor"
				});
			}
			PuzzleConfig pzc = cl_472.config.Puzzle;
			int num8;
			if (pzc != null)
			{
				bool flag3 = pzc.Enabled;
				num8 = (flag3 ? 1 : 0);
			}
			else
			{
				num8 = 0;
			}
			if (((uint)num8 & (eCommands ? 1u : 0u) & (isCommand ? 1u : 0u)) != 0 && cmdName == pzc.Command && (g?.PuzzleCommandEnabled ?? pzc.CommandEnabled))
			{
				if (cl_472.puzzlePool.Count == 0)
				{
					await PostJson(cl_472.http, outBase + "/send", new
					{
						jid = msg.Jid,
						text = "Puzzle belum siap. Coba lagi nanti."
					});
					return Results.Json(new
					{
						ok = true,
						action = "puzzle-nopool"
					});
				}
				ActivePuzzle cur;
				lock (cl_472.puzzleLock)
				{
					cl_472.activePuzzles.TryGetValue(msg.Jid, out cur);
				}
				if (cur != null && !cur.Revealed)
				{
					await PostJson(cl_472.http, outBase + "/send", new
					{
						jid = msg.Jid,
						text = "Puzzle masih berjalan. Balas langkahmu, atau ketik " + cl_472.config.CommandPrefix + pzc.SolveCommand + " nanti."
					});
					return Results.Json(new
					{
						ok = true,
						action = "puzzle-busy"
					});
				}
				if (cur != null && cur.Revealed && (DateTime.UtcNow - cur.SolvedAt).TotalSeconds < 12.0)
				{
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "Puzzle barusan selesai. Santai dulu sebentar ya — ketik " + cl_472.config.CommandPrefix + "peringkat untuk papan skor." });
					return Results.Json(new { ok = true, action = "puzzle-cooldown" });
				}
				if (!cl_472.cmdCooldown.Allow(msg.Jid + "|pznew", 12))
				{
					await PostJson(cl_472.http, outBase + "/send", new { jid = msg.Jid, text = "Sabar ya, puzzle baru bisa diminta tiap beberapa detik. Coba lagi sebentar." });
					return Results.Json(new { ok = true, action = "puzzle-ratelimited" });
				}
				await cl_472.PostPuzzleAsync(msg.Jid, false, null, PuzzleMove.DifficultySlot(cmdText, pzc.RevealMinutes));
				return Results.Json(new
				{
					ok = true,
					action = "puzzle"
				});
			}
			PuzzleConfig zc2 = cl_472.config.Puzzle;
			int num9;
			if (zc2 != null)
			{
				bool flag3 = zc2.Enabled;
				num9 = (flag3 ? 1 : 0);
			}
			else
			{
				num9 = 0;
			}
			if (((uint)num9 & (eCommands ? 1u : 0u) & (isCommand ? 1u : 0u)) != 0 && (cmdName == zc2.SolveCommand || cmdName == "nyerah" || cmdName == "menyerah"))
			{
				ActivePuzzle ap;
				lock (cl_472.puzzleLock)
				{
					cl_472.activePuzzles.TryGetValue(msg.Jid, out ap);
				}
				if (ap == null)
				{
					await PostJson(cl_472.http, outBase + "/send", new
					{
						jid = msg.Jid,
						text = "Belum ada puzzle aktif. Mulai: " + cl_472.config.CommandPrefix + zc2.Command + "."
					});
				}
				else if (ap.Revealed || ap.WrongCount >= 6 || !(DateTimeOffset.UtcNow.UtcDateTime < ap.PostedAt.AddMinutes(g?.PuzzleSolveAfterMinutes ?? zc2.SolveAfterMinutes)))
				{
					await cl_472.RevealPuzzleAsync(msg.Jid, ap, false);
				}
				else
				{
					await PostJson(cl_472.http, outBase + "/send", new
					{
						jid = msg.Jid,
						text = zc2.TryHarderMessage
					});
				}
				return Results.Json(new
				{
					ok = true,
					action = "solusi"
				});
			}
			PuzzleConfig puzzle = cl_472.config.Puzzle;
			int num10;
			if (puzzle != null)
			{
				bool flag3 = puzzle.Enabled;
				num10 = (flag3 ? 1 : 0);
			}
			else
			{
				num10 = 0;
			}
			int num11;
			if (((uint)num10 & (eCommands ? 1u : 0u) & (isCommand ? 1u : 0u)) != 0)
			{
				switch (cmdName)
				{
				default:
					num11 = ((cmdName == "leaderboard") ? 1 : 0);
					break;
				case "peringkat":
				case "ranking":
				case "rangking":
				case "rank":
				case "papan":
				case "skor":
					num11 = 1;
					break;
				}
			}
			else
			{
				num11 = 0;
			}
			if (num11 != 0)
			{
				List<PuzzleScoreStore.PlayerScore> top2 = PuzzleScoreStore.Top(msg.Jid, 10);
				string text3;
				if (top2.Count == 0)
				{
					text3 = "Belum ada skor puzzle di grup ini. Jawab puzzle harian untuk mulai mengumpulkan poin! \ud83e\udde9";
				}
				else
				{
					StringBuilder sb2 = new StringBuilder();
					sb2.AppendLine("\ud83c\udfc6 *Papan Peringkat Puzzle*");
					string[] medal = new string[3] { "\ud83e\udd47", "\ud83e\udd48", "\ud83e\udd49" };
					for (int i3 = 0; i3 < top2.Count; i3++)
					{
						string pos = ((i3 < 3) ? medal[i3] : $"{i3 + 1}.");
						string nm = (string.IsNullOrWhiteSpace(top2[i3].Name) ? "Pemain" : top2[i3].Name);
						StringBuilder stringBuilder = sb2;
						StringBuilder stringBuilder22 = stringBuilder;
						StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(21, 4, stringBuilder);
						handler.AppendFormatted(pos);
						handler.AppendLiteral(" *");
						handler.AppendFormatted(nm);
						handler.AppendLiteral("* — ");
						handler.AppendFormatted(top2[i3].Points);
						handler.AppendLiteral(" poin · ");
						handler.AppendFormatted(top2[i3].Solves);
						handler.AppendLiteral(" solusi");
						stringBuilder22.AppendLine(ref handler);
					}
					sb2.Append("\nKetik langkah saat puzzle harian untuk naik peringkat ♟\ufe0f");
					text3 = sb2.ToString();
				}
				await PostJson(cl_472.http, outBase + "/send", new
				{
					jid = msg.Jid,
					text = text3
				});
				return Results.Json(new
				{
					ok = true,
					action = "peringkat"
				});
			}
			puzzle = cl_472.config.Puzzle;
			int num12;
			if (puzzle != null)
			{
				bool flag3 = puzzle.Enabled;
				num12 = (flag3 ? 1 : 0);
			}
			else
			{
				num12 = 0;
			}
			if (((uint)num12 & (eCommands ? 1u : 0u) & (isCommand ? 1u : 0u)) != 0 && cmdName == "resetperingkat")
			{
				if (!AdminSync.IsAllowed(cl_472.config, senderNum, senderPhone))
				{
					return Results.Json(new
					{
						ok = true,
						action = "reset-denied"
					});
				}
				bool had2 = PuzzleScoreStore.Reset(msg.Jid);
				await PostJson(cl_472.http, outBase + "/send", new
				{
					jid = msg.Jid,
					text = (had2 ? "Papan peringkat puzzle grup ini sudah direset. \ud83e\uddf9" : "Belum ada skor untuk direset.")
				});
				return Results.Json(new
				{
					ok = true,
					action = "reset-peringkat"
				});
			}
			int num13;
			if (eCommands && isCommand)
			{
				switch (cmdName)
				{
				default:
					num13 = ((cmdName == "eval") ? 1 : 0);
					break;
				case "analisa":
				case "analisis":
				case "analyze":
					num13 = 1;
					break;
				}
			}
			else
			{
				num13 = 0;
			}
			if (num13 != 0)
			{
				if (!StockfishEngine.Available)
				{
					await PostJson(cl_472.http, outBase + "/send", new
					{
						jid = msg.Jid,
						text = "Engine analisa belum siap di server."
					});
					return Results.Json(new
					{
						ok = true,
						action = "analisa-noengine"
					});
				}
				if (false) // cooldown internal DIMATIKAN: bentrok kunci dgn cooldown command global -> dulu selalu blok !analisa
				{
					return Results.Json(new
					{
						ok = true,
						action = "analisa-cooldown"
					});
				}
				string rawAn = msg.Text.TrimStart();
				int spAn = rawAn.IndexOfAny(new char[4] { ' ', '\n', '\r', '\t' });
				string argAn = ((spAn >= 0) ? rawAn.Substring(spAn + 1).Trim() : "");
				string noteAn = "";
				if (!argAn.Contains('/') && argAn.Length <= 15)
				{
					JsonElement kAn;
					string mediaId = ((!(msg.MediaType == "image")) ? ((msg.QuotedId.Length > 0) ? msg.QuotedId : "") : ((msg.Key.ValueKind == JsonValueKind.Object && msg.Key.TryGetProperty("id", out kAn)) ? (kAn.GetString() ?? "") : ""));
					if (mediaId.Length > 0)
					{
						string al = argAn.ToLowerInvariant();
						bool blackGiven = al.Contains("hitam") || al.Contains("black");
						bool sideGiven = blackGiven || al.Contains("putih") || al.Contains("white");
						byte[] imgBytes = null;
						try
						{
							using HttpResponseMessage rsp = await cl_472.http.PostAsync(outBase + "/get-media", new StringContent(JsonSerializer.Serialize(new
							{
								id = mediaId
							}), Encoding.UTF8, "application/json"));
							if (rsp.IsSuccessStatusCode)
							{
								using JsonDocument doc = JsonDocument.Parse(await rsp.Content.ReadAsStringAsync());
								string s = default(string);
								int num14;
								if (doc.RootElement.TryGetProperty("base64", out var b64))
								{
									s = b64.GetString();
									num14 = ((s != null) ? 1 : 0);
								}
								else
								{
									num14 = 0;
								}
								if (num14 != 0)
								{
									imgBytes = Convert.FromBase64String(s);
								}
							}
						}
						catch
						{
						}
						if (imgBytes == null)
						{
							await PostJson(cl_472.http, outBase + "/send", new
							{
								jid = msg.Jid,
								text = "Tak bisa ambil gambarnya. Kirim ulang gambar papan + caption !analisa ya."
							});
							return Results.Json(new
							{
								ok = true,
								action = "analisa-nomedia"
							});
						}
						try { File.WriteAllBytes(Path.Combine(cl_472.puzzleCacheDir, "_last_analisa.png"), imgBytes); } catch { } // DEBUG: tangkap gambar asli utk tuning
						bool autoFlipped;
						string placement = BoardVision.RecognizeFenAuto(imgBytes, cl_472.pieceAssetsDir, out autoFlipped);
						if (placement == null)
						{
							await PostJson(cl_472.http, outBase + "/send", new
							{
								jid = msg.Jid,
								text = "Gagal membaca papan dari gambar. Pastikan screenshot papan (Lichess/Chess.com) yang jelas, hanya papannya."
							});
							return Results.Json(new
							{
								ok = true,
								action = "analisa-norecog"
							});
						}
						if (!sideGiven)
						{
							PendingAnalysis.Set(msg.Jid + "|" + senderNum, placement);
							string imgAsk = null;
							try
							{
								imgAsk = BoardRenderer.Render(BoardVision.BuildFullFen(placement, true), false, cl_472.puzzleCacheDir, cl_472.pieceAssetsDir);
							}
							catch
							{
							}
							string ask = (autoFlipped ? "\ud83d\udd04 Papan terdeteksi dari sisi Hitam \u2014 sudah kubalik otomatis.\n" : "") + "\ud83d\udcf7 Ini posisi yang kubaca. *Giliran siapa?* Balas *Putih* atau *Hitam*.\n(kalau orientasi masih salah, balas mis. *hitam balik*. Bidak salah baca? kirim FEN-nya.)";
							if (imgAsk == null)
							{
								await PostJson(cl_472.http, outBase + "/send", new
								{
									jid = msg.Jid,
									text = ask
								});
							}
							else
							{
								await PostJson(cl_472.http, outBase + "/send-image", new
								{
									jid = msg.Jid,
									path = imgAsk,
									caption = ask
								});
							}
							return Results.Json(new
							{
								ok = true,
								action = "analisa-ask-side"
							});
						}
						if (al.Contains("balik") || al.Contains("terbalik") || al.Contains("flip")) placement = BoardVision.FlipPlacement(placement); // papan sisi Hitam -> putar 180
						argAn = BoardVision.BuildFullFen(placement, !blackGiven);
						noteAn = "\ud83d\udcf7 Posisi terbaca dari gambar (" + (blackGiven ? "Hitam" : "Putih") + " jalan). Kalau ada bidak salah baca, kirim FEN-nya ya.\n\n";
					}
					else if (argAn.Length == 0)
					{
						await PostJson(cl_472.http, outBase + "/send", new
						{
							jid = msg.Jid,
							text = "Kirim *FEN*/*PGN*, atau kirim *GAMBAR* papan (screenshot Lichess/Chess.com) dengan caption *!analisa* (tambah 'hitam' kalau giliran Hitam)."
						});
						return Results.Json(new
						{
							ok = true,
							action = "analisa-empty"
						});
					}
				}
				cl_472.SendTyping(msg.Jid, ctx.Channel);
				ChessAnalysis.Output outp = (await ChessAnalysis.Run(argAn, cl_472.config.Ai, cl_472.http, cl_472.app.Logger)) ?? new ChessAnalysis.Output("Gagal menganalisa.", "");
				string capAn = noteAn + outp.Text;
				string imgAn = null;
				if (outp.Fen.Length > 0)
				{
					try
					{
						imgAn = BoardRenderer.Render(outp.Fen, !outp.Fen.Contains(" w "), cl_472.puzzleCacheDir, cl_472.pieceAssetsDir);
					}
					catch
					{
					}
				}
				if (imgAn == null)
				{
					await PostJson(cl_472.http, outBase + "/send", new
					{
						jid = msg.Jid,
						text = capAn
					});
				}
				else
				{
					await PostJson(cl_472.http, outBase + "/send-image", new
					{
						jid = msg.Jid,
						path = imgAn,
						caption = capAn
					});
				}
				return Results.Json(new
				{
					ok = true,
					action = "analisa"
				});
			}
			puzzle = cl_472.config.Puzzle;
			int num15;
			if (puzzle != null)
			{
				bool flag3 = puzzle.Enabled;
				num15 = (flag3 ? 1 : 0);
			}
			else
			{
				num15 = 0;
			}
			if (num15 != 0)
			{
				ActivePuzzle pap;
				lock (cl_472.puzzleLock)
				{
					if (msg.QuotedId.Length > 0 && cl_472.puzzleByMsg.TryGetValue(msg.QuotedId, out ActivePuzzle byMsg))
					{
						pap = byMsg;
					}
					else
					{
						cl_472.activePuzzles.TryGetValue(msg.Jid, out pap);
					}
				}
				JsonElement _idEl;
				string inMsgId = ((msg.Key.ValueKind == JsonValueKind.Object && msg.Key.TryGetProperty("id", out _idEl)) ? (_idEl.GetString() ?? "") : "");
				if (pap != null && !pap.Revealed && pap.Puzzle.SolutionSan.Length != 0)
				{
					string[] sol = pap.Puzzle.SolutionSan;
					string attempt = PuzzleMove.StripMoveNumber(cmdText.TrimStart('!', ' ').Trim());
					if (PuzzleMove.IsMoveLike(attempt))
					{
						int idx;
						lock (cl_472.puzzleLock)
						{
							idx = pap.Progress;
						}
						if (idx < sol.Length && (PuzzleMove.Matches(attempt, sol[idx]) || PuzzleMove.MatchesByPosition((idx > 0 && idx - 1 < pap.Puzzle.Fens.Length) ? pap.Puzzle.Fens[idx - 1] : pap.Puzzle.Fen, attempt, sol[idx])))
						{
							string oppMove = null;
							bool done;
							int prog;
							lock (cl_472.puzzleLock)
							{
								pap.Progress++;
								if (pap.Progress < sol.Length)
								{
									oppMove = sol[pap.Progress];
									pap.Progress++;
								}
								done = pap.Progress >= sol.Length;
								if (done)
								{
									pap.Revealed = true;
									pap.SolvedAt = DateTime.UtcNow;
								}
								prog = pap.Progress;
								if (!pap.SolverNums.Contains(senderNum))
								{
									pap.SolverNums.Add(senderNum);
									pap.SolverJids.Add(msg.Participant);
								}
							}
							cl_472.SaveActivePuzzles();
							int pts = PuzzleScoreStore.Tier(pap.Puzzle.Rating);
							PuzzleScoreStore.Award(msg.Jid, senderNum, msg.PushName, pts, done);
							try
							{
								await PostJson(cl_472.http, outBase + "/react", new
								{
									jid = msg.Jid,
									key = msg.Key,
									emoji = (done ? "\ud83c\udf89" : "✅")
								});
							}
							catch
							{
							}
							if (done)
							{
								List<string> helperNums = new List<string>();
								List<string> mentionList = new List<string> { msg.Participant };
								lock (cl_472.puzzleLock)
								{
									for (int i4 = 0; i4 < pap.SolverNums.Count; i4++)
									{
										if (pap.SolverNums[i4] != senderNum)
										{
											helperNums.Add(pap.SolverNums[i4]);
											mentionList.Add(pap.SolverJids[i4]);
										}
									}
								}
								string credit = ((helperNums.Count > 0) ? ("\nDibantu " + string.Join(" ", helperNums.Select((string h) => "@" + h)) + " \ud83d\udc4f") : "");
								List<PuzzleScoreStore.PlayerScore> topN = PuzzleScoreStore.Top(msg.Jid, 3);
								string board = "";
								if (topN.Count > 0)
								{
									string[] md = new string[3] { "\ud83e\udd47", "\ud83e\udd48", "\ud83e\udd49" };
									List<string> parts2 = new List<string>();
									for (int i5 = 0; i5 < topN.Count; i5++)
									{
										string nm2 = (string.IsNullOrWhiteSpace(topN[i5].Name) ? "Pemain" : topN[i5].Name);
										parts2.Add($"{md[i5]} {nm2} ({topN[i5].Points})");
									}
									board = $"\n\n\ud83c\udfc6 *Peringkat:* {string.Join(" · ", parts2)}\n_Ketik {cl_472.config.CommandPrefix}peringkat untuk lengkap_";
								}
								string t = ((oppMove == null) ? $"✅ *Tepat sekali, @{senderNum}!* \ud83c\udf89 Itu jurus pamungkasnya — puzzle selesai. Keren! (+{pts} poin) ♟\ufe0f{credit}{board}" : $"✅ *Tepat sekali, @{senderNum}!* Lawan terpaksa main *{oppMove}*, dan itu menutup variannya. \ud83c\udf89 Puzzle selesai, mantap! (+{pts} poin) ♟\ufe0f{credit}{board}");
								await PostJson(cl_472.http, outBase + "/send", new
								{
									jid = msg.Jid,
									text = t + PuzzleMove.ThemeNote(pap.Puzzle.Themes),
									mentions = mentionList.ToArray(),
									replyToId = inMsgId
								});
							}
							else
							{
								string cap = $"✅ *Benar, @{senderNum}!* (+{pts} poin) \ud83d\udc4f Lawan membalas *{oppMove}*.\nSekarang giliranmu — langkah terbaik berikutnya apa? \ud83e\udd14";
								string[] fens = pap.Puzzle.Fens;
								string img = null;
								if (prog - 1 >= 0 && prog - 1 < fens.Length)
								{
									try
									{
										img = BoardRenderer.Render(fens[prog - 1], pap.Puzzle.Side == "b", cl_472.puzzleCacheDir, cl_472.pieceAssetsDir);
									}
									catch
									{
									}
								}
								if (img == null)
								{
									await PostJson(cl_472.http, outBase + "/send", new
									{
										jid = msg.Jid,
										text = cap,
										mentions = new string[1] { msg.Participant },
										replyToId = inMsgId
									});
								}
								else
								{
									await PostJson(cl_472.http, outBase + "/send-image", new
									{
										jid = msg.Jid,
										path = img,
										caption = cap,
										mentions = new string[1] { msg.Participant },
										replyToId = inMsgId
									});
								}
							}
							return Results.Json(new
							{
								ok = true,
								action = "puzzle-correct",
								progress = prog
							});
						}
						bool alreadyPlayed = false;
						for (int i6 = 0; i6 < idx && i6 < sol.Length; i6 += 2)
						{
							if (PuzzleMove.Matches(attempt, sol[i6]))
							{
								alreadyPlayed = true;
								break;
							}
						}
						if (alreadyPlayed)
						{
							if (cl_472.cmdCooldown.Allow(msg.Jid + "|" + senderNum + "|pzplayed", 8))
							{
								await PostJson(cl_472.http, outBase + "/react", new
								{
									jid = msg.Jid,
									key = msg.Key,
									emoji = "\ud83d\udc4d"
								});
							}
							return Results.Json(new
							{
								ok = true,
								action = "puzzle-already"
							});
						}
						bool isReplyToPuzzle = msg.QuotedId.Length > 0 && (msg.QuotedId == pap.MsgId || cl_472.puzzleByMsg.ContainsKey(msg.QuotedId));
						bool strongChess = Regex.IsMatch(attempt, "[KQRBNGMx=+#]") || attempt.Contains("O-O") || attempt.Contains("0-0");
						if (!isReplyToPuzzle && !strongChess)
						{
							return Results.Json(new
							{
								ok = true,
								action = "puzzle-maybe-chat"
							});
						}
						pap.WrongCount++;
						if (cl_472.cmdCooldown.Allow(msg.Jid + "|" + senderNum + "|pzwrong", (pap.WrongCount <= 3) ? 10 : 25) && cl_472.cmdCooldown.Allow(msg.Jid + "|pzwrongAny", (pap.WrongCount <= 3) ? 4 : 25))
						{
							// nama tampil lewat mention @senderNum (di-tag agar pemain ke-notify)
							string nextSanW = (idx < sol.Length) ? sol[idx] : "";
							string engHintW = null;
							if (pap.WrongCount <= 3 && StockfishEngine.Available) { string curFenW = (idx > 0 && idx - 1 < pap.Puzzle.Fens.Length) ? pap.Puzzle.Fens[idx - 1] : pap.Puzzle.Fen; try { engHintW = await ChessAnalysis.CritiqueSafe(curFenW, attempt); } catch { } }
							string text4 = (pap.WrongCount <= 3) ? ("Belum pas, @" + senderNum + ".\n" + (engHintW ?? PuzzleMove.LocalWrongHint(nextSanW, pap.WrongCount))) : ("Belum pas, @" + senderNum + ".");
							if (pap.WrongCount >= 4 && !pap.SolveHintShown)
							{
								text4 += "\n\nKetik " + cl_472.config.CommandPrefix + (cl_472.config.Puzzle?.SolveCommand ?? "solusi") + " untuk lihat jawabannya."; pap.SolveHintShown = true;
							}
							await PostJson(cl_472.http, outBase + "/send", new
							{
								jid = msg.Jid,
								text = text4,
								mentions = new string[1] { msg.Participant },
								replyToId = inMsgId
							});
						}
						return Results.Json(new
						{
							ok = true,
							action = "puzzle-wrong"
						});
					}
					ai = cl_472.config.Ai;
					int num17;
					if (ai != null)
					{
						bool flag3 = ai.Enabled;
						num17 = (flag3 ? 1 : 0);
					}
					else
					{
						num17 = 0;
					}
					if (num17 != 0)
					{
						string low = msg.Text.ToLowerInvariant();
						bool otherTopic = Regex.IsMatch(low, "\\b(jadwal|turnamen|tournament|daftar|register|next|help|bantuan|standing|klasemen|pairing|hasil|result|info|kapan|dimana|di mana|harga|biaya|bayar|admin|grup|join|link)\\b");
						bool chessCue = Regex.IsMatch(low, "(langkah|soal|solusi|jawab|posisi|skak|sekak|bidak|menteri|benteng|kuda|gajah|\\braja\\b|pion|puzzle|kenapa|knp|napa|kok|gimana|gmn|gmna|jelas|maksud|salah)");
						bool isReplyToThis = msg.QuotedId.Length > 0 && msg.QuotedId == pap.MsgId;
						bool relevant = chessCue || isReplyToThis || msg.MentionedBot;
						if ((low.Contains('?') || chessCue) && !otherTopic && relevant && cl_472.cmdCooldown.Allow(msg.Jid + "|" + senderNum + "|pzask", 15))
						{
							string sideT2 = ((pap.Puzzle.Side == "w") ? "Putih" : "Hitam");
							string solLine2 = FormatPuzzleSolution(pap.Puzzle);
							string prompt2 = $"Ini puzzle catur yang BELUM diselesaikan pemain. Posisi FEN: {pap.Puzzle.Fen}. {sideT2} yang jalan. Langkah terbaik menurut mesin (RAHASIA, untuk pemahamanmu saja): {solLine2}. Pemain bertanya/berkomentar: \"{msg.Text.Trim()}\". " + "Jawab 1-3 kalimat pendek, ramah, Bahasa Indonesia natural. Jelaskan alasan posisi atau konsekuensi dari pertanyaan pemain. Kalau pemain menanyakan langkah yang belum pas, jawab seperti teman latihan: ringan, jelas, dan tidak menggurui. Jangan pakai istilah 'refutasi', 'konkret', 'varian', 'aku belum yakin', atau 'tidak mau asal menebak'. Kalau tidak yakin detailnya, beri arahan umum tanpa mengarang. JANGAN memberi kandidat langkah terbaik untuk pemain. JANGAN sebut atau parafrasekan langkah terbaik/solusi rahasia.";
							string ans3 = await Ai.Ask(cl_472.config.Ai, cl_472.http, prompt2, cl_472.app.Logger);
							string reply2 = (string.IsNullOrWhiteSpace(ans3) ? "Maaf, aku belum bisa menjelaskan dengan baik sekarang. Silakan coba lagi sebentar ya." : PuzzleMove.HumanizeWrongExplanation(PuzzleMove.CleanWrongExplanation(ans3, sol)));
							if (reply2.Length > cl_472.config.Ai.MaxOutputChars)
							{
								reply2 = reply2.Substring(0, cl_472.config.Ai.MaxOutputChars) + "…";
							}
							await PostJson(cl_472.http, outBase + "/send", new
							{
								jid = msg.Jid,
								text = "@" + senderNum + " " + reply2,
								mentions = new string[1] { msg.Participant },
								replyToId = inMsgId
							});
							return Results.Json(new
							{
								ok = true,
								action = "puzzle-explain"
							});
						}
					}
				}
				else if (pap != null && pap.Revealed && pap.Puzzle.SolutionSan.Length != 0)
				{
					string attempt2 = PuzzleMove.StripMoveNumber(cmdText.TrimStart('!', ' ').Trim());
					bool recent2 = (DateTime.UtcNow - pap.SolvedAt).TotalMinutes <= 3.0 || (msg.QuotedId.Length > 0 && msg.QuotedId == pap.MsgId);
					if (PuzzleMove.IsMoveLike(attempt2) && recent2 && cl_472.cmdCooldown.Allow(msg.Jid + "|" + senderNum + "|pzdone", 12))
					{
						string[] sol2 = pap.Puzzle.SolutionSan;
						bool wasRight = false;
						for (int i7 = 0; i7 < sol2.Length; i7 += 2)
						{
							if (PuzzleMove.Matches(attempt2, sol2[i7]))
							{
								wasRight = true;
								break;
							}
						}
						string text5 = (wasRight ? ("✅ Betul juga, @" + senderNum + "! \ud83d\udc4f Tapi puzzle ini sudah keburu diselesaikan tadi. Tunggu puzzle berikutnya ya \ud83d\ude42") : ("Puzzle ini sudah selesai, @" + senderNum + ". \ud83d\ude42 Tunggu puzzle berikutnya ya!"));
						await PostJson(cl_472.http, outBase + "/send", new
						{
							jid = msg.Jid,
							text = text5,
							mentions = new string[1] { msg.Participant },
							replyToId = inMsgId
						});
						return Results.Json(new
						{
							ok = true,
							action = "puzzle-done-late"
						});
					}
				}
			}
			if (eCommands && isCommand)
			{
				string reply3 = await CommandHandler.Handle(cmdText, cl_472.config, cl_472.http, cl_472.app.Logger);
				if (reply3 != null)
				{
					await PostJson(cl_472.http, outBase + "/send", new
					{
						jid = msg.Jid,
						text = reply3
					});
					TopicStore.Set(msg.Jid, cmdName);
				}
				return Results.Json(new
				{
					ok = true,
					action = "command",
					replied = (reply3 != null)
				});
			}
			FaqConfig faq = cl_472.config.Faq;
			int num18;
			if (faq != null)
			{
				bool flag3 = faq.Enabled;
				num18 = (flag3 ? 1 : 0);
			}
			else
			{
				num18 = 0;
			}
			if (num18 != 0)
			{
				FaqEntry[] entries = cl_472.config.Faq.Entries;
				foreach (FaqEntry f in entries)
				{
					if (!string.IsNullOrEmpty(f.Pattern) && (!cl_472.config.Faq.RequireMention || msg.MentionedBot))
					{
						try
						{
							if (Regex.IsMatch(msg.Text, f.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
							{
								if (quietNow)
								{
									return Results.Json(new
									{
										ok = true,
										action = "quiet-faq",
										id = f.Id
									});
								}
								if (cooldownSec > 0 && !senderExempt && !cl_472.cmdCooldown.Allow($"{msg.Jid}|{senderNum}|faq:{f.Id}", cooldownSec))
								{
									return Results.Json(new
									{
										ok = true,
										action = "faq-cooldown",
										id = f.Id
									});
								}
								string faqReply = f.Reply;
								if (faqReply.Contains("{schedule}"))
								{
									string text6 = faqReply;
									faqReply = text6.Replace("{schedule}", await CommandHandler.BuildSchedule(cl_472.config, cl_472.http, cl_472.app.Logger));
								}
								if (faqReply.Contains("{rules}"))
								{
									faqReply = faqReply.Replace("{rules}", cl_472.config.RulesText);
								}
								await PostJson(cl_472.http, outBase + "/send", new
								{
									jid = msg.Jid,
									text = faqReply
								});
								TopicStore.Set(msg.Jid, f.Id);
								return Results.Json(new
								{
									ok = true,
									action = "faq",
									id = f.Id
								});
							}
						}
						catch
						{
						}
					}
				}
			}
			number = NumberUtil.Normalize(msg.Participant);
			if (senderExempt)
			{
				return Results.Json(new
				{
					ok = true,
					action = "exempt"
				});
			}
			string probationKey = number;
			bool isMedia = !string.IsNullOrEmpty(msg.MediaType);
			bool hasUnsafeLink = ModUtil.HasUnsafeLink(msg.Text);
			string probeReason = null;
			ProbationConfig pc = cl_472.config.Probation;
			if (pc != null && pc.Enabled && cl_472.joins.InProbation(probationKey, pc.Minutes, DateTimeOffset.UtcNow.UtcDateTime))
			{
				if (pc.BlockMedia && isMedia && (!pc.BlockForwardedOnly || msg.IsForwarded))
				{
					probeReason = "media (anggota baru)";
				}
				else if (pc.BlockLinks && hasUnsafeLink)
				{
					probeReason = "link (anggota baru)";
				}
			}
			string mediaReason = null;
			int num20;
			if (probeReason == null && isMedia)
			{
				MediaModerationConfig mm = cl_472.config.MediaModeration;
				if (mm != null && mm.BlockForwardedMedia && msg.IsForwarded)
				{
					num20 = ((msg.ForwardScore >= Math.Max(1, mm.ForwardScoreThreshold)) ? 1 : 0);
					goto IL_a1ed;
				}
			}
			num20 = 0;
			goto IL_a1ed;
			IL_a1ed:
			if (num20 != 0)
			{
				mediaReason = "media sering diteruskan";
			}
			if (probeReason != null || mediaReason != null)
			{
				if (ctx.Caps.CanDelete)
				{
					await PostJson(cl_472.http, outBase + "/delete", new
					{
						jid = msg.Jid,
						key = msg.Key
					});
				}
				int pcount = cl_472.warnings.Increment(msg.Jid + "|" + msg.Participant);
				if (!quietNow)
				{
					string tmpl = ((probeReason == null) ? (cl_472.config.MediaModeration?.Message ?? "@user, media saya rapikan dulu untuk menjaga grup dari spam.") : (cl_472.config.Probation?.Message ?? "@user, untuk anggota baru, link/media saya tahan sementara agar grup tetap aman."));
					string warnText2 = tmpl.Replace("@user", "@" + number).Replace("{count}", pcount.ToString());
					await PostJson(cl_472.http, outBase + "/send", new
					{
						jid = msg.Jid,
						text = warnText2,
						mentions = new string[1] { msg.Participant }
					});
				}
				string tag = ((probeReason != null) ? "probation" : "fwd-media");
				cl_472.audit.Write(msg.Jid, msg.Participant, msg.PushName, tag, pcount, string.IsNullOrEmpty(msg.Text) ? ("[" + msg.MediaType + "]") : msg.Text);
				cl_472.app.Logger.LogInformation("HAPUS ({Tag}) dari {Number}, peringatan ke-{Count}", tag, number, pcount);
				return Results.Json(new
				{
					ok = true,
					action = tag,
					warnCount = pcount
				});
			}
			bool isFlood;
			bool shouldWarnFlood;
			if (eFlood)
			{
				(isFlood, shouldWarnFlood) = cl_472.floodTracker.Check(msg.Jid + "|" + msg.Participant);
			}
			else
			{
				isFlood = false;
				shouldWarnFlood = false;
			}
			matched = (eModeration ? cl_472.rules.FirstOrDefault((Rule rule) => RuleActive(rule, g) && !rule.Shadow && rule.Compiled.IsMatch(msg.Text)) : null);
			if (matched == null && eModeration)
			{
				Rule shadow = cl_472.rules.FirstOrDefault((Rule rule) => RuleActive(rule, g) && rule.Shadow && rule.Compiled.IsMatch(msg.Text));
				if (shadow != null)
				{
					cl_472.audit.Write(msg.Jid, msg.Participant, msg.PushName, "SHADOW:" + shadow.Id, 0, msg.Text);
					cl_472.app.Logger.LogInformation("SHADOW (tidak dihapus) dari {Number} (aturan {Rule})", number, shadow.Id);
				}
			}
			if (matched != null)
			{
				if (ctx.Caps.CanDelete)
				{
					await PostJson(cl_472.http, outBase + "/delete", new
					{
						jid = msg.Jid,
						key = msg.Key
					});
				}
				count = cl_472.warnings.Increment(msg.Jid + "|" + msg.Participant);
				if (!quietNow)
				{
					string[] wv = cl_472.config.WarningMessageVariants;
					if (wv != null)
					{
						int num4 = wv.Length;
						if (num4 > 0)
						{
							obj4 = wv[Random.Shared.Next(wv.Length)];
							goto IL_a94f;
						}
					}
					obj4 = cl_472.config.WarningMessage;
					goto IL_a94f;
				}
				goto IL_aa9d;
			}
			if (!isFlood)
			{
				return Results.Json(new
				{
					ok = true,
					action = "clean"
				});
			}
			if (ctx.Caps.CanDelete)
			{
				await PostJson(cl_472.http, outBase + "/delete", new
				{
					jid = msg.Jid,
					key = msg.Key
				});
			}
			if (!shouldWarnFlood || quietNow)
			{
				return Results.Json(new
				{
					ok = true,
					action = "flood",
					warned = false
				});
			}
			fcount = cl_472.warnings.Increment(msg.Jid + "|" + msg.Participant);
			string[] fv = cl_472.config.FloodWarningMessageVariants;
			if (fv != null)
			{
				int num4 = fv.Length;
				if (num4 > 0)
				{
					obj3 = fv[Random.Shared.Next(fv.Length)];
					goto IL_ad48;
				}
			}
			obj3 = cl_472.config.FloodWarningMessage;
			goto IL_ad48;
		});
		cl_472.app.MapPost("/member-joined", (Func<MemberJoined, Task<IResult>>)async delegate(MemberJoined ev)
		{
			cl_472.config.Groups.TryGetValue(ev.Jid, out GroupConfig g);
			if (!cl_472.config.ManageAllGroups && g == null)
			{
				return Results.Json(new
				{
					ok = true,
					action = "unmanaged"
				});
			}
			ProbationConfig probation = cl_472.config.Probation;
			int num;
			if (probation != null && probation.Enabled)
			{
				string[] participants = ev.Participants;
				num = ((participants != null && participants.Length > 0) ? 1 : 0);
			}
			else
			{
				num = 0;
			}
			if (num != 0)
			{
				DateTime nowJoin = DateTimeOffset.UtcNow.UtcDateTime;
				string[] participants2 = ev.Participants;
				foreach (string pjid in participants2)
				{
					cl_472.joins.Record(NumberUtil.Normalize(pjid), nowJoin);
				}
			}
			if (!(g?.WelcomeEnabled ?? cl_472.config.WelcomeEnabled))
			{
				return Results.Json(new
				{
					ok = true,
					action = "welcome-disabled"
				});
			}
			if (QuietHours.IsActive(g?.QuietHours ?? cl_472.config.QuietHours, DateTimeOffset.UtcNow))
			{
				return Results.Json(new
				{
					ok = true,
					action = "quiet-welcome"
				});
			}
			if (ev.Participants == null || ev.Participants.Length == 0)
			{
				return Results.Json(new
				{
					ok = true,
					action = "no-participants"
				});
			}
			string welcomeMsg = g?.WelcomeMessage ?? cl_472.config.WelcomeMessage;
			string rulesText = g?.RulesText ?? cl_472.config.RulesText;
			string[] participants3 = ev.Participants;
			foreach (string p in participants3)
			{
				string number = NumberUtil.Normalize(p);
				string text3 = welcomeMsg.Replace("@user", "@" + number).Replace("{group}", ev.GroupName ?? "").Replace("{rules}", rulesText);
				await cl_472.PostImportant(ChannelRoute.BaseForJid(cl_472.config, ev.Jid) + "/send", new
				{
					jid = ev.Jid,
					text = text3,
					mentions = new string[1] { p }
				});
			}
			cl_472.app.Logger.LogInformation("Sambutan dikirim ke {Count} member baru di {Jid}", ev.Participants.Length, ev.Jid);
			return Results.Json(new
			{
				ok = true,
				action = "welcomed",
				count = ev.Participants.Length
			});
		});
		cl_472.app.MapPost("/broadcast", (Func<BroadcastRequest, Task<IResult>>)async delegate(BroadcastRequest req)
		{
			if (string.IsNullOrWhiteSpace(cl_472.config.BroadcastToken))
			{
				return Results.Json(new
				{
					ok = false,
					error = "broadcast nonaktif (set broadcastToken di config)"
				}, (JsonSerializerOptions?)null, (string?)null, (int?)403);
			}
			if (req.Token != cl_472.config.BroadcastToken)
			{
				return Results.Json(new
				{
					ok = false,
					error = "token salah"
				}, (JsonSerializerOptions?)null, (string?)null, (int?)401);
			}
			if (string.IsNullOrWhiteSpace(req.Text))
			{
				return Results.Json(new
				{
					ok = false,
					error = "text wajib diisi"
				}, (JsonSerializerOptions?)null, (string?)null, (int?)400);
			}
			string jid = req.Jid;
			int tid = default(int);
			int num;
			if (string.IsNullOrWhiteSpace(jid))
			{
				int? tournamentId = req.TournamentId;
				if (tournamentId.HasValue)
				{
					tid = tournamentId.GetValueOrDefault();
					num = 1;
				}
				else
				{
					num = 0;
				}
			}
			else
			{
				num = 0;
			}
			if (num != 0)
			{
				cl_472.config.TournamentGroups.TryGetValue(tid.ToString(), out jid);
			}
			if (string.IsNullOrWhiteSpace(jid))
			{
				return Results.Json(new
				{
					ok = false,
					error = "sertakan 'jid' grup atau 'tournamentId' yang terdaftar di tournamentGroups"
				}, (JsonSerializerOptions?)null, (string?)null, (int?)400);
			}
			string outText = await EnrichBroadcastText(req.Text, cl_472.http, cl_472.app.Logger, cl_472.config);
			if (!(await cl_472.PostImportant(ChannelRoute.BaseForJid(cl_472.config, jid) + "/send", new
			{
				jid = jid,
				text = outText,
				mentions = ExtractWhatsAppMentions(outText)
			})))
			{
				cl_472.app.Logger.LogWarning("Broadcast GAGAL ke {Jid}", jid);
				return Results.Json(new
				{
					ok = false,
					error = "gagal kirim ke gateway/channel",
					jid = jid
				}, (JsonSerializerOptions?)null, (string?)null, (int?)502);
			}
			cl_472.app.Logger.LogInformation("Broadcast ke {Jid} ({Len} karakter)", jid, outText.Length);
			return Results.Json(new
			{
				ok = true,
				action = "broadcast",
				jid = jid
			});
		});
		cl_472.app.Run(cl_472.config.ListenUrl);
	}

	[CompilerGenerated]
	private static async Task<string> EnrichBroadcastText(string text, HttpClient http, ILogger logger, AppConfig config)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(text) || text.IndexOf("Pairing Manual", StringComparison.OrdinalIgnoreCase) < 0)
			{
				return text;
			}
			Match link = Regex.Match(text, @"https?://lichess\.org/([A-Za-z0-9]{8,12})(?![A-Za-z0-9/])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
			if (!link.Success)
			{
				return text;
			}
			string gameId = link.Groups[1].Value;
			string gameUrl = link.Value;
			Match pair = Regex.Match(text, @"(?im)^\s*([^\s()]+)\s*\(Putih\)\s+vs\s+([^\s()]+)\s*\(Hitam\)", RegexOptions.CultureInvariant);
			string whiteUser = pair.Success ? pair.Groups[1].Value.Trim() : "";
			string blackUser = pair.Success ? pair.Groups[2].Value.Trim() : "";
			Match tc = Regex.Match(text, @"(?im)^\s*(G?\d+\+\d+)\s*-\s*(rated|casual)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
			string timeLine = tc.Success ? (tc.Groups[1].Value.ToUpperInvariant() + " - " + tc.Groups[2].Value.ToLowerInvariant()) : "";
			using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "https://lichess.org/game/export/" + Uri.EscapeDataString(gameId) + "?players=true&moves=false&clocks=false&evals=false&opening=false");
			req.Headers.Add("User-Agent", "WA-Bot");
			req.Headers.Accept.ParseAdd("application/json");
			using HttpResponseMessage resp = await http.SendAsync(req);
			if (resp.IsSuccessStatusCode)
			{
				using JsonDocument doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
				JsonElement root = doc.RootElement;
				if (root.TryGetProperty("players", out var players))
				{
					whiteUser = LichessGameUser(players, "white", whiteUser);
					blackUser = LichessGameUser(players, "black", blackUser);
				}
				if (timeLine.Length == 0)
				{
					string rated = (root.TryGetProperty("rated", out var ratedEl) && ratedEl.ValueKind == JsonValueKind.True) ? "rated" : "casual";
					if (root.TryGetProperty("clock", out var clock) && clock.TryGetProperty("initial", out var ini) && ini.ValueKind == JsonValueKind.Number && clock.TryGetProperty("increment", out var inc) && inc.ValueKind == JsonValueKind.Number)
					{
						timeLine = "G" + Math.Max(1, ini.GetInt32() / 60).ToString(CultureInfo.InvariantCulture) + "+" + inc.GetInt32().ToString(CultureInfo.InvariantCulture) + " - " + rated;
					}
				}
			}
			if (whiteUser.Length == 0 || blackUser.Length == 0)
			{
				return text;
			}
			string whiteName = await LichessDisplayName(whiteUser, http, logger);
			string blackName = await LichessDisplayName(blackUser, http, logger);
			string[] mentionJids = PairingMentionJids(config, whiteUser, blackUser);
			string tagLine = PairingTagLine(mentionJids);
			string header = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).FirstOrDefault(x => x.Length > 0) ?? "Pairing Manual - Liga Catur";
			StringBuilder sb = new StringBuilder();
			sb.Append("\ud83d\udcaa *").Append(header).AppendLine("*");
			sb.AppendLine("Duel siap dimulai. Main rapi, gas sampai akhir!");
			if (tagLine.Length > 0)
			{
							sb.AppendLine("Tag pemain: " + tagLine);
			}
			sb.AppendLine();
			sb.AppendLine("Putih: " + FormatPairingPlayer(whiteName, whiteUser));
			sb.AppendLine("Hitam: " + FormatPairingPlayer(blackName, blackUser));
			if (timeLine.Length > 0)
			{
				sb.AppendLine();
				sb.AppendLine(timeLine);
			}
			sb.AppendLine();
			sb.Append(gameUrl);
			return sb.ToString();
		}
		catch (Exception ex)
		{
			logger.LogWarning("Enrich pairing manual gagal: {Msg}", ex.Message);
			return text;
		}
	}

	private static string LichessGameUser(JsonElement players, string color, string fallback)
	{
		if (players.TryGetProperty(color, out var side) && side.TryGetProperty("user", out var user))
		{
			if (user.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
			{
				return name.GetString() ?? fallback;
			}
			if (user.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
			{
				return id.GetString() ?? fallback;
			}
		}
		return fallback;
	}

	private static async Task<string> LichessDisplayName(string username, HttpClient http, ILogger logger)
	{
		try
		{
			using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "https://lichess.org/api/user/" + Uri.EscapeDataString(username));
			req.Headers.Add("User-Agent", "WA-Bot");
			using HttpResponseMessage resp = await http.SendAsync(req);
			if (!resp.IsSuccessStatusCode)
			{
				return username;
			}
			using JsonDocument doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
			JsonElement root = doc.RootElement;
			string uname = root.TryGetProperty("username", out var un) && un.ValueKind == JsonValueKind.String ? (un.GetString() ?? username) : username;
			string title = root.TryGetProperty("title", out var ti) && ti.ValueKind == JsonValueKind.String ? ((ti.GetString() ?? "") + " ") : "";
			if (root.TryGetProperty("profile", out var profile))
			{
				string real = GetStringProp(profile, "realName");
				if (real.Length == 0)
				{
					string first = GetStringProp(profile, "firstName");
					string last = GetStringProp(profile, "lastName");
					real = (first + " " + last).Trim();
				}
				if (real.Length > 0)
				{
					return (title + real).Trim();
				}
			}
			return (title + uname).Trim();
		}
		catch (Exception ex)
		{
			logger.LogWarning("Lichess display name gagal untuk {User}: {Msg}", username, ex.Message);
			return username;
		}
	}

	private static string GetStringProp(JsonElement obj, string name)
	{
		return obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "").Trim() : "";
	}

	private static string[] PairingMentionJids(AppConfig config, params string[] lichessUsers)
	{
		Dictionary<string, string> map = config.PlayerMentions ?? new Dictionary<string, string>();
		if (map.Count == 0)
		{
			return Array.Empty<string>();
		}
		List<string> result = new List<string>();
		foreach (string user in lichessUsers ?? Array.Empty<string>())
		{
			if (string.IsNullOrWhiteSpace(user))
			{
				continue;
			}
			string? raw = null;
			if (!map.TryGetValue(user.Trim(), out raw))
			{
				raw = map.FirstOrDefault(kv => kv.Key.Equals(user.Trim(), StringComparison.OrdinalIgnoreCase)).Value;
			}
			string jid = NormalizeMentionJid(raw ?? "");
			if (jid.Length > 0 && !result.Contains(jid, StringComparer.OrdinalIgnoreCase))
			{
				result.Add(jid);
			}
		}
		return result.Take(5).ToArray();
	}

	private static string NormalizeMentionJid(string raw)
	{
		string value = (raw ?? "").Trim();
		if (value.Length == 0)
		{
			return "";
		}
		if (value.Contains('@'))
		{
			return value;
		}
		string digits = Regex.Replace(value, @"\D", "");
		return digits.Length >= 6 ? (digits + "@s.whatsapp.net") : "";
	}

	private static string PairingTagLine(string[] mentionJids)
	{
		return string.Join(" ", (mentionJids ?? Array.Empty<string>()).Select(j => "@" + Regex.Replace(j.Split('@')[0], @"\D", "")).Where(x => x.Length > 1));
	}

	private static string[] ExtractWhatsAppMentions(string text)
	{
		List<string> result = new List<string>();
		foreach (Match m in Regex.Matches(text ?? "", @"@(\d{6,16})", RegexOptions.CultureInvariant))
		{
			string jid = m.Groups[1].Value + "@s.whatsapp.net";
			if (!result.Contains(jid, StringComparer.OrdinalIgnoreCase))
			{
				result.Add(jid);
			}
			if (result.Count >= 5)
			{
				break;
			}
		}
		return result.ToArray();
	}
	private static string FormatPairingPlayer(string displayName, string username)
	{
		if (string.IsNullOrWhiteSpace(displayName))
		{
			return username;
		}
		if (displayName.Equals(username, StringComparison.OrdinalIgnoreCase))
		{
			return displayName;
		}
		return "*" + displayName + "* (@" + username + ")";
	}
	internal static IResult PanelDeny(HttpContext c)
	{
		c.Response.Headers["WWW-Authenticate"] = "Basic realm=\"WA Bot Admin\"";
		return Results.Text("Perlu login admin (password = token admin).", "text/plain", null, 401);
	}

	[CompilerGenerated]
	internal static HashSet<string> BuildExempt(AppConfig cfg)
	{
		return (from s in cfg.ExemptNumbers.Select(NumberUtil.Normalize)
			where s.Length > 0
			select s).ToHashSet();
	}

	[CompilerGenerated]
	internal static List<PuzzleItem> LoadPuzzlePool(string path, ILogger logger)
	{
		try
		{
			if (!File.Exists(path))
			{
				logger.LogWarning("puzzles.json tidak ditemukan: {Path}", path);
				return new List<PuzzleItem>();
			}
			return JsonSerializer.Deserialize<List<PuzzleItem>>(File.ReadAllText(path)) ?? new List<PuzzleItem>();
		}
		catch (Exception ex)
		{
			logger.LogError("Gagal muat puzzles.json: {Msg}", ex.Message);
			return new List<PuzzleItem>();
		}
	}

	[CompilerGenerated]
	internal static Dictionary<string, ActivePuzzle> LoadActivePuzzles(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				return JsonSerializer.Deserialize<Dictionary<string, ActivePuzzle>>(File.ReadAllText(path)) ?? new Dictionary<string, ActivePuzzle>();
			}
		}
		catch
		{
		}
		return new Dictionary<string, ActivePuzzle>();
	}

	[CompilerGenerated]
	internal static HashSet<string> LoadPuzzleDailyState(string path)
	{
		try
		{
			if (!File.Exists(path))
			{
				return new HashSet<string>();
			}
			string text = File.ReadAllText(path).Trim();
			if (text.StartsWith("["))
			{
				return JsonSerializer.Deserialize<HashSet<string>>(text) ?? new HashSet<string>();
			}
			return string.IsNullOrWhiteSpace(text) ? new HashSet<string>() : new HashSet<string> { text };
		}
		catch
		{
			return new HashSet<string>();
		}
	}

	[CompilerGenerated]
	internal static void SavePuzzleDailyState(string path, HashSet<string> sentSlots, string today)
	{
		try
		{
			string[] value = (from s in sentSlots
				where s.StartsWith(today + "|", StringComparison.Ordinal)
				orderby s
				select s).ToArray();
			File.WriteAllText(path, JsonSerializer.Serialize(value));
		}
		catch
		{
		}
	}

	[CompilerGenerated]
	internal static PuzzleItem PickPuzzleForSlot(List<PuzzleItem> pool, PuzzleDailySlot slot, HashSet<int> usedIdx, HashSet<string>? excludeIds = null)
	{
		DC_0_9 cl_12 = new DC_0_9();
		cl_12.excludeIds = excludeIds;
		cl_12.usedIdx = usedIdx;
		cl_12.min = Math.Max(0, slot.MinRating);
		cl_12.max = ((slot.MaxRating > 0) ? slot.MaxRating : 9999);
		List<(PuzzleItem, int)> list = (from x in pool.Select((PuzzleItem p, int i) => (p: p, i: i))
			where !cl_12.usedIdx.Contains(x.i) && cl_12.NotActive(x) && x.p.Rating >= cl_12.min && x.p.Rating <= cl_12.max
			select x).ToList();
		if (list.Count == 0)
		{
			list = (from x in pool.Select((PuzzleItem p, int i) => (p: p, i: i))
				where !cl_12.usedIdx.Contains(x.i) && cl_12.NotActive(x)
				select x).ToList();
		}
		if (list.Count == 0)
		{
			list = (from x in pool.Select((PuzzleItem p, int i) => (p: p, i: i))
				where !cl_12.usedIdx.Contains(x.i)
				select x).ToList();
		}
		if (list.Count == 0)
		{
			list = pool.Select((PuzzleItem p, int i) => (p: p, i: i)).ToList();
		}
		(PuzzleItem, int) tuple = list[Random.Shared.Next(list.Count)];
		cl_12.usedIdx.Add(tuple.Item2);
		return tuple.Item1;
	}

	[CompilerGenerated]
	internal static string FormatPuzzleSolution(PuzzleItem p)
	{
		string[] solutionSan = p.SolutionSan;
		if (solutionSan.Length == 0)
		{
			return "(solusi tidak tersedia)";
		}
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < solutionSan.Length; i += 2)
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(1, 2, stringBuilder2);
			handler.AppendFormatted(i / 2 + 1);
			handler.AppendLiteral(".");
			handler.AppendFormatted(solutionSan[i]);
			stringBuilder3.Append(ref handler);
			if (i + 1 < solutionSan.Length)
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder4 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(1, 1, stringBuilder2);
				handler.AppendLiteral(" ");
				handler.AppendFormatted(solutionSan[i + 1]);
				stringBuilder4.Append(ref handler);
			}
			stringBuilder.Append(' ');
		}
		return stringBuilder.ToString().Trim();
	}

	[CompilerGenerated]
	internal static bool RuleActive(Rule r, GroupConfig? g)
	{
		bool result = r.Enabled;
		if (g?.DisabledRules != null && ((ReadOnlySpan<string>)g.DisabledRules).Contains(r.Id))
		{
			result = false;
		}
		if (g?.EnabledRules != null && ((ReadOnlySpan<string>)g.EnabledRules).Contains(r.Id))
		{
			result = true;
		}
		return result;
	}

	internal static string DmAnnounceKey(IncomingMessage msg, string senderNum, string senderPhone)
	{
		if (!string.IsNullOrWhiteSpace(senderPhone))
		{
			return NumberUtil.Normalize(senderPhone);
		}
		if (!string.IsNullOrWhiteSpace(senderNum))
		{
			return NumberUtil.Normalize(senderNum);
		}
		return msg.Jid ?? "";
	}

	internal static async Task<string?> TryHandleDmAnnouncement(AppConfig config, HttpClient http, IncomingMessage msg, string senderNum, string senderPhone, string q, ILogger logger, AuditLog audit, int puzzleRevealMinutes, int puzzlePoolCount, Func<string, string, Task<bool>> sendPuzzle, Func<string, Task<bool>> revealPuzzle, Func<string> activePuzzleSummary)
	{
		string key = DmAnnounceKey(msg, senderNum, senderPhone);
		if (key.Length == 0) key = msg.Jid ?? "";
		string admin = (senderPhone.Length > 0) ? senderPhone : senderNum;
		string raw = (q ?? "").Trim();
		string low = raw.ToLowerInvariant();
		bool isAdmin = IsDmAdmin(config, senderNum, senderPhone);
		if (low == "bantuan" || low == "help" || low == "menu")
		{
			audit.WriteAdminDm(admin, "help", "dm", "ok", raw);
			return DmAdminHelp();
		}
		if (Regex.IsMatch(low, "^(status|cek status)\\s+bot$|^bot\\s+(status|online)\\??$", RegexOptions.CultureInvariant))
		{
			string status = await BuildDmBotStatus(config, http, puzzlePoolCount);
			audit.WriteAdminDm(admin, "status", "dm", "ok", raw);
			return status;
		}
		if ((low == "pending" || low == "cek pending") && isAdmin)
		{
			audit.WriteAdminDm(admin, "pending", "dm", "ok", raw);
			return BuildDmPendingSummary(key);
		}
		if ((low == "batal semua" || low == "cancel semua") && isAdmin)
		{
			bool removed;
			lock (DmAnnounceLock) removed = DmAnnouncePending.Remove(key);
			audit.WriteAdminDm(admin, "pending-clear", "dm", removed ? "ok" : "empty", raw);
			return removed ? "Semua pending PM kamu sudah dibatalkan." : "Tidak ada pending PM.";
		}
		if (Regex.IsMatch(low, "^(jadwal|jadwal\\s+malam\\s+ini|next|turnamen)$", RegexOptions.CultureInvariant) && isAdmin)
		{
			string sched = await CommandHandler.BuildSchedule(config, http, logger);
			audit.WriteAdminDm(admin, "schedule", "dm", "ok", raw);
			return sched;
		}
		if (Regex.IsMatch(low, "^(standings|klasemen)(\\s+.*)?$", RegexOptions.CultureInvariant) && isAdmin)
		{
			string standings = await BuildDmStandings(raw, http, logger);
			audit.WriteAdminDm(admin, "standings", "dm", "ok", raw);
			return standings;
		}
		if (Regex.IsMatch(low, "^(hasil|result|hasil\\s+turnamen\\s+terakhir|hasil\\s+terakhir)(\\s+.*)?$", RegexOptions.CultureInvariant) && isAdmin)
		{
			string result = await BuildDmResult(config, raw, http, logger);
			audit.WriteAdminDm(admin, "result", "dm", "ok", raw);
			return result;
		}
		if ((low == "audit terakhir" || low == "log terakhir") && isAdmin)
		{
			List<string> lines = audit.Tail(10);
			audit.WriteAdminDm(admin, "audit-tail", "dm", "ok", raw);
			return FormatAuditTail(lines);
		}
		if ((low == "puzzle aktif" || low == "cek puzzle aktif") && isAdmin)
		{
			string summary = activePuzzleSummary();
			audit.WriteAdminDm(admin, "puzzle-active", "dm", "ok", raw);
			return summary;
		}
		if ((low == "tidur bot" || low == "bot tidur") && isAdmin)
		{
			lock (DmAnnounceLock)
			{
				DmAnnouncePending[key] = new DmAnnouncementPending { Kind = "sleep", TargetName = "bot", TargetJid = "dm", Text = "tidur bot", CreatedAt = DateTimeOffset.UtcNow };
			}
			audit.WriteAdminDm(admin, "sleep-preview", "dm", "preview", raw);
			return "Yakin tidurkan bot? Balas *ya tidur* untuk lanjut, atau *batal*.";
		}
		if ((low == "bangun bot" || low == "bot bangun") && isAdmin)
		{
			Sleeper.Set(false);
			audit.WriteAdminDm(admin, "wake", "dm", "ok", raw);
			return "Siap, saya bangun lagi.";
		}
		Match aliasSet = Regex.Match(raw, "^alias\\s+([A-Za-z0-9_-]+)\\s*=\\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (aliasSet.Success && isAdmin)
		{
			string aliasKey = aliasSet.Groups[1].Value.Trim();
			string aliasTargetName = aliasSet.Groups[2].Value.Trim().Trim('"');
			GroupOption? target = await ResolveOneGroup(config, http, aliasTargetName);
			if (target == null) return await DescribeGroupResolutionFailure(config, http, aliasTargetName);
			AliasStore.Set(aliasKey, target.Subject);
			audit.WriteAdminDm(admin, "alias-set", target.Subject, "ok", aliasKey);
			return "Alias tersimpan: " + aliasKey + " = " + target.Subject;
		}
		if (low == "alias" && isAdmin)
		{
			List<KeyValuePair<string, string>> all = AliasStore.All();
			audit.WriteAdminDm(admin, "alias-list", "dm", "ok", raw);
			return all.Count == 0 ? "Belum ada alias grup." : ("Alias grup:\n" + string.Join("\n", all.Select(kv => "- " + kv.Key + " = " + kv.Value)));
		}
		Match template = Regex.Match(raw, "^template\\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (template.Success && isAdmin)
		{
			string body = BuildAnnouncementDraft(template.Groups[1].Value.Trim());
			audit.WriteAdminDm(admin, "template", "dm", "ok", body);
			return "Template:\n\n" + body;
		}
		Match remind = Regex.Match(raw, "^ingatkan\\s+([^\\s]+)\\s+vs\\s+([^\\s]+)(?:\\s+ke\\s+(.+))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (remind.Success && isAdmin)
		{
			string body = await BuildPairingReminderText(config, http, logger, remind.Groups[1].Value.Trim(), remind.Groups[2].Value.Trim());
			string target = remind.Groups[3].Success ? remind.Groups[3].Value.Trim() : DefaultAnnouncementTarget(config);
			if (target.Length == 0) return "Target grup belum jelas. Tambahkan: ke <nama grup>.";
			return await PrepareDmPending(config, http, key, target, "reminder", body, "", "Preview reminder siap", logger, audit, admin);
		}
		DmAnnouncementPending? pending = null;
		lock (DmAnnounceLock)
		{
			if (DmAnnouncePending.TryGetValue(key, out pending) && (DateTimeOffset.UtcNow - pending.CreatedAt).TotalMinutes > 10.0)
			{
				DmAnnouncePending.Remove(key);
				pending = null;
			}
		}
		if (pending != null)
		{
			if (low == "batal" || low == "cancel")
			{
				lock (DmAnnounceLock) DmAnnouncePending.Remove(key);
				audit.WriteAdminDm(admin, pending.Kind, pending.TargetName, "cancel", pending.Text);
				return "Siap, dibatalkan.";
			}
			if (low == "ya" || low == "y" || low == "kirim" || low == "gas" || low == "ok" || low == "oke" || low == "ya tidur")
			{
				bool sent = false;
				if (pending.Kind == "sleep")
				{
					sent = true;
					_ = Task.Run(async delegate { await Task.Delay(1500); Sleeper.Set(true); });
				}
				else if (pending.Kind == "puzzle")
				{
					sent = await sendPuzzle(pending.TargetJid, pending.Level);
				}
				else if (pending.Kind == "delete-last")
				{
					sent = await PostJson(http, ChannelRoute.BaseForJid(config, pending.TargetJid) + "/delete", new { jid = pending.TargetJid, key = pending.DeleteKey });
				}
				else
				{
					(string text, List<string> mentions) resolved = await ResolveOutgoingMentions(config, http, pending.TargetJid, pending.Text);
					string[] mentions = (pending.Kind == "pairing" || pending.Kind == "reminder") ? ExtractWhatsAppMentions(resolved.text) : resolved.mentions.ToArray();
					sent = await PostJson(http, ChannelRoute.BaseForJid(config, pending.TargetJid) + "/send", new { jid = pending.TargetJid, text = resolved.text, mentions = mentions });
				}
				lock (DmAnnounceLock) DmAnnouncePending.Remove(key);
				audit.WriteAdminDm(admin, pending.Kind, pending.TargetName, sent ? "ok" : "failed", pending.Text);
				if (sent) return pending.Kind == "sleep" ? "Baik, saya tidur dulu. Bangunkan dengan *bangun bot*." : pending.Kind == "puzzle" ? ("Puzzle terkirim ke \"" + pending.TargetName + "\".") : pending.Kind == "delete-last" ? ("Pesan terakhir di \"" + pending.TargetName + "\" sudah dihapus.") : ("Terkirim ke \"" + pending.TargetName + "\".");
				return "Gagal menjalankan aksi untuk \"" + pending.TargetName + "\". Coba lagi sebentar.";
			}
			return "Masih ada preview. Balas *ya* untuk lanjut, atau *batal*.";
		}
		if (LooksDmAdminCommand(raw) && !isAdmin)
		{
			audit.WriteAdminDm(admin, "denied", "dm", "not-admin", raw);
			return "Perintah ini khusus admin PM.";
		}
		Match draft = Regex.Match(raw, "^buat\\s+pengumuman\\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (draft.Success)
		{
			string body = BuildAnnouncementDraft(draft.Groups[1].Value.Trim());
			audit.WriteAdminDm(admin, "draft", "dm", "ok", body);
			return "Draft pengumuman:\n\n" + body + "\n\nUntuk kirim, balas: kirim ke <nama grup>: " + body;
		}
		Match solusi = Regex.Match(raw, "^solusi\\s+puzzle\\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (solusi.Success)
		{
			string solusiTargetName = solusi.Groups[1].Value.Trim().Trim('\"');
			GroupOption? target = await ResolveOneGroup(config, http, solusiTargetName);
			if (target == null) return await DescribeGroupResolutionFailure(config, http, solusiTargetName);
			bool ok = await revealPuzzle(target.Jid);
			audit.WriteAdminDm(admin, "solusi-puzzle", target.Subject, ok ? "ok" : "no-active-puzzle", raw);
			return ok ? ("Solusi puzzle dikirim ke \"" + target.Subject + "\".") : ("Tidak ada puzzle aktif di \"" + target.Subject + "\".");
		}
		Match del = Regex.Match(raw, "^hapus\\s+pesan\\s+terakhir(?:\\s+spam)?\\s+(?:di|dari)\\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (del.Success)
		{
			string delTargetName = del.Groups[1].Value.Trim().Trim('\"');
			GroupOption? target = await ResolveOneGroup(config, http, delTargetName);
			if (target == null) return await DescribeGroupResolutionFailure(config, http, delTargetName);
			DmAnnouncementPending? p = await BuildDeleteLastPending(config, http, target);
			if (p == null) return "Saya belum menemukan pesan terakhir yang bisa dihapus di \"" + target.Subject + "\".";
			lock (DmAnnounceLock) DmAnnouncePending[key] = p;
			audit.WriteAdminDm(admin, "delete-last-preview", target.Subject, "preview", p.Text);
			return "Preview hapus pesan.\nGrup: " + target.Subject + "\nPesan: " + p.Text + "\n\nBalas *ya* untuk hapus, atau *batal*.";
		}
		Match puzzle = Regex.Match(raw, "^(?:kirim\\s+)?puzzle\\s*(mudah|sedang|sulit|easy|medium|hard|gampang|susah)?\\s+ke\\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (puzzle.Success)
		{
			string level = puzzle.Groups[1].Value.Trim().ToLowerInvariant();
			return await PrepareDmPending(config, http, key, puzzle.Groups[2].Value.Trim().Trim('\"'), "puzzle", "", level, "Preview puzzle siap", logger, audit, admin);
		}
		Match pair = Regex.Match(raw, "^pair(?:ing)?\\s+([^\\s]+)\\s+vs\\s+([^\\s]+)\\s+((?:G)?\\d+\\+\\d+)\\s+(rated|casual)\\s+(https?://lichess\\.org/[A-Za-z0-9]{8,12})(?:\\s+ke\\s+(.+))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (pair.Success)
		{
			string text = "Pairing Manual - Liga Catur\n\n" + pair.Groups[1].Value.Trim() + " (Putih) vs " + pair.Groups[2].Value.Trim() + " (Hitam)\n" + pair.Groups[3].Value.Trim().ToUpperInvariant() + " - " + pair.Groups[4].Value.Trim().ToLowerInvariant() + "\n\n" + pair.Groups[5].Value.Trim();
			string enriched = await EnrichBroadcastText(text, http, logger, config);
			string target = pair.Groups[6].Success ? pair.Groups[6].Value.Trim() : DefaultAnnouncementTarget(config);
			if (target.Length == 0) return "Pairing sudah diformat, tapi target grup belum jelas. Tambahkan: ke <nama grup>.";
			return await PrepareDmPending(config, http, key, target, "pairing", enriched, "", "Preview pairing siap", logger, audit, admin);
		}
		Match announce = Regex.Match(raw, "^!?(?:kirim|umumkan|pengumuman)\\s+ke\\s+(.+?)\\s*:\\s*([\\s\\S]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (announce.Success)
		{
			string body = announce.Groups[2].Value.Trim();
			if (announce.Groups[1].Value.Trim().Length == 0 || body.Length == 0) return "Format: kirim ke <nama grup>: <pesan>";
			return await PrepareDmPending(config, http, key, announce.Groups[1].Value.Trim().Trim('\"'), "text", body, "", "Preview pengumuman siap", logger, audit, admin);
		}
		return null;
	}

	internal static string BuildDmPendingSummary(string key)
	{
		lock (DmAnnounceLock)
		{
			if (!DmAnnouncePending.TryGetValue(key, out DmAnnouncementPending? p) || p == null)
			{
				return "Tidak ada pending PM.";
			}
			int age = Math.Max(0, (int)(DateTimeOffset.UtcNow - p.CreatedAt).TotalMinutes);
			string target = string.IsNullOrWhiteSpace(p.TargetName) ? "dm" : p.TargetName;
			return "Pending PM:\n- aksi: " + p.Kind + "\n- tujuan: " + target + "\n- umur: " + age.ToString(CultureInfo.InvariantCulture) + " menit\n\nBalas *ya* untuk lanjut, atau *batal*.";
		}
	}

	internal static string FormatAuditTail(List<string> lines)
	{
		if (lines == null || lines.Count == 0)
		{
			return "Audit masih kosong.";
		}
		List<string> rows = new List<string>();
		foreach (string line in lines.Take(10))
		{
			string clean = Regex.Replace(line ?? "", "\\s*\\|\\s*teks=\".*\"$", "").Trim();
			rows.Add("- " + clean);
		}
		return "Audit terakhir:\n" + string.Join("\n", rows);
	}

	internal static async Task<string> BuildDmStandings(string raw, HttpClient http, ILogger logger)
	{
		Match id = Regex.Match(raw ?? "", "\\b(\\d{3,})\\b", RegexOptions.CultureInvariant);
		if (id.Success && int.TryParse(id.Groups[1].Value, out int tid))
		{
			return await CommandHandler.BuildStandings(tid, http, logger);
		}
		List<(string url, string swiss, string name, string date)> recent = await CommandHandler.GetRecentTournaments(http, logger, 5);
		if (recent.Count == 0)
		{
			return "Daftar turnamen belum terbaca. Coba standings <id>.";
		}
		var t = recent[0];
		return await CommandHandler.BuildStandingsSmart(t.url, t.swiss, t.name, http, logger);
	}

	internal static async Task<string> BuildDmResult(AppConfig config, string raw, HttpClient http, ILogger logger)
	{
		Match id = Regex.Match(raw ?? "", "\\b(\\d{3,})\\b", RegexOptions.CultureInvariant);
		if (id.Success && int.TryParse(id.Groups[1].Value, out int tid))
		{
			return await CommandHandler.BuildResult(tid, http, logger);
		}
		return await CommandHandler.BuildLatestResult(config, http, logger);
	}

	internal static async Task<string> DescribeGroupResolutionFailure(AppConfig config, HttpClient http, string groupName)
	{
		string name = groupName;
		string? alias = AliasStore.Get(name);
		if (!string.IsNullOrEmpty(alias)) name = alias;
		List<GroupOption> matches = MatchGroups(await FetchGroups(config.GatewayUrl, http), name);
		if (matches.Count == 0)
		{
			return "Grup tidak ketemu. Cek nama atau buat alias dulu.";
		}
		StringBuilder sb = new StringBuilder("Nama grup ambigu. Pilih yang lebih spesifik:\n");
		int max = Math.Min(5, matches.Count);
		for (int i = 0; i < max; i++)
		{
			sb.Append(i + 1).Append(". ").Append(matches[i].Subject).Append('\n');
		}
		sb.Append("\nUlangi command dengan nama lengkapnya.");
		return sb.ToString().TrimEnd();
	}
	internal static bool IsDmAdmin(AppConfig config, string senderNum, string senderPhone)
	{
		string[] admins = config.DmAdmins ?? Array.Empty<string>();
		if (admins.Length == 0) return AdminSync.IsAllowed(config, senderNum, senderPhone);
		return admins.Any(a => { string n = NumberUtil.Normalize(a); return n.Length > 0 && (n == senderNum || (senderPhone.Length > 0 && n == senderPhone)); });
	}

	internal static bool LooksDmAdminCommand(string raw)
	{
		return Regex.IsMatch(raw ?? "", "^(kirim|umumkan|pengumuman|pair|pairing|puzzle|solusi\\s+puzzle|hapus\\s+pesan|buat\\s+pengumuman|audit|log|alias|template|ingatkan|tidur\\s+bot|bangun\\s+bot|bot\\s+tidur|bot\\s+bangun|jadwal|next|turnamen|standings|klasemen|hasil|result|pending|cek\\s+pending|batal\\s+semua|cancel\\s+semua)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	}

	internal static string DmAdminHelp()
	{
		return "Bisa, ini format PM admin:\n- status bot\n- pending / batal semua\n- jadwal malam ini\n- standings LCI\n- hasil terakhir\n- kirim ke LCI: teks\n- pair user1 vs user2 G5+3 casual link ke LCI\n- puzzle mudah ke LCI\n- solusi puzzle LCI\n- buat pengumuman Bendino malam ini\n- hapus pesan terakhir di LCI\n- audit terakhir\n- alias LCI = Liga Catur Indonesia\n- puzzle aktif\n- template Bendino\n- ingatkan ade21h vs Mikaysr ke LCI\n- tidur bot / bangun bot";
	}

	internal static async Task<GroupOption?> ResolveOneGroup(AppConfig config, HttpClient http, string groupName)
	{
		string name = groupName;
		string? alias = AliasStore.Get(name);
		if (!string.IsNullOrEmpty(alias)) name = alias;
		List<GroupOption> groups = await FetchGroups(config.GatewayUrl, http);
		List<GroupOption> matches = MatchGroups(groups, name);
		return matches.Count == 1 ? matches[0] : null;
	}

	private static async Task<DmAnnouncementPending?> BuildDeleteLastPending(AppConfig config, HttpClient http, GroupOption target)
	{
		try
		{
			using HttpResponseMessage r = await http.GetAsync(config.GatewayUrl + "/last-message?jid=" + Uri.EscapeDataString(target.Jid));
			if (!r.IsSuccessStatusCode) return null;
			using JsonDocument doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
			if (!doc.RootElement.TryGetProperty("message", out var m) || !m.TryGetProperty("key", out var key)) return null;
			string who = m.TryGetProperty("who", out var w) ? (w.GetString() ?? "?") : "?";
			string body = m.TryGetProperty("text", out var t) ? (t.GetString() ?? "") : "";
			string preview = (who + ": " + body).Trim();
			if (preview.Length > 180) preview = preview.Substring(0, 180) + "...";
			return new DmAnnouncementPending { TargetJid = target.Jid, TargetName = target.Subject, Kind = "delete-last", Text = preview, DeleteKey = key.Clone(), CreatedAt = DateTimeOffset.UtcNow };
		}
		catch
		{
			return null;
		}
	}

	internal static async Task<string> PrepareDmPending(AppConfig config, HttpClient http, string key, string groupName, string kind, string body, string level, string title, ILogger logger, AuditLog audit, string admin)
	{
		GroupOption? target = await ResolveOneGroup(config, http, groupName);
		if (target == null) return await DescribeGroupResolutionFailure(config, http, groupName);
		lock (DmAnnounceLock)
		{
			DmAnnouncePending[key] = new DmAnnouncementPending { TargetJid = target.Jid, TargetName = target.Subject, Text = body, Kind = kind, Level = level, CreatedAt = DateTimeOffset.UtcNow };
		}
		audit.WriteAdminDm(admin, kind + "-preview", target.Subject, "preview", body.Length > 0 ? body : level);
		if (kind == "puzzle")
		{
			string lv = level.Length > 0 ? (" (" + level + ")") : "";
			return title + ".\nTujuan: " + target.Subject + lv + "\n\nBalas *ya* untuk kirim, atau *batal*.";
		}
		return title + ".\nTujuan: " + target.Subject + "\n\n" + body + "\n\nBalas *ya* untuk kirim, atau *batal*.";
	}

	internal static string BuildActivePuzzleSummary(Dictionary<string, ActivePuzzle> active)
	{
		if (active == null || active.Count == 0)
		{
			return "Tidak ada puzzle aktif.";
		}
		List<string> rows = new List<string>();
		foreach (KeyValuePair<string, ActivePuzzle> kv in active.Take(10))
		{
			ActivePuzzle ap = kv.Value;
			string label = (ap?.Puzzle?.Rating > 0) ? ("Rating " + ap.Puzzle.Rating.ToString(CultureInfo.InvariantCulture)) : "Puzzle";
			string id = ap?.Puzzle?.Id ?? "?";
			rows.Add("- " + ShortJid(kv.Key) + " | " + label + " | " + id);
		}
		return "Puzzle aktif:\n" + string.Join("\n", rows);
	}

	internal static string ShortJid(string jid)
	{
		string s = jid ?? "";
		int at = s.IndexOf('@');
		if (at > 0) s = s.Substring(0, at);
		return s.Length > 18 ? (s.Substring(0, 8) + "..." + s.Substring(s.Length - 6)) : s;
	}

	internal static async Task<string> BuildPairingReminderText(AppConfig config, HttpClient http, ILogger logger, string whiteUser, string blackUser)
	{
		string whiteName = await LichessDisplayName(whiteUser, http, logger);
		string blackName = await LichessDisplayName(blackUser, http, logger);
		string[] mentionJids = PairingMentionJids(config, whiteUser, blackUser);
		string tagLine = PairingTagLine(mentionJids);
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("*Reminder Pairing*");
		if (tagLine.Length > 0)
		{
			sb.AppendLine("Tag pemain: " + tagLine);
			sb.AppendLine();
		}
		sb.AppendLine("Putih: " + FormatPairingPlayer(whiteName, whiteUser));
		sb.AppendLine("Hitam: " + FormatPairingPlayer(blackName, blackUser));
		sb.AppendLine();
		sb.AppendLine("Mohon mulai tepat waktu. Main rapi, gas sampai selesai!");
		return sb.ToString().Trim();
	}
	internal static async Task<string> BuildDmBotStatus(AppConfig config, HttpClient http, int puzzlePoolCount)
	{
		bool gatewayOnline = false;
		bool waConnected = false;
		try
		{
			string gw = await http.GetStringAsync(config.GatewayUrl + "/health");
			gatewayOnline = true;
			waConnected = gw.Contains("\"connected\":true");
		}
		catch
		{
		}
		return "Brain online.\nGateway " + (gatewayOnline ? "online" : "offline") + ".\nPuzzle pool " + puzzlePoolCount.ToString(CultureInfo.InvariantCulture) + ".\nWA " + (waConnected ? "tersambung" : "belum tersambung") + ".";
	}

	internal static string BuildAnnouncementDraft(string topic)
	{
		string t = (topic ?? "").Trim();
		if (t.Length == 0) return "Info liga malam ini. Siapkan diri, cek jadwal, dan main tepat waktu.";
		return "Info LCI: " + t.TrimEnd('.') + ". Mohon hadir tepat waktu dan konfirmasi bila berhalangan.";
	}

	internal static string DefaultAnnouncementTarget(AppConfig config)
	{
		string[] targets = config.Relay?.TargetGroups ?? Array.Empty<string>();
		if (targets.Length > 0 && !string.IsNullOrWhiteSpace(targets[0])) return targets[0];
		return config.AdminSyncGroupJid ?? "";
	}
	internal static async Task<(string text, List<string> mentions)> ResolveOutgoingMentions(AppConfig config, HttpClient http, string groupJid, string text)
	{
		List<string> mentions = new List<string>();
		List<(string jid, string number, string phone)> members = await FetchGroupMembers(config.GatewayUrl, http, groupJid);
		string resolved = Regex.Replace(text, "@([A-Za-z0-9]+)", delegate(Match mt)
		{
			string tok = mt.Groups[1].Value;
			string wantPhone = TagAliasStore.Get(tok) ?? "";
			(string jid, string number, string phone) hit = default((string, string, string));
			bool found = false;
			if (wantPhone.Length > 0)
			{
				string wp = NumberUtil.Normalize(wantPhone);
				foreach ((string jid, string number, string phone) mem in members)
				{
					if (NumberUtil.Normalize(mem.phone) == wp && mem.jid.Length > 0)
					{
						hit = mem;
						found = true;
						break;
					}
				}
			}
			else if (tok.Length >= 3 && tok.All(char.IsDigit))
			{
				List<(string jid, string number, string phone)> sfx = members.Where(delegate((string jid, string number, string phone) mem)
				{
					string pp = NumberUtil.Normalize(mem.phone);
					return mem.jid.Length > 0 && pp.Length > 0 && (pp == tok || pp.EndsWith(tok));
				}).ToList();
				if (sfx.Count == 1)
				{
					hit = sfx[0];
					found = true;
				}
			}
			if (found)
			{
				mentions.Add(hit.jid);
				return "@" + hit.number;
			}
			return mt.Value;
		});
		return (resolved, mentions);
	}
	internal static List<GroupOption> MatchGroups(List<GroupOption> groups, string name)
	{
		List<GroupOption> exact = groups.Where((GroupOption go) => go.Jid.Length > 0 && go.Subject.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
		if (exact.Count > 0)
		{
			return exact;
		}
		return groups.Where((GroupOption go) => go.Jid.Length > 0 && go.Subject.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
	}

	internal static async Task<List<(string jid, string number, string phone)>> FetchGroupMembers(string gatewayUrl, HttpClient http, string groupJid)
	{
		List<(string, string, string)> list = new List<(string, string, string)>();
		try
		{
			using HttpResponseMessage r = await http.GetAsync(gatewayUrl + "/group-members?jid=" + Uri.EscapeDataString(groupJid));
			if (!r.IsSuccessStatusCode)
			{
				return list;
			}
			using JsonDocument doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
			if (doc.RootElement.TryGetProperty("members", out var arr))
			{
				foreach (JsonElement m in arr.EnumerateArray())
				{
					string jid = (m.TryGetProperty("jid", out var j) ? (j.GetString() ?? "") : "");
					string number = (m.TryGetProperty("number", out var n) ? (n.GetString() ?? "") : "");
					string phone = (m.TryGetProperty("phone", out var p) ? (p.GetString() ?? "") : "");
					list.Add((jid, number, phone));
				}
			}
		}
		catch
		{
		}
		return list;
	}

	[CompilerGenerated]
	internal static async Task<List<GroupOption>> FetchGroups(string gatewayUrl, HttpClient http)
	{
		List<GroupOption> list = new List<GroupOption>();
		try
		{
			using HttpResponseMessage r = await http.GetAsync(gatewayUrl + "/groups");
			if (!r.IsSuccessStatusCode)
			{
				return list;
			}
			using JsonDocument doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
			if (doc.RootElement.TryGetProperty("groups", out var arr))
			{
				foreach (JsonElement gentry in arr.EnumerateArray())
				{
					list.Add(new GroupOption
					{
						Jid = (gentry.TryGetProperty("jid", out var j) ? (j.GetString() ?? "") : ""),
						Subject = (gentry.TryGetProperty("subject", out var s) ? (s.GetString() ?? "") : "")
					});
					j = default(JsonElement);
					s = default(JsonElement);
				}
			}
		}
		catch
		{
		}
		return list;
	}

	[CompilerGenerated]
	internal static string TargetPrompt(List<GroupOption> opts)
	{
		if (opts.Count == 0)
		{
			return "Bot belum menemukan grup tujuan. Silakan tambahkan bot ke grup tujuan dulu, lalu coba lagi.";
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("Kirim ke grup mana? Balas nomornya (pisah koma) atau ketik 'semua':");
		for (int i = 0; i < opts.Count; i++)
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(2, 2, stringBuilder2);
			handler.AppendFormatted(i + 1);
			handler.AppendLiteral(". ");
			handler.AppendFormatted(opts[i].Subject);
			stringBuilder2.AppendLine(ref handler);
		}
		stringBuilder.Append("(ketik !batal untuk membatalkan)");
		return stringBuilder.ToString();
	}

	[CompilerGenerated]
	internal static List<GroupOption> ParseSelection(string text, List<GroupOption> opts)
	{
		List<GroupOption> list = new List<GroupOption>();
		string text2 = text.Trim().ToLowerInvariant();
		if (text2 == "semua" || text2 == "all" || text2 == "*")
		{
			return new List<GroupOption>(opts);
		}
		string[] array = text.Split(new char[4] { ',', ' ', '.', ';' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string text3 in array)
		{
			if (int.TryParse(text3.Trim(), out var result) && result >= 1 && result <= opts.Count && !list.Contains(opts[result - 1]))
			{
				list.Add(opts[result - 1]);
			}
		}
		return list;
	}

	[CompilerGenerated]
	internal static async Task<bool> PostJson(HttpClient http, string url, object body)
	{
		if (Sleeper.Asleep)
		{
			return false;
		}
		try
		{
			string json = JsonSerializer.Serialize(body);
			using StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
			HttpResponseMessage resp = await http.PostAsync(url, content);
			if (!resp.IsSuccessStatusCode)
			{
				Interlocked.Increment(ref SendLog.Failed);
				SendLog.Logger?.LogWarning("Kirim GAGAL (HTTP {Status}) -> {Url}", (int)resp.StatusCode, url);
				return false;
			}
			Interlocked.Increment(ref SendLog.Sent);
			return true;
		}
		catch (Exception ex)
		{
			Interlocked.Increment(ref SendLog.Failed);
			SendLog.Logger?.LogWarning("Kirim ERROR -> {Url}: {Msg}", url, ex.Message);
			return false;
		}
	}

	[CompilerGenerated]
	internal static async Task<string?> PostAndGetId(HttpClient http, string url, object body)
	{
		if (Sleeper.Asleep)
		{
			return null;
		}
		try
		{
			using StringContent content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
			HttpResponseMessage resp = await http.PostAsync(url, content);
			if (!resp.IsSuccessStatusCode)
			{
				SendLog.Logger?.LogWarning("Kirim gambar GAGAL (HTTP {Status}) -> {Url}", (int)resp.StatusCode, url);
				return null;
			}
			using JsonDocument doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
			JsonElement e;
			return doc.RootElement.TryGetProperty("id", out e) ? (e.GetString() ?? "") : "";
		}
		catch (Exception ex)
		{
			SendLog.Logger?.LogWarning("Kirim gambar ERROR -> {Url}: {Msg}", url, ex.Message);
			return null;
		}
	}
}
internal record IncomingMessage([property: JsonPropertyName("jid")] string Jid, [property: JsonPropertyName("participant")] string Participant, [property: JsonPropertyName("pushName")] string PushName, [property: JsonPropertyName("text")] string Text, [property: JsonPropertyName("key")] JsonElement Key, [property: JsonPropertyName("mentionedBot")] bool MentionedBot, [property: JsonPropertyName("quotedText")] string QuotedText = "", [property: JsonPropertyName("quotedAuthor")] string QuotedAuthor = "", [property: JsonPropertyName("participantPhone")] string ParticipantPhone = "", [property: JsonPropertyName("mediaType")] string MediaType = "", [property: JsonPropertyName("isForwarded")] bool IsForwarded = false, [property: JsonPropertyName("forwardScore")] int ForwardScore = 0, [property: JsonPropertyName("edited")] bool Edited = false, [property: JsonPropertyName("quotedId")] string QuotedId = "", [property: JsonPropertyName("channel")] string Channel = "whatsapp", [property: JsonPropertyName("mentions")] MentionPair[]? Mentions = null);
internal record MentionPair([property: JsonPropertyName("lid")] string Lid = "", [property: JsonPropertyName("phone")] string Phone = "");
internal record MemberJoined([property: JsonPropertyName("jid")] string Jid, [property: JsonPropertyName("groupName")] string GroupName, [property: JsonPropertyName("participants")] string[] Participants, [property: JsonPropertyName("participantsPhone")] string[]? ParticipantsPhone = null);
internal record BroadcastRequest([property: JsonPropertyName("token")] string Token, [property: JsonPropertyName("tournamentId")] int? TournamentId, [property: JsonPropertyName("jid")] string? Jid, [property: JsonPropertyName("text")] string Text);
internal class AppConfig
{
	public string GatewayUrl { get; set; } = "http://127.0.0.1:3211";

	public Dictionary<string, string> Channels { get; set; } = new Dictionary<string, string>();

	public string ListenUrl { get; set; } = "http://127.0.0.1:5050";

	public string WarningMessage { get; set; } = "@user, saya bantu rapikan pesan tadi karena terdeteksi {reason}. Terima kasih sudah ikut menjaga grup tetap nyaman.";

	public string[] WarningMessageVariants { get; set; } = Array.Empty<string>();

	public string[] ExemptNumbers { get; set; } = Array.Empty<string>();

	public bool WelcomeEnabled { get; set; } = true;

	public string WelcomeMessage { get; set; } = "\ud83d\udc4b Selamat datang @user!";

	public string RulesText { get; set; } = "";

	public bool FloodEnabled { get; set; } = true;

	public int FloodMaxMessages { get; set; } = 6;

	public int FloodWindowSeconds { get; set; } = 8;

	public int FloodWarnCooldownSeconds { get; set; } = 30;

	public string FloodWarningMessage { get; set; } = "@user, saya bantu jeda sebentar ya. Beberapa pesan yang sangat berdekatan saya rapikan agar grup tetap enak dibaca.";

	public string[] FloodWarningMessageVariants { get; set; } = Array.Empty<string>();

	public bool CommandsEnabled { get; set; } = true;

	public string CommandPrefix { get; set; } = "!";

	public int CommandCooldownSeconds { get; set; } = 8;

	public string DbConnectionString { get; set; } = "";

	public DataCommand[] DataCommands { get; set; } = Array.Empty<DataCommand>();

	public string BroadcastToken { get; set; } = "";

	public string AdminApiToken { get; set; } = "";

	public string WabotToken { get; set; } = "";

	public LciConfig? Lci { get; set; }

	public Dictionary<string, string> TournamentGroups { get; set; } = new Dictionary<string, string>();

	public Dictionary<string, string> PlayerMentions { get; set; } = new Dictionary<string, string>();

	public bool ModerationEnabled { get; set; } = true;

	public bool ManageAllGroups { get; set; } = true;

	public string[] AdminNumbers { get; set; } = Array.Empty<string>();

	public string[] DmAdmins { get; set; } = Array.Empty<string>();

	public string AdminSyncGroupJid { get; set; } = "";

	public int AdminSyncMinutes { get; set; } = 30;

	public string LaporGroupJid { get; set; } = "";

	public Dictionary<string, GroupConfig> Groups { get; set; } = new Dictionary<string, GroupConfig>();

	public AnnouncerConfig? Announcer { get; set; }

	public AiConfig? Ai { get; set; }

	public NaturalTriggersConfig? NaturalTriggers { get; set; }

	public PrivateChatConfig? PrivateChat { get; set; }

	public FaqConfig? Faq { get; set; }

	public RelayConfig? Relay { get; set; }

	public CclConfig? Ccl { get; set; }

	public QuietHoursConfig? QuietHours { get; set; }

	public ProbationConfig? Probation { get; set; }

	public MediaModerationConfig? MediaModeration { get; set; }

	public PuzzleConfig? Puzzle { get; set; }

	public WorkspaceConfig? Workspace { get; set; }

	public ModerationReportConfig? ModerationReport { get; set; }
}
internal class ModerationReportConfig
{
	public bool Enabled { get; set; } = false;

	public int Hour { get; set; } = 8;

	public string GroupJid { get; set; } = "";
}
internal class WorkspaceConfig
{
	public string Name { get; set; } = "";

	public string Domain { get; set; } = "";

	public string Scope { get; set; } = "";
}
internal class ChannelCaps
{
	public bool CanDelete { get; init; }

	public bool SupportsImage { get; init; }

	public bool SupportsMention { get; init; }

	public bool CanReact { get; init; }

	public bool CanGetMembers { get; init; }
}
internal static class Caps
{
	private static readonly ChannelCaps Whatsapp = new ChannelCaps
	{
		CanDelete = true,
		SupportsImage = true,
		SupportsMention = true,
		CanReact = true,
		CanGetMembers = true
	};

	private static readonly ChannelCaps Email = new ChannelCaps
	{
		CanDelete = false,
		SupportsImage = true,
		SupportsMention = false,
		CanReact = false,
		CanGetMembers = false
	};

	private static readonly ChannelCaps Telegram = new ChannelCaps
	{
		CanDelete = true,
		SupportsImage = true,
		SupportsMention = true,
		CanReact = true,
		CanGetMembers = false
	};

	public static ChannelCaps Of(string? channel)
	{
		string text = (channel ?? "").ToLowerInvariant();
		if (1 == 0)
		{
		}
		ChannelCaps result = ((text == "email") ? Email : ((!(text == "telegram")) ? Whatsapp : Telegram));
		if (1 == 0)
		{
		}
		return result;
	}
}
internal class ConvContext
{
	public string ConversationId { get; init; } = "";

	public string SenderId { get; init; } = "";

	public string SenderNum { get; init; } = "";

	public string Channel { get; init; } = "whatsapp";

	public ChannelCaps Caps { get; init; } = new ChannelCaps();

	public bool IsExempt { get; init; }

	public bool QuietNow { get; init; }

	public string GroupLabel { get; init; } = "";

	public string WorkspaceName { get; init; } = "";

	public string Topic { get; init; } = "";
}
internal static class TopicStore
{
	private static readonly object _l = new object();

	private static readonly Dictionary<string, (string topic, DateTime at)> _m = new Dictionary<string, (string, DateTime)>();

	private const int TtlMinutes = 30;

	public static void Set(string convId, string topic)
	{
		if (string.IsNullOrWhiteSpace(convId) || string.IsNullOrWhiteSpace(topic))
		{
			return;
		}
		lock (_l)
		{
			_m[convId] = (topic, DateTime.UtcNow);
			if (_m.Count <= 5000)
			{
				return;
			}
			foreach (string item in (from kv in _m
				where (DateTime.UtcNow - kv.Value.at).TotalMinutes > 30.0
				select kv.Key).ToList())
			{
				_m.Remove(item);
			}
		}
	}

	public static string Get(string convId)
	{
		lock (_l)
		{
			object result;
			if (_m.TryGetValue(convId, out (string, DateTime) value) && (DateTime.UtcNow - value.Item2).TotalMinutes <= 30.0)
			{
				(result, _) = value;
			}
			else
			{
				result = "";
			}
			return (string)result;
		}
	}
}
internal enum ChatIntent
{
	Empty,
	General,
	Schedule,
	Result
}
internal enum Scenario
{
	Unmanaged,
	Cooldown,
	AdminBroadcast,
	StandingsWizard,
	EventsWizard,
	AiChat,
	Status,
	WarningsView,
	AuditView,
	TrustMember,
	Lapor,
	Puzzle,
	PuzzleSolve,
	Command,
	Faq,
	Moderation,
	Flood,
	Welcome,
	Clean
}
internal static class ChatIntents
{
	private static readonly Regex ResultRx = new Regex("juara|pemenang|peraih|siapa\\s*(yang\\s*)?menang|\\bhasil\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex ScheduleRx = new Regex("jadwal|turnamen|tanding|pertanding|kapan\\s*(main|turnamen|tanding|mulai|ada|lomba)|main\\s*kapan|hari\\s*ini|\\blomba\\b|\\bevent\\b|\\bnext\\b|akan\\s*datang", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	public static ChatIntent Classify(string question)
	{
		if (string.IsNullOrWhiteSpace(question))
		{
			return ChatIntent.Empty;
		}
		if (ResultRx.IsMatch(question))
		{
			return ChatIntent.Result;
		}
		if (ScheduleRx.IsMatch(question))
		{
			return ChatIntent.Schedule;
		}
		return ChatIntent.General;
	}
}
internal static class SendLog
{
	public static ILogger? Logger;

	public static long Sent;

	public static long Failed;
}
internal static class RetryQueue
{
	public class Item
	{
		public string Url = "";

		public string Json = "";

		public int Attempts;

		public DateTime NextTry;

		public DateTime EnqueuedAt;
	}

	private static readonly object _l = new object();

	private static readonly List<Item> _q = new List<Item>();

	private static string _path = "";

	private const int MaxAttempts = 5;

	private const int MaxAgeMinutes = 60;

	private static readonly int[] BackoffSec = new int[5] { 5, 15, 30, 60, 120 };

	public static int Count
	{
		get
		{
			lock (_l)
			{
				return _q.Count;
			}
		}
	}

	public static void Init(string path)
	{
		_path = path;
		try
		{
			if (!File.Exists(path))
			{
				return;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(path));
			DateTime dateTime = DateTime.UtcNow.AddMinutes(-60.0);
			lock (_l)
			{
				_q.Clear();
				foreach (JsonElement item in jsonDocument.RootElement.EnumerateArray())
				{
					DateTime dateTime2 = new DateTime(item.GetProperty("enq").GetInt64(), DateTimeKind.Utc);
					if (!(dateTime2 < dateTime))
					{
						_q.Add(new Item
						{
							Url = (item.GetProperty("url").GetString() ?? ""),
							Json = (item.GetProperty("json").GetString() ?? ""),
							Attempts = item.GetProperty("att").GetInt32(),
							NextTry = new DateTime(item.GetProperty("next").GetInt64(), DateTimeKind.Utc),
							EnqueuedAt = dateTime2
						});
					}
				}
			}
		}
		catch
		{
		}
	}

	private static void Save()
	{
		if (string.IsNullOrEmpty(_path))
		{
			return;
		}
		try
		{
			var value = _q.Select((Item x) => new
			{
				url = x.Url,
				json = x.Json,
				att = x.Attempts,
				next = x.NextTry.Ticks,
				enq = x.EnqueuedAt.Ticks
			}).ToArray();
			File.WriteAllText(_path, JsonSerializer.Serialize(value));
		}
		catch
		{
		}
	}

	public static void Enqueue(string url, object body)
	{
		Item item = new Item
		{
			Url = url,
			Json = JsonSerializer.Serialize(body),
			Attempts = 0,
			NextTry = DateTime.UtcNow,
			EnqueuedAt = DateTime.UtcNow
		};
		lock (_l)
		{
			if (_q.Count < 500)
			{
				_q.Add(item);
				Save();
			}
		}
	}

	public static async Task RunLoop(HttpClient http, ILogger logger)
	{
		while (true)
		{
			await Task.Delay(TimeSpan.FromSeconds(5L));
			if (Sleeper.Asleep)
			{
				continue;
			}
			List<Item> due;
			lock (_l)
			{
				due = _q.Where((Item x) => x.NextTry <= DateTime.UtcNow).ToList();
			}
			foreach (Item it in due)
			{
				bool ok = false;
				try
				{
					using StringContent c = new StringContent(it.Json, Encoding.UTF8, "application/json");
					ok = (await http.PostAsync(it.Url, c)).IsSuccessStatusCode;
				}
				catch
				{
					ok = false;
				}
				lock (_l)
				{
					if (ok)
					{
						_q.Remove(it);
						logger.LogInformation("RetryQueue: terkirim ulang OK -> {Url}", it.Url);
						continue;
					}
					it.Attempts++;
					if (it.Attempts >= 5)
					{
						_q.Remove(it);
						logger.LogWarning("RetryQueue: menyerah setelah {N}x -> {Url}", it.Attempts, it.Url);
					}
					else
					{
						it.NextTry = DateTime.UtcNow.AddSeconds(BackoffSec[Math.Min(it.Attempts, BackoffSec.Length - 1)]);
					}
				}
			}
			if (due.Count > 0)
			{
				lock (_l)
				{
					Save();
				}
			}
			due = null;
		}
	}
}
internal static class Sleeper
{
	private static string _path = "";

	public static bool Asleep { get; private set; }

	public static void Init(string path)
	{
		_path = path;
		Asleep = File.Exists(path);
	}

	public static void Set(bool asleep)
	{
		Asleep = asleep;
		try
		{
			if (asleep)
			{
				File.WriteAllText(_path, DateTime.UtcNow.ToString("o"));
			}
			else if (File.Exists(_path))
			{
				File.Delete(_path);
			}
		}
		catch
		{
		}
	}
}
internal interface IChannelAdapter
{
	string Channel { get; }

	ChannelCaps Capabilities { get; }

	Task<bool> SendText(string jid, string text, string[]? mentions = null);

	Task<bool> SendImage(string jid, string path, string caption, string[]? mentions = null);

	Task<bool> Delete(string jid, object key);

	Task<bool> React(string jid, object key, string emoji);

	Task<string[]> GetMembers(string jid);

	Task<bool> Health();
}
internal class HttpChannelAdapter : IChannelAdapter
{
	private readonly HttpClient _http;

	private readonly string _base;

	public string Channel { get; }

	public ChannelCaps Capabilities { get; }

	public HttpChannelAdapter(string channel, string baseUrl, HttpClient http)
	{
		Channel = channel;
		_base = (baseUrl ?? "").TrimEnd('/');
		_http = http;
		Capabilities = Caps.Of(channel);
	}

	private async Task<bool> Post(string path, object body)
	{
		try
		{
			using StringContent c = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
			return (await _http.PostAsync(_base + path, c)).IsSuccessStatusCode;
		}
		catch
		{
			return false;
		}
	}

	public Task<bool> SendText(string jid, string text, string[]? mentions = null)
	{
		return Post("/send", (mentions != null && mentions.Length > 0 && Capabilities.SupportsMention) ? ((object)new { jid, text, mentions }) : ((object)new { jid, text }));
	}

	public Task<bool> SendImage(string jid, string path, string caption, string[]? mentions = null)
	{
		return Capabilities.SupportsImage ? Post("/send-image", new { jid, path, caption }) : Task.FromResult(false);
	}

	public Task<bool> Delete(string jid, object key)
	{
		return Capabilities.CanDelete ? Post("/delete", new { jid, key }) : Task.FromResult(false);
	}

	public Task<bool> React(string jid, object key, string emoji)
	{
		return Capabilities.CanReact ? Post("/react", new { jid, key, emoji }) : Task.FromResult(false);
	}

	public async Task<string[]> GetMembers(string jid)
	{
		if (!Capabilities.CanGetMembers)
		{
			return Array.Empty<string>();
		}
		try
		{
			using JsonDocument doc = JsonDocument.Parse(await _http.GetStringAsync(_base + "/group-members?jid=" + Uri.EscapeDataString(jid)));
			if (doc.RootElement.TryGetProperty("members", out var m) && m.ValueKind == JsonValueKind.Array)
			{
				return (from x in m.EnumerateArray()
					select x.GetString() ?? "" into x
					where x.Length > 0
					select x).ToArray();
			}
		}
		catch
		{
		}
		return Array.Empty<string>();
	}

	public async Task<bool> Health()
	{
		try
		{
			return (await _http.GetAsync(_base + "/health")).IsSuccessStatusCode;
		}
		catch
		{
			return false;
		}
	}
}
internal static class ChannelRoute
{
	public static string Base(AppConfig cfg, string? channel)
	{
		if (!string.IsNullOrWhiteSpace(channel) && cfg.Channels != null && cfg.Channels.TryGetValue(channel, out string value) && !string.IsNullOrWhiteSpace(value))
		{
			return value.TrimEnd('/');
		}
		return cfg.GatewayUrl;
	}

	public static string OfJid(string? jid)
	{
		if (string.IsNullOrEmpty(jid))
		{
			return "whatsapp";
		}
		if (jid.EndsWith("@g.us") || jid.EndsWith("@s.whatsapp.net") || jid.EndsWith("@lid"))
		{
			return "whatsapp";
		}
		if (jid.Contains('@') && jid.Contains('.'))
		{
			return "email";
		}
		return "whatsapp";
	}

	public static string BaseForJid(AppConfig cfg, string? jid)
	{
		return Base(cfg, OfJid(jid));
	}
}
internal class PuzzleConfig
{
	public bool Enabled { get; set; } = false;

	public int DailyHour { get; set; } = 8;

	public int TimezoneOffsetHours { get; set; } = 7;

	public string GroupJid { get; set; } = "";

	public string[] GroupJids { get; set; } = Array.Empty<string>();

	public bool CommandEnabled { get; set; } = true;

	public int RevealMinutes { get; set; } = 30;

	public int SolveAfterMinutes { get; set; } = 5;

	public PuzzleDailySlot[] DailySlots { get; set; } = Array.Empty<PuzzleDailySlot>();

	public string Command { get; set; } = "puzzle";

	public string SolveCommand { get; set; } = "solusi";

	public string TryHarderMessage { get; set; } = "Semangat! Coba dulu ya, jangan menyerah. Solusinya muncul otomatis sebentar lagi. \ud83d\udcaa";
}
internal class PuzzleDailySlot
{
	public int Hour { get; set; }

	public int RevealMinutes { get; set; } = 180;

	public int MinRating { get; set; } = 0;

	public int MaxRating { get; set; } = 9999;

	public string Label { get; set; } = "Harian";

	// Filter tema Lichess (mis. "fork", "endgame", "mateIn2"); kosong = semua tema.
	public string Theme { get; set; } = "";
}
internal class PuzzleItem
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = "";

	[JsonPropertyName("fen")]
	public string Fen { get; set; } = "";

	[JsonPropertyName("side")]
	public string Side { get; set; } = "w";

	[JsonPropertyName("rating")]
	public int Rating { get; set; }

	[JsonPropertyName("themes")]
	[JsonConverter(typeof(ThemesStringConverter))]
	public string Themes { get; set; } = "";

	[JsonPropertyName("solutionSan")]
	public string[] SolutionSan { get; set; } = Array.Empty<string>();

	[JsonPropertyName("fens")]
	public string[] Fens { get; set; } = Array.Empty<string>();
}
internal class ThemesStringConverter : JsonConverter<string>
{
	public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.String)
		{
			return reader.GetString() ?? "";
		}
		if (reader.TokenType == JsonTokenType.StartArray)
		{
			List<string> items = new List<string>();
			while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
			{
				if (reader.TokenType == JsonTokenType.String)
				{
					string? item = reader.GetString();
					if (!string.IsNullOrWhiteSpace(item))
					{
						items.Add(item);
					}
				}
			}
			return string.Join(" ", items);
		}
		return "";
	}

	public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value ?? "");
	}
}
internal class ActivePuzzle
{
	public PuzzleItem Puzzle { get; set; } = new PuzzleItem();

	public DateTime RevealAt { get; set; }

	public bool Revealed { get; set; }

	public int Progress { get; set; }

	// Jumlah jawaban salah sejak puzzle ini aktif (untuk menawarkan !solusi setelah beberapa kali meleset).
	public int WrongCount { get; set; }

	// Sudah pernah menampilkan tawaran !solusi pada puzzle ini (agar tidak diulang terus = tidak cerewet).
	public bool SolveHintShown { get; set; }

	public string MsgId { get; set; } = "";

	public string Jid { get; set; } = "";

	public DateTime PostedAt { get; set; }

	public DateTime SolvedAt { get; set; }

	public List<string> SolverNums { get; set; } = new List<string>();

	public List<string> SolverJids { get; set; } = new List<string>();
}
internal static class PuzzleScoreStore
{
	public class PlayerScore
	{
		public string Name { get; set; } = "";

		public int Points { get; set; }

		public int Solves { get; set; }

		public int Moves { get; set; }

		public string LastAt { get; set; } = "";
	}

	private static readonly object _l = new object();

	private static string _path = "";

	private static Dictionary<string, Dictionary<string, PlayerScore>> _m = new Dictionary<string, Dictionary<string, PlayerScore>>();

	public static void Init(string path)
	{
		_path = path;
		try
		{
			if (File.Exists(path))
			{
				Dictionary<string, Dictionary<string, PlayerScore>> dictionary = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, PlayerScore>>>(File.ReadAllText(path));
				if (dictionary != null)
				{
					_m = dictionary;
				}
			}
		}
		catch
		{
		}
	}

	public static int Tier(int rating)
	{
		return (rating >= 2200) ? 3 : ((rating < 1600) ? 1 : 2);
	}

	public static void Award(string jid, string playerKey, string name, int points, bool solved)
	{
		if (string.IsNullOrWhiteSpace(jid) || string.IsNullOrWhiteSpace(playerKey) || points <= 0)
		{
			return;
		}
		lock (_l)
		{
			if (!_m.TryGetValue(jid, out Dictionary<string, PlayerScore> value))
			{
				value = new Dictionary<string, PlayerScore>();
				_m[jid] = value;
			}
			if (!value.TryGetValue(playerKey, out var value2))
			{
				value2 = (value[playerKey] = new PlayerScore());
			}
			if (!string.IsNullOrWhiteSpace(name))
			{
				value2.Name = name;
			}
			value2.Points += points;
			value2.Moves++;
			if (solved)
			{
				value2.Solves++;
			}
			value2.LastAt = DateTime.UtcNow.ToString("o");
			SaveNoLock();
		}
	}

	public static List<PlayerScore> Top(string jid, int n)
	{
		lock (_l)
		{
			Dictionary<string, PlayerScore> value;
			return _m.TryGetValue(jid, out value) ? (from s in value.Values
				orderby s.Points descending, s.Solves descending
				select s).Take(n).ToList() : new List<PlayerScore>();
		}
	}

	public static bool Reset(string jid)
	{
		lock (_l)
		{
			bool flag = _m.Remove(jid);
			if (flag)
			{
				SaveNoLock();
			}
			return flag;
		}
	}

	private static void SaveNoLock()
	{
		try
		{
			File.WriteAllText(_path, JsonSerializer.Serialize(_m));
		}
		catch
		{
		}
	}
}
internal static class PuzzleMove
{
	private static readonly Regex MoveLike = new Regex("^([KQRBNGM]?[a-h]?[1-8]?x?[a-h][1-8](=[QRBNMG])?|O-O(-O)?|0-0(-0)?)[+#]?[!?]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Dictionary<char, char> IndoToEng = new Dictionary<char, char>
	{
		['R'] = 'K',
		['M'] = 'Q',
		['B'] = 'R',
		['G'] = 'B',
		['K'] = 'N'
	};

	private static readonly Regex MoveNumRx = new Regex("\\b\\d+\\.(\\.\\.)?", RegexOptions.CultureInvariant);

	private static readonly Regex SanRx = new Regex("(O-O-O|O-O|0-0-0|0-0|[KQRBNGM][a-h]?[1-8]?x?[a-h][1-8](=[QRBNMG])?[+#]?|[a-h]x[a-h][1-8](=[QRBNMG])?[+#]?|\\b[a-h][1-8](=[QRBNMG])?[+#]?)", RegexOptions.CultureInvariant);

	private static readonly Regex LeadMoveNumRx = new Regex("^\\s*\\d{1,3}\\.(\\.\\.)?\\s*[?!]*\\s*", RegexOptions.CultureInvariant);

	// Buang awalan nomor langkah + tanda anotasi: "1.", "1...", "1...?", "12. " -> sisakan langkahnya saja.
	// Sengaja hanya strip AWALAN (kalimat chat tak diawali nomor langkah, jadi tetap dianggap chat).
	public static string StripMoveNumber(string s)
	{
		return LeadMoveNumRx.Replace((s ?? "").Trim(), "").Trim();
	}

	public static bool IsMoveLike(string s)
	{
		return !string.IsNullOrWhiteSpace(s) && s.Trim().Length <= 8 && MoveLike.IsMatch(s.Trim());
	}

	private static string Norm(string s)
	{
		return new string(s.Where((char c) => !"xX+#!? ".Contains(c)).ToArray()).Replace('0', 'O').ToLowerInvariant();
	}

	private static string IndoToEnglish(string s)
	{
		s = s.Trim();
		if (s.Length == 0)
		{
			return s;
		}
		char key = char.ToUpperInvariant(s[0]);
		char value;
		return IndoToEng.TryGetValue(key, out value) ? (value + s.Substring(1)) : s;
	}

	public static bool Matches(string attempt, string solution)
	{
		string text = Norm(solution);
		if (Norm(attempt) == text)
		{
			return true;
		}
		return Norm(IndoToEnglish(attempt)) == text;
	}

	// Petunjuk LOKAL singkat untuk jawaban salah (TANPA AI, tanpa membocorkan langkah).
	// Arahkan ke langkah forcing berdasar TIPE langkah solusi berikutnya: skak / tangkapan / umum.
	// 'variant' memutar pilihan kata agar tidak terdengar robotik.
	public static string LocalWrongHint(string nextSolutionSan, int variant)
	{
		if (variant < 0)
		{
			variant = -variant;
		}
		string s = (nextSolutionSan ?? "").Trim();
		bool isCheck = s.EndsWith("+") || s.EndsWith("#");
		bool isCapture = s.IndexOf('x') >= 0 || s.IndexOf('X') >= 0;
		if (isCheck)
		{
			string[] v = new string[3]
			{
				"Cari langkah yang memberi skak — itu paling memaksa.",
				"Ada skak kuat di posisi ini. Pilih langkah yang membuat raja lawan harus bergerak.",
				"Mulai dari langkah pemberi skak; lawan jadi tak punya banyak pilihan."
			};
			return v[variant % v.Length];
		}
		if (isCapture)
		{
			string[] v2 = new string[3]
			{
				"Ada tangkapan kuat di sini. Lihat bidak lawan yang bisa diambil.",
				"Coba cari tangkapan dulu — mungkin ada bidak lawan yang tak terjaga.",
				"Perhatikan bidak lawan yang menggantung; tangkapan bisa jadi kuncinya."
			};
			return v2[variant % v2.Length];
		}
		string[] g = new string[3]
		{
			"Cari langkah forcing dulu: skak, tangkapan, atau ancaman langsung.",
			"Mulai dari langkah memaksa — skak, tangkapan, atau ancaman mat.",
			"Langkah terbaik biasanya memaksa. Cek dulu: skak, tangkapan, atau ancaman."
		};
		return g[variant % g.Length];
	}

	// Nama tampilan ramah (mis. "Ade") dari PushName WhatsApp: kata pertama yang mengandung huruf.
	// Kalau kosong/angka saja (mis. nomor), kembalikan "" supaya pemanggil menyapa tanpa ID mentah.
	public static string FriendlyName(string pushName)
	{
		if (string.IsNullOrWhiteSpace(pushName))
		{
			return "";
		}
		string[] parts = pushName.Trim().Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		string first = (parts.Length > 0) ? parts[0] : "";
		if (first.Length == 0 || !first.Any(char.IsLetter))
		{
			return "";
		}
		return (first.Length > 20) ? first.Substring(0, 20) : first;
	}

	static string UpperFirst(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return s;
		}
		return char.ToUpperInvariant(s[0]) + s.Substring(1);
	}

	// Fallback cocok-langkah berbasis POSISI (Gera.Chess): mainkan solusi -> FEN target,
	// mainkan langkah pemain (interpretasi Inggris & Indonesia, huruf bidak besar) -> bandingkan FEN.
	// Menangani disambiguasi (Nbd2 vs Nd2), notasi Indonesia, dan variasi +/#/x yang string-match lewatkan.
	public static bool MatchesByPosition(string fen, string attempt, string solutionSan)
	{
		if (string.IsNullOrWhiteSpace(fen) || string.IsNullOrWhiteSpace(attempt) || string.IsNullOrWhiteSpace(solutionSan))
		{
			return false;
		}
		try
		{
			Chess.ChessBoard solBoard = Chess.ChessBoard.LoadFromFen(fen);
			if (!solBoard.Move(solutionSan))
			{
				return false;
			}
			string solFen = solBoard.ToFen();
			string eng = IndoToEnglish(attempt);
			string[] cands = new string[4] { attempt, eng, UpperFirst(attempt), UpperFirst(eng) };
			foreach (string c in cands)
			{
				if (string.IsNullOrWhiteSpace(c))
				{
					continue;
				}
				try
				{
					Chess.ChessBoard b = Chess.ChessBoard.LoadFromFen(fen);
					if (b.Move(c) && b.ToFen() == solFen)
					{
						return true;
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
		return false;
	}

	// Argumen kesulitan on-demand: "!puzzle mudah|sedang|sulit" -> slot dengan rentang rating.
	// null kalau tak ada argumen / tak dikenali (perilaku acak lama dipertahankan).
	public static PuzzleDailySlot DifficultySlot(string cmdText, int revealMinutes)
	{
		if (string.IsNullOrWhiteSpace(cmdText))
		{
			return null;
		}
		string ct = cmdText.TrimStart('!', ' ').Trim();
		int sp = ct.IndexOf(' ');
		if (sp < 0)
		{
			return null;
		}
		string arg = ct.Substring(sp + 1).Trim().ToLowerInvariant();
		int min;
		int max;
		string label;
		if (arg.StartsWith("mudah") || arg.StartsWith("easy") || arg.StartsWith("gampang"))
		{
			min = 0;
			max = 1400;
			label = "Mudah";
		}
		else if (arg.StartsWith("seda") || arg.StartsWith("menengah") || arg.StartsWith("medium"))
		{
			min = 1400;
			max = 2000;
			label = "Menengah";
		}
		else if (arg.StartsWith("sulit") || arg.StartsWith("susah") || arg.StartsWith("sukar") || arg.StartsWith("hard"))
		{
			min = 2000;
			max = 9999;
			label = "Sulit";
		}
		else
		{
			string th = MapTheme(arg);
			if (th.Length == 0)
			{
				return null;
			}
			return new PuzzleDailySlot
			{
				Label = char.ToUpperInvariant(arg[0]) + arg.Substring(1),
				Theme = th,
				RevealMinutes = revealMinutes
			};
		}
		return new PuzzleDailySlot
		{
			Label = label,
			MinRating = min,
			MaxRating = max,
			RevealMinutes = revealMinutes
		};
	}

	// Petakan kata tema (Indonesia/Inggris) -> nama tema Lichess. "" kalau tak dikenal.
	static string MapTheme(string arg)
	{
		switch (arg)
		{
		case "fork": case "garpu": return "fork";
		case "pin": case "ikat": case "ikatan": return "pin";
		case "skewer": case "tusuk": case "tusukan": return "skewer";
		case "endgame": case "akhir": case "endgames": return "endgame";
		case "opening": case "pembukaan": return "opening";
		case "middlegame": case "tengah": return "middlegame";
		case "mate": case "skakmat": case "mat": case "matt": case "mati": return "mate";
		case "matein1": case "mate1": return "mateIn1";
		case "matein2": case "mate2": return "mateIn2";
		case "matein3": case "mate3": return "mateIn3";
		case "sacrifice": case "korban": case "sac": case "pengorbanan": return "sacrifice";
		case "promotion": case "promosi": return "promotion";
		case "discovered": case "discoveredattack": case "buka": return "discoveredAttack";
		case "backrank": case "backrankmate": return "backRankMate";
		case "hanging": case "hangingpiece": case "menggantung": return "hangingPiece";
		case "deflection": case "deflesi": return "deflection";
		case "trapped": case "trappedpiece": case "terperangkap": return "trappedPiece";
		case "zugzwang": return "zugzwang";
		case "advantage": case "unggul": return "advantage";
		case "crushing": case "telak": return "crushing";
		default: return "";
		}
	}

	// Catatan tema taktik (Bahasa Indonesia) untuk pesan puzzle SELESAI. Maks 3 tema bermakna.
	static readonly Dictionary<string, string> ThemeId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		{ "fork", "garpu" }, { "pin", "ikatan" }, { "skewer", "tusukan" }, { "endgame", "akhir permainan" },
		{ "opening", "pembukaan" }, { "middlegame", "tengah permainan" }, { "sacrifice", "pengorbanan" },
		{ "mate", "skakmat" }, { "mateIn1", "skakmat 1 langkah" }, { "mateIn2", "skakmat 2 langkah" },
		{ "mateIn3", "skakmat 3 langkah" }, { "promotion", "promosi" }, { "discoveredAttack", "serangan terbuka" },
		{ "backRankMate", "skakmat baris belakang" }, { "hangingPiece", "bidak menggantung" }, { "deflection", "pengalihan" },
		{ "trappedPiece", "bidak terperangkap" }, { "zugzwang", "zugzwang" }, { "doubleCheck", "skak ganda" },
		{ "attraction", "pemancingan" }, { "clearance", "pembersihan jalur" }, { "quietMove", "langkah tenang" },
		{ "defensiveMove", "langkah bertahan" }, { "interference", "interferensi" }, { "intermezzo", "langkah antara" }
	};

	public static string ThemeNote(string themes)
	{
		if (string.IsNullOrWhiteSpace(themes))
		{
			return "";
		}
		List<string> picked = new List<string>();
		foreach (string raw in themes.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
		{
			if (ThemeId.TryGetValue(raw, out string id) && !picked.Contains(id))
			{
				picked.Add(id);
				if (picked.Count >= 3)
				{
					break;
				}
			}
		}
		return (picked.Count == 0) ? "" : ("\n\U0001F4A1 Tema: " + string.Join(", ", picked) + ".");
	}

	public static string StripNotation(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return s;
		}
		s = MoveNumRx.Replace(s, "");
		s = SanRx.Replace(s, "…");
		s = Regex.Replace(s, "\\u2026[\\s,.\\-]*(\\u2026[\\s,.\\-]*)+", "… ");
		s = Regex.Replace(s, "\\s{2,}", " ").Trim();
		return s;
	}

	public static string RedactSolutionMoves(string s, IEnumerable<string> solutionMoves)
	{
		if (string.IsNullOrWhiteSpace(s))
		{
			return s;
		}
		foreach (string item in solutionMoves.Where((string m) => !string.IsNullOrWhiteSpace(m)).Distinct<string>(StringComparer.OrdinalIgnoreCase))
		{
			string text = Regex.Escape(item.Trim());
			s = Regex.Replace(s, "(?<!\\w)" + text + "(?!\\w)", "langkah solusi", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
			string text2 = Regex.Escape(item.Trim().TrimEnd('+', '#', '!', '?'));
			if (text2.Length > 0)
			{
				s = Regex.Replace(s, "(?<!\\w)" + text2 + "[+#]?[!?]*(?!\\w)", "langkah solusi", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
			}
		}
		return Regex.Replace(s, "\\s{2,}", " ").Trim();
	}

	public static string HumanizeWrongExplanation(string s)
	{
		if (string.IsNullOrWhiteSpace(s))
		{
			return "";
		}
		string[] aiPhrases = new string[10] { "aku belum yakin", "belum yakin", "refutasi konkret", "refutasi", "tidak mau asal menebak", "asal menebak", "varian konkretnya", "varian konkret", "konkretnya", "secara umum tanpa mengarang" };
		List<string> list = (from x in Regex.Split(s.Trim(), "(?<=[.!?])\\s+")
			select x.Trim() into x
			where x.Length > 0
			where !aiPhrases.Any((string p) => x.Contains(p, StringComparison.OrdinalIgnoreCase))
			select x).Take(2).ToList();
		if (list.Count == 0)
		{
			return "";
		}
		string input = string.Join(" ", list);
		input = Regex.Replace(input, "\\bkurang tepat\\b", "kurang pas", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		input = Regex.Replace(input, "\\bkonsekuensi konkret\\b", "akibatnya", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		// Normalisasi istilah catur: pakai istilah Indonesia konsisten, hindari campur Inggris.
		input = Regex.Replace(input, "\\bbidak\\s+rook\\b", "benteng", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		input = Regex.Replace(input, "\\brook\\b", "benteng", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		input = Regex.Replace(input, "\\bbidak\\s+piece\\b", "bidak", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		input = Regex.Replace(input, "\\bpiece\\b", "bidak", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		input = Regex.Replace(input, "\\bsafety\\b", "aman", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		input = Regex.Replace(input, "\\btemporimu\\b", "temponya", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		input = Regex.Replace(input, "\\btempo\\s+mu\\b", "temponya", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		return Regex.Replace(input, "\\s{2,}", " ").Trim();
	}

	public static string CleanWrongExplanation(string s, IEnumerable<string> solutionMoves)
	{
		s = RedactSolutionMoves(s, solutionMoves);
		if (string.IsNullOrWhiteSpace(s))
		{
			return "";
		}
		string[] hinty = new string[14]
		{
			"coba cari", "carilah", "sebaiknya", "langkah terbaik", "kandidat", "petunjuk", "hint", "jawaban yang benar", "solusinya", "mainkan",
			"temukan", "arahnya adalah", "yang tepat adalah", "harusnya"
		};
		List<string> list = (from x in Regex.Split(s, "(?<=[.!?])\\s+")
			select x.Trim() into x
			where x.Length > 0
			where !hinty.Any((string h) => x.Contains(h, StringComparison.OrdinalIgnoreCase))
			where !x.Contains("langkah solusi", StringComparison.OrdinalIgnoreCase)
			select x).Take(2).ToList();
		return (list.Count == 0) ? "" : string.Join(" ", list);
	}
}
internal class ProbationConfig
{
	public bool Enabled { get; set; } = false;

	public int Minutes { get; set; } = 1440;

	public bool BlockLinks { get; set; } = true;

	public bool BlockMedia { get; set; } = true;

	public bool BlockForwardedOnly { get; set; } = false;

	public string Message { get; set; } = "@user, untuk anggota baru, link/gambar saya tahan sementara agar grup tetap aman dari spam. Setelah masa awal lewat, akses akan terbuka otomatis.";
}
internal class MediaModerationConfig
{
	public bool BlockForwardedMedia { get; set; } = false;

	public int ForwardScoreThreshold { get; set; } = 4;

	public string Message { get; set; } = "@user, media yang sering diteruskan saya rapikan dulu untuk menjaga grup dari spam. Kalau ini materi yang relevan, silakan kirim ulang tanpa forward ya.";
}
internal class QuietHoursConfig
{
	public bool Enabled { get; set; } = false;

	public int StartHour { get; set; } = 23;

	public int EndHour { get; set; } = 6;

	public int TimezoneOffsetHours { get; set; } = 7;

	public bool SuppressReminders { get; set; } = true;

	public string Notice { get; set; } = "";
}
internal class RelayConfig
{
	public bool Enabled { get; set; } = false;

	public string HubGroupJid { get; set; } = "";

	public string Command { get; set; } = "sebar";

	public string[] TargetGroups { get; set; } = Array.Empty<string>();

	public int ThrottleSeconds { get; set; } = 4;

	public string Footer { get; set; } = "";
}
internal static class ConvMemory
{
	private static readonly object _l = new object();

	private static readonly Dictionary<string, List<(string role, string text, DateTime at)>> _m = new Dictionary<string, List<(string, string, DateTime)>>();

	private static string _path = "";

	private const int MaxTurns = 6;

	private const int TtlMinutes = 30;

	private const int MaxLen = 400;

	public static void Init(string path)
	{
		_path = path;
		try
		{
			if (!File.Exists(path))
			{
				return;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(path));
			DateTime dateTime = DateTime.UtcNow.AddMinutes(-30.0);
			lock (_l)
			{
				_m.Clear();
				foreach (JsonProperty item in jsonDocument.RootElement.EnumerateObject())
				{
					List<(string, string, DateTime)> list = new List<(string, string, DateTime)>();
					foreach (JsonElement item2 in item.Value.EnumerateArray())
					{
						DateTime dateTime2 = new DateTime(item2.GetProperty("at").GetInt64(), DateTimeKind.Utc);
						if (!(dateTime2 < dateTime))
						{
							list.Add((item2.GetProperty("role").GetString() ?? "user", item2.GetProperty("text").GetString() ?? "", dateTime2));
						}
					}
					if (list.Count > 0)
					{
						_m[item.Name] = list;
					}
				}
			}
		}
		catch
		{
		}
	}

	private static void Save()
	{
		if (string.IsNullOrEmpty(_path))
		{
			return;
		}
		try
		{
			var value = Enumerable.ToDictionary(_m, (KeyValuePair<string, List<(string role, string text, DateTime at)>> kv) => kv.Key, (KeyValuePair<string, List<(string role, string text, DateTime at)>> kv) => kv.Value.Select(((string role, string text, DateTime at) t) => new
			{
				role = t.role,
				text = t.text,
				at = t.at.Ticks
			}).ToArray());
			File.WriteAllText(_path, JsonSerializer.Serialize(value));
		}
		catch
		{
		}
	}

	public static void Append(string key, string role, string text)
	{
		if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		text = text.Trim();
		if (text.Length > 400)
		{
			text = text.Substring(0, 400) + "…";
		}
		lock (_l)
		{
			if (!_m.TryGetValue(key, out List<(string, string, DateTime)> value))
			{
				value = new List<(string, string, DateTime)>();
				_m[key] = value;
			}
			value.Add((role, text, DateTime.UtcNow));
			if (value.Count > 6)
			{
				value.RemoveRange(0, value.Count - 6);
			}
			Save();
		}
	}

	public static string Recent(string key)
	{
		lock (_l)
		{
			if (!_m.TryGetValue(key, out List<(string, string, DateTime)> value))
			{
				return "";
			}
			DateTime cutoff = DateTime.UtcNow.AddMinutes(-30.0);
			value.RemoveAll(((string role, string text, DateTime at) t) => t.at < cutoff);
			if (value.Count == 0)
			{
				_m.Remove(key);
				return "";
			}
			StringBuilder stringBuilder = new StringBuilder();
			foreach (var item in value)
			{
				stringBuilder.AppendLine(((item.Item1 == "user") ? "User: " : "Asisten: ") + item.Item2);
			}
			return stringBuilder.ToString().TrimEnd();
		}
	}
}
internal class SessionStore
{
	public readonly Dictionary<string, BroadcastSession> Broadcast = new Dictionary<string, BroadcastSession>();

	public readonly object BroadcastLock = new object();

	public readonly Dictionary<string, StandingsSession> Standings = new Dictionary<string, StandingsSession>();

	public readonly object StandingsLock = new object();

	public readonly Dictionary<string, CclSession> Ccl = new Dictionary<string, CclSession>();

	public readonly object CclLock = new object();
}
internal class BroadcastSession
{
	public string Stage { get; set; } = "text";

	public string Text { get; set; } = "";

	public List<GroupOption> Options { get; set; } = new List<GroupOption>();

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
internal class GroupOption
{
	public string Jid { get; set; } = "";

	public string Subject { get; set; } = "";
}
internal class StandingsSession
{
	public List<(string url, string swiss, string name, string date)> Options { get; set; } = new List<(string, string, string, string)>();

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
internal class AiConfig
{
	public bool Enabled { get; set; } = false;

	public string Provider { get; set; } = "ollama";

	public string Url { get; set; } = "http://localhost:11434";

	public string Model { get; set; } = "qwen2.5:7b";

	public string SystemPrompt { get; set; } = "";

	public bool RequireMention { get; set; } = true;

	public string[] Commands { get; set; } = new string[2] { "tanya", "ai" };

	public int MaxOutputChars { get; set; } = 1200;

	public int NumPredict { get; set; } = 400;

	public int TimeoutSeconds { get; set; } = 60;

	public string KeepAlive { get; set; } = "30m";

	public double Temperature { get; set; } = 0.4;

	public double TopP { get; set; } = 0.9;

	public double RepeatPenalty { get; set; } = 1.15;
}
internal class FaqConfig
{
	public bool Enabled { get; set; } = false;

	public bool RequireMention { get; set; } = false;

	public FaqEntry[] Entries { get; set; } = Array.Empty<FaqEntry>();
}
internal class FaqEntry
{
	public string Id { get; set; } = "";

	public string Pattern { get; set; } = "";

	public string Reply { get; set; } = "";
}
internal class NaturalTriggersConfig
{
	public bool Enabled { get; set; } = true;

	public bool RequireMention { get; set; } = true;

	public NaturalTriggerItem[] Map { get; set; } = Array.Empty<NaturalTriggerItem>();
}
internal class NaturalTriggerItem
{
	public string Command { get; set; } = "";

	public string[] Phrases { get; set; } = Array.Empty<string>();
}
internal class PrivateChatConfig
{
	public bool Enabled { get; set; } = false;

	public string Persona { get; set; } = "";

	public string[] AllowedNumbers { get; set; } = Array.Empty<string>();

	public string[] ConsoleGroupJids { get; set; } = Array.Empty<string>();
}
internal static class PrivateChatAccess
{
	public static bool IsAllowed(AppConfig config, PrivateChatConfig pc, string senderNum, string senderPhone)
	{
		string[] array = pc.AllowedNumbers ?? Array.Empty<string>();
		if (array.Length == 0)
		{
			return AdminSync.IsAllowed(config, senderNum, senderPhone);
		}
		return array.Any(delegate(string a)
		{
			string text = NumberUtil.Normalize(a);
			return text.Length > 0 && (text == senderNum || (senderPhone.Length > 0 && text == senderPhone));
		});
	}
}
internal static class NaturalIntent
{
	public static string? Detect(AppConfig config, string text, bool mentioned)
	{
		NaturalTriggersConfig naturalTriggers = config.NaturalTriggers;
		if (naturalTriggers == null || !naturalTriggers.Enabled || naturalTriggers.Map.Length == 0)
		{
			return null;
		}
		if (naturalTriggers.RequireMention && !mentioned)
		{
			return null;
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		string text2 = text.ToLowerInvariant();
		NaturalTriggerItem[] map = naturalTriggers.Map;
		foreach (NaturalTriggerItem naturalTriggerItem in map)
		{
			if (string.IsNullOrWhiteSpace(naturalTriggerItem.Command) || naturalTriggerItem.Phrases.Length == 0)
			{
				continue;
			}
			string[] phrases = naturalTriggerItem.Phrases;
			foreach (string text3 in phrases)
			{
				if (!string.IsNullOrWhiteSpace(text3) && text2.Contains(text3.ToLowerInvariant()))
				{
					return naturalTriggerItem.Command.Trim();
				}
			}
		}
		return null;
	}
}
internal class AnnouncerConfig
{
	public bool Enabled { get; set; } = false;

	public string TeamId { get; set; } = "";

	public string GroupJid { get; set; } = "";

	public string[] GroupJids { get; set; } = Array.Empty<string>();

	public int[] RemindersMinutes { get; set; } = new int[2] { 300, 15 };

	public int PollMinutes { get; set; } = 5;

	public int TimezoneOffsetHours { get; set; } = 7;

	public string NameFilter { get; set; } = "";

	public bool ResultsEnabled { get; set; } = false;

	public string ResultsGroupJid { get; set; } = "";

	public string[] ResultsGroupJids { get; set; } = Array.Empty<string>();

	public int ResultsMaxAgeHours { get; set; } = 12;
}
internal class GroupConfig
{
	public string? Label { get; set; }

	public bool? ModerationEnabled { get; set; }

	public bool? FloodEnabled { get; set; }

	public bool? WelcomeEnabled { get; set; }

	public bool? CommandsEnabled { get; set; }

	public string? WelcomeMessage { get; set; }

	public string? RulesText { get; set; }

	public string[]? DisabledRules { get; set; }

	public string[]? EnabledRules { get; set; }

	public string[]? ExemptNumbers { get; set; }

	public string? EventsHint { get; set; }

	public QuietHoursConfig? QuietHours { get; set; }

	public int? CommandCooldownSeconds { get; set; }

	public int? PuzzleRevealMinutes { get; set; }

	public int? PuzzleSolveAfterMinutes { get; set; }

	public bool? PuzzleCommandEnabled { get; set; }
}
internal class DataCommand
{
	public string Cmd { get; set; } = "";

	public string Sp { get; set; } = "";

	public string Param { get; set; } = "TournamentID";

	public string Title { get; set; } = "";
}
internal static class Db
{
	public static async Task<List<Dictionary<string, object?>>> QueryStoredProc(string connectionString, string sp, string paramName, int id)
	{
		List<Dictionary<string, object?>> rows = new List<Dictionary<string, object>>();
		using SqlConnection conn = new SqlConnection(connectionString);
		await conn.OpenAsync();
		using SqlCommand cmd = new SqlCommand(sp, conn)
		{
			CommandType = CommandType.StoredProcedure
		};
		cmd.Parameters.Add(new SqlParameter("@" + paramName, SqlDbType.Int)
		{
			Value = id
		});
		using SqlDataReader reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
		{
			Dictionary<string, object?> row = new Dictionary<string, object>();
			for (int i = 0; i < reader.FieldCount; i++)
			{
				row[reader.GetName(i)] = (reader.IsDBNull(i) ? null : reader.GetValue(i));
			}
			rows.Add(row);
		}
		return rows;
	}
}
internal class FloodTracker
{
	private readonly int _max;

	private readonly int _windowSec;

	private readonly int _cooldownSec;

	private readonly object _sync = new object();

	private readonly Dictionary<string, List<DateTime>> _times = new Dictionary<string, List<DateTime>>();

	private readonly Dictionary<string, DateTime> _lastWarn = new Dictionary<string, DateTime>();

	public FloodTracker(int max, int windowSec, int cooldownSec)
	{
		_max = max;
		_windowSec = windowSec;
		_cooldownSec = cooldownSec;
	}

	public (bool flood, bool warn) Check(string participant)
	{
		lock (_sync)
		{
			DateTime now = DateTime.UtcNow;
			if (!_times.TryGetValue(participant, out List<DateTime> value))
			{
				value = new List<DateTime>();
				_times[participant] = value;
			}
			value.Add(now);
			value.RemoveAll((DateTime t) => (now - t).TotalSeconds > (double)_windowSec);
			if (_times.Count > 10000)
			{
				foreach (string item2 in (from kv in _times.Where<KeyValuePair<string, List<DateTime>>>(delegate(KeyValuePair<string, List<DateTime>> kv)
					{
						int result;
						if (kv.Key != participant)
						{
							if (kv.Value.Count != 0)
							{
								DateTime dateTime = now;
								List<DateTime> value3 = kv.Value;
								result = (((dateTime - value3[value3.Count - 1]).TotalSeconds > (double)_windowSec) ? 1 : 0);
							}
							else
							{
								result = 1;
							}
						}
						else
						{
							result = 0;
						}
						return (byte)result != 0;
					})
					select kv.Key).ToList())
				{
					_times.Remove(item2);
				}
				foreach (string item3 in (from kv in _lastWarn
					where (now - kv.Value).TotalSeconds > (double)_cooldownSec
					select kv.Key).ToList())
				{
					_lastWarn.Remove(item3);
				}
			}
			bool flag = value.Count > _max;
			bool item = false;
			if (flag)
			{
				_lastWarn.TryGetValue(participant, out var value2);
				if ((now - value2).TotalSeconds >= (double)_cooldownSec)
				{
					item = true;
					_lastWarn[participant] = now;
				}
			}
			return (flood: flag, warn: item);
		}
	}
}
internal static class TagAliasStore
{
	private static string _path = "";

	private static Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private static readonly object _lock = new object();

	public static void Init(string path)
	{
		_path = path;
		try
		{
			if (File.Exists(path))
			{
				Dictionary<string, string>? d = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
				if (d != null)
				{
					_map = new Dictionary<string, string>(d, StringComparer.OrdinalIgnoreCase);
				}
			}
		}
		catch
		{
		}
	}

	public static string? Get(string alias)
	{
		lock (_lock)
		{
			return _map.TryGetValue((alias ?? "").Trim(), out string? v) ? v : null;
		}
	}

	public static void Set(string alias, string name)
	{
		lock (_lock)
		{
			_map[(alias ?? "").Trim()] = name;
			Save();
		}
	}

	public static bool Remove(string alias)
	{
		lock (_lock)
		{
			bool r = _map.Remove((alias ?? "").Trim());
			if (r)
			{
				Save();
			}
			return r;
		}
	}

	public static List<KeyValuePair<string, string>> All()
	{
		lock (_lock)
		{
			return _map.OrderBy((KeyValuePair<string, string> kv) => kv.Key).ToList();
		}
	}

	private static void Save()
	{
		try
		{
			File.WriteAllText(_path, JsonSerializer.Serialize(_map, new JsonSerializerOptions { WriteIndented = true }));
		}
		catch
		{
		}
	}
}
internal static class AliasStore
{
	private static string _path = "";

	private static Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private static readonly object _lock = new object();

	public static void Init(string path)
	{
		_path = path;
		try
		{
			if (File.Exists(path))
			{
				Dictionary<string, string>? d = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
				if (d != null)
				{
					_map = new Dictionary<string, string>(d, StringComparer.OrdinalIgnoreCase);
				}
			}
		}
		catch
		{
		}
	}

	public static string? Get(string alias)
	{
		lock (_lock)
		{
			return _map.TryGetValue((alias ?? "").Trim(), out string? v) ? v : null;
		}
	}

	public static void Set(string alias, string name)
	{
		lock (_lock)
		{
			_map[(alias ?? "").Trim()] = name;
			Save();
		}
	}

	public static bool Remove(string alias)
	{
		lock (_lock)
		{
			bool r = _map.Remove((alias ?? "").Trim());
			if (r)
			{
				Save();
			}
			return r;
		}
	}

	public static List<KeyValuePair<string, string>> All()
	{
		lock (_lock)
		{
			return _map.OrderBy((KeyValuePair<string, string> kv) => kv.Key).ToList();
		}
	}

	private static void Save()
	{
		try
		{
			File.WriteAllText(_path, JsonSerializer.Serialize(_map, new JsonSerializerOptions { WriteIndented = true }));
		}
		catch
		{
		}
	}
}
internal static class AdminSync
{
	public static volatile HashSet<string> Numbers = new HashSet<string>();

	public static bool IsAllowed(AppConfig config, string senderNum, string senderPhone = "")
	{
		string[] array = config.AdminNumbers ?? Array.Empty<string>();
		HashSet<string> numbers = Numbers;
		if (array.Length == 0 && numbers.Count == 0)
		{
			return false;
		}
		if (array.Any(delegate(string a)
		{
			string text = NumberUtil.Normalize(a);
			return text == senderNum || (senderPhone.Length > 0 && text == senderPhone);
		}))
		{
			return true;
		}
		return numbers.Contains(senderNum) || (senderPhone.Length > 0 && numbers.Contains(senderPhone));
	}

	public static string[] Effective(AppConfig config)
	{
		HashSet<string> hashSet = new HashSet<string>(Numbers);
		string[] array = config.AdminNumbers ?? Array.Empty<string>();
		foreach (string s in array)
		{
			string text = NumberUtil.Normalize(s);
			if (text.Length > 0)
			{
				hashSet.Add(text);
			}
		}
		return hashSet.ToArray();
	}

	public static async Task RunLoop(Func<AppConfig> getConfig, HttpClient http, ILogger logger)
	{
		while (true)
		{
			int mins = 30;
			try
			{
				AppConfig cfg = getConfig();
				mins = ((cfg.AdminSyncMinutes < 1) ? 30 : cfg.AdminSyncMinutes);
				if (!string.IsNullOrWhiteSpace(cfg.AdminSyncGroupJid))
				{
					string url = cfg.GatewayUrl + "/group-members?jid=" + Uri.EscapeDataString(cfg.AdminSyncGroupJid);
					using HttpResponseMessage resp = await http.GetAsync(url);
					if (resp.IsSuccessStatusCode)
					{
						JsonElement ok;
						JsonElement ms;
						using (JsonDocument doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
						{
							JsonElement root = doc.RootElement;
							if (root.TryGetProperty("ok", out ok) && ok.ValueKind == JsonValueKind.True && root.TryGetProperty("members", out ms) && ms.ValueKind == JsonValueKind.Array)
							{
								HashSet<string> set = new HashSet<string>();
								foreach (JsonElement m in ms.EnumerateArray())
								{
									if (m.TryGetProperty("number", out var nu))
									{
										string n = nu.GetString() ?? "";
										if (n.Length > 0)
										{
											set.Add(n);
										}
									}
									if (m.TryGetProperty("phone", out var ph))
									{
										string p = ph.GetString() ?? "";
										if (p.Length > 0)
										{
											set.Add(p);
										}
									}
									nu = default(JsonElement);
									ph = default(JsonElement);
								}
								if (set.Count > 0)
								{
									Numbers = set;
									logger.LogInformation("Admin sync: {N} id admin dari grup", set.Count);
								}
							}
						}
						ok = default(JsonElement);
						ms = default(JsonElement);
					}
				}
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				logger.LogError("Admin sync error: {Msg}", ex2.Message);
			}
			await Task.Delay(TimeSpan.FromMinutes(mins));
		}
	}
}
internal static class QuietHours
{
	public static bool IsActive(QuietHoursConfig? q, DateTimeOffset utcNow)
	{
		if (q == null || !q.Enabled)
		{
			return false;
		}
		int num = (q.StartHour % 24 + 24) % 24;
		int num2 = (q.EndHour % 24 + 24) % 24;
		if (num == num2)
		{
			return false;
		}
		int hour = utcNow.UtcDateTime.AddHours(q.TimezoneOffsetHours).Hour;
		return (num >= num2) ? (hour >= num || hour < num2) : (hour >= num && hour < num2);
	}
}
internal class CooldownTracker
{
	private readonly object _sync = new object();

	private readonly Dictionary<string, DateTime> _last = new Dictionary<string, DateTime>();

	public bool Allow(string key, int seconds)
	{
		if (seconds <= 0)
		{
			return true;
		}
		lock (_sync)
		{
			DateTime now = DateTime.UtcNow;
			if (_last.TryGetValue(key, out var value) && (now - value).TotalSeconds < (double)seconds)
			{
				return false;
			}
			_last[key] = now;
			if (_last.Count > 10000)
			{
				foreach (string item in (from kv in _last
					where (now - kv.Value).TotalHours > 24.0
					select kv.Key).ToList())
				{
					_last.Remove(item);
				}
			}
			return true;
		}
	}
}
internal class Rule
{
	public string Id { get; set; } = "";

	public string Name { get; set; } = "";

	public string? Reason { get; set; }

	public bool Enabled { get; set; } = true;

	public bool Shadow { get; set; } = false;

	public string Flags { get; set; } = "i";

	public string Pattern { get; set; } = "";

	public Regex Compiled { get; set; } = new Regex("(?!)");
}
internal static class NumberUtil
{
	public static string Normalize(string? s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		int num = s.IndexOf('@');
		string source = ((num >= 0) ? s.Substring(0, num) : s);
		return new string(source.Where(char.IsDigit).ToArray());
	}
}
internal class AuditLog
{
	private readonly string _path;

	private readonly object _sync = new object();

	public AuditLog(string path)
	{
		_path = path;
	}

	public void Write(string jid, string participant, string pushName, string rule, int count, string text)
	{
		string text2 = text.Replace("\r", " ").Replace("\n", " ");
		if (text2.Length > 200)
		{
			text2 = text2.Substring(0, 200) + "…";
		}
		string value = NumberUtil.Normalize(participant);
		string text3 = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | HAPUS | grup={jid} | dari={value} ({pushName}) | aturan={rule} | peringatan_ke={count} | teks=\"{text2}\"";
		lock (_sync)
		{
			RotateIfBig();
			File.AppendAllText(_path, text3 + Environment.NewLine, Encoding.UTF8);
		}
	}


	public void WriteAdminDm(string admin, string action, string target, string result, string text)
	{
		string clean = (text ?? "").Replace("\r", " ").Replace("\n", " ");
		if (clean.Length > 200)
		{
			clean = clean.Substring(0, 200) + "...";
		}
		string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | ADMIN-DM | admin={admin} | aksi={action} | target={target} | hasil={result} | teks=\"{clean}\"";
		lock (_sync)
		{
			RotateIfBig();
			File.AppendAllText(_path, line + Environment.NewLine, Encoding.UTF8);
		}
	}
	private void RotateIfBig()
	{
		try
		{
			FileInfo fileInfo = new FileInfo(_path);
			if (fileInfo.Exists && fileInfo.Length > 2097152)
			{
				string text = _path + ".1";
				if (File.Exists(text))
				{
					File.Delete(text);
				}
				File.Move(_path, text);
			}
		}
		catch
		{
		}
	}

	public List<string> LinesSince(DateTime since)
	{
		lock (_sync)
		{
			try
			{
				if (!File.Exists(_path))
				{
					return new List<string>();
				}
				List<string> list = new List<string>();
				string[] array = File.ReadAllLines(_path, Encoding.UTF8);
				foreach (string text in array)
				{
					if (text.Length >= 19 && DateTime.TryParse(text.Substring(0, 19), out var result) && result >= since)
					{
						list.Add(text);
					}
				}
				return list;
			}
			catch
			{
				return new List<string>();
			}
		}
	}

	public List<string> Tail(int n)
	{
		lock (_sync)
		{
			try
			{
				if (!File.Exists(_path))
				{
					return new List<string>();
				}
				string[] source = File.ReadAllLines(_path, Encoding.UTF8);
				return source.Reverse().Take(n).Reverse()
					.ToList();
			}
			catch
			{
				return new List<string>();
			}
		}
	}
}
internal static class ModUtil
{
	private static readonly Regex LinkRegex = new Regex("(https?://|www\\.|chat\\.whatsapp\\.com|wa\\.me/|t\\.me/|\\b[a-z0-9-]+\\.(com|net|org|xyz|vip|club|online|link|id|info|site|store|shop|biz|app|io|gg|win|bet|live)\\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex SafeLink = new Regex("(https?://)?(www\\.)?(lichess\\.org|chess\\.com|chess\\.college|ligacatur\\.com|youtube\\.com|youtu\\.be)\\S*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	public static bool HasUnsafeLink(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		string input = SafeLink.Replace(text, " ");
		return LinkRegex.IsMatch(input);
	}

	public static bool IdInSet(HashSet<string> set, string phone, string rawDigits)
	{
		return (!string.IsNullOrEmpty(phone) && set.Contains(phone)) || (!string.IsNullOrEmpty(rawDigits) && set.Contains(rawDigits));
	}
}
internal class JoinStore
{
	private readonly string _path;

	private readonly object _sync = new object();

	private readonly Dictionary<string, DateTime> _data;

	private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public JoinStore(string path)
	{
		_path = path;
		try
		{
			_data = (File.Exists(path) ? (JsonSerializer.Deserialize<Dictionary<string, DateTime>>(File.ReadAllText(path)) ?? new Dictionary<string, DateTime>()) : new Dictionary<string, DateTime>());
		}
		catch
		{
			_data = new Dictionary<string, DateTime>();
		}
	}

	public void Record(string key, DateTime utcNow)
	{
		if (string.IsNullOrEmpty(key))
		{
			return;
		}
		lock (_sync)
		{
			_data[key] = utcNow;
			try
			{
				File.WriteAllText(_path, JsonSerializer.Serialize(_data, Opts));
			}
			catch
			{
			}
		}
	}

	public bool InProbation(string key, int minutes, DateTime utcNow)
	{
		if (string.IsNullOrEmpty(key) || minutes <= 0)
		{
			return false;
		}
		lock (_sync)
		{
			DateTime value;
			return _data.TryGetValue(key, out value) && (utcNow - value).TotalMinutes < (double)minutes;
		}
	}

	public bool Clear(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return false;
		}
		lock (_sync)
		{
			bool flag = _data.Remove(key);
			if (flag)
			{
				try
				{
					File.WriteAllText(_path, JsonSerializer.Serialize(_data, Opts));
				}
				catch
				{
				}
			}
			return flag;
		}
	}
}
internal class WarningStore
{
	private readonly string _path;

	private readonly object _sync = new object();

	private readonly Dictionary<string, int> _data;

	private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public int Count
	{
		get
		{
			lock (_sync)
			{
				return _data.Count;
			}
		}
	}

	public WarningStore(string path)
	{
		_path = path;
		try
		{
			_data = (File.Exists(path) ? (JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(path)) ?? new Dictionary<string, int>()) : new Dictionary<string, int>());
		}
		catch
		{
			_data = new Dictionary<string, int>();
		}
	}

	public int Increment(string participant)
	{
		lock (_sync)
		{
			_data.TryGetValue(participant, out var value);
			value++;
			_data[participant] = value;
			try
			{
				File.WriteAllText(_path, JsonSerializer.Serialize(_data, Opts));
			}
			catch
			{
			}
			return value;
		}
	}

	public bool Reset(string key)
	{
		lock (_sync)
		{
			bool flag = _data.Remove(key);
			if (flag)
			{
				try
				{
					File.WriteAllText(_path, JsonSerializer.Serialize(_data, Opts));
				}
				catch
				{
				}
			}
			return flag;
		}
	}

	public List<(string num, int count)> TopForGroup(string jid, int n)
	{
		string prefix = jid + "|";
		lock (_sync)
		{
			return (from kv in (from kv in _data
					where kv.Key.StartsWith(prefix)
					orderby kv.Value descending
					select kv).Take(n)
				select (NumberUtil.Normalize(kv.Key.Substring(prefix.Length)), Value: kv.Value)).ToList();
		}
	}
}
internal static class CommandHandler
{
	[StructLayout(LayoutKind.Auto)]
	[CompilerGenerated]
	private struct DC_4_0
	{
		public JsonElement perfs;

		public StringBuilder sb;
	}

	[CompilerGenerated]
	private sealed class DC_6_0
	{
		public HttpClient http;

		internal async Task<JsonDocument?> BuildChesscomProfile_Get(string url)
		{
			using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, url);
			req.Headers.Add("User-Agent", "WA-Bot Liga Catur Indonesia");
			HttpResponseMessage resp = await http.SendAsync(req);
			if (resp.StatusCode == HttpStatusCode.NotFound)
			{
				return null;
			}
			if (!resp.IsSuccessStatusCode)
			{
				throw new Exception("HTTP " + (int)resp.StatusCode);
			}
			return JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
		}
	}

	[StructLayout(LayoutKind.Auto)]
	[CompilerGenerated]
	private struct DC_6_1
	{
		public StringBuilder sb;
	}

	[StructLayout(LayoutKind.Auto)]
	[CompilerGenerated]
	private struct DC_6_2
	{
		public JsonElement s;
	}

	private static readonly string[] ListingPages = new string[2] { "dailytournament", "pairing12" };

	private static readonly string[] StandingsPrefixes = new string[2] { "dailystandings", "pairing12standings" };

	private static Dictionary<string, string> _swissUrlMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private static Dictionary<int, string> _tidToSwiss = new Dictionary<int, string>();

	private static DateTimeOffset _swissUrlMapAt = DateTimeOffset.MinValue;

	public static async Task<string?> Handle(string raw, AppConfig config, HttpClient http, ILogger logger)
	{
		string body = raw.Substring(config.CommandPrefix.Length).Trim();
		if (body.Length == 0)
		{
			return null;
		}
		string[] parts = body.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		string cmd = parts[0].ToLowerInvariant();
		switch (cmd)
		{
		case "help":
			return BuildHelp(config);
		case "rules":
		case "aturan":
			return string.IsNullOrWhiteSpace(config.RulesText) ? "Aturan grup belum diset." : ("Aturan grup:\n" + CompactNumberedLines(config.RulesText, 5));
		case "info":
		case "ping":
			return "Bot aktif. Grup saya bantu jaga. Info cepat: " + config.CommandPrefix + "help.";
		case "next":
		case "turnamen":
		case "event":
			return await BuildSchedule(config, http, logger);
		case "jadwal":
			if (parts.Length != 1)
			{
				break;
			}
			return await BuildSchedule(config, http, logger);
		case "hasil":
		case "juara":
		{
			if (parts.Length < 2 || !int.TryParse(parts[1], out var tid))
			{
				return $"Format: {config.CommandPrefix}{cmd} <id>. Contoh: {config.CommandPrefix}{cmd} 9175.";
			}
			return await BuildResult(tid, http, logger);
		}
		case "rating":
		case "profil":
		case "profile":
			if (parts.Length < 2)
			{
				return $"Format: {config.CommandPrefix}{cmd} <user>. Chess.com: {config.CommandPrefix}{cmd} chesscom <user>.";
			}
			if (parts[1].Equals("chesscom", StringComparison.OrdinalIgnoreCase) || parts[1].Equals("chess.com", StringComparison.OrdinalIgnoreCase))
			{
				return (parts.Length >= 3) ? (await BuildChesscomProfile(parts[2], http, logger)) : ("Formatnya: " + config.CommandPrefix + cmd + " chesscom <username Chess.com>");
			}
			return await BuildLichessProfile(parts[1], http, logger);
		case "chesscom":
		case "chessdotcom":
			if (parts.Length < 2)
			{
				return $"Format: {config.CommandPrefix}chesscom <user>. Contoh: {config.CommandPrefix}chesscom MagnusCarlsen.";
			}
			return await BuildChesscomProfile(parts[1], http, logger);
		case "daftar":
		case "join":
		case "gabung":
			return await BuildDaftar(config, http, logger);
		}
		DataCommand dc = config.DataCommands.FirstOrDefault((DataCommand d) => d.Cmd.Equals(cmd, StringComparison.OrdinalIgnoreCase));
		if (dc != null)
		{
			if (parts.Length < 2 || !int.TryParse(parts[1], out var id))
			{
				return $"Format: {config.CommandPrefix}{cmd} <id>. Contoh: {config.CommandPrefix}{cmd} 8990.";
			}
			if (string.IsNullOrWhiteSpace(config.DbConnectionString))
			{
				return "Data internal belum aktif. Jadwal: " + config.CommandPrefix + "next.";
			}
			try
			{
				List<Dictionary<string, object?>> rows = await Db.QueryStoredProc(config.DbConnectionString, dc.Sp, dc.Param, id);
				return RenderRows(dc.Title, id, rows);
			}
			catch (Exception ex)
			{
				return "Maaf, data turnamen belum bisa diambil: " + ex.Message;
			}
		}
		string byName = await FindTournamentByName(config, http, logger, cmd);
		if (byName != null)
		{
			return byName;
		}
		return null;
	}

	private static async Task<string?> FindTournamentByName(AppConfig config, HttpClient http, ILogger logger, string keyword)
	{
		if (config.Announcer == null || string.IsNullOrWhiteSpace(config.Announcer.TeamId))
		{
			return null;
		}
		if (keyword.Length < 3)
		{
			return null;
		}
		List<SwissItem> tournaments;
		try
		{
			tournaments = await Announcer.Fetch(config, http, logger);
		}
		catch
		{
			return null;
		}
		DateTimeOffset now = DateTimeOffset.UtcNow;
		List<SwissItem> matches = (from swissItem in tournaments
			where swissItem.StartsAt > now && swissItem.Name.Replace(" ", "").Contains(keyword, StringComparison.OrdinalIgnoreCase)
			orderby swissItem.StartsAt
			select swissItem).ToList();
		if (matches.Count == 0)
		{
			return null;
		}
		int tzh = config.Announcer.TimezoneOffsetHours;
		string zone = ((tzh == 7) ? "WIB" : $"UTC{tzh:+#;-#;0}");
		CultureInfo idc = new CultureInfo("id-ID");
		StringBuilder sb = new StringBuilder();
		StringBuilder stringBuilder = sb;
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(26, 1, stringBuilder);
		handler.AppendLiteral("\ud83c\udfc6 Turnamen *");
		handler.AppendFormatted(matches[0].Name);
		handler.AppendLiteral("* berikutnya:");
		stringBuilder2.AppendLine(ref handler);
		sb.AppendLine();
		sb.Append(RenderTournamentSummary(config, matches[0], now));
		if (matches.Count > 1)
		{
			sb.AppendLine();
			sb.AppendLine();
			sb.AppendLine("\ud83d\udcc6 Jadwal berikutnya:");
			foreach (SwissItem t in matches.Skip(1).Take(4))
			{
				DateTimeOffset local = t.StartsAt.ToOffset(TimeSpan.FromHours(tzh));
				stringBuilder = sb;
				StringBuilder stringBuilder3 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(6, 3, stringBuilder);
				handler.AppendLiteral("• ");
				handler.AppendFormatted(local.ToString("ddd, dd MMM HH:mm", idc));
				handler.AppendLiteral(" ");
				handler.AppendFormatted(zone);
				handler.AppendLiteral(" — ");
				handler.AppendFormatted(t.Name);
				stringBuilder3.AppendLine(ref handler);
				stringBuilder = sb;
				StringBuilder stringBuilder4 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(28, 1, stringBuilder);
				handler.AppendLiteral("  https://lichess.org/swiss/");
				handler.AppendFormatted(t.Id);
				stringBuilder4.AppendLine(ref handler);
			}
			sb.Length -= Environment.NewLine.Length;
		}
		sb.Append("\u001f☝\ud83c\udffb " + FormatDuration(matches[0].StartsAt - now) + " lagi");
		return sb.ToString();
	}

	private static string BuildHelp(AppConfig config)
	{
		string commandPrefix = config.CommandPrefix;
		List<string> values = new List<string>
		{
			"Menu cepat Judit Polica:",
			commandPrefix + "next - jadwal",
			commandPrefix + "rules - aturan",
			commandPrefix + "puzzle - puzzle",
			commandPrefix + "standings - klasemen",
			commandPrefix + "events - event CCL",
			commandPrefix + "rating <user> - rating",
			commandPrefix + "tanya <soal> - tanya catur",
			commandPrefix + "admin <catatan> - panggil admin",
			commandPrefix + "sleep - bot istirahat"
		};
		return string.Join("\n", values);
	}

	private static string CompactNumberedLines(string text, int maxLines)
	{
		List<string> values = (from s in text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
			select s.Trim() into s
			where s.Length > 0
			select s).Take(maxLines).ToList();
		return string.Join("\n", values);
	}

	public static async Task<string> BuildLichessProfile(string username, HttpClient http, ILogger logger)
	{
		username = username.Trim().TrimStart('@');
		try
		{
			using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "https://lichess.org/api/user/" + Uri.EscapeDataString(username));
			req.Headers.Add("User-Agent", "WA-Bot");
			using HttpResponseMessage resp = await http.SendAsync(req);
			if (resp.StatusCode == HttpStatusCode.NotFound)
			{
				return "Pemain Lichess \"" + username + "\" tidak ditemukan.";
			}
			if (!resp.IsSuccessStatusCode)
			{
				return "Maaf, data Lichess belum bisa diambil sekarang.";
			}
			using JsonDocument doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
			JsonElement r = doc.RootElement;
			if (r.TryGetProperty("disabled", out var dis) && dis.ValueKind == JsonValueKind.True)
			{
				return "Akun Lichess \"" + username + "\" nonaktif.";
			}
			JsonElement un;
			string uname = (r.TryGetProperty("username", out un) ? (un.GetString() ?? username) : username);
			JsonElement ti;
			string title = ((r.TryGetProperty("title", out ti) && ti.ValueKind == JsonValueKind.String) ? (ti.GetString() + " ") : "");
			DC_4_0 DCv_4_ = default(DC_4_0);
			DCv_4_.sb = new StringBuilder();
			StringBuilder stringBuilder = DCv_4_.sb;
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(15, 2, stringBuilder);
			handler.AppendLiteral("♟\ufe0f *");
			handler.AppendFormatted(title);
			handler.AppendFormatted(uname);
			handler.AppendLiteral("* — Lichess");
			stringBuilder2.AppendLine(ref handler);
			if (r.TryGetProperty("perfs", out DCv_4_.perfs))
			{
				BuildLichessProfile_Add("bullet", "Bullet", ref DCv_4_);
				BuildLichessProfile_Add("blitz", "Blitz", ref DCv_4_);
				BuildLichessProfile_Add("rapid", "Rapid", ref DCv_4_);
				BuildLichessProfile_Add("classical", "Classical", ref DCv_4_);
				BuildLichessProfile_Add("puzzle", "Puzzle", ref DCv_4_);
			}
			JsonElement u;
			string url = ((r.TryGetProperty("url", out u) && u.ValueKind == JsonValueKind.String) ? u.GetString() : ("https://lichess.org/@/" + uname));
			stringBuilder = DCv_4_.sb;
			StringBuilder stringBuilder3 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(3, 1, stringBuilder);
			handler.AppendLiteral("\ud83d\udd17 ");
			handler.AppendFormatted(url);
			stringBuilder3.Append(ref handler);
			return DCv_4_.sb.ToString();
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			logger.LogError("Lichess profile gagal: {Msg}", ex2.Message);
			return "Maaf, data Lichess belum bisa diambil sekarang.";
		}
	}

	public static async Task<string> BuildPuzzle(HttpClient http, ILogger logger)
	{
		try
		{
			using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "https://lichess.org/api/puzzle/daily");
			req.Headers.Add("User-Agent", "WA-Bot");
			using HttpResponseMessage resp = await http.SendAsync(req);
			if (!resp.IsSuccessStatusCode)
			{
				return "Maaf, puzzle belum bisa diambil sekarang.";
			}
			using JsonDocument doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
			JsonElement r = doc.RootElement;
			string id = "";
			int rating = 0;
			if (r.TryGetProperty("puzzle", out var pz))
			{
				if (pz.TryGetProperty("id", out var pid))
				{
					id = pid.GetString() ?? "";
				}
				if (pz.TryGetProperty("rating", out var pr) && pr.ValueKind == JsonValueKind.Number)
				{
					rating = pr.GetInt32();
				}
			}
			string link = ((id.Length > 0) ? ("https://lichess.org/training/" + id) : "https://lichess.org/training/daily");
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("\ud83e\udde9 *Puzzle Harian Lichess*");
			StringBuilder stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler;
			if (rating > 0)
			{
				stringBuilder = sb;
				StringBuilder stringBuilder2 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder);
				handler.AppendLiteral("Tingkat kesulitan: ~");
				handler.AppendFormatted(rating);
				stringBuilder2.AppendLine(ref handler);
			}
			sb.AppendLine("Cari langkah terbaiknya! Jangan intip solusi dulu \ud83d\ude09");
			stringBuilder = sb;
			StringBuilder stringBuilder3 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(3, 1, stringBuilder);
			handler.AppendLiteral("\ud83d\udd17 ");
			handler.AppendFormatted(link);
			stringBuilder3.Append(ref handler);
			return sb.ToString();
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			logger.LogError("Puzzle gagal: {Msg}", ex2.Message);
			return "Maaf, puzzle belum bisa diambil sekarang.";
		}
	}

	public static async Task<string> BuildChesscomProfile(string username, HttpClient http, ILogger logger)
	{
		DC_6_0 DCv_6_ = new DC_6_0();
		DCv_6_.http = http;
		username = username.Trim().TrimStart('@').ToLowerInvariant();
		try
		{
			using JsonDocument prof = await DCv_6_.BuildChesscomProfile_Get("https://api.chess.com/pub/player/" + Uri.EscapeDataString(username));
			if (prof == null)
			{
				return "Pemain Chess.com \"" + username + "\" tidak ditemukan.";
			}
			JsonElement pr = prof.RootElement;
			JsonElement un;
			string uname = (pr.TryGetProperty("username", out un) ? (un.GetString() ?? username) : username);
			JsonElement ti;
			string title = ((pr.TryGetProperty("title", out ti) && ti.ValueKind == JsonValueKind.String) ? (ti.GetString() + " ") : "");
			JsonElement u;
			string url2 = ((pr.TryGetProperty("url", out u) && u.ValueKind == JsonValueKind.String) ? u.GetString() : ("https://www.chess.com/member/" + uname));
			DC_6_1 DC_6_2 = default(DC_6_1);
			DC_6_2.sb = new StringBuilder();
			StringBuilder stringBuilder = DC_6_2.sb;
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(17, 2, stringBuilder);
			handler.AppendLiteral("♟\ufe0f *");
			handler.AppendFormatted(title);
			handler.AppendFormatted(uname);
			handler.AppendLiteral("* — Chess.com");
			stringBuilder2.AppendLine(ref handler);
			using JsonDocument stats = await DCv_6_.BuildChesscomProfile_Get("https://api.chess.com/pub/player/" + Uri.EscapeDataString(username) + "/stats");
			if (stats != null)
			{
				DC_6_2 DC_6_3 = default(DC_6_2);
				DC_6_3.s = stats.RootElement;
				BuildChesscomProfile_Add("chess_bullet", "Bullet", ref DC_6_2, ref DC_6_3);
				BuildChesscomProfile_Add("chess_blitz", "Blitz", ref DC_6_2, ref DC_6_3);
				BuildChesscomProfile_Add("chess_rapid", "Rapid", ref DC_6_2, ref DC_6_3);
				BuildChesscomProfile_Add("chess_daily", "Daily", ref DC_6_2, ref DC_6_3);
				if (DC_6_3.s.TryGetProperty("tactics", out var tac) && tac.TryGetProperty("highest", out var hi) && hi.TryGetProperty("rating", out var tr) && tr.ValueKind == JsonValueKind.Number)
				{
					stringBuilder = DC_6_2.sb;
					StringBuilder stringBuilder3 = stringBuilder;
					handler = new StringBuilder.AppendInterpolatedStringHandler(22, 1, stringBuilder);
					handler.AppendLiteral("• Puzzle (tertinggi): ");
					handler.AppendFormatted(tr.GetInt32());
					stringBuilder3.AppendLine(ref handler);
				}
			}
			stringBuilder = DC_6_2.sb;
			StringBuilder stringBuilder4 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(3, 1, stringBuilder);
			handler.AppendLiteral("\ud83d\udd17 ");
			handler.AppendFormatted(url2);
			stringBuilder4.Append(ref handler);
			return DC_6_2.sb.ToString();
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			logger.LogError("Chess.com profile gagal: {Msg}", ex2.Message);
			return "Maaf, data Chess.com belum bisa diambil sekarang.";
		}
	}

	public static async Task<string> BuildDaftar(AppConfig config, HttpClient http, ILogger logger)
	{
		string reply = config.Faq?.Entries?.FirstOrDefault((FaqEntry e) => e.Id == "daftar")?.Reply ?? "";
		if (string.IsNullOrWhiteSpace(reply))
		{
			reply = "Cara ikut turnamen: gabung tim Lichess, lalu klik Join pada turnamen yang dipilih.\nJadwal terdekat:\n{schedule}";
		}
		if (reply.Contains("{schedule}"))
		{
			string text = reply;
			reply = text.Replace("{schedule}", await BuildSchedule(config, http, logger));
		}
		if (reply.Contains("{rules}"))
		{
			reply = reply.Replace("{rules}", config.RulesText);
		}
		return reply;
	}

	private static async Task<(string name, string url, List<(string score, string player)> rows)?> FetchStandingsUrl(string url, HttpClient http, ILogger logger)
	{
		string html;
		try
		{
			using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, url);
			req.Headers.Add("User-Agent", "Mozilla/5.0 (WA-Bot)");
			using HttpResponseMessage resp = await http.SendAsync(req);
			if (!resp.IsSuccessStatusCode)
			{
				return null;
			}
			html = await resp.Content.ReadAsStringAsync();
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			logger.LogError("Ambil standings gagal: {Msg}", ex2.Message);
			return null;
		}
		string name = WebUtility.HtmlDecode(Regex.Match(html, "<h2[^>]*>(.*?)</h2>", RegexOptions.Singleline).Groups[1].Value).Trim();
		if (string.IsNullOrEmpty(name))
		{
			name = Regex.Match(Uri.UnescapeDataString(Regex.Match(html, "whatsapp://send\\?text=([^\"']+)").Groups[1].Value), "Hasil\\s+(.+?):").Groups[1].Value.Trim();
		}
		List<(string score, string player)> rows = new List<(string, string)>();
		string bodyHtml = Regex.Match(html, "<tbody>(.*?)</tbody>", RegexOptions.Singleline).Groups[1].Value;
		if (string.IsNullOrEmpty(bodyHtml))
		{
			bodyHtml = html;
		}
		foreach (Match row in Regex.Matches(bodyHtml, "<tr>(.*?)</tr>", RegexOptions.Singleline))
		{
			MatchCollection cells = Regex.Matches(row.Groups[1].Value, "<td[^>]*>(.*?)</td>", RegexOptions.Singleline);
			if (cells.Count >= 4)
			{
				string score = StripTags(cells[1].Groups[1].Value);
				string player = Regex.Replace(StripTags(cells[3].Groups[1].Value), "\\s*\\(\\d+\\)\\s*$", "").Trim();
				if (player.Length != 0)
				{
					rows.Add((score, player));
				}
			}
		}
		return (name, url, rows);
	}

	private static async Task<(string name, string url, List<(string score, string player)> rows)?> FetchStandingsByTid(int tid, HttpClient http, ILogger logger)
	{
		(string name, string url, List<(string score, string player)> rows)? last = null;
		string[] standingsPrefixes = StandingsPrefixes;
		foreach (string pfx in standingsPrefixes)
		{
			(string name, string url, List<(string score, string player)> rows)? d = await FetchStandingsUrl($"https://ligacatur.com/{pfx}?TournamentID={tid}", http, logger);
			if (d.HasValue)
			{
				if (d.Value.rows.Count > 0)
				{
					return d;
				}
				last = d;
			}
		}
		return last;
	}

	private static string FormatResult(string name, string url, List<(string score, string player)> rows)
	{
		string[] array = new string[3] { "\ud83e\udd47", "\ud83e\udd48", "\ud83e\udd49" };
		StringBuilder stringBuilder = new StringBuilder();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(16, 1, stringBuilder2);
		handler.AppendLiteral("\ud83c\udfc6 *HASIL — ");
		handler.AppendFormatted(string.IsNullOrEmpty(name) ? "Turnamen" : name);
		handler.AppendLiteral("* \ud83c\udfc6");
		stringBuilder3.AppendLine(ref handler);
		stringBuilder.AppendLine();
		for (int i = 0; i < rows.Count && i < 3; i++)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(6, 3, stringBuilder2);
			handler.AppendFormatted(array[i]);
			handler.AppendLiteral(" *");
			handler.AppendFormatted(rows[i].player);
			handler.AppendLiteral("* — ");
			handler.AppendFormatted(rows[i].score.Replace(".5", "½"));
			stringBuilder4.AppendLine(ref handler);
		}
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("\ud83d\udc4f Selamat untuk para juara! Terima kasih semua yang sudah bertanding \ud83d\udd25♟\ufe0f");
		stringBuilder.AppendLine("Sampai jumpa di turnamen berikutnya!");
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("\ud83d\udcca Hasil lengkap:");
		stringBuilder.Append(url);
		return stringBuilder.ToString();
	}

	public static async Task<string> BuildResult(int tid, HttpClient http, ILogger logger)
	{
		string url = $"https://ligacatur.com/dailystandings?TournamentID={tid}";
		(string name, string url, List<(string score, string player)> rows)? d = await FetchStandingsByTid(tid, http, logger);
		if (d.HasValue)
		{
			url = d.Value.url;
			if (d.Value.rows.Count > 0)
			{
				return FormatResult(d.Value.name, d.Value.url, d.Value.rows);
			}
		}
		string swiss = await GetSwissForTid(tid, http, logger);
		if (swiss != null)
		{
			string m = await BuildResultFromLichess(swiss, null, http, logger);
			if (m != null)
			{
				return m;
			}
		}
		return "Hasil belum tersedia untuk turnamen ini (mungkin belum selesai).\nCek: " + url;
	}

	public static async Task<string?> BuildResultByUrl(string url, HttpClient http, ILogger logger)
	{
		(string name, string url, List<(string score, string player)> rows)? d = await FetchStandingsUrl(url, http, logger);
		if (!d.HasValue || d.Value.rows.Count == 0)
		{
			return null;
		}
		return FormatResult(d.Value.name, d.Value.url, d.Value.rows);
	}

	private static string FormatStandings(string name, string url, List<(string score, string player)> rows)
	{
		int num = ((rows.Count <= 8) ? Math.Min(3, rows.Count) : 10);
		string[] array = new string[3] { "\ud83e\udd47", "\ud83e\udd48", "\ud83e\udd49" };
		StringBuilder stringBuilder = new StringBuilder();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(19, 1, stringBuilder2);
		handler.AppendLiteral("\ud83c\udfc6 *KLASEMEN — ");
		handler.AppendFormatted(string.IsNullOrEmpty(name) ? "Turnamen" : name);
		handler.AppendLiteral("* \ud83c\udfc6");
		stringBuilder3.AppendLine(ref handler);
		stringBuilder.AppendLine();
		for (int i = 0; i < rows.Count && i < num; i++)
		{
			string value = ((i < 3) ? array[i] : $"{i + 1}.");
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(4, 3, stringBuilder2);
			handler.AppendFormatted(value);
			handler.AppendLiteral(" ");
			handler.AppendFormatted(rows[i].player);
			handler.AppendLiteral(" — ");
			handler.AppendFormatted(rows[i].score.Replace(".5", "½"));
			stringBuilder4.AppendLine(ref handler);
		}
		if (rows.Count > num)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder5 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder2);
			handler.AppendLiteral("… (+");
			handler.AppendFormatted(rows.Count - num);
			handler.AppendLiteral(" pemain lagi)");
			stringBuilder5.AppendLine(ref handler);
		}
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("\ud83d\udcca Klasemen lengkap:");
		stringBuilder.Append(url);
		return stringBuilder.ToString();
	}

	public static async Task<string> BuildStandings(int tid, HttpClient http, ILogger logger)
	{
		string url = $"https://ligacatur.com/dailystandings?TournamentID={tid}";
		(string name, string url, List<(string score, string player)> rows)? d = await FetchStandingsByTid(tid, http, logger);
		if (d.HasValue)
		{
			url = d.Value.url;
			if (d.Value.rows.Count > 0)
			{
				return FormatStandings(d.Value.name, d.Value.url, d.Value.rows);
			}
		}
		string swiss = await GetSwissForTid(tid, http, logger);
		if (swiss != null)
		{
			string m = await BuildStandingsFromLichess(swiss, null, http, logger);
			if (m != null)
			{
				return m;
			}
		}
		return "Klasemen belum tersedia untuk turnamen ini (mungkin belum mulai).\nCek: " + url;
	}

	public static async Task<string> BuildStandingsByUrl(string url, HttpClient http, ILogger logger)
	{
		(string name, string url, List<(string score, string player)> rows)? d = await FetchStandingsUrl(url, http, logger);
		if (!d.HasValue)
		{
			return "Saya belum bisa mengambil klasemen sekarang. Coba lagi sebentar lagi ya.";
		}
		if (d.Value.rows.Count == 0)
		{
			return "Klasemen belum tersedia untuk turnamen ini (mungkin belum mulai).\nCek: " + url;
		}
		return FormatStandings(d.Value.name, d.Value.url, d.Value.rows);
	}

	public static async Task<Dictionary<string, string>> GetSwissUrlMap(HttpClient http, ILogger logger)
	{
		if (_swissUrlMap.Count > 0 && (DateTimeOffset.UtcNow - _swissUrlMapAt).TotalMinutes < 10.0)
		{
			return _swissUrlMap;
		}
		Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		Dictionary<int, string> tidMap = new Dictionary<int, string>();
		string[] listingPages = ListingPages;
		foreach (string page in listingPages)
		{
			try
			{
				using (HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "https://ligacatur.com/" + page))
				{
					req.Headers.Add("User-Agent", "Mozilla/5.0 (WA-Bot)");
					using HttpResponseMessage resp = await http.SendAsync(req);
					if (!resp.IsSuccessStatusCode)
					{
						goto end_IL_0183;
					}
					foreach (Match row in Regex.Matches(await resp.Content.ReadAsStringAsync(), "<tr>(.*?)</tr>", RegexOptions.Singleline))
					{
						string rc = row.Groups[1].Value;
						Match hm = Regex.Match(rc, "href=\"([A-Za-z0-9]+standings\\?TournamentID=(\\d+)[^\"]*)\"");
						Match sm = Regex.Match(rc, "lichess\\.org/swiss/([A-Za-z0-9]+)");
						if (hm.Success && sm.Success)
						{
							map[sm.Groups[1].Value] = "https://ligacatur.com/" + WebUtility.HtmlDecode(hm.Groups[1].Value);
							if (int.TryParse(hm.Groups[2].Value, out var tid))
							{
								tidMap[tid] = sm.Groups[1].Value;
							}
						}
					}
					goto end_IL_00a4;
					end_IL_0183:;
				}
				end_IL_00a4:;
			}
			catch (Exception ex)
			{
				logger.LogError("GetSwissUrlMap {Page} gagal: {Msg}", page, ex.Message);
			}
		}
		if (map.Count > 0)
		{
			_swissUrlMap = map;
			_tidToSwiss = tidMap;
			_swissUrlMapAt = DateTimeOffset.UtcNow;
		}
		return (_swissUrlMap.Count > 0) ? _swissUrlMap : map;
	}

	private static async Task<string?> GetSwissForTid(int tid, HttpClient http, ILogger logger)
	{
		await GetSwissUrlMap(http, logger);
		string s;
		return _tidToSwiss.TryGetValue(tid, out s) ? s : null;
	}

	public static async Task<List<(string url, string swiss, string name, string date)>> GetRecentTournaments(HttpClient http, ILogger logger, int perPage)
	{
		List<(string, string, string, string)> list = new List<(string, string, string, string)>();
		string[] listingPages = ListingPages;
		foreach (string page in listingPages)
		{
			try
			{
				using (HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "https://ligacatur.com/" + page))
				{
					req.Headers.Add("User-Agent", "Mozilla/5.0 (WA-Bot)");
					using HttpResponseMessage resp = await http.SendAsync(req);
					if (!resp.IsSuccessStatusCode)
					{
						goto end_IL_012b;
					}
					string html = await resp.Content.ReadAsStringAsync();
					int taken = 0;
					foreach (Match row in Regex.Matches(html, "<tr>(.*?)</tr>", RegexOptions.Singleline))
					{
						string rc = row.Groups[1].Value;
						Match hm = Regex.Match(rc, "href=\"([A-Za-z0-9]+standings\\?TournamentID=\\d+[^\"]*)\"");
						if (!hm.Success)
						{
							continue;
						}
						string url = "https://ligacatur.com/" + WebUtility.HtmlDecode(hm.Groups[1].Value);
						string swiss = Regex.Match(rc, "lichess\\.org/swiss/([A-Za-z0-9]+)").Groups[1].Value;
						string nm = StripTags(Regex.Match(rc, "standings\\?TournamentID=\\d+[^>]*>(.*?)</a>", RegexOptions.Singleline).Groups[1].Value);
						string date = StripTags(Regex.Match(rc, "<b>(.*?)</b>", RegexOptions.Singleline).Groups[1].Value);
						if (nm.Length != 0)
						{
							list.Add((url, swiss, nm, date));
							int num = taken + 1;
							taken = num;
							if (num >= perPage)
							{
								break;
							}
						}
					}
					goto end_IL_0050;
					end_IL_012b:;
				}
				end_IL_0050:;
			}
			catch (Exception ex)
			{
				logger.LogError("GetRecentTournaments {Page} gagal: {Msg}", page, ex.Message);
			}
		}
		return list;
	}

	private static async Task<List<(string score, string player)>> FetchLichessRows(string swiss, HttpClient http, ILogger logger)
	{
		List<(string, string)> rows = new List<(string, string)>();
		try
		{
			using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "https://lichess.org/api/swiss/" + swiss + "/results?nb=30");
			req.Headers.Add("Accept", "application/x-ndjson");
			using HttpResponseMessage resp = await http.SendAsync(req);
			if (!resp.IsSuccessStatusCode)
			{
				return rows;
			}
			string[] array = (await resp.Content.ReadAsStringAsync()).Split('\n');
			foreach (string line in array)
			{
				string s = line.Trim();
				if (s.Length == 0)
				{
					continue;
				}
				try
				{
					JsonElement u;
					JsonElement pd;
					using (JsonDocument doc = JsonDocument.Parse(s))
					{
						JsonElement r = doc.RootElement;
						string user = (r.TryGetProperty("username", out u) ? (u.GetString() ?? "") : "");
						string pts = "";
						if (r.TryGetProperty("points", out pd) && pd.ValueKind == JsonValueKind.Number)
						{
							pts = pd.GetDouble().ToString(CultureInfo.InvariantCulture);
						}
						if (user.Length > 0)
						{
							rows.Add((pts, user));
						}
					}
					u = default(JsonElement);
					pd = default(JsonElement);
				}
				catch
				{
				}
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			logger.LogError("Lichess results gagal: {Msg}", ex2.Message);
		}
		return rows;
	}

	private static async Task<string> LichessSwissName(string swiss, HttpClient http)
	{
		try
		{
			using HttpResponseMessage resp = await http.GetAsync("https://lichess.org/api/swiss/" + swiss);
			if (resp.IsSuccessStatusCode)
			{
				using (JsonDocument doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
				{
					JsonElement n;
					return doc.RootElement.TryGetProperty("name", out n) ? (n.GetString() ?? "") : "";
				}
			}
		}
		catch
		{
		}
		return "";
	}

	public static async Task<string?> BuildResultFromLichess(string swiss, string? name, HttpClient http, ILogger logger)
	{
		List<(string score, string player)> rows = await FetchLichessRows(swiss, http, logger);
		if (rows.Count == 0)
		{
			return null;
		}
		if (string.IsNullOrEmpty(name))
		{
			name = await LichessSwissName(swiss, http);
		}
		return FormatResult(name ?? "", "https://lichess.org/swiss/" + swiss, rows);
	}

	public static async Task<string?> BuildStandingsFromLichess(string swiss, string? name, HttpClient http, ILogger logger)
	{
		List<(string score, string player)> rows = await FetchLichessRows(swiss, http, logger);
		if (rows.Count == 0)
		{
			return null;
		}
		if (string.IsNullOrEmpty(name))
		{
			name = await LichessSwissName(swiss, http);
		}
		return FormatStandings(name ?? "", "https://lichess.org/swiss/" + swiss, rows);
	}

	public static async Task<string> BuildStandingsSmart(string url, string swiss, string name, HttpClient http, ILogger logger)
	{
		(string name, string url, List<(string score, string player)> rows)? d = await FetchStandingsUrl(url, http, logger);
		if (d.HasValue && d.Value.rows.Count > 0)
		{
			return FormatStandings(d.Value.name, d.Value.url, d.Value.rows);
		}
		string text = ((!string.IsNullOrEmpty(swiss)) ? (await BuildStandingsFromLichess(swiss, name, http, logger)) : null);
		string m = text;
		return m ?? ("Klasemen belum tersedia.\nCek: " + url);
	}

	private static string StripTags(string s)
	{
		return WebUtility.HtmlDecode(Regex.Replace(s, "<.*?>", "", RegexOptions.Singleline)).Trim();
	}

	public static async Task<string> BuildLatestResult(AppConfig config, HttpClient http, ILogger logger)
	{
		if (config.Announcer == null || string.IsNullOrWhiteSpace(config.Announcer.TeamId))
		{
			return "Hasil turnamen belum aktif di server ini.";
		}
		List<SwissItem> list;
		try
		{
			list = await Announcer.Fetch(config, http, logger);
		}
		catch
		{
			return "Saya belum bisa mengambil hasil sekarang. Coba lagi sebentar lagi ya.";
		}
		SwissItem t = (from x in list
			where x.Status == "finished"
			orderby x.StartsAt descending
			select x).FirstOrDefault();
		if (t == null)
		{
			return "Belum ada hasil turnamen terbaru. Ketik " + config.CommandPrefix + "standings untuk klasemen turnamen berjalan.";
		}
		Dictionary<string, string> urlMap = await GetSwissUrlMap(http, logger);
		string msg = null;
		if (urlMap.TryGetValue(t.Id, out string url))
		{
			msg = await BuildResultByUrl(url, http, logger);
		}
		if (msg == null)
		{
			msg = await BuildResultFromLichess(t.Id, t.Name, http, logger);
		}
		return msg ?? ("Hasil \"" + t.Name + "\" belum tersedia.\nCek: https://lichess.org/swiss/" + t.Id);
	}

	public static async Task<string> BuildSchedule(AppConfig config, HttpClient http, ILogger logger)
	{
		if (config.Announcer == null || string.IsNullOrWhiteSpace(config.Announcer.TeamId))
		{
			return "Jadwal turnamen belum aktif. Admin perlu mengisi team Lichess di config bot.";
		}
		List<SwissItem> tournaments;
		try
		{
			tournaments = await Announcer.Fetch(config, http, logger);
		}
		catch (Exception ex)
		{
			logger.LogError("Gagal mengambil jadwal Lichess: {Msg}", ex.Message);
			return "Saya belum bisa mengambil jadwal Lichess sekarang. Coba lagi beberapa saat lagi ya.";
		}
		DateTimeOffset now = DateTimeOffset.UtcNow;
		int tzh = config.Announcer.TimezoneOffsetHours;
		List<SwissItem> upcoming = (from swissItem in tournaments
			where swissItem.StartsAt > now
			orderby swissItem.StartsAt
			select swissItem).ToList();
		if (upcoming.Count == 0)
		{
			return "Belum ada turnamen Lichess mendatang yang terdeteksi untuk tim ini. Cek lagi nanti ya.";
		}
		CultureInfo idCulture = new CultureInfo("id-ID");
		DateTime tomorrowLocal = now.ToOffset(TimeSpan.FromHours(tzh)).Date.AddDays(1.0);
		List<SwissItem> tomorrowList = (from swissItem in upcoming.Skip(1)
			where swissItem.StartsAt.ToOffset(TimeSpan.FromHours(tzh)).Date == tomorrowLocal
			select swissItem).ToList();
		string zone = ((tzh == 7) ? "WIB" : $"UTC{tzh:+#;-#;0}");
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("\ud83d\udcc5 Jadwal Turnamen — Liga Catur Indonesia");
		sb.AppendLine();
		sb.AppendLine("▶\ufe0f Berikutnya:");
		sb.Append(RenderTournamentSummary(config, upcoming[0], now));
		sb.AppendLine();
		sb.AppendLine();
		StringBuilder stringBuilder = sb;
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(12, 1, stringBuilder);
		handler.AppendLiteral("\ud83d\udcc6 Besok (");
		handler.AppendFormatted(tomorrowLocal.ToString("dddd, dd MMM", idCulture));
		handler.AppendLiteral("):");
		stringBuilder2.AppendLine(ref handler);
		if (tomorrowList.Count == 0)
		{
			sb.Append("Belum ada turnamen terjadwal besok. Ketik " + config.CommandPrefix + "next untuk yang berikutnya.");
		}
		else
		{
			foreach (SwissItem t in tomorrowList)
			{
				DateTimeOffset local = t.StartsAt.ToOffset(TimeSpan.FromHours(tzh));
				stringBuilder = sb;
				StringBuilder stringBuilder3 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(6, 3, stringBuilder);
				handler.AppendLiteral("• ");
				handler.AppendFormatted(local, "HH:mm");
				handler.AppendLiteral(" ");
				handler.AppendFormatted(zone);
				handler.AppendLiteral(" — ");
				handler.AppendFormatted(t.Name);
				stringBuilder3.AppendLine(ref handler);
				stringBuilder = sb;
				StringBuilder stringBuilder4 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(28, 1, stringBuilder);
				handler.AppendLiteral("  https://lichess.org/swiss/");
				handler.AppendFormatted(t.Id);
				stringBuilder4.AppendLine(ref handler);
			}
			sb.Length -= Environment.NewLine.Length;
		}
		return sb.ToString();
	}

	private static string RenderTournamentSummary(AppConfig config, SwissItem t, DateTimeOffset now)
	{
		int num = config.Announcer?.TimezoneOffsetHours ?? 7;
		CultureInfo cultureInfo = new CultureInfo("id-ID");
		DateTimeOffset dateTimeOffset = t.StartsAt.ToOffset(TimeSpan.FromHours(num));
		string value = ((num == 7) ? "WIB" : $"UTC{num:+#;-#;0}");
		string value2 = ((t.ClockLimit <= 0) ? "clock belum tersedia" : ((t.ClockLimit % 60 == 0) ? $"{t.ClockLimit / 60}+{t.ClockIncrement}" : $"{(double)t.ClockLimit / 60.0:0.#}+{t.ClockIncrement}"));
		string value3 = (t.Rated ? "Rated" : "Casual");
		string value4 = (string.IsNullOrWhiteSpace(t.Variant) ? "standard" : t.Variant);
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine(t.Name);
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(4, 2, stringBuilder2);
		handler.AppendLiteral("\ud83d\udd52 ");
		handler.AppendFormatted(dateTimeOffset.ToString("dddd, dd MMM HH:mm", cultureInfo));
		handler.AppendLiteral(" ");
		handler.AppendFormatted(value);
		stringBuilder3.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(21, 4, stringBuilder2);
		handler.AppendLiteral("♟\ufe0f ");
		handler.AppendFormatted(t.NbRounds);
		handler.AppendLiteral(" ronde Swiss, ");
		handler.AppendFormatted(value2);
		handler.AppendLiteral(", ");
		handler.AppendFormatted(value4);
		handler.AppendLiteral(", ");
		handler.AppendFormatted(value3);
		stringBuilder4.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(29, 1, stringBuilder2);
		handler.AppendLiteral("\ud83d\udc49 https://lichess.org/swiss/");
		handler.AppendFormatted(t.Id);
		stringBuilder5.Append(ref handler);
		return stringBuilder.ToString();
	}

	private static string FormatDuration(TimeSpan span)
	{
		if (span.TotalMinutes < 1.0)
		{
			return "sebentar";
		}
		int num = (int)span.TotalDays;
		int hours = span.Hours;
		int minutes = span.Minutes;
		if (num <= 0)
		{
			if (hours <= 0)
			{
				return $"{Math.Max(1, minutes)} menit";
			}
			return $"{hours} jam {minutes} menit";
		}
		return $"{num} hari {hours} jam";
	}

	private static string RenderRows(string title, int id, List<Dictionary<string, object?>> rows)
	{
		if (rows.Count == 0)
		{
			return $"{title} — #{id}\n(tidak ada data)";
		}
		StringBuilder stringBuilder = new StringBuilder();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(4, 2, stringBuilder2);
		handler.AppendFormatted(title);
		handler.AppendLiteral(" — #");
		handler.AppendFormatted(id);
		stringBuilder3.AppendLine(ref handler);
		int num = Math.Min(rows.Count, 25);
		for (int i = 0; i < num; i++)
		{
			IEnumerable<string> values = rows[i].Select<KeyValuePair<string, object>, string>((KeyValuePair<string, object> kv) => $"{kv.Key}: {kv.Value}");
			stringBuilder.AppendLine($"{i + 1}. " + string.Join(" | ", values));
		}
		if (rows.Count > num)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(18, 1, stringBuilder2);
			handler.AppendLiteral("... (+");
			handler.AppendFormatted(rows.Count - num);
			handler.AppendLiteral(" baris lagi)");
			stringBuilder4.AppendLine(ref handler);
		}
		return stringBuilder.ToString().TrimEnd();
	}

	[CompilerGenerated]
	private static void BuildLichessProfile_Add(string key, string label, ref DC_4_0 P_2)
	{
		if (P_2.perfs.TryGetProperty(key, out var value) && value.TryGetProperty("rating", out var value2) && value2.ValueKind == JsonValueKind.Number)
		{
			JsonElement value3;
			int num = ((value.TryGetProperty("games", out value3) && value3.ValueKind == JsonValueKind.Number) ? value3.GetInt32() : 0);
			if (num != 0)
			{
				JsonElement value4;
				bool flag = value.TryGetProperty("prov", out value4) && value4.ValueKind == JsonValueKind.True;
				StringBuilder stringBuilder = P_2.sb;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(12, 4, stringBuilder);
				handler.AppendLiteral("• ");
				handler.AppendFormatted(label);
				handler.AppendLiteral(": ");
				handler.AppendFormatted(value2.GetInt32());
				handler.AppendFormatted(flag ? "?" : "");
				handler.AppendLiteral(" (");
				handler.AppendFormatted(num);
				handler.AppendLiteral(" game)");
				stringBuilder.AppendLine(ref handler);
			}
		}
	}

	[CompilerGenerated]
	private static void BuildChesscomProfile_Add(string key, string label, ref DC_6_1 P_2, ref DC_6_2 P_3)
	{
		if (P_3.s.TryGetProperty(key, out var value) && value.TryGetProperty("last", out var value2) && value2.TryGetProperty("rating", out var value3) && value3.ValueKind == JsonValueKind.Number)
		{
			StringBuilder stringBuilder = P_2.sb;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(4, 2, stringBuilder);
			handler.AppendLiteral("• ");
			handler.AppendFormatted(label);
			handler.AppendLiteral(": ");
			handler.AppendFormatted(value3.GetInt32());
			stringBuilder.AppendLine(ref handler);
		}
	}
}
internal static class Ai
{
	public static async Task<string?> Ask(AiConfig cfg, HttpClient http, string question, ILogger logger, string systemSuffix = "", string priorTurns = "")
	{
		try
		{
			string baseSystem = (string.IsNullOrWhiteSpace(systemSuffix) ? cfg.SystemPrompt : (cfg.SystemPrompt + "\n\n" + systemSuffix));
			string langLock = "\n\nATURAN OUTPUT (WAJIB): Jawab dalam BAHASA YANG SAMA dengan pesan pengguna terakhir (pengguna menulis Bahasa Indonesia -> jawab Bahasa Indonesia; menulis Inggris -> jawab Inggris). DILARANG beralih ke bahasa yang TIDAK dipakai pengguna (mis. Mandarin/中文, Jepang, Thai) walau sebagian. JANGAN menampilkan proses berpikir. JANGAN mengarang giliran 'user'/'assistant'/'system'. JANGAN menyalin atau menerjemahkan instruksi ini. Langsung beri jawaban singkat saja.";
			string system = baseSystem + langLock;
			string prompt = (string.IsNullOrWhiteSpace(priorTurns) ? question : (priorTurns + "\nUser: " + question + "\nAsisten:"));
			var payload = new
			{
				model = cfg.Model,
				prompt = prompt,
				system = system,
				stream = false,
				keep_alive = cfg.KeepAlive,
				options = new
				{
					num_predict = cfg.NumPredict,
					temperature = cfg.Temperature,
					top_p = cfg.TopP,
					repeat_penalty = cfg.RepeatPenalty,
					stop = new string[6] { "\nUser:", "\nuser", "\nAsisten:", "\nassistant", "\nSystem:", "\nsystem" }
				}
			};
			using StringContent content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
			using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(cfg.TimeoutSeconds));
			using HttpResponseMessage r = await http.PostAsync(cfg.Url + "/api/generate", content, cts.Token);
			if (!r.IsSuccessStatusCode)
			{
				logger.LogWarning("Ollama HTTP {Code}", (int)r.StatusCode);
				return null;
			}
			using JsonDocument doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
			JsonElement rp;
			string resp = ((!doc.RootElement.TryGetProperty("response", out rp)) ? null : rp.GetString()?.Trim());
			return StripDrift(resp);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			logger.LogError("Ollama error: {Msg}", ex2.Message);
			return null;
		}
	}

	private static string? StripDrift(string? s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return s;
		}
		string[] array = new string[4] { "\nUser:", "\nAsisten:", "\nAssistant:", "\nSystem:" };
		foreach (string value in array)
		{
			int num = s.IndexOf(value, StringComparison.OrdinalIgnoreCase);
			if (num >= 0)
			{
				s = s.Substring(0, num);
			}
		}
		for (int j = 0; j < s.Length; j++)
		{
			char c = s[j];
			if ((c >= '\u3040' && c <= '鿿') || (c >= '가' && c <= '힣') || (c >= '\u0e00' && c <= '\u0e7f') || (c >= '\u0600' && c <= 'ۿ') || (c >= 'Ѐ' && c <= 'ӿ') || (c >= '豈' && c <= '\ufaff'))
			{
				s = s.Substring(0, j);
				break;
			}
		}
		s = s.Trim();
		return (s.Length < 4) ? "" : s;
	}
}
internal static class ModerationReport
{
	public static async Task RunLoop(Func<AppConfig> getConfig, AuditLog audit, HttpClient http, ILogger logger, string statePath)
	{
		while (true)
		{
			try
			{
				AppConfig cfg = getConfig();
				ModerationReportConfig mr = cfg.ModerationReport;
				if (mr?.Enabled ?? false)
				{
					DateTime now = DateTime.Now;
					string today = now.ToString("yyyy-MM-dd");
					string last = "";
					try
					{
						if (File.Exists(statePath))
						{
							last = File.ReadAllText(statePath).Trim();
						}
					}
					catch
					{
					}
					if (now.Hour >= mr.Hour && last != today)
					{
						string jid = ((!string.IsNullOrWhiteSpace(mr.GroupJid)) ? mr.GroupJid : cfg.AdminSyncGroupJid);
						if (!string.IsNullOrWhiteSpace(jid) && !Sleeper.Asleep)
						{
							string report = Build(audit, cfg, now.AddHours(-24.0));
							string payload = JsonSerializer.Serialize(new
							{
								jid = jid,
								text = report
							});
							using StringContent content = new StringContent(payload, Encoding.UTF8, "application/json");
							await http.PostAsync(ChannelRoute.BaseForJid(cfg, jid) + "/send", content);
							try
							{
								File.WriteAllText(statePath, today);
							}
							catch
							{
							}
							logger.LogInformation("Laporan moderasi harian terkirim ke {Jid}.", jid);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				logger.LogError("ModReport error: {M}", ex2.Message);
			}
			await Task.Delay(TimeSpan.FromMinutes(10L));
		}
	}

	public static string Build(AuditLog audit, AppConfig cfg, DateTime since)
	{
		List<string> list = audit.LinesSince(since);
		Dictionary<string, Dictionary<string, int>> dictionary = new Dictionary<string, Dictionary<string, int>>();
		int num = 0;
		int num2 = 0;
		foreach (string item in list)
		{
			string text = Extract(item, "grup=");
			string text2 = Extract(item, "aturan=");
			if (text.Length == 0)
			{
				continue;
			}
			if (text2.StartsWith("SHADOW:"))
			{
				num2++;
				continue;
			}
			if (!dictionary.TryGetValue(text, out var value))
			{
				value = (dictionary[text] = new Dictionary<string, int>());
			}
			value[text2] = value.GetValueOrDefault(text2) + 1;
			num++;
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("\ud83d\udcca *Laporan Moderasi 24 Jam*");
		stringBuilder.AppendLine($"Total dihapus: {num}" + ((num2 > 0) ? $" (+{num2} shadow)" : ""));
		if (num == 0)
		{
			stringBuilder.AppendLine("Tidak ada moderasi dalam 24 jam terakhir. \ud83d\udc4d");
			return stringBuilder.ToString().TrimEnd();
		}
		foreach (KeyValuePair<string, Dictionary<string, int>> item2 in dictionary.OrderByDescending((KeyValuePair<string, Dictionary<string, int>> g) => g.Value.Values.Sum()))
		{
			GroupConfig value3;
			string value2 = ((cfg.Groups.TryGetValue(item2.Key, out value3) && !string.IsNullOrWhiteSpace(value3.Label)) ? value3.Label : item2.Key);
			stringBuilder.AppendLine();
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(5, 2, stringBuilder2);
			handler.AppendLiteral("*");
			handler.AppendFormatted(value2);
			handler.AppendLiteral("* — ");
			handler.AppendFormatted(item2.Value.Values.Sum());
			stringBuilder3.AppendLine(ref handler);
			foreach (KeyValuePair<string, int> item3 in item2.Value.OrderByDescending((KeyValuePair<string, int> x) => x.Value))
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder4 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(6, 2, stringBuilder2);
				handler.AppendLiteral("  • ");
				handler.AppendFormatted(item3.Key);
				handler.AppendLiteral(": ");
				handler.AppendFormatted(item3.Value);
				stringBuilder4.AppendLine(ref handler);
			}
		}
		return stringBuilder.ToString().TrimEnd();
	}

	private static string Extract(string line, string key)
	{
		int num = line.IndexOf(key, StringComparison.Ordinal);
		if (num < 0)
		{
			return "";
		}
		int num2 = num + key.Length;
		int num3 = line.IndexOf(" | ", num2, StringComparison.Ordinal);
		if (num3 < 0)
		{
			num3 = line.Length;
		}
		return line.Substring(num2, num3 - num2).Trim();
	}
}
internal static class Announcer
{
	private static List<string> Targets(AnnouncerConfig a)
	{
		List<string> list = (a.GroupJids ?? Array.Empty<string>()).Where((string s) => !string.IsNullOrWhiteSpace(s)).ToList();
		if (list.Count == 0 && !string.IsNullOrWhiteSpace(a.GroupJid))
		{
			list.Add(a.GroupJid);
		}
		return list;
	}

	private static List<string> ResultTargets(AnnouncerConfig a)
	{
		List<string> list = (a.ResultsGroupJids ?? Array.Empty<string>()).Where((string s) => !string.IsNullOrWhiteSpace(s)).ToList();
		if (list.Count == 0 && !string.IsNullOrWhiteSpace(a.ResultsGroupJid))
		{
			list.Add(a.ResultsGroupJid);
		}
		return (list.Count > 0) ? list : Targets(a);
	}

	public static async Task RunLoop(Func<AppConfig> getConfig, HttpClient http, ILogger logger, string sentPath, string resultsPath)
	{
		HashSet<string> sent = LoadSent(sentPath);
		HashSet<string> resultsSent = LoadSent(resultsPath);
		if (!File.Exists(resultsPath))
		{
			try
			{
				AppConfig cfg0 = getConfig();
				AnnouncerConfig announcer = cfg0.Announcer;
				if (announcer != null && announcer.Enabled && announcer.ResultsEnabled && !string.IsNullOrWhiteSpace(cfg0.Announcer.TeamId))
				{
					foreach (SwissItem t in (await Fetch(cfg0, http, logger)).Where((SwissItem swissItem) => swissItem.Status == "finished"))
					{
						resultsSent.Add(t.Id);
					}
					SaveSent(resultsPath, resultsSent);
					logger.LogInformation("Results seed: {N} turnamen selesai ditandai (tidak dikirim).", resultsSent.Count);
				}
			}
			catch
			{
			}
		}
		while (true)
		{
			int poll = 5;
			try
			{
				AppConfig cfg1 = getConfig();
				AnnouncerConfig a = cfg1.Announcer;
				if (a != null && a.Enabled && Targets(a).Count > 0 && !string.IsNullOrWhiteSpace(a.TeamId))
				{
					poll = ((a.PollMinutes < 1) ? 1 : a.PollMinutes);
					if (!(cfg1.QuietHours?.SuppressReminders ?? true) || !QuietHours.IsActive(cfg1.QuietHours, DateTimeOffset.UtcNow))
					{
						await Tick(cfg1, http, logger, sent, sentPath, resultsSent, resultsPath);
					}
				}
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				logger.LogError("Announcer error: {Msg}", ex2.Message);
			}
			await Task.Delay(TimeSpan.FromMinutes(poll));
		}
	}

	private static async Task Tick(AppConfig cfg, HttpClient http, ILogger logger, HashSet<string> sent, string sentPath, HashSet<string> resultsSent, string resultsPath)
	{
		List<SwissItem> list = await Fetch(cfg, http, logger);
		DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
		int[] remindersMinutes = cfg.Announcer.RemindersMinutes;
		int[] reminders = ((remindersMinutes != null && remindersMinutes.Length > 0) ? cfg.Announcer.RemindersMinutes : new int[2] { 300, 15 });
		List<string> targets = Targets(cfg.Announcer);
		foreach (SwissItem t in list)
		{
			double mins = (t.StartsAt - nowUtc).TotalMinutes;
			int[] array = reminders;
			for (int i = 0; i < array.Length; i++)
			{
				int T = array[i];
				if (mins <= 0.0 || mins > (double)T || mins < (double)(T - 60))
				{
					continue;
				}
				string text = BuildText(cfg, t, T);
				foreach (string jid in targets)
				{
					string key = t.Id + "|" + T + "|" + jid;
					if (!sent.Contains(key) && await Send(http, ChannelRoute.BaseForJid(cfg, jid), jid, text, logger))
					{
						sent.Add(key);
						SaveSent(sentPath, sent);
						logger.LogInformation("Announcer: '{Name}' reminder {T} mnt -> {Jid}", t.Name, T, jid);
					}
				}
			}
		}
		if (!cfg.Announcer.ResultsEnabled)
		{
			return;
		}
		List<string> resTargets = ResultTargets(cfg.Announcer);
		int maxAge = ((cfg.Announcer.ResultsMaxAgeHours <= 0) ? 12 : cfg.Announcer.ResultsMaxAgeHours);
		foreach (SwissItem t2 in list)
		{
			if (t2.Status != "finished" || (nowUtc - t2.StartsAt).TotalHours > (double)maxAge || resultsSent.Contains(t2.Id))
			{
				continue;
			}
			Dictionary<string, string> urlMap = await CommandHandler.GetSwissUrlMap(http, logger);
			string msg = null;
			if (urlMap.TryGetValue(t2.Id, out string surl))
			{
				msg = await CommandHandler.BuildResultByUrl(surl, http, logger);
			}
			if (msg == null)
			{
				msg = await CommandHandler.BuildResultFromLichess(t2.Id, t2.Name, http, logger);
			}
			if (msg == null)
			{
				continue;
			}
			foreach (string jid2 in resTargets)
			{
				string rkey = t2.Id + "|" + jid2;
				if (!resultsSent.Contains(rkey) && await Send(http, ChannelRoute.BaseForJid(cfg, jid2), jid2, msg, logger))
				{
					resultsSent.Add(rkey);
					SaveSent(resultsPath, resultsSent);
					logger.LogInformation("Hasil otomatis terkirim: '{Name}' -> {Jid}", t2.Name, jid2);
				}
			}
			surl = null;
		}
	}

	public static async Task<List<SwissItem>> Fetch(AppConfig cfg, HttpClient http, ILogger logger)
	{
		List<SwissItem> result = new List<SwissItem>();
		string url = "https://lichess.org/api/team/" + cfg.Announcer.TeamId + "/swiss?max=30";
		using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, url);
		req.Headers.Add("Accept", "application/x-ndjson");
		using HttpResponseMessage resp = await http.SendAsync(req);
		if (!resp.IsSuccessStatusCode)
		{
			logger.LogWarning("Announcer: Lichess API {Code}", (int)resp.StatusCode);
			return result;
		}
		string body = await resp.Content.ReadAsStringAsync();
		string filter = cfg.Announcer.NameFilter;
		string[] array = body.Split('\n');
		foreach (string line in array)
		{
			string s = line.Trim();
			if (s.Length == 0)
			{
				continue;
			}
			JsonDocument doc;
			try
			{
				doc = JsonDocument.Parse(s);
			}
			catch
			{
				continue;
			}
			using (doc)
			{
				JsonElement r = doc.RootElement;
				JsonElement st;
				string status = (r.TryGetProperty("status", out st) ? (st.GetString() ?? "") : "");
				JsonElement idv;
				string id = (r.TryGetProperty("id", out idv) ? (idv.GetString() ?? "") : "");
				JsonElement nm;
				string name = (r.TryGetProperty("name", out nm) ? (nm.GetString() ?? id) : id);
				JsonElement sa;
				string startsAtStr = (r.TryGetProperty("startsAt", out sa) ? (sa.GetString() ?? "") : "");
				if (id.Length == 0 || !DateTimeOffset.TryParse(startsAtStr, null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var startsAt) || (!string.IsNullOrWhiteSpace(filter) && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0))
				{
					continue;
				}
				int limit = 0;
				int inc = 0;
				if (r.TryGetProperty("clock", out var ck))
				{
					if (ck.TryGetProperty("limit", out var lm))
					{
						limit = lm.GetInt32();
					}
					if (ck.TryGetProperty("increment", out var ic))
					{
						inc = ic.GetInt32();
					}
					lm = default(JsonElement);
					ic = default(JsonElement);
				}
				result.Add(new SwissItem
				{
					Id = id,
					Name = name,
					StartsAt = startsAt,
					ClockLimit = limit,
					ClockIncrement = inc,
					NbRounds = (r.TryGetProperty("nbRounds", out var nr) ? nr.GetInt32() : 0),
					Variant = (r.TryGetProperty("variant", out var vr) ? (vr.GetString() ?? "standard") : "standard"),
					Rated = (r.TryGetProperty("rated", out var rt) && rt.GetBoolean()),
					Status = status
				});
				st = default(JsonElement);
				idv = default(JsonElement);
				nm = default(JsonElement);
				sa = default(JsonElement);
				ck = default(JsonElement);
				nr = default(JsonElement);
				vr = default(JsonElement);
				rt = default(JsonElement);
			}
		}
		return result;
	}

	public static string BuildText(AppConfig cfg, SwissItem t, int T)
	{
		int hours = cfg.Announcer?.TimezoneOffsetHours ?? 7;
		string value = t.StartsAt.ToOffset(TimeSpan.FromHours(hours)).ToString("dd/MM HH:mm") + " WIB";
		string value2 = ((t.ClockLimit <= 0) ? "?" : ((t.ClockLimit % 60 == 0) ? $"{t.ClockLimit / 60}+{t.ClockIncrement}" : $"{(double)t.ClockLimit / 60.0:0.#}+{t.ClockIncrement}"));
		string value3 = (t.Variant.Equals("standard", StringComparison.OrdinalIgnoreCase) ? "" : (" · " + t.Variant));
		string value4 = (t.Rated ? " · Rated" : "");
		string value5 = "https://lichess.org/swiss/" + t.Id;
		string text = ((T >= 60) ? $"☝\ud83c\udffb dimulai sekitar {T / 60} jam lagi!" : $"☝\ud83c\udffb dimulai {T} menit lagi!");
		return $"\ud83c\udfc6 *{t.Name}*\n\n\ud83d\udd52 {value}\n♟\ufe0f {value2} · {t.NbRounds} ronde Swiss{value3}{value4}\n\ud83d\udd17 {value5}\n\n" + "Pastikan sudah gabung tim *Liga Catur Indonesia* di Lichess dan join turnamennya sebelum mulai.\n\nSampai jumpa di papan! ♟\ufe0f\u001f" + text;
	}

	private static async Task<bool> Send(HttpClient http, string gatewayUrl, string jid, string text, ILogger logger)
	{
		if (Sleeper.Asleep)
		{
			return false;
		}
		try
		{
			string payload = JsonSerializer.Serialize(new { jid, text });
			using StringContent content = new StringContent(payload, Encoding.UTF8, "application/json");
			return (await http.PostAsync(gatewayUrl + "/send", content)).IsSuccessStatusCode;
		}
		catch (Exception ex)
		{
			logger.LogError("Announcer send gagal: {Msg}", ex.Message);
			return false;
		}
	}

	private static HashSet<string> LoadSent(string path)
	{
		try
		{
			return File.Exists(path) ? (JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(path)) ?? new HashSet<string>()) : new HashSet<string>();
		}
		catch
		{
			return new HashSet<string>();
		}
	}

	private static void SaveSent(string path, HashSet<string> sent)
	{
		try
		{
			File.WriteAllText(path, JsonSerializer.Serialize(sent));
		}
		catch
		{
		}
	}
}
internal class SwissItem
{
	public string Id { get; set; } = "";

	public string Name { get; set; } = "";

	public DateTimeOffset StartsAt { get; set; }

	public int ClockLimit { get; set; }

	public int ClockIncrement { get; set; }

	public int NbRounds { get; set; }

	public string Variant { get; set; } = "standard";

	public bool Rated { get; set; }

	public string Status { get; set; } = "";
}
internal static class ConfigStore
{
	private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true
	};

	public static AppConfig LoadConfig(string dir)
	{
		string path = Path.Combine(dir, "config.json");
		AppConfig appConfig = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), JsonOpts) ?? new AppConfig();
		ApplySecrets(dir, appConfig);
		return appConfig;
	}

	private static void ApplySecrets(string dir, AppConfig cfg)
	{
		try
		{
			string path = Path.Combine(dir, "secrets.json");
			if (File.Exists(path))
			{
				using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(path));
				JsonElement rootElement = jsonDocument.RootElement;
				if (rootElement.TryGetProperty("adminApiToken", out var value))
				{
					string text = value.GetString();
					if (text != null && text.Length > 0)
					{
						cfg.AdminApiToken = text;
					}
				}
				if (rootElement.TryGetProperty("broadcastToken", out var value2))
				{
					string text2 = value2.GetString();
					if (text2 != null && text2.Length > 0)
					{
						cfg.BroadcastToken = text2;
					}
				}
				if (rootElement.TryGetProperty("wabotToken", out var valueWb))
					{
						string textWb = valueWb.GetString();
						if (textWb != null && textWb.Length > 0)
						{
							cfg.WabotToken = textWb;
						}
					}
					if (rootElement.TryGetProperty("dbConnectionString", out var value3))
				{
					string text3 = value3.GetString();
					if (text3 != null && text3.Length > 0)
					{
						cfg.DbConnectionString = text3;
					}
				}
			}
		}
		catch
		{
		}
		string environmentVariable = Environment.GetEnvironmentVariable("WABOT_ADMIN_TOKEN");
		string environmentVariable2 = Environment.GetEnvironmentVariable("WABOT_BROADCAST_TOKEN");
		string environmentVariable3 = Environment.GetEnvironmentVariable("WABOT_DB_CONN");
		if (!string.IsNullOrEmpty(environmentVariable))
		{
			cfg.AdminApiToken = environmentVariable;
		}
		if (!string.IsNullOrEmpty(environmentVariable2))
		{
			cfg.BroadcastToken = environmentVariable2;
		}
		if (!string.IsNullOrEmpty(environmentVariable3))
		{
			cfg.DbConnectionString = environmentVariable3;
		}
	}

	public static List<Rule> LoadRules(string dir, ILogger logger)
	{
		string path = Path.Combine(dir, "rules.json");
		using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(path));
		List<Rule> list = new List<Rule>();
		if (!jsonDocument.RootElement.TryGetProperty("rules", out var value))
		{
			return list;
		}
		foreach (JsonElement item in value.EnumerateArray())
		{
			Rule rule = item.Deserialize<Rule>(JsonOpts);
			if (rule != null && !string.IsNullOrEmpty(rule.Pattern))
			{
				RegexOptions regexOptions = RegexOptions.CultureInvariant;
				if ((rule.Flags ?? "").Contains('i'))
				{
					regexOptions |= RegexOptions.IgnoreCase;
				}
				try
				{
					rule.Compiled = new Regex(rule.Pattern, regexOptions);
					list.Add(rule);
				}
				catch (Exception ex)
				{
					logger.LogError("Pola regex tidak valid pada aturan {Id}, dilewati: {Msg}", rule.Id, ex.Message);
				}
			}
		}
		return list;
	}
}











