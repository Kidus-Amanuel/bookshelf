# BookShelf — System Design & Flow

## 0. What you actually described

One platform, two channels (online store + physical/local store), three kinds of participants, and two revenue models (one-off sale/loan, plus subscription). That's a real bookstore-meets-library system. The tricky part isn't the entities — it's the **role model**, because "everyone signs up as a customer, then some switch to author, but admins are invited and are never customers" doesn't fit cleanly into your current single `RoleId` on `User`. I've redesigned that part; everything else extends what you already have.

---

## 1. Roles — redesigned

Your current model: `User.RoleId → Role`. That's a **single, exclusive role**. It breaks the moment a Customer becomes an Author but should *keep* being a Customer (still buying/loaning books).

**Fix: separate "account type" from "capabilities."**

| Concept | How it's modeled | Exclusive? |
|---|---|---|
| Customer | Default state of every signed-up `User`. Not even a flag — it's just what a `User` *is* by default. | — |
| Admin | `Role.Name = "Admin"` on `User.RoleId`. Created **only** via invite. Can never also be a customer-flow user. | Yes — separate signup path entirely |
| Author | **Not a role.** A `AuthorProfile` record (1:1, optional) attached to a Customer `User`. Having one grants author capabilities on top of normal customer capabilities. | No — additive |

So `Role` table shrinks to just `Customer` and `Admin` (keep it for admin-vs-customer distinction and future roles like `Staff`, see §7). "Author" stops being a role and becomes a capability check: `user.AuthorProfile != null && AuthorProfile.Status == Approved`.

This is the single most important change — get this wrong and you'll be fighting your own schema every time a user needs to be "customer AND author" or "not-quite-admin."

---

## 2. Updated / new entities

### `Role` (existing, trimmed)
| Field | Notes |
|---|---|
| Id | |
| Name | `Customer`, `Admin` (add `Staff` later if you adopt §7) |

### `User` (existing, unchanged shape)
`RoleId` now only ever points to `Customer` or `Admin`.

### `AuthorProfile` (new)
| Field | Notes |
|---|---|
| Id | |
| UserId | FK, unique (1:1 with User) |
| PenName | display name, may differ from `FullName` |
| Bio | |
| Status | `Pending`, `Approved`, `Rejected`, `Suspended` |
| AppliedAt | |
| ApprovedAt | nullable |
| ApprovedByAdminId | nullable FK → User |

### `AdminInvite` (new)
| Field | Notes |
|---|---|
| Id | |
| Email | invitee's email |
| Token | random, single-use |
| ExpiresAt | |
| IsUsed | bool |
| CreatedByAdminId | FK → User |
| CreatedAt | |

### `Book` (new — you didn't have this yet, but `Loan.BookId` already assumes it)
| Field | Notes |
|---|---|
| Id | |
| Title, Description, ISBN, CoverImageUrl | |
| AuthorProfileId | FK — who submitted it |
| Price | nullable (null if loan-only) |
| IsForSale, IsForLoan | independent bools — a book can be either, both, or (temporarily) neither |
| StockForSale | copies available to purchase |
| StockForLoan | copies available to borrow |
| Status | `Pending`, `Approved`, `Rejected`, `Delisted` — admin gate before it's publicly visible |
| CreatedAt | |

*Why split stock instead of one pool:* a sale removes a copy permanently, a loan removes it temporarily. Sharing one counter means you need a reservation/locking layer to avoid double-booking. Two counters is simpler for a first version — you can merge into a smarter inventory model later if it becomes a real bottleneck.

### `Branch` (new — needed once "local store" is more than one location)
| Field | Notes |
|---|---|
| Id | |
| Name, Address, Phone | |

### `Order` (existing, extended)
| Field | Notes |
|---|---|
| ...existing fields | |
| Channel | `Online`, `Local` |
| BranchId | nullable FK → Branch (set when Channel = Local) |
| ProcessedByStaffId | nullable FK → User (who at the branch handled it) |

### `OrderItem` (new — your `Order` currently has no line items, just a `TotalAmount`, which won't survive multi-book carts)
| Field | Notes |
|---|---|
| Id | |
| OrderId | FK |
| BookId | FK |
| Quantity | |
| UnitPrice | snapshot price at purchase time |

