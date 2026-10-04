# PRD 01: Authentication & Login

## Traceability

- Epic: [#2 — Authentication & Login](https://github.com/lhjenner/recipe-app-monorepo/issues/2)
- Stories: #3 (account creation), #4 (login), #5 (invalid-credentials error), #6 (password reset), #7 (persistent session), #8 (logout)
- Tasks: #9 (account lockout)
- Pull Requests: _TBD — link as opened_

## Overview & Problem Statement

The Recipe App currently has no concept of a user. All features (recipes, grocery list) will eventually be scoped per user account, so authentication is the foundational prerequisite for every subsequent feature.

This PRD covers the complete authentication slice: account creation, login, logout, password reset, and a temporary post-login landing page that acts as the routing target until the recipes feature exists.

**Context:** The app's realistic initial audience is two people sharing one email address (single shared account). Requirements are nonetheless written generally — the system must not hardcode single-user assumptions (e.g., registration remains a real, working flow).

**Explicitly out of scope for this PRD:**

- Per-IP rate limiting (noted as future hardening — see Edge Cases)
- Email verification on registration
- OAuth / third-party login
- User profile management beyond what login requires

## User Stories

- As a user, I want to create an account with my email and a password (reachable from the login page) so that I can access the app.
- As a user, I want to log in with my email and password so that I can access my recipes and grocery list.
- As a user, I want to see a clear error when my credentials are wrong so that I know my login failed (without being told which field was wrong).
- As a user, I want to reset my password via a link emailed to me (reachable from the login page) so that I can regain access securely if I forget it.
- As a user, I want to stay logged in across browser sessions so that I don't have to log in every visit.
- As a user, I want to log out so that I can end my session on a shared device.

## Acceptance Criteria

Each criterion is a Given/When/Then scenario. The **Test Type** column declares which test layers verify it (Unit = xUnit, BDD = Reqnroll, E2E = Playwright).

### Login page & flow

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-01 | The login page loads | — | The form shows email and password fields, a "Log in" button, and links to account creation and password reset | E2E |
| AC-02 | The login form is displayed | The user submits with an empty email or password | An inline validation message appears under the offending field and no network request is sent | E2E |
| AC-03 | The login form is displayed | The user submits a malformed email | An inline validation message appears and no request is sent | E2E |
| AC-04 | A registered user with valid credentials | The user submits the login form | The server returns `200 OK` with the user DTO, sets the session cookie, and the user is routed to the temporary landing page | BDD + E2E |
| AC-05 | A login attempt with a wrong email or password | The form is submitted | The server returns `401` with the generic message "Invalid email or password" that does not reveal which field was wrong | Unit + BDD |
| AC-06 | A request with missing or malformed fields bypasses client validation | The server receives it | The server returns `400` with an RFC 7807 Problem Details body | Unit + BDD |
| AC-07 | A login request is in flight | — | The submit button is disabled (blocking double-submit), its label shows a spinner + "Logging in…" within a fixed minimum width so no layout shift occurs, and the Create Account / Forgot Password links remain enabled | E2E |
| AC-08 | The login page renders on desktop or mobile | — | One shared set of styles presents well at both viewport sizes (no mobile-specific stylesheet for this page) | E2E (visual) |
| AC-09 | A user interacts with the form via keyboard or screen reader | They tab through the form | Every input has an associated `<label>`, focus order follows visual order, and the standard focus ring is visible; E2E tests select elements via `getByRole` / `data-testid`, not CSS paths | E2E |

### Session behavior

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-10 | A successful login | The server sets the session cookie | The cookie is `HttpOnly`, `Secure`, and `SameSite`; the session token is never accessible to JavaScript | BDD |
| AC-11 | An active session | The user makes requests over time | Activity renews the session (sliding expiration); 14 days of inactivity requires re-login | Unit + BDD |
| AC-12 | The app loads | The frontend calls `GET /api/auth/me` | A valid session (`200`) routes to the landing page; no valid session (`401`) routes to the login page | BDD + E2E |
| AC-13 | An authenticated request carries an expired or invalid session | The server responds `401` | The frontend redirects to the login page | BDD + E2E |
| AC-14 | A logged-in user | They log out via `POST /api/auth/logout` | The server destroys the session, clears the cookie, returns `204`, and subsequent requests are unauthenticated | BDD + E2E |

### Account creation

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-15 | A new email and valid password | The user submits registration via `POST /api/auth/register` | The server validates input and returns `201 Created` with the user DTO | Unit + BDD |
| AC-16 | An already-registered email | The user submits registration | The server returns `409 Conflict` with Problem Details (account existence may be revealed here, unlike login) | BDD |
| AC-17 | Any successful registration | The account is persisted | The password is stored only as a bcrypt/Argon2 hash — never plaintext, never logged. Non-negotiable. | Unit + BDD |

### Password reset

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-18 | Any email address, registered or not | `POST /api/auth/forgot-password` is called | The server always returns `200 OK` (prevents account enumeration) | BDD |
| AC-19 | A registered email | `POST /api/auth/forgot-password` is called | A reset email is sent containing a time-limited, single-use token link | BDD |
| AC-20 | A valid reset token and new password | `POST /api/auth/reset-password` is called | The password hash is updated and the token invalidated; an invalid or expired token returns `400` with Problem Details | Unit + BDD |
| AC-21 | An issued reset token | 1 hour passes, or the token is used once | The token is rejected on any further use | Unit + BDD |

### Lockout

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-22 | 5 consecutive failed login attempts on an account | A further attempt is made | The account is locked for 15 minutes | Unit + BDD |
| AC-23 | A locked account | A login attempt is made during the lockout window | The server returns `423 Locked` (or `429`) with a Problem Details body | BDD |
| AC-24 | An account with prior consecutive failures | A login succeeds | The consecutive-failure counter resets to zero | Unit + BDD |

## Technical Requirements & Data Schema

### API endpoints

| Endpoint | Success | Failure modes |
|---|---|---|
| `POST /api/auth/register` | `201` + user DTO | `400` validation, `409` duplicate email |
| `POST /api/auth/login` | `200` + user DTO + session cookie | `400` validation, `401` bad credentials, `423` locked |
| `POST /api/auth/logout` | `204` | — |
| `GET /api/auth/me` | `200` + user DTO | `401` no valid session |
| `POST /api/auth/forgot-password` | `200` (always) | `400` validation |
| `POST /api/auth/reset-password` | `204` | `400` invalid/expired token |

### User entity (PostgreSQL, via EF Core migration)

- `Id` (GUID or bigint — strong type, not primitive string)
- `Email` (unique, required)
- `PasswordHash` (required)
- `CreatedAtUtc` (required)
- `FailedLoginAttempts` (int, for lockout counting)
- `LockedUntilUtc` (nullable — set when lockout triggered)
- No `Username` field — email is the login identifier.

A separate `PasswordResetToken` entity/table: token (hashed at rest), user FK, expiry, consumed flag.

### Backend standards (per project guidelines)

- Controllers are thin; authentication logic lives in an Application/Service layer registered via DI with appropriate lifetimes.
- All responses use DTOs / `record` types — the `User` entity is never serialized to a response.
- All errors use RFC 7807 Problem Details via centralized exception-handling middleware (no per-controller try/catch).
- All I/O is `async`/`await` with `CancellationToken` passthrough.
- `#nullable enable` on all projects.
- All schema changes ship as EF Core migrations — no raw SQL.
- Connection strings and SMTP credentials live in .NET User Secrets locally and `.env` (git-ignored) for Docker; `appsettings.json` and `.env.example` contain only non-sensitive placeholders.

### Frontend standards (per project guidelines)

- Strict TypeScript; typed interfaces for all API payloads; no `any`.
- Zod (or equivalent) validates API responses at the boundary.
- API calls centralized in a fetch service abstraction / typed hooks — no raw `fetch` scattered in components.
- Login state (loading, errors) stays local to the login component; no global store for this feature.
- Styles follow the modular structure in `apps/web/src/styles/` (colors, buttons, typography); the login page needs no mobile-specific stylesheet.

## Edge Cases & Failure Scenarios

- **Double-submit:** blocked by the disabled submit button (criterion 7).
- **Account enumeration:** prevented on login (generic 401) and forgot-password (always 200); permitted on registration (409) by design.
- **Deliberate lockout attack:** an attacker who knows an email can trigger the 15-minute lockout. Accepted risk for this app's audience; per-IP rate limiting is the documented future mitigation.
- **Offline / unreachable API:** the login page shows a generic "service unavailable" state distinct from credential errors.
- **Expired session mid-use:** any `401` redirects to login (criterion 13).
- **Reset token reuse / expiry:** rejected with `400` (criterion 21).
- **Email delivery failure during reset:** logged server-side; the client still receives `200` (enumeration prevention takes precedence).

## Test Strategy

Each acceptance criterion's **Test Type** column declares which layers verify it. Framework details per layer:

- **xUnit + FluentAssertions (unit/integration):** password hashing, email/password validation rules, lockout counter and `LockedUntilUtc` logic, DTO mapping (entity never leaks), Problem Details shaping.
- **Reqnroll (BDD component tests, isolated Docker test DB):** Gherkin scenarios per acceptance criterion — successful login, invalid credentials, lockout after 5 failures, register duplicate email, full reset-token flow, logout invalidates session.
- **Playwright (E2E):** complete login journey, empty-field and malformed-email client validation (assert no request fires), button loading state during flight, navigation to signup/reset, logout, post-login landing on the temporary page. Resilient selectors only (`getByRole`, `data-testid`).
- **JMeter:** deferred — single-user app; revisit if the audience grows.

Tests are written **before** implementation (TDD): failing tests/bindings first, then application code.
