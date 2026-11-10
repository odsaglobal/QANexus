#!/usr/bin/env python3
"""Seed a complete Flipkart demo project into ATIP: project, environment, environment
variables, a business context document, test data, manual test cases (with exact, grounded
steps) and a suite.

Every scenario's steps below were captured by DRIVING www.flipkart.com with Playwright MCP
on 2026-09-28 as a guest (no login) and recording exactly what happened -- no invented/guessed
steps or assertions. See the inline "grounded" comment above each scenario for what was observed.

Usage:
  API_KEY=qk_... [BASE=http://localhost:5125] [RESET=1] python3 tools/flipkart/seed-full.py

Requires an existing ATIP API key (Administration -> API Keys in the UI, after logging in via
Auth0 once) -- creating the very first key requires a real user session, so it can't be
bootstrapped headlessly. Everything after that (project/environment/variables/test data/
scenarios/suite) is created by this script and is idempotent: re-running it reuses the
project/environment/test-data set by name instead of duplicating them. Scenarios are only
skipped if RESET is unset and same-titled scenarios already exist; set RESET=1 to delete and
recreate them.
"""
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request

BASE = os.environ.get("BASE", "http://localhost:5125")
API_KEY = os.environ.get("API_KEY")
RESET = os.environ.get("RESET") == "1"
EMPTY_GUID = "00000000-0000-0000-0000-000000000000"

PROJECT_NAME = "Flipkart Demo"
PROJECT_KEY = "flipkart-demo"
ENV_NAME = "Production"
ENV_BASE_URL = "https://www.flipkart.com"
TEST_DATA_NAME = "Search Terms"
SUITE_NAME = "Flipkart Guest Regression"
BUSINESS_CONTEXT_NAME = "Flipkart Guest Shopping - Business Context"

# Fed to the AI engine (scenario generation + exploration) via the project's Business Context
# docs. Every statement here was directly observed during the same Playwright MCP exploration
# that grounded the scenarios below -- it is not product documentation or an assumption.
BUSINESS_CONTEXT_MARKDOWN = """# Flipkart -- Guest Shopping Business Context

Observed by driving https://www.flipkart.com as a signed-out guest.

## Authentication
- The site can be browsed, searched and added-to-cart entirely as a guest.
- A "Login / Get access to your Orders, Wishlist and Recommendations" modal may appear on
  landing; it is dismissed with its "x" close button and does not block browsing.
- The header always shows a "Login" link when signed out.
- Checkout is gated behind login: clicking "Place Order" on the cart page as a guest does not
  navigate away from /viewcart or reach an order confirmation -- the "Login" link is still shown
  afterwards.

## Search
- Searching a keyword (e.g. "laptop") navigates to /search?q=<keyword> and shows a results
  summary line "Showing 1 - 24 of <N> results for \"<keyword>\"".
- Even a nonsense/unmatched keyword (e.g. "zzxxqqwweeasdqwerty12345") does not produce an empty
  "no results" state -- Flipkart falls back to a populated grid of "related" products instead.
- Results can be sorted via a "Sort By" bar (Relevance, Popularity, Price -- Low to High,
  Price -- High to Low, Newest First). Sort must be triggered by clicking an option in the UI --
  navigating directly to a URL with a sort= query parameter does not work (net::ERR_ABORTED).
  A successful sort click appends sort=price_asc or sort=price_desc to the URL.
- Results can be narrowed with facets in the Filters sidebar, e.g. Brand (chips include HP,
  Lenovo, ASUS, DELL, Acer, Apple). Selecting a brand chip adds a facets.brand query parameter
  and reduces the total result count shown in the summary line.

## Product details / purchase options
- Purchase options vary by product category: laptop listings observed only expose "Buy now"
  (no "Add to cart"), while accessory listings (e.g. a USB cable) expose both "Add to cart" and
  "Buy now".
- Product detail pages are reached at URLs containing /p/itm...; on the results grid, product
  cards are anchor tags that open the PDP.

## Cart
- "Add to cart" updates the header Cart icon with a numeric item-count badge.
- The cart page (/viewcart) shows the item with its quantity ("Qty: 1"), a "Price Details" panel
  and a "Total Amount" line.
- Guest cart contents persist across navigation: navigating away (e.g. to the home page) and
  back still shows the same cart badge count and the same item on /viewcart.

## Navigation
- The top navigation bar exposes top-level category links, including Fashion, Mobiles,
  Electronics and Beauty, which route to category-specific listing pages (e.g. Mobiles ->
  /mobile-phones-store).
"""


