# PRD 02: App Shell & Navigation

## Traceability

- Epic: [#21 — App shell & navigation](https://github.com/lhjenner/recipe-app-monorepo/issues/21)
- Stories: #24 (Home page), #25 (menu and stub pages), #26 (sticky header), #27 (active item), #28 (mobile flyout), #29 (logout), #30 (route protection)
- Tasks: #23 (Vitest setup)
- Pull Requests: [#33 — Replace temporary landing page with permanent Home](https://github.com/lhjenner/recipe-app-monorepo/pull/33), [#35 — Add navigation menu and stub pages](https://github.com/lhjenner/recipe-app-monorepo/pull/35)
- Depends on: [PRD 01 — Authentication & Login](./01-authentication-and-login.md)

## Overview & Problem Statement

PRD 01 ended with a temporary post-login landing page (`TemporaryLandingPage`) whose only content is a "You're signed in" message and a logout button. There is no way to move around the app, and every future feature (recipes, grocery list, meal planner, etc.) needs a consistent place to live and be navigated to.

This PRD introduces the **app shell**: a persistent layout wrapping every authenticated page, containing a sticky navigation header (desktop) or hamburger flyout (mobile), plus a permanent Home page and a stub page for every menu destination. Stub pages let the navigation be fully tested end-to-end before any real feature exists.

The temporary landing page is **replaced and removed**.

**Explicitly out of scope:**

- Home page widgets (Home shows a "Widgets coming soon" placeholder; widgets get their own PRD)
- Page-specific header items on mobile or desktop (the layout leaves room for them; they are added by the pages that need them)
- Real content for any stub page
- Icons (text-only menu)
- Displaying the signed-in user's email (shown on the old temporary page; not carried over)

## User Stories

- As a user, I want a permanent Home page after logging in so that I have a consistent starting point in the app.
- As a user, I want a menu on every logged-in page so that I can move between Recipe List, Add Recipe, Grocery List, Meal Planner, Food Diary, Ingredient Maintenance and Archived Recipes without going back to Home.
- As a user, I want the menu to stay visible as I scroll so that I can navigate from anywhere on a long page.
- As a user, I want the current page highlighted in the menu so that I can see at a glance where I am.
- As a mobile user, I want the menu in a hamburger flyout so that it doesn't consume screen space.
- As a user, I want to log out from the menu so that I can end my session on a shared device.
- As a user, I want to be sent to the page I originally asked for after logging in so that expired sessions and bookmarks don't lose my place.

## Acceptance Criteria

Each criterion is a Given/When/Then scenario. The **Test Type** column declares which layers verify it (Unit = Vitest, BDD = Reqnroll, E2E = Playwright). This PRD is frontend-only, so no BDD coverage is expected.

### Menu structure & content

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-01 | A logged-in user on desktop width | Any logged-in page loads | The header shows the logo (R in speech bubble) at the left, the navigation links (AC-02) next to it, and the Log out button at the far right | E2E |
| AC-02 | A logged-in user | The menu is displayed | Items appear in this order, text only: Home, Recipe List, Add Recipe, Grocery List, Meal Planner, Food Diary, Ingredient Maintenance, Archived Recipes, then Log out | Unit + E2E |
| AC-03 | The menu is displayed | A user inspects Log out | It is rightmost on desktop, visually separated from the navigation links, and styled differently from them | E2E |
| AC-04 | A logged-in user | Each menu item is clicked in turn | The URL changes to that item's route, the stub page renders with an `h1` equal to the item's page title, and `document.title` is "{Page title} – Recipe App" | E2E |
| AC-05 | Any logged-in page | The menu is displayed | It sits inside a `<nav>` landmark with an accessible label | E2E |

### Active page

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-06 | The user is on a menu destination | The menu is displayed | That item is styled distinctly (colour from `colors.css`), has `aria-current="page"`, and all other items do not | Unit + E2E |
| AC-07 | The user is on a page whose route starts with an item's route (e.g. a future `/recipes/123` or `/recipes/edit`; recipe editing is expected to be inline, so a dedicated edit route may never exist) | The menu is displayed | The item with the **longest matching route prefix** is active (so `/recipes/new` highlights Add Recipe, not Recipe List) | Unit |
| AC-08 | The active item is displayed | It is clicked (or activated by keyboard) | Nothing happens: no reload, no navigation, no new history entry; the item remains focusable | E2E |
| AC-09 | A route that matches no menu item (e.g. the 404 page) | The menu is displayed | No item is active | Unit + E2E |

### Sticky header

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-10 | Any logged-in page | The header's computed style is inspected | `position` is `sticky` and `top` is `0` | E2E |
| AC-11 | A very short viewport (e.g. 300px high) so a stub page overflows | The page is scrolled to the bottom | The header is still fully visible at the top of the viewport | E2E |

### Header appearance

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-12 | A user moves from the login page to a logged-in page (desktop) | The page loads | The header has the same background colour, logo, light-green bottom border and height as the login page banner, so it looks like the same header with different content | E2E (visual) |
| AC-13 | A narrow desktop viewport above the mobile breakpoint | The 9 items do not fit on one row | The nav wraps onto additional rows; the header grows in height; nothing is clipped or overflows horizontally | E2E |

### Mobile flyout

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-14 | Viewport at or below the mobile breakpoint | A logged-in page loads | The sticky header shows: hamburger button at left, then the page title, then space reserved for future page-specific items. Links and Log out are not visible | E2E |
| AC-15 | The mobile header | The hamburger is activated | A flyout slides in from the left containing all menu items in order with Log out visually separated at the bottom; the hamburger has `aria-expanded="true"` and `aria-controls` pointing at the flyout; focus moves into the flyout; background scroll is locked | E2E |
| AC-16 | The flyout is open | A menu item is selected | The page navigates and the flyout closes (and scroll lock is released) | E2E |
| AC-17 | The flyout is open | The user taps outside it, or presses Escape | The flyout closes, scroll lock is released, and focus returns to the hamburger | E2E |
| AC-18 | The flyout is open | Tab / Shift+Tab is pressed repeatedly | Focus cycles within the flyout and never reaches background content | E2E |
| AC-19 | The flyout is open | The viewport is resized above the mobile breakpoint | The flyout closes, the desktop menu appears and scroll lock is released | E2E |
| AC-20 | The user has `prefers-reduced-motion: reduce` | The flyout opens or closes | No slide animation is used | E2E |
| AC-21 | The active item in the flyout | It is selected | The flyout closes with no navigation (consistent with AC-08) | E2E |

### Logout

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-22 | A logged-in user | Log out is activated | The session is ended immediately (no confirmation), client-side user state is cleared, and the user lands on the login page | E2E |
| AC-23 | The user has just logged out | The browser Back button is used (including after a bfcache restore) | No protected page content is shown; the user is sent to login | E2E |
| AC-24 | The logout request fails | Log out is activated | The user stays on the current page, an error is shown (`role="alert"`), and they can retry | Unit + E2E |

### Route protection & redirects

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-25 | A logged-out user | They open a protected URL directly (e.g. `/grocery-list`) | They are redirected to login; after logging in they land on `/grocery-list` | E2E |
| AC-26 | A user whose session expires mid-use | Any API call returns `401` | They are redirected to login, then returned to the page they were on after logging in | E2E |
| AC-27 | A login redirect target | It is not a same-origin relative path (e.g. `https://evil.example`, `//evil.example`) | It is ignored and the user lands on Home | Unit + E2E |
| AC-28 | A logged-in user | They visit the login page (`/`) | They are redirected to Home | E2E |
| AC-29 | The page is loading and the stored session is still being checked | The check has not completed | A loading state is shown; neither the login page nor protected content flashes | E2E |
| AC-30 | A logged-out user | They visit the login, registration, forgot-password or reset-password pages | No menu or app shell is rendered | E2E |
| AC-31 | A logged-in user | They visit an unknown URL | A 404 page renders inside the app shell with no item active | E2E |
| AC-32 | A logged-out user | They visit an unknown URL | They are redirected to login | E2E |

### Home & stubs

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-33 | A user logs in successfully | Login completes | They land on Home (or their intended page per AC-25) showing the "Widgets coming soon" placeholder | E2E |
| AC-34 | The old `TemporaryLandingPage` | This PRD is complete | The component and its route usage are deleted, and PRD 01 tests asserting "You're signed in" instead assert the Log out button is visible | E2E |

## Technical Requirements & Data Schema

No backend or data changes. Existing endpoints (`GET /api/auth/me`, `POST /api/auth/logout`) are reused.

### Routes

| Label | Route | Page title |
|---|---|---|
| Home | `/home` | Home |
| Recipe List | `/recipes` | Recipe List |
| Add Recipe | `/recipes/new` | Add Recipe |
| Grocery List | `/grocery-list` | Grocery List |
| Meal Planner | `/meal-planner` | Meal Planner |
| Food Diary | `/food-diary` | Food Diary |
| Ingredient Maintenance | `/ingredient-maintenance` | Ingredient Maintenance |
| Archived Recipes | `/recipes/archived` | Archived Recipes |

Log out is an action, not a route. The login page remains at `/`.

### Single source of truth

One typed nav config (label, route, page title) drives the desktop menu, the flyout, the mobile header title, `document.title`, stub page headings, and the active-item logic. Adding a page means adding one entry.

### Frontend structure

Small, single-purpose modules, each well under the 200–250 line limit:

- nav config and active-item matching (pure function, unit-tested)
- app shell layout (header + `<Outlet />`)
- desktop nav
- mobile flyout (including focus trap and Escape/outside-click handling)
- scroll-lock hook; media-query hook for the breakpoint
- a protected-route guard (handles session check, loading state, and redirect with intended destination)
- a generic stub page component, plus Home and 404 pages

Post-login redirect target travels as a `returnTo` query parameter on the login URL, validated as a same-origin relative path (must start with a single `/`, not `//`, no scheme) before use.

### Styling

- **Colours:** all colours, including the active-item and logout colours, are defined as variables in `colors.css`. Reuse existing tokens (`--color-primary-hover`, `--color-focus`, `--color-on-brand`) for the header so it matches the login banner.
- **Shared header base:** the header base styles currently in `login.css` (background, border, height, brand mark) move to a shared `header.css` consumed by both the login banner and the app header, so they cannot drift apart. Desktop height 76px, mobile 64px, 4px bottom border (unchanged).
- Logout reuses `buttons.css` variants where possible; new single-purpose files for `app-header.css` and `flyout.css` under `apps/web/src/styles/`.
- **Breakpoint:** one value, 700px (matches the existing mobile breakpoint in `login.css`). It is defined once as a TypeScript constant for the media-query hook and referenced by a comment in each CSS file that uses it, since CSS media queries cannot use variables.
- Flyout animation is disabled under `prefers-reduced-motion`.

### Mobile hamburger icon (decision)

The hamburger uses a conventional three-line icon, **not** the R logo. A recognisable icon is more discoverable, and the logo is shown at the top of the flyout instead. Revisit only if the audience grows and user feedback suggests otherwise.

### Accessibility

- Active item uses `aria-current="page"` and stays focusable; it is **not** `disabled`.
- Hamburger: `aria-expanded`, `aria-controls`, accessible name "Open menu" / "Close menu".
- Flyout: focus moves in on open, trapped while open, returns to the hamburger on close.
- Menu inside `<nav aria-label="Main">`.

### Test hooks

Role-based selectors (`getByRole('navigation')`, `getByRole('link')`, `getByRole('button', { name: 'Log out' })`) with `data-testid` for the flyout, hamburger and header. The Log out button being visible is the E2E signal that a user is logged in.

## Edge Cases & Failure Scenarios

- **Narrow desktop widths:** nav wraps rather than overflowing (AC-13).
- **Stubs don't scroll:** sticky behaviour is verified with a deliberately short viewport (AC-11), and re-verified naturally once real pages exist.
- **Rapid open/close of the flyout:** scroll lock and focus state must not be left stuck (covered by AC-17, AC-19).
- **Open redirect:** unsafe `returnTo` values are discarded (AC-27).
- **bfcache / Back after logout:** protected routes re-check the session when restored (AC-23).
- **Session check failure on startup (API unreachable):** keep the existing PRD 01 behaviour (treated as logged out, notice shown on login).
- **Logout API failure:** user stays logged in with a retryable error (AC-24).
- **Deep link to a future dynamic route:** handled by longest-prefix matching (AC-07).

## Test Strategy

- **Unit (Vitest):** the web project has no frontend unit tests yet. Vitest, React Testing Library, jest-dom and jsdom are installed but unconfigured, so this PRD includes the setup: Vitest config (in `vite.config.ts`, jsdom environment), a `test` script in `package.json`, a jest-dom setup file, and a unit-test step in the CI workflow. Tests cover: nav config ordering, longest-prefix active matching (including `/recipes/new` and `/recipes/archived` vs `/recipes`, and no match), `returnTo` validation, logout-failure handling.
- **BDD (Reqnroll):** none — no backend changes.
- **E2E (Playwright):** every criterion marked E2E, using `getByRole` / `data-testid`, no arbitrary timeouts. Desktop and mobile viewport projects. Header-match check (AC-12) as a visual/computed-style comparison against the login banner.
- **Existing tests to update:** `login-journey.spec.ts` (replace the "You're signed in" heading assertion with Log out visible); review `session.spec.ts` and `login-accessibility.spec.ts`, which navigate to `/home` and click Log out, for any dependency on the removed page's markup.
- **JMeter:** not applicable.

Tests are written **before** implementation (TDD).
