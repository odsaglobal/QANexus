#!/usr/bin/env python3
"""Seed 10 guest (no-login) Flipkart scenarios into an ATIP project via the API.

Usage:
  API_KEY=qk_... PROJECT_ID=<guid> [BASE=http://localhost:5125] python3 seed-scenarios.py
"""
import json
import os
import sys
import urllib.request

BASE = os.environ.get("BASE", "http://localhost:5125")
API_KEY = os.environ["API_KEY"]
PROJECT_ID = os.environ["PROJECT_ID"]
EMPTY_GUID = "00000000-0000-0000-0000-000000000000"

DISMISS = {
    "action": "If a login / OTP modal appears, close it (click the ✕ / press Escape) and continue as a guest.",
    "expectedResult": "The login modal is dismissed; the storefront is usable without signing in.",
}

SCENARIOS = [
    {
        "title": "Search for a product by keyword",
        "type": "Positive", "priority": "High", "risk": "Medium",
        "tags": ["search", "guest"],
        "preconditions": "Flipkart home page (https://www.flipkart.com) is open. No user is signed in.",
        "expectedResult": "A results listing for the query is shown with a visible result count and a product grid.",
        "steps": [
            DISMISS,
            {"action": "Type \"mobiles under 10000\" into the \"Search for Products, Brands and More\" box.",
             "expectedResult": "The query text appears in the search box; autocomplete suggestions may appear."},
            {"action": "Submit the search (press Enter or click the search icon).",
             "expectedResult": "The results/listing page loads for \"mobiles under 10000\"."},
            {"action": "Observe the results header and grid.",
             "expectedResult": "A result count (e.g. \"Showing 1–24 of N results\") and a grid of products are displayed."},
        ],
    },
    {
        "title": "Sort search results by Price -- High to Low",
        "type": "Positive", "priority": "High", "risk": "Medium",
        "tags": ["search", "sort", "guest"],
        "preconditions": "A search results page (e.g. for \"mobiles under 10000\") is open as a guest.",
        "expectedResult": "The \"Price -- High to Low\" option becomes active and products are re-ordered so prices are non-increasing from top to bottom.",
        "steps": [
            DISMISS,
            {"action": "Locate the sort bar (Relevance, Popularity, Price -- Low to High, Price -- High to Low, Newest First).",
             "expectedResult": "The sort options are visible above the product grid."},
            {"action": "Click \"Price -- High to Low\".",
             "expectedResult": "The option is highlighted/active and the grid refreshes (URL gains a sort parameter such as sort=price_desc)."},
            {"action": "Inspect the prices of the first several products top to bottom.",
             "expectedResult": "Prices are in non-increasing order (each product costs the same or less than the one above it)."},
        ],
    },
    {
        "title": "Sort search results by Price -- Low to High",
        "type": "Positive", "priority": "Medium", "risk": "Low",
        "tags": ["search", "sort", "guest"],
        "preconditions": "A search results page is open as a guest.",
        "expectedResult": "The \"Price -- Low to High\" option becomes active and products are re-ordered so prices are non-decreasing from top to bottom.",
        "steps": [
            DISMISS,
            {"action": "Click \"Price -- Low to High\" in the sort bar.",
             "expectedResult": "The option is active and the grid refreshes (URL gains sort=price_asc)."},
            {"action": "Inspect the prices of the first several products top to bottom.",
             "expectedResult": "Prices are in non-decreasing order (each product costs the same or more than the one above it)."},
        ],
    },
    {
        "title": "Filter search results by Brand",
        "type": "Positive", "priority": "High", "risk": "Medium",
        "tags": ["search", "filter", "guest"],
        "preconditions": "A search results page with a left-rail Brand filter is open as a guest.",
        "expectedResult": "Only products of the selected brand remain and the result count does not increase.",
        "steps": [
            DISMISS,
            {"action": "In the left rail, find the Brand filter and select one available brand (e.g. the first listed brand).",
             "expectedResult": "The brand checkbox is checked and shown as an active filter chip."},
            {"action": "Wait for the grid to refresh.",
             "expectedResult": "The result count does not increase and the grid updates to the filtered set."},
            {"action": "Verify the visible products belong to the selected brand.",
             "expectedResult": "All shown products match the selected brand."},
        ],
    },
    {
        "title": "Search results respect the maximum display limit (no more than 10000 products)",
        "type": "Negative", "priority": "Medium", "risk": "Medium",
        "tags": ["search", "limit", "qa-quantitative", "guest"],
        "preconditions": "A search results page is open as a guest.",
        "expectedResult": "The listing does not present more than 10000 products — the total/displayed result count is less than or equal to 10000.",
        "steps": [
            DISMISS,
            {"action": "Read the result-count text on the listing header (e.g. \"Showing 1–24 of 10,492 results\").",
             "expectedResult": "A numeric total result count is visible."},
            {"action": "Compare the total result count against the 10000 limit.",
             "expectedResult": "The total number of products is 10000 or fewer (if it exceeds 10000, this is a defect)."},
        ],
    },
    {
        "title": "Open a product details page from the results grid",
        "type": "Positive", "priority": "High", "risk": "Medium",
        "tags": ["pdp", "guest"],
        "preconditions": "A search results page with a product grid is open as a guest.",
        "expectedResult": "The product details page (PDP) opens showing the product title, price and ratings.",
        "steps": [
            DISMISS,
            {"action": "Click the first product card in the results grid.",
             "expectedResult": "The product details page opens (a new route/tab for that product)."},
            {"action": "Observe the PDP content.",
             "expectedResult": "The product title, current price (₹) and ratings/reviews count are displayed."},
        ],
    },
    {
        "title": "Browse a category from the top navigation",
        "type": "Positive", "priority": "Medium", "risk": "Low",
        "tags": ["browse", "category", "guest"],
        "preconditions": "Flipkart home page is open as a guest.",
        "expectedResult": "Selecting a category opens a listing scoped to that category with relevant products.",
        "steps": [
            DISMISS,
            {"action": "Hover or click a top-navigation category such as \"Electronics\".",
             "expectedResult": "A menu of sub-categories is revealed."},
            {"action": "Select a sub-category from the menu.",
             "expectedResult": "A category listing page opens showing products relevant to that category."},
        ],
    },
    {
        "title": "Add a product to the cart as a guest",
        "type": "Positive", "priority": "High", "risk": "High",
        "tags": ["cart", "guest"],
        "preconditions": "A product details page (PDP) is open as a guest.",
        "expectedResult": "The item is added to the cart and the header cart count increments by one; the cart shows the line item.",
        "steps": [
            DISMISS,
            {"action": "On the PDP, click \"Add to Cart\".",
             "expectedResult": "A confirmation is shown and the header cart badge increases by 1."},
            {"action": "Open the cart from the header.",
             "expectedResult": "The cart page lists the added product with a price summary. (Do not proceed to Place Order, which requires login.)"},
        ],
    },
    {
        "title": "Search with an invalid query returns no results / empty state",
        "type": "Negative", "priority": "Medium", "risk": "Low",
        "tags": ["search", "empty-state", "guest"],
        "preconditions": "Flipkart home page is open as a guest.",
        "expectedResult": "An invalid/gibberish query yields zero results or a clear no-results/empty-state message (not an error page).",
        "steps": [
            DISMISS,
            {"action": "Type the gibberish query \"zxqwlkjhgf12345\" into the search box and submit.",
             "expectedResult": "The search executes for the gibberish term."},
            {"action": "Observe the results area.",
             "expectedResult": "Zero products are shown or a \"no results\"/empty-state message is displayed; the page does not error."},
        ],
    },
    {
        "title": "Check delivery availability by pincode on a product page",
        "type": "Positive", "priority": "Medium", "risk": "Low",
        "tags": ["pdp", "delivery", "guest"],
        "preconditions": "A product details page (PDP) with a delivery/pincode field is open as a guest.",
        "expectedResult": "Entering a valid 6-digit pincode shows a delivery estimate or serviceability message for that location.",
        "steps": [
            DISMISS,
            {"action": "On the PDP, enter the pincode \"560001\" in the delivery/pincode field and confirm it.",
             "expectedResult": "The pincode is accepted."},
            {"action": "Observe the delivery information.",
             "expectedResult": "A delivery estimate (e.g. an expected delivery date) or a serviceability message is displayed for 560001."},
        ],
    },
]