def call(method, path, body=None):
    url = f"{BASE}{path}"
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("X-Api-Key", API_KEY)
    req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req) as resp:
            raw = resp.read()
            return json.loads(raw) if raw else None
    except urllib.error.HTTPError as e:
        print(f"{method} {path} -> {e.code}: {e.read().decode('utf-8', 'replace')}", file=sys.stderr)
        raise


def upload_requirement(project_id, name, filename, content_bytes, content_type):
    boundary = "----ATIPSeedBoundary"
    parts = [
        f"--{boundary}\r\nContent-Disposition: form-data; name=\"name\"\r\n\r\n{name}\r\n".encode("utf-8"),
        f"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{filename}\"\r\n"
        f"Content-Type: {content_type}\r\n\r\n".encode("utf-8"),
        content_bytes,
        f"\r\n--{boundary}--\r\n".encode("utf-8"),
    ]
    body = b"".join(parts)

    path = f"/api/v1/projects/{project_id}/requirements"
    req = urllib.request.Request(f"{BASE}{path}", data=body, method="POST")
    req.add_header("X-Api-Key", API_KEY)
    req.add_header("Content-Type", f"multipart/form-data; boundary={boundary}")
    try:
        with urllib.request.urlopen(req) as resp:
            raw = resp.read()
            return json.loads(raw) if raw else None
    except urllib.error.HTTPError as e:
        print(f"POST {path} -> {e.code}: {e.read().decode('utf-8', 'replace')}", file=sys.stderr)
        raise


def find_project(name):
    result = call("GET", f"/api/v1/projects?search={urllib.parse.quote(name)}&pageSize=50")
    for item in result["items"]:
        if item["name"] == name:
            return item
    return None


def ensure_project():
    existing = find_project(PROJECT_NAME)
    if existing:
        print(f"Reusing project '{PROJECT_NAME}' ({existing['id']})")
        return existing["id"]
    created = call("POST", "/api/v1/projects", {
        "name": PROJECT_NAME,
        "key": PROJECT_KEY,
        "description": "Guest-shopper regression suite for www.flipkart.com, seeded from a live Playwright MCP exploration.",
    })
    print(f"Created project '{PROJECT_NAME}' ({created['id']})")
    return created["id"]


def ensure_environment(project_id):
    envs = call("GET", f"/api/v1/projects/{project_id}/environments")
    for env in envs:
        if env["name"] == ENV_NAME:
            print(f"Reusing environment '{ENV_NAME}' ({env['id']})")
            return env["id"]
    created = call("POST", f"/api/v1/projects/{project_id}/environments", {
        "projectId": project_id,
        "name": ENV_NAME,
        "type": "Production",
        "baseUrl": ENV_BASE_URL,
        "isDefault": True,
    })
    print(f"Created environment '{ENV_NAME}' ({created['id']})")
    return created["id"]


def set_variables(project_id, environment_id):
    call("PUT", f"/api/v1/projects/{project_id}/environments/{environment_id}/variables", {
        "projectId": project_id,
        "environmentId": environment_id,
        "variables": [
            {"key": "baseUrl", "value": ENV_BASE_URL, "type": "text"},
            {"key": "searchTerm", "value": "laptop", "type": "text"},
            {"key": "unmatchedSearchTerm", "value": "zzxxqqwweeasdqwerty12345", "type": "text"},
            {"key": "accessorySearchTerm", "value": "usb cable", "type": "text"},
            {"key": "brandFilter", "value": "HP", "type": "text"},
        ],
    })
    print("Set environment variables")


def ensure_business_context(project_id):
    existing = call("GET", f"/api/v1/projects/{project_id}/requirements")
    for req in existing:
        if req["name"] == BUSINESS_CONTEXT_NAME:
            print(f"Reusing business context document '{BUSINESS_CONTEXT_NAME}' ({req['id']})")
            return req["id"]
    created = upload_requirement(
        project_id,
        BUSINESS_CONTEXT_NAME,
        "flipkart-guest-shopping-business-context.md",
        BUSINESS_CONTEXT_MARKDOWN.encode("utf-8"),
        "text/markdown",
    )
    print(f"Uploaded business context document '{BUSINESS_CONTEXT_NAME}' ({created['id']})")
    return created["id"]


