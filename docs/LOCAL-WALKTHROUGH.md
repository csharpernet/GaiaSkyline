# Run GaiaSkyline on your own machine — a click-through guide

A plain-language guide to start the whole site locally and try **every** feature yourself — no developer
needed. Nothing here touches the internet: payments are in Stripe **test** mode, email goes to a local
inbox, and there is no real domain. For the technical operations guide see [runbook.md](runbook.md).

---

## 1. What you need (one-time install)

Open **PowerShell** and install these (copy–paste each line):

```powershell
# .NET 10 SDK — the app's runtime (if "dotnet --version" already prints 10.x you can skip)
winget install Microsoft.DotNet.SDK.10

# SQL Server LocalDB — the local database (ships with this; if missing:)
winget install Microsoft.SQLServer.2022.LocalDB

# smtp4dev — a fake inbox that catches the site's emails
dotnet tool install -g Rnwood.Smtp4dev

# (optional) Node.js — only needed if you want to re-run the automated browser tests
winget install OpenJS.NodeJS.LTS

# (optional) Stripe CLI — only needed to complete real test-card payments (section 5)
winget install Stripe.StripeCLI
```

You also need an **authenticator app** on your phone (Microsoft Authenticator, Google Authenticator, 1Password…)
for the owner login's one-time code.

---

## 2. Start and stop

**Start everything with one command** (from the project folder):

```powershell
powershell -ExecutionPolicy Bypass -File tools/run-local.ps1
```

It configures a local owner login, starts the email inbox, and runs the site. The first start takes a minute
(it creates the database and loads the sample content). When it prints **"Now listening on https://localhost:7020"**
you're ready. It also prints your owner login and the key addresses.

**Stop everything:** click the PowerShell window and press **Ctrl+C**. Close the minimized smtp4dev window.

> Your browser may warn about the local HTTPS certificate the first time — choose **Advanced → continue**. To
> remove the warning once: `dotnet dev-certs https --trust`.

---

## 3. The local addresses

| What | Address |
| --- | --- |
| Public site (English) | <https://localhost:7020/en> |
| Other languages | `/pt-pt` · `/es` · `/fr` · `/de` |
| Book a stay | <https://localhost:7020/en/book> |
| Owner admin | <https://localhost:7020/admin> |
| Background jobs (Hangfire) | <https://localhost:7020/hangfire> (owner-only) |
| **Email inbox** (smtp4dev) | <http://localhost:5000> |

---

## 4. Logging in

### Owner (you)
The start script seeds a local owner:

- **Email:** `owner@gaiaskyline.local`  **Password:** `LocalDev!2026`

Go to `/admin`, sign in, and on first login it asks you to **set up two-factor**: scan the QR code with your
authenticator app and **save the 10 recovery codes** it shows. From then on, login = email + password + the
6-digit code from the app. (Change the password any time: `dotnet user-secrets --project src/GaiaSkyline.Web set "Owner:Password" "your-new-one"`, then restart.)

