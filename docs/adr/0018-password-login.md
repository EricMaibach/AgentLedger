# 0018. Password login: own User aggregate, deny by default, first-run setup page

- **Status:** Accepted
- **Date:** 2026-10-22

## Context

The web UI ([ADR 0015](0015-web-ui-and-evidence.md)) and the JSON read API ([ADR 0017](0017-api-prefix-for-read-endpoints.md)) show everything agents recorded, including conversation text. They must not be released without login (roadmap, Phase 2). AgentLedger is self-hosted, mostly for personal setups, so first-time setup and recovery must be easy without a mail server or external identity provider. API keys (step 3), HTTPS (step 4) and GitHub login (step 5) come later.

## Decision

**Users and passwords**
- **Our own `User` aggregate**, not full ASP.NET Core Identity: one `users` table (ID, username, password hash, security stamp, admin flag, failed sign-in count, lockout end). Full Identity would add about seven `AspNet*` tables we don't use and a framework user type outside our domain model.
- Security-critical parts come from Microsoft: **`PasswordHasher<T>`** (Identity's PBKDF2 hasher, which upgrades old hashes on sign-in) and **cookie authentication**. No crypto of our own.
- Usernames are case-insensitive.
- **Password rules** (NIST SP 800-63B): 15 to 128 characters, any characters including spaces, no composition rules, no forced periodic changes.

**What requires login: deny by default**
- A **fallback authorization policy** requires a signed-in user everywhere; only explicitly anonymous endpoints are open. A page or endpoint added later is protected unless someone deliberately opens it.
- **Open:** `POST /events` (until ingest keys, step 3: the CLI can't sign in), `/status` (name and version only), the login and setup pages, the not-found page, static assets.
- **Protected:** every page, and `/api/…` (signed-in browsers now; read keys in step 3).
- Not signed in: pages **redirect to `/login`** (and back afterwards); `/api` returns **401**, never the login page's HTML.

**First admin: a time-limited setup page**
- While there are no users, pages go to **`/setup`**, which creates the first admin (username and password).
- Setup **closes 10 minutes after startup** if not completed ("restart the container to try again"), and is gone for good once a user exists. This is Portainer's approach: familiar to self-hosters, and it limits the window in which someone else on the network could claim the instance.
- Rejected: a default password (widely considered unsafe, and banned for connected devices in some jurisdictions); container variables (the password stays in the stack configuration); a one-time code in the container log (safe, but unfamiliar to the people this is for).

**Signing in and staying signed in**
- `/login` is a static SSR form with antiforgery. Wrong username and wrong password get the same message, and a password check runs even for an unknown username, so neither the message nor the timing reveals which usernames exist.
- The cookie lasts **7 days, sliding** (renewed on use); `HttpOnly`, `SameSite=Lax`, `Secure` once served over HTTPS. No "remember me".
- **Sign out** is a POST (a form button), not a link.
- A **security stamp** changes with the password (and when a user is disabled); cookies are re-validated against it every few minutes, so a password change or reset ends other sessions.
- **Lockout:** 5 wrong passwords in a row lock that user for 15 minutes. **Rate limit:** at most 10 login attempts per minute per IP address.

**Passwords after setup**
- Every user can change their own password (current password required).
- An admin can reset another user's password to a temporary one that must be changed at next sign-in (step 2b).
- The admin's own forgotten password: a command in the container, `reset-password <username>`, which prints a new random password. Only someone with Docker access on the host can run it.
- No emailed reset links: they need SMTP settings, and the two paths above cover the cases.

**More than one user (step 2b)**
- Two roles: **member** (reads everything; one installation's data is not partitioned per user) and **admin** (also manages users).
- `/settings/users`: add a user (temporary password), reset a password, disable or enable (never delete: this is an audit ledger), make or remove an admin. The last admin can't be disabled or demoted.

**Network exposure**
- The stack's port is published on **`127.0.0.1` by default** (a bind-address variable in `deploy/compose.yml`), so a fresh install isn't reachable from the network. Traffic that never leaves the machine can't be sniffed, so plain HTTP is acceptable there.
- Reaching the ledger from other machines is an explicit choice that needs HTTPS (step 4: reverse-proxy support plus recipes such as Tailscale or Caddy).
- The author's own setup binds to Docker's bridge address (`172.17.0.1`), so the dev container can reach it via `host.docker.internal` while the home network can't.

**Order:** 2a (setup, login, sign-out, change own password, `reset-password`) → release with the UI → 2b (user management) → API keys → HTTPS → GitHub login.

## Consequences

- One small table and a few use cases instead of the Identity framework; the user is a normal aggregate with unit-tested rules (lockout, password changes, last-admin guard).
- Lockout can be abused to lock a known user out for 15 minutes; accepted, as is usual for lockout, and `reset-password` still works.
- Someone on the network could complete setup first within the 10-minute window; you'd notice immediately (your setup fails), and `reset-password` plus a restart recovers.
- Until HTTPS exists, the ledger must only be reachable locally; the default binding enforces that for new installs.
- `/api` is usable only from a signed-in browser until read keys exist (step 3).