### `Loan` (existing, extended)
| Field | Notes |
|---|---|
| ...existing fields | |
| DueAt | `BorrowedAt` + loan period |
| Channel | `Online`, `Local` |
| BranchId | nullable FK |
| FineAmount | decimal, default 0, computed on/after `DueAt` if not returned |
| FinePaid | bool |

### `Payment` (existing, generalized)
Right now `Payment.OrderId` is a hard FK — it can't cover subscription payments or loan-fine payments. Generalize:

| Field | Notes |
|---|---|
| Id, Amount, PaymentMethod, Status, PaidAt | unchanged |
| OrderId | **nullable** |
| SubscriptionId | **nullable**, FK → UserSubscription |
| LoanId | **nullable**, FK → Loan (fine payment) |

Enforce in application logic (or a DB check constraint) that **exactly one** of the three is set per payment row.

### `SubscriptionPlan` (new)
| Field | Notes |
|---|---|
| Id | |
| Name | e.g. "Reader+", "Reader Pro" |
| Price | |
| DurationInDays | e.g. 30 |
| MaxActiveLoans | how many books can be borrowed concurrently |
| DiscountPercentOnPurchases | applied at checkout |
| IsActive | admin can retire a plan |

### `UserSubscription` (new)
| Field | Notes |
|---|---|
| Id | |
| UserId | FK |
| PlanId | FK |
| StartDate, EndDate | |
| Status | `Active`, `Expired`, `Cancelled` |
| AutoRenew | bool |

---

## 3. Core workflows

### 3.1 Sign up / log in (one form for everyone)
1. User submits full name, email, password.
2. `User` row created, `RoleId = Customer`.
3. That's it — no role picker at signup. Author and Admin are never chosen at this step.

### 3.2 Customer → Author
1. Logged-in customer submits an "become an author" application (pen name, bio) → `AuthorProfile` row, `Status = Pending`.
2. Admin reviews in the admin panel → `Approved` or `Rejected`.
3. On approval, the user's UI unlocks an "Author Dashboard" — but their `RoleId` never changes. They can still buy and borrow books as before.

### 3.3 Author adds a book
1. Approved author submits book metadata, sets `IsForSale` / `IsForLoan`, price, initial stock → `Book.Status = Pending`.
2. Admin reviews (content, pricing sanity, rights) → `Approved` or `Rejected`.
3. Approved books appear in the public catalog for both online purchase and in-store handling.

### 3.4 Buy a book — online
1. Customer browses catalog, adds to cart, checks out.
2. `Order` created, `Channel = Online`, `OrderItem`(s) created from cart.
3. `Payment` created against the `Order`. On success → `Order.Status = Paid`, decrement each `Book.StockForSale`.

### 3.5 Buy or loan a book — local store (walk-in)
1. Customer walks into a branch. **Decision needed** on whether they must already have an account (see §6) — assuming yes:
2. Branch staff/admin looks up the customer (by email/phone) or, if unregistered, has them sign up on a kiosk/staff device (same signup as §3.1 — no special "in-store" account type).
3. Staff processes the transaction: `Order` or `Loan` created with `Channel = Local`, `BranchId` set, `ProcessedByStaffId` set to the staff member.
4. Same stock/payment logic as online, just attributed to a branch instead of a browser session.

### 3.6 Borrow a book
1. Customer requests a loan (online self-service, or in-store via staff) on a book with `IsForLoan = true` and `StockForLoan > 0`.
2. If the customer has an active subscription, check `MaxActiveLoans` isn't exceeded.
3. `Loan` created: `BorrowedAt = now`, `DueAt = now + loan period`, decrement `StockForLoan`.
4. On return: `IsReturned = true`, `ReturnedAt = now`, increment `StockForLoan` back.
5. If `ReturnedAt > DueAt` (or still unreturned past due), compute `FineAmount`. Customer settles it → `Payment` row with `LoanId` set, `FinePaid = true`.

### 3.7 Subscribe
1. Customer picks a `SubscriptionPlan`, pays → `Payment` with `SubscriptionId` set.
2. `UserSubscription` created, `Status = Active`, `EndDate = now + DurationInDays`.
3. Business logic checks `UserSubscription` at checkout/loan time to apply discounts or raise loan limits.
4. On `EndDate`, a background job flips `Status = Expired` (or renews it if `AutoRenew = true` and payment succeeds).