def ensure_test_data(project_id, environment_id):
    existing = call("GET", f"/api/v1/projects/{project_id}/environments/{environment_id}/test-data")
    for ds in existing:
        if ds["name"] == TEST_DATA_NAME:
            print(f"Reusing test data set '{TEST_DATA_NAME}' ({ds['id']})")
            return ds["id"]
    created = call("POST", f"/api/v1/projects/{project_id}/environments/{environment_id}/test-data", {
        "projectId": project_id,
        "environmentId": environment_id,
        "name": TEST_DATA_NAME,
        "description": "Search keywords used across the guest browsing/search scenarios.",
        "columns": ["keyword", "kind"],
        "rows": [
            {"keyword": "laptop", "kind": "matched"},
            {"keyword": "usb cable", "kind": "matched-accessory"},
            {"keyword": "zzxxqqwweeasdqwerty12345", "kind": "unmatched"},
        ],
    })
    print(f"Created test data set '{TEST_DATA_NAME}' ({created['id']})")
    return created["id"]


# ---------------------------------------------------------------------------
# Scenarios. Every step below is grounded in an action actually performed and
# an outcome actually observed via Playwright MCP against the live site.
# ---------------------------------------------------------------------------

OPEN_HOME = {
    "action": "Open https://www.flipkart.com/",
    "expectedResult": 'The "Search for Products, Brands and More" search box is displayed in the header.',
}
DISMISS_LOGIN_MODAL = {
    "action": 'If a "Login / Get access to your Orders, Wishlist and Recommendations" modal appears, click its "✕" close button.',
    "expectedResult": "The modal is dismissed and the storefront remains usable without signing in.",
}

