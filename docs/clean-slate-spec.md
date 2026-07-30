# Clean Slate — Schedule I Storefront Mod

**Tagline:** Storefronts, legit business, and clean money.
**Type:** Schedule I content mod (MelonLoader / C#, IL2CPP + Mono builds)
**Status:** Spec / pre-development
**Author:** Legion

---

## One-line pitch

Convert street dealing into a legitimate retail operation: player-owned storefronts where dealers work the counter, customers walk in to buy, product is delivered to the store via a dock, and each business runs its own rent-vs-income economy with an optional "run profits through the books" laundering toggle.

---

## Core Fantasy

Going legit. The player stops slinging on corners and starts running actual businesses — storefronts with staff, inventory, foot traffic, overhead, and clean books. It's the tycoon/retail layer on top of the drug empire, and the name cuts both ways: a fresh start *and* clean money.

---

## Core Mechanics

### 1. Storefronts (player-owned businesses)
- Player can own one or more storefronts. Design supports **one per zone** (Downtown, Suburbia, Uptown, Westville, Northtown, etc.), but MVP may ship with a single store.
- Each storefront is a physical building the player buys/unlocks.
- Each storefront has:
  - **A set weekly rent** (fixed cost, the pressure)
  - **An income potential** (the payoff, driven by sales)
  - **Product storage** on-site (dealers pull from this to fill orders)
  - **One or more dealer counter positions**

### 2. Dealers as counter staff (bar-style)
- Dealers are assigned to a storefront counter, working it like a bartender works a bar.
- Customers who would normally street-deal with that dealer instead **walk into the storefront and approach the counter**.
- Transaction loop per customer:
  1. Customer enters, walks to the counter, places an order.
  2. Dealer walks to on-site product storage, picks up the ordered product.
  3. Dealer walks back to the counter, delivers to the customer, collects cash.
  4. ~30 seconds per deal (tunable).
- **Safety:** storefront sales are safe. Benzies / muggers can't rob the storefront. (Street risk removed for this channel — legit selling is the low-risk, high-volume path.)

### 3. Cash & product handling
- Dealers **pick up their product allotment when they arrive at the office/store** (start of shift).
- Dealers **deposit collected cash into the store safe at night** (end of shift).
- Player collects from the safe (or it auto-sweeps — see laundering toggle).

### 4. Delivery logistics (the supply side)
- Product reaches the storefront via a **delivery system**: a **dock** and **handlers** move product from the player's production/stockpile to the storefront's on-site storage.
- This mirrors the existing manor/handler/dock patterns in the base game — needs design work to define how handlers route product to storefronts (see Open Questions).

### 5. Economy: rent + income per business
- Each business has **its own set rent amount and income amount.**
- Rent is charged weekly (recurring cost).
- Income is driven by sales volume, which is **much higher than street dealing** (the volume incentive to go legit).
- Net profit per business = income − rent. Player manages a portfolio of businesses, each with its own margin.

### 6. Laundering toggle (the "clean money" mechanic)
- A **toggle per business (or global)** to route profits **through the bank/wallet** — i.e., clean money vs. dirty cash.
- This is the thematic core of "Clean Slate": legit storefront income can be run through the books as clean money.

### 7. Weekly mixed-product specials
- Each week, the game **randomly selects a "special"** — a mixed-product promotion.
- Specials drive variety and give the player a reason to keep stock diverse (ties into the mixing/stockpile strategy the base game rewards).
- Randomized weekly so it stays fresh.

---

## MVP (v1 — smallest shippable version)

Goal: prove the core loop with the least surface area.

- **1 storefront** (single location, not yet per-zone)
- **1 dealer** assigned to the counter
- **Walk-in customer loop**: customer enters → orders → dealer fetches from on-site storage → delivers → collects cash (~30s/deal)
- **On-site product storage** the dealer pulls from
- **Manual restock** (player drops product into store storage — dock/handler automation deferred)
- **Flat weekly rent + sales income** per store
- **Night safe deposit** of collected cash
- **Storefront sales are robbery-safe**

MVP explicitly defers: multi-zone, handler/dock delivery automation, laundering toggle, weekly specials. Those are v2+.

---

## Future Scope (v2 and beyond)

- **Multi-zone storefronts** — one per zone, each with zone-appropriate customer wealth/traffic
- **Delivery automation** — dock + handlers routing product from production to storefronts (no manual restock)
- **Laundering toggle** — route profits through bank/wallet as clean money
- **Weekly randomized mixed-product specials**
- **Multiple dealers per store** / staffing depth
- **Dealer skill / storefront upgrades** affecting throughput or income
- **Storefront customization** (build-out, décor, capacity)
- **Scaling rent** (better/bigger locations cost more)
- **Reputation / heat mechanics** for the legit channel

---

## Open Questions / Design TODO

1. **Handler → storefront routing:** how do handlers/dock deliver to a storefront's on-site storage? Reuse manor handler logic, or new system? (Biggest unknown.)
2. **Customer diversion:** mechanically, how are a dealer's assigned customers redirected from street deals to storefront walk-ins? Does the base game's customer/deal system allow intercepting this cleanly?
3. **Per-zone vs. single store for MVP** — confirm MVP is single-store.
4. **Rent charging:** tie into the existing weekly cycle? What happens on non-payment (eviction? debt?)?
5. **Laundering:** per-business toggle or global? How does "through the bank" interact with the base game's existing laundering mechanics?
6. **Specials:** how is the "special" selected and surfaced to the player? UI/notification?
7. **Relationship to OTC (OverTheCounter):** OTC covered similar ground (storefronts, budtenders, Vic-laundering) but is abandoned/version-broken. Decide: from-scratch clean take, or reference its approach? Position Clean Slate as the maintained, current-version storefront mod.
8. **Base-game version target:** build against current beta (0.4.6f9) from the start to avoid OTC's version-drift fate.

---

## Task Breakdown (draft — for Commander dockets)

**Epic: Clean Slate mod**

- **Docket: Research & setup**
  - Stand up mod project (MelonLoader, S1API deps, IL2CPP+Mono builds)
  - Audit base-game NPC/deal/customer systems for diversion hooks
  - Audit handler/dock/storage systems for reuse
  - Study OTC source for approach reference (already audited clean)

- **Docket: Storefront core**
  - Define storefront building/ownership
  - On-site product storage
  - Buy/unlock flow

- **Docket: Dealer-as-counter loop**
  - Assign dealer to counter
  - Customer walk-in → order → fetch → deliver → collect (~30s)
  - Divert assigned customers from street to storefront

- **Docket: Cash & shift handling**
  - Dealer picks up product on arrival
  - Dealer deposits cash to safe at night
  - Player collection from safe

- **Docket: Economy**
  - Per-business rent (weekly charge)
  - Per-business income from sales
  - Robbery-safe storefront sales

- **Docket: Delivery (v2)**
  - Dock + handler routing to storefront storage

- **Docket: Laundering toggle (v2)**
  - Route profits through bank/wallet

- **Docket: Weekly specials (v2)**
  - Random weekly mixed-product special + surfacing

- **Docket: Ship**
  - Test on Vortex against 0.4.6f9
  - Thunderstore publish (credit conventions, descriptive tagline for discoverability)
  - Community funnel note: warm audience for Poker Defense
