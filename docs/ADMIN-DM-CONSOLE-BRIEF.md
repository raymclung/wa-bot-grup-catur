# HANDOFF BRIEF — WABot "Judit Polica" Admin DM Console

> Author: Chacha (architect) → Ray (implementation)
> Date: 2026-06-28
> Status: ready to start. **Do Step 0 before building anything.**

## Goal

Let a **small set of authorized admins** instruct Judit **privately via direct message**
(natural language, no `!commands`), and have her carry out actions in the WhatsApp groups.
Non-authorized people who DM her must be **ignored** (as today). This replaces unnatural
public `!commands` in the groups — a real assistant takes private instructions.

## Stack context (don't re-derive)

- **Gateway** (Node, `gateway/src/index.js`) = thin WhatsApp adapter via Baileys (linked
  device). Receives messages, calls the brain, sends replies.
- **Brain** (C#, `brain/`) = all logic. Gateway POSTs inbound to the brain's `/incoming`;
  brain decides and calls back gateway endpoints (`/send`, etc.).
- **AI** = local Ollama (`Ai.Ask`), used for **flavor / intent only**, never for executing
  correctness-sensitive actions.

## Verified current state (tested 2026-06-28 via gateway chat log — do NOT re-test blindly)

1. ✅ **Inbound DM works.** A 1:1 DM from an admin *is received and logged* by the gateway
   (`chatLog('IN', …)` at `gateway/src/index.js:688-691`, fires before any filter).
2. ✅ **LID→phone resolution works.** The DM arrived addressed by `@lid`
   (e.g. `59116080869380@lid`) and the gateway **correctly resolved it to the real phone**
   (`resolvePhone` / `refreshGroupLidMap`, around `index.js:735-740`). So we *can* identify
   the human behind a DM.
3. ❌ **No reply today — by config, NOT a bug.** The DM gate at `index.js:694-703` skips DMs
   unless the sender is in the private-chat allowlist. `gateway/config.json` has
   `privateChatAllowNumbers: []` (empty) → `privateChatAllowed()` is false → `"DM dilewati"`
   → `continue`, brain never runs.
4. ⚠️ **UNTESTED: the reply-back path.** Because the brain never ran, we have NOT confirmed
   whether the gateway's `/send` to an `@lid` 1:1 chat succeeds or hits the known
   **Baileys 6.7.0 LID outbound bug**. **This is the first thing to resolve.**

## STEP 0 — Resolve the open question before building anything

Add the admin's phone (`18284130303`) to `privateChatAllowNumbers`, `POST /reload`, have the
admin DM Judit once, and watch:

- **Judit replies in the DM** → outbound-to-`@lid` works → 1:1 DM console is viable. Proceed.
- **Typing-then-nothing / silence** → LID outbound bug confirmed → **fall back to a private
  "admin group" console** (same logic, but Judit replies into a group JID, which is not
  affected by the bug).

Respect the project rule: **max 1 real WhatsApp send per change, no rapid restarts.**

## Requirements

### A. Authorized-admin list (the gatekeeper — build this carefully)

- A dedicated config array of **admin phone numbers** (canonical identity), e.g.
  `dmAdmins: ["18284130303", ...]`. Do **not** authorize by `@lid` directly (resolve incoming
  `@lid` → phone first, then match against this list).
- Only senders whose resolved phone is in `dmAdmins` may issue commands. Everyone else's DM is
  ignored (current behavior preserved).
- Keep this list **separate** from `privateChatAllowNumbers` conceptually: "allowed to DM at
  all" vs "allowed to *command*". (You can let `dmAdmins` imply private-chat-allowed.)
- Edge case: if `@lid → phone` resolution fails for a sender, treat as **NOT authorized**
  (fail closed).

### B. Control pipeline (NL in → action out) — keep correctness OFF the LLM

```
admin DM (natural language)
  → LLM parses INTENT ONLY → structured action {verb, targetGroup, targetUser, params}
    → AUTH: resolved phone ∈ dmAdmins?  (fail closed)
      → CONFIRM if action is destructive (kick/delete/broadcast)
        → DETERMINISTIC execution (the real action via existing gateway endpoints)
          → AUDIT log (who / what / which group / when)
```

- The LLM may *interpret* ("keluarkan si spammer di grup pagi" → `{verb:kick, targetGroup:"pagi", ...}`)
  but must **never directly fire** a destructive action. A deterministic C# layer validates +
  executes.
- **Target-group resolution:** support group aliases/names; if ambiguous, Judit **asks**
  (natural, human).
- **Confirmation:** risky verbs require a yes/no before executing. (If reply-back DM is broken
  per Step 0, the confirm must happen wherever Judit *can* write — i.e., the admin group.)

### C. Audit trail

- Log every admin instruction and the action taken (who, what, target group, timestamp).
  Actions happen *out of sight of the group*, so this is mandatory for accountability and
  dispute resolution.

## Constraints & safety

- **Host-can-read caveat:** the bot phone is hosted by a third party until Chacha self-hosts
  (~2 weeks). The primary device can read everything Judit sees, including admin DMs.
  **Until self-hosting, do not route secrets/PII through DM**, and treat the channel as
  non-private. Re-evaluate after self-host + a possible Baileys upgrade.
- **Keep the deterministic-core principle** (same lesson as the puzzle wrong-answer fix):
  AI at the edges, real logic in the middle.
- **Persona:** in DM/console, Judit stays in the "Polisi Liga Catur" / POLIKA persona —
  natural, asks when unsure.
- **No over-testing WhatsApp** (account ban risk): one deliberate send per change.

## Code anchors

| What | Location |
|------|----------|
| DM gate / allowlist | `gateway/src/index.js:694-703` |
| Inbound logging (proof of receipt) | `gateway/src/index.js:688-691` |
| LID→phone resolution | `gateway/src/index.js:735-740` |
| Brain handoff (`postBrain('/incoming', {…})`) | `gateway/src/index.js:771` |
| Outbound (`app.post('/send', …)`) | `gateway/src/index.js:893` |
| Config | `gateway/config.json` (`moderateGroupsOnly`, `allowPrivateChat`, `privateChatAllowNumbers`) |
| Brain `/incoming` handler | `brain/Program.cs` |

Note: the brain handoff at `index.js:771` already passes `participantPhone`, `participant`,
and `pushName` — so the brain already has what it needs to authorize the sender.

## Definition of done

1. Step 0 answered (DM reply-back works, or fall back to admin group).
2. `dmAdmins` allowlist enforced (fail-closed on unknown/unresolved sender).
3. One **non-destructive** action works end-to-end from a private NL instruction
   (e.g. "post a puzzle to group X" or "show standings of group Y").
4. One **destructive** action works **with a confirmation step** (e.g. kick/delete).
5. Every action is audit-logged.
6. Non-admins DMing Judit are still silently ignored.

## Suggested phasing

1. **PoC:** Step 0 + `dmAdmins` allowlist + one read-only action (e.g. "show standings").
   No LLM yet — accept a simple structured phrasing to prove the plumbing.
2. **NL layer:** add LLM intent-parsing at the input edge → structured action.
3. **Destructive actions:** add confirmation + audit log.
4. **Polish:** group-alias resolution, ambiguity questions, persona wording.