### A test partner (influencer)
1. Open `/en/partners/apply` and submit the short form (any name + a **different** email, e.g. `partner@example.com`).
2. As owner, go to `/admin/partners` → **Applications**, open it, and **Approve**. This emails an invite.
3. Open the **email inbox** (<http://localhost:5000>), find the invite, click the join link, set a password,
   accept the terms, and add payout details (any IBAN-shaped value, e.g. `PT50 0002 0123 1234 5678 9015 4`).
4. The partner can now sign in and see `/en/partners/dashboard` (stats, bookings, payouts, the referral link,
   the media-kit download).

### A test guest
You don't create guests — anyone who books is a guest. To manage a booking later, a guest requests a
**magic link** (section 6, "Manage a booking"): the link arrives in the email inbox, no password needed.

---

## 5. Turn on card payments (optional, for the payment steps)

Payments run in Stripe **test mode** — no real money. You need your own Stripe **test** keys (free Stripe
account → Developers → API keys → the `pk_test_…` / `sk_test_…` pair):

```powershell
dotnet user-secrets --project src/GaiaSkyline.Web set "Stripe:PublishableKey" "pk_test_…"
dotnet user-secrets --project src/GaiaSkyline.Web set "Stripe:SecretKey"      "sk_test_…"
```

Payment **confirmation** arrives by webhook, so in a **second** PowerShell window run the Stripe CLI to relay
test events to the site, then save the secret it prints and restart the app:

```powershell
stripe login
stripe listen --forward-to https://localhost:7020/webhooks/stripe
# it prints "whsec_…" — put it in secrets, then restart run-local.ps1:
dotnet user-secrets --project src/GaiaSkyline.Web set "Stripe:WebhookSecret" "whsec_…"
```

**Test cards** (any future expiry, any CVC, any postcode):

| Card number | What happens |
| --- | --- |
| `4242 4242 4242 4242` | pays immediately ✅ |
| `4000 0025 0000 3155` | asks for 3-D Secure, then pays |
| `4000 0000 0000 0002` | declined |

> If you skip this section you can still browse everything and start a booking; only the final payment step
> needs the keys.

---

## 6. The click-through checklist

Tick these off to exercise the whole product.

### Browse the public site
- [ ] Open `/en` and scroll the home page (hero, gallery, reviews, stories, map).
- [ ] Switch languages with the picker (or edit the URL): **`/pt-pt`, `/es`, `/fr`, `/de`** — text, dates and
      prices change per language.
- [ ] Open **Gallery**, **Stories** (open one story), and **Partners** (`/en/partners`).

### Make a booking (card)
- [ ] Go to `/en/book`, pick a check-in and check-out on the calendar and set guests — a live **price quote**
      appears.
- [ ] Click **Reserve**, fill in name / email / phone, and pay with **`4242 4242 4242 4242`**.
- [ ] You land on the **confirmation page** with a **Download PDF** link — open it: it's the branded
      confirmation/receipt, and it says *"not a tax invoice"* in the footer.
- [ ] Open the **email inbox** (<http://localhost:5000>): the guest **confirmation email** is there with the
      **PDF attached**; a separate owner-notification email arrived too.

### The other payment paths
- [ ] Book again and pay with the **3-D Secure** card `4000 0025 0000 3155` — complete the challenge popup.
- [ ] Book again with the **declined** card `4000 0000 0000 0002` — you see a clear error; then switch to
      `4242…` and it succeeds.
- [ ] **Multibanco:** book dates **at least 10 days out**; at payment choose **Multibanco** — you get a voucher
      (Entity + Reference) and the booking stays *awaiting payment*. To simulate the voucher being paid, in the
      Stripe CLI window run `stripe trigger payment_intent.succeeded`; the booking then confirms and the
      Multibanco email appears in the inbox.

### Manage a booking (as the guest, via magic link)
- [ ] On a confirmation page (or at `/en/my/booking/<reference>`) choose to **manage / sign-in link**, enter the
      guest email, and submit.
- [ ] Open the inbox, click the **magic link** — you now see the booking without a password.
- [ ] **Cancel** it. If the cancellation policy grants a refund, it's issued automatically; a
      **cancellation/refund PDF** and email follow (check the inbox).

### Refund from the admin
- [ ] As owner at `/admin/bookings`, open a paid booking → **Refund** (full or partial), or **Cancel** (issues
      the policy refund and frees the dates). Both send the guest the refund email with its PDF.

### Edit content in another language
- [ ] At `/admin/content`, pick a text block, edit its **Portuguese** (or ES/FR/DE) version and save.
- [ ] View the matching public page at `/pt-pt/…` — your edit shows. (Untranslated blocks fall back to English.)

### Replace a photo & upload a hero video
- [ ] At `/admin/media`, **replace** one of the gallery images with a new file. Reload the public gallery — the
      new picture shows immediately (the image web-address gets a new version tag so caches don't serve the old
      one).
- [ ] In the media/hero area, **upload a hero video**. Renditions are produced locally by **FFmpeg** — install
      it first (`winget install Gyan.FFmpeg`) or this step is skipped with a clear message.

### Calendar & prices
- [ ] At `/admin/calendar`, add an **external booking block** for a few dates (something booked on Airbnb you
      must not double-book). Those dates now show unavailable on `/en/book`.
- [ ] At `/admin/prices`, set a **nightly rate** for a date range in the rates grid. Get a quote for those
      dates on `/en/book` — the new price is used.

### The owner dashboard & manual-sync to-do
- [ ] Open `/admin`. See the KPI tiles, the month calendar, and — because direct bookings must be mirrored in
      the channel manager — the **"Mirror in Hostify"** to-do list and the **"Manual sync overdue"** health tile.
- [ ] Mark one item **done** — it clears from the list.

### The partner referral → commission loop
- [ ] Sign in as your **test partner** (`/en/partners/dashboard`) and copy the **referral link** (it ends in
      `?ref=THEIRCODE`).
- [ ] Open that link in a **private/incognito** window (so it's a fresh visitor), then make a booking and pay.
- [ ] Back as owner at `/admin/partners`, open the partner — the booking is **attributed** and a **commission**
      is recorded (its basis = total minus tourist tax and cleaning). Commissions become payable 30 days after
      check-out; you can also open **Payouts** to see how a monthly statement would be generated and download
      the branded PDF.

### (Optional) run the automated checks
- [ ] `powershell -ExecutionPolicy Bypass -File tools/run-ci-gates.ps1` runs the same build, tests, Lighthouse,
      Playwright + axe and k6 that CI runs — a full local health check.

---

## 7. Known local-only limitations

- **No cloud.** No real domain, CDN, cloud storage, Key Vault or Application Insights. Media is on local disk;
  the web-vitals/alerts pipeline is a no-op locally. Going live is the runbook's **Deployment phase**.
- **Email stays local.** Every email lands in smtp4dev (<http://localhost:5000>), never a real inbox. Email
  links point at `https://localhost:7020` because the start script sets `Email:SiteBaseUrl` to it — if you run
  on a different port, pass `-Url` to the script.
- **Payments are test-mode.** No real charges. Completing a payment needs your Stripe test keys **and** the
  Stripe CLI running (section 5); without them you can browse and start a booking but not finish paying.
- **Hero video needs FFmpeg** installed locally (`winget install Gyan.FFmpeg`).
- **First page can be slow on some PCs.** A few machines hit a ~25-second stall on the very first page render
  (a Windows LocalDB quirk, not the app). If it happens, reboot and re-run; details in
  [runbook.md](runbook.md) → "Local LocalDB health."
- **Deployed-site Lighthouse and live alerts** are measured/enabled only after deployment (Deployment phase).