SCENARIOS = [
    {
        "title": "Search for a product by keyword",
        "type": "Smoke", "priority": "High", "risk": "Medium",
        "tags": ["smoke", "search", "guest"],
        "preconditions": "The shopper is browsing https://www.flipkart.com as a guest and is not signed in.",
        "expectedResult": 'A results page for "laptop" is displayed with a results count and a Sort By bar.',
        "steps": [
            OPEN_HOME, DISMISS_LOGIN_MODAL,
            {"action": 'Type "laptop" into the "Search for Products, Brands and More" search box and press Enter.',
             "expectedResult": 'The URL becomes https://www.flipkart.com/search?q=laptop... and the page title starts with "Laptop-".'},
            {"action": "Read the results summary line shown above the product grid.",
             "expectedResult": 'The text "Showing 1 – 24 of <N> results for \\"laptop\\"" is displayed, and a "Sort By" bar with the choices Relevance, Popularity, Price -- Low to High, Price -- High to Low and Newest First is shown.'},
        ],
    },
    {
        "title": "Search with an unmatched keyword still returns related products, not an empty state",
        "type": "Negative", "priority": "Medium", "risk": "Medium",
        "tags": ["search", "negative", "guest"],
        "preconditions": "The shopper is browsing as a guest. The keyword used matches no product on Flipkart.",
        "expectedResult": 'Flipkart falls back to related products rather than showing an empty "no results" state.',
        "steps": [
            OPEN_HOME, DISMISS_LOGIN_MODAL,
            {"action": 'Type "zzxxqqwweeasdqwerty12345" into the search box and press Enter.',
             "expectedResult": "The URL becomes https://www.flipkart.com/search?q=zzxxqqwweeasdqwerty12345."},
            {"action": "Read the results summary line and the product grid.",
             "expectedResult": 'A "Showing 1 – 40 of <N> results for \\"zzxxqqwweeasdqwerty12345\\"" line and a populated product grid are displayed, even though the keyword matches nothing exactly.'},
        ],
    },
    {
        "title": "Sort search results by Price -- Low to High",
        "type": "Positive", "priority": "High", "risk": "Medium",
        "tags": ["search", "sort", "guest"],
        "preconditions": "A search results page for \"laptop\" is open as a guest.",
        "expectedResult": 'The "Price -- Low to High" sort becomes active.',
        "steps": [
            OPEN_HOME, DISMISS_LOGIN_MODAL,
            {"action": 'Type "laptop" into the search box and press Enter.',
             "expectedResult": 'The results page for "laptop" is displayed with a "Sort By" bar.'},
            {"action": 'Click "Price -- Low to High" in the Sort By bar.',
             "expectedResult": "The URL gains the query parameter sort=price_asc (e.g. .../search?q=laptop&...&sort=price_asc)."},
        ],
    },
    {
        "title": "Sort search results by Price -- High to Low",
        "type": "Positive", "priority": "High", "risk": "Medium",
        "tags": ["search", "sort", "guest"],
        "preconditions": "A search results page for \"laptop\" is open as a guest.",
        "expectedResult": 'The "Price -- High to Low" sort becomes active.',
        "steps": [
            OPEN_HOME, DISMISS_LOGIN_MODAL,
            {"action": 'Type "laptop" into the search box and press Enter.',
             "expectedResult": 'The results page for "laptop" is displayed with a "Sort By" bar.'},
            {"action": 'Click "Price -- High to Low" in the Sort By bar.',
             "expectedResult": "The URL gains the query parameter sort=price_desc (e.g. .../search?q=laptop&...&sort=price_desc)."},
        ],
    },
    {
        "title": "Filter search results by Brand",
        "type": "Positive", "priority": "High", "risk": "Medium",
        "tags": ["search", "filter", "brand", "guest"],
        "preconditions": "A search results page for \"laptop\" with a Brand facet in the Filters sidebar is open as a guest.",
        "expectedResult": "Only the selected brand's products remain, and the total result count is lower than the unfiltered search.",
        "steps": [
            OPEN_HOME, DISMISS_LOGIN_MODAL,
            {"action": 'Type "laptop" into the search box and press Enter.',
             "expectedResult": 'The results page for "laptop" is displayed with a "Filters" sidebar containing a "Brand" facet.'},
            {"action": 'Click the "Brand" facet header in the Filters sidebar to expand it.',
             "expectedResult": 'Brand chips including "HP", "Lenovo", "ASUS", "DELL", "Acer" and "Apple" become visible.'},
            {"action": 'Click the "HP" brand chip.',
             "expectedResult": 'The URL gains a "facets.brand" query parameter for HP (e.g. p%5B%5D=facets.brand%255B%255D%3DHP), and the results summary line shows a smaller total result count than the unfiltered "laptop" search.'},
        ],
    },
    {
        "title": "Open a product details page from the search results grid",
        "type": "Positive", "priority": "High", "risk": "Medium",
        "tags": ["pdp", "guest"],
        "preconditions": "A search results page for \"laptop\" with a populated product grid is open as a guest.",
        "expectedResult": "The product details page opens and shows a purchase action.",
        "steps": [
            OPEN_HOME, DISMISS_LOGIN_MODAL,
            {"action": 'Type "laptop" into the search box and press Enter.',
             "expectedResult": 'The results page for "laptop" is displayed with a product grid.'},
            {"action": "Click the first product card in the results grid.",
             "expectedResult": 'The product details page opens on a flipkart.com/.../p/itm... URL and shows a "Buy now" action.'},
            {"action": 'Look for an "Add to cart" action on this page.',
             "expectedResult": 'No "Add to cart" action is present -- this laptop listing offers "Buy now" only, so it cannot be added to the cart directly.'},
        ],
    },
    {
        "title": "Add an accessory to the cart as a guest and view the cart",
        "type": "Positive", "priority": "Critical", "risk": "Medium",
        "tags": ["cart", "guest", "smoke"],
        "preconditions": "The shopper is browsing as a guest. Accessory listings (e.g. cables) expose an \"Add to cart\" action, unlike the laptop listings above.",
        "expectedResult": "The item appears in the cart with quantity 1 and a computed total.",
        "steps": [
            OPEN_HOME, DISMISS_LOGIN_MODAL,
            {"action": 'Type "usb cable" into the search box and press Enter.',
             "expectedResult": 'The results page for "usb cable" is displayed with a product grid.'},
            {"action": "Click the first product card in the results grid.",
             "expectedResult": 'The product details page opens and shows both "Add to cart" and "Buy now" actions.'},
            {"action": 'Click the "Add to cart" action.',
             "expectedResult": "The header Cart icon updates to show an item count badge."},
            {"action": "Open https://www.flipkart.com/viewcart",
             "expectedResult": 'The cart page is displayed showing the added item with "Qty: 1", a "Price Details" panel, and a "Total Amount" line.'},
        ],
    },
    {
        "title": "Guest cart contents persist across navigation",
        "type": "Regression", "priority": "Medium", "risk": "Low",
        "tags": ["cart", "guest", "regression"],
        "preconditions": "An item was previously added to the cart as a guest (see \"Add an accessory to the cart as a guest\").",
        "expectedResult": "The cart item count badge is still shown after navigating away and back.",
        "steps": [
            {"action": "With an item already in the cart, open https://www.flipkart.com/ (the home page).",
             "expectedResult": 'The header "Cart" link shows a numeric badge (e.g. "1") next to the cart icon.'},
            {"action": 'Open https://www.flipkart.com/viewcart again.',
             "expectedResult": "The same item added earlier is still present in the cart, confirming the guest cart persisted across navigation."},
        ],
    },
    {
        "title": "Guest checkout is blocked at Place Order without signing in",
        "type": "Security", "priority": "Critical", "risk": "High",
        "tags": ["checkout", "guest", "security"],
        "preconditions": "An item is in the cart as a guest and the /viewcart page is open. The shopper is not signed in (the header shows a \"Login\" link).",
        "expectedResult": "Checkout does not complete for an unauthenticated shopper.",
        "steps": [
            {"action": 'On the cart page, click the "Place Order" action in the Price Details panel.',
             "expectedResult": 'The page stays on https://www.flipkart.com/viewcart (title "Shopping Cart | Flipkart.com") and the header still shows a "Login" link -- no order confirmation is reached while unauthenticated.'},
        ],
    },
    {
        "title": "Browse a category from the top navigation",
        "type": "Sanity", "priority": "Medium", "risk": "Low",
        "tags": ["browse", "category", "guest"],
        "preconditions": "The Flipkart home page is open as a guest.",
        "expectedResult": "Selecting a top-nav category opens a listing scoped to that category.",
        "steps": [
            OPEN_HOME, DISMISS_LOGIN_MODAL,
            {"action": 'Click "Mobiles" in the top navigation bar.',
             "expectedResult": "The page navigates to https://www.flipkart.com/mobile-phones-store."},
        ],
    },
]