def post(scn):
    body = {
        "projectId": PROJECT_ID,
        "featureId": EMPTY_GUID,
        "title": scn["title"],
        "type": scn["type"],
        "priority": scn["priority"],
        "risk": scn["risk"],
        "preconditions": scn.get("preconditions"),
        "expectedResult": scn.get("expectedResult"),
        "tags": scn.get("tags", []),
        "steps": scn["steps"],
    }
    req = urllib.request.Request(
        f"{BASE}/api/v1/projects/{PROJECT_ID}/scenarios/manual",
        data=json.dumps(body).encode(),
        headers={"Content-Type": "application/json", "X-Api-Key": API_KEY},
        method="POST",
    )
    with urllib.request.urlopen(req) as resp:
        d = json.load(resp)
        return d.get("id"), len(d.get("steps", []))


def main():
    print(f"Seeding {len(SCENARIOS)} scenarios into project {PROJECT_ID}")
    for i, scn in enumerate(SCENARIOS, 1):
        try:
            sid, steps = post(scn)
            print(f"  {i:2d}. OK  {scn['title'][:60]:<60} id={sid} steps={steps}")
        except urllib.error.HTTPError as e:
            print(f"  {i:2d}. ERR {scn['title'][:60]:<60} {e.code} {e.read().decode()[:200]}")
            sys.exit(1)


if __name__ == "__main__":
    main()