### 3.8 Admin invites another admin
1. An existing admin enters an email in the admin panel → `AdminInvite` created with a random token, expiry set.
2. System emails a signup link containing the token.
3. Invitee opens the link → a **separate signup form** (not the public one) pre-filled with their email.
4. On submit: `User` created directly with `RoleId = Admin`, `AdminInvite.IsUsed = true`.
5. This path never touches the Customer signup flow — admins are never customers, by construction.

### 3.9 Admin's day-to-day
- Approve/reject `AuthorProfile` applications.
- Approve/reject/delist `Book` submissions.
- Manage `Branch` records and (if adopted) `Staff` accounts.
- View/manage `Order`, `Loan`, `Payment`, `UserSubscription` records across both channels.
- Issue `AdminInvite`s.
- Handle overdue-loan fines, refunds, disputes.

---

## 4. Entity relationship summary

```
User 1───1 AuthorProfile (optional)
User 1───N Order
User 1───N Loan
User 1───N UserSubscription
User (Admin) 1───N AdminInvite (as inviter)

AuthorProfile 1───N Book

Book 1───N OrderItem
Book 1───N Loan

Order 1───N OrderItem
Order 1───1 Payment (nullable link)
Order N───1 Branch (nullable, if Channel=Local)

Loan N───1 Branch (nullable, if Channel=Local)
Loan 1───1 Payment (nullable, fine only)

UserSubscription N───1 SubscriptionPlan
UserSubscription 1───1 Payment (nullable link)

Role 1───N User
```

---

## 5. What's genuinely missing from your original list

- **`Book`** — you referenced `Loan.BookId` but had no `Book` model. It's the center of the whole system; added above.
- **`OrderItem`** — without it, `Order.TotalAmount` is a number with no breakdown. Multi-book carts need line items.
- **Approval gates** — for both `AuthorProfile` and `Book`. Without them, anyone who flips to "author" can publish anything instantly, which is probably not what you want for a store with an admin role.
- **Loan due dates and fines** — your current `Loan` has no `DueAt` or fine tracking, so "local store loans" has no actual lending-period logic yet.

---

## 6. Decisions you need to make (I picked defaults above — override if wrong)

1. **Do in-store (local) customers need an account before staff can process a sale/loan?**
   Default assumed: **yes**, same signup, just staff-assisted. Alternative: allow anonymous walk-in transactions logged against a generic placeholder user — faster at the counter, but breaks your "one login" philosophy and makes loan tracking / fines impossible for those transactions. I'd push back on the anonymous option — a library loan with no accountable borrower doesn't work.

2. **Do you need a `Staff` role for branch employees, or does `Admin` run the counter?**
   Not in your original ask, but every local-store flow above (§3.5, §3.9) assumes *someone* at the branch is doing this. Full `Admin` for every cashier is a privilege-creep risk (they'd also get author/book approval rights). I'd add a scoped `Staff` role (branch-limited: process sales, loans, returns — nothing else) — see §7.

3. **Author payouts / royalties.** You said authors "sell" books, which implies money eventually flows back to them. Nothing in your ask or in this doc handles that yet. If it's in scope for even v2, flag it now so `Payment`/`Order` fields don't need retrofitting later (e.g. a `RoyaltyPercent` on `Book` or `AuthorProfile`, and a payout ledger).

4. **Shared vs. split inventory** (§2, `Book.StockForSale` / `StockForLoan`). Simple now, but if you ever want "any physical copy can be sold *or* loaned, whichever happens first," you'll need a real inventory/reservation model instead of two static counters.

---

## 7. Recommended (not required) addition: `Staff` role

If local-store volume is more than "admin does everything," add:

**Role**: `Staff` (alongside `Customer`, `Admin`)
**`StaffProfile`**: `Id`, `UserId` FK, `BranchId` FK — scopes what they can touch.

Staff would be created the same way as Admins — **invited**, never self-signed-up as customers — but scoped to `Order`/`Loan` processing at their assigned branch only. Skip this for an MVP; add it the moment you have more than one physical branch or more than one person working a counter.