def ensure_scenarios(project_id):
    existing = call("GET", f"/api/v1/projects/{project_id}/scenarios")
    existing_by_title = {s["title"]: s for s in existing}

    if RESET:
        for s in SCENARIOS:
            match = existing_by_title.get(s["title"])
            if match:
                call("DELETE", f"/api/v1/projects/{project_id}/scenarios/{match['id']}")
                print(f"Deleted existing scenario '{s['title']}'")
        existing_by_title = {}

    scenario_ids = []
    for s in SCENARIOS:
        match = existing_by_title.get(s["title"])
        if match:
            print(f"Reusing scenario '{s['title']}' ({match['id']})")
            scenario_ids.append(match["id"])
            continue
        created = call("POST", f"/api/v1/projects/{project_id}/scenarios/manual", {
            "projectId": project_id,
            "featureId": EMPTY_GUID,
            "title": s["title"],
            "type": s["type"],
            "priority": s["priority"],
            "risk": s["risk"],
            "preconditions": s["preconditions"],
            "expectedResult": s["expectedResult"],
            "tags": s["tags"],
            "steps": s["steps"],
        })
        print(f"Created scenario '{s['title']}' ({created['id']})")
        scenario_ids.append(created["id"])
    return scenario_ids


def ensure_suite(project_id, scenario_ids):
    existing = call("GET", f"/api/v1/projects/{project_id}/suites")
    for suite in existing:
        if suite["name"] == SUITE_NAME:
            call("PUT", f"/api/v1/projects/{project_id}/suites/{suite['id']}", {
                "id": suite["id"],
                "projectId": project_id,
                "name": SUITE_NAME,
                "description": "Guest (no-login) regression coverage across search, sort, filter, PDP, cart and checkout-gate scenarios.",
                "scenarioIds": scenario_ids,
            })
            print(f"Updated suite '{SUITE_NAME}' ({suite['id']}) with {len(scenario_ids)} scenarios")
            return suite["id"]
    created = call("POST", f"/api/v1/projects/{project_id}/suites", {
        "projectId": project_id,
        "name": SUITE_NAME,
        "description": "Guest (no-login) regression coverage across search, sort, filter, PDP, cart and checkout-gate scenarios.",
        "scenarioIds": scenario_ids,
    })
    print(f"Created suite '{SUITE_NAME}' ({created['id']}) with {len(scenario_ids)} scenarios")
    return created["id"]


def main():
    if not API_KEY:
        print("Set API_KEY (create one via Administration -> API Keys in the UI).", file=sys.stderr)
        sys.exit(1)

    project_id = ensure_project()
    environment_id = ensure_environment(project_id)
    set_variables(project_id, environment_id)
    ensure_business_context(project_id)
    ensure_test_data(project_id, environment_id)
    scenario_ids = ensure_scenarios(project_id)
    ensure_suite(project_id, scenario_ids)

    print()
    print(f"Done. Project: {BASE.replace('5125', '5173')}/projects/{project_id}")


if __name__ == "__main__":
    main()
