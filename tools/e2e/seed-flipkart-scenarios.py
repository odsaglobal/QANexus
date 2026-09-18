#!/usr/bin/env python3
"""Replace every scenario in the Flipkart project with a freshly explored set.

Deletes all existing scenarios through the API, then creates one scenario per user journey with
steps grounded in a live Playwright exploration of www.flipkart.com (Sept 2026 layout).

Expected results are phrased so ExplorerAgent's zero-token local confirmation can decide them:
quoted literals appear verbatim on the page or in the URL, and every other word is QA boilerplate.
"""
import json
import sys
import urllib.error
import urllib.request

BASE = "http://localhost:5125/api/v1"
API_KEY = "qk_flipkart_seed_20260904_x7f2"
PROJECT_ID = "87c3b228-cb0e-42e8-a3ee-a5ff57f7dd41"
FEATURE_ID = "50cc4ad8-eaf6-43c2-b048-24185eac8499"

HOME = "Open https://www.flipkart.com/"
HOME_EXPECT = 'The "Search for Products, Brands and More" search box is displayed.'


def search(term):
    return (
        f'Type "{term}" into the "Search for Products, Brands and More" search box and press Enter.',
        f'The search results page for "{term}" is displayed.',
    )


OPEN_FIRST_PDP = (
    "Click the first product card in the search results grid to open its product details page.",
    'The product details page is displayed with the "Select delivery location" option.',
)

SCENARIOS = [
    {
        "title": "Search for a product by keyword",
        "type": "Positive",
        "priority": "High",
        "preconditions": "The shopper is browsing as a guest and is not signed in.",
        "steps": [
            (HOME, HOME_EXPECT),
            search("laptop"),
            ('Read the results summary line shown above the product grid.',
             'The "results for" text is displayed.'),
        ],
    },
    {
        "title": "Search falls back to related products for an unmatched keyword",
        "type": "Negative",
        "priority": "Medium",
        "preconditions": "The shopper is browsing as a guest. Flipkart has no exact match for the keyword used.",
        "steps": [
            (HOME, HOME_EXPECT),
            search("qwzxjkvbnmplokijuhygtfrdesw"),
            ('Read the results area to see how Flipkart handles a keyword that matches no product.',
             'Product results are displayed with the "results for" text, because Flipkart falls back to related products instead of an empty state.'),
        ],
    },
    {
        "title": "Sort search results by Price -- Low to High",
        "type": "Positive",
        "priority": "High",
        "preconditions": "The shopper is browsing as a guest and has a populated results grid.",
        "steps": [
            (HOME, HOME_EXPECT),
            search("laptop"),
            ('Click the "Price -- Low to High" choice in the "Sort By" bar above the results grid.',
             'The results page for "sort=price_asc" is displayed.'),
            ('Read the "Sort By" bar to see which choice is active.',
             'The "Price -- Low to High" sort is displayed.'),
        ],
    },
    {
        "title": "Sort search results by Price -- High to Low",
        "type": "Positive",
        "priority": "High",
        "preconditions": "The shopper is browsing as a guest and has a populated results grid.",
        "steps": [
            (HOME, HOME_EXPECT),
            search("laptop"),
            ('Click the "Price -- High to Low" choice in the "Sort By" bar above the results grid.',
             'The results page for "sort=price_desc" is displayed.'),
            ('Read the "Sort By" bar to see which choice is active.',
             'The "Price -- High to Low" sort is displayed.'),
        ],
    },
    {
        "title": "Filter search results by Brand",
        "type": "Positive",
        "priority": "High",
        "preconditions": "The shopper is browsing as a guest and has a populated results grid with a Brand facet.",
        "steps": [
            (HOME, HOME_EXPECT),
            search("laptop"),
            ('In the "Filters" sidebar, open the "Brand" facet if it is collapsed and tick the "HP" checkbox.',
             'The results page for "facets.brand" is displayed.'),
            ('Read the "Filters" sidebar to see which brand facet is ticked.',
             'The "HP" brand filter is displayed.'),
        ],
    },
    {
        "title": "Open a product details page from the results grid",
        "type": "Positive",
        "priority": "High",
        "preconditions": "The shopper is browsing as a guest and has a populated results grid.",
        "steps": [
            (HOME, HOME_EXPECT),
            search("laptop"),
            OPEN_FIRST_PDP,
        ],
    },
    {
        "title": "Add a product to the cart as a guest",
        "type": "Positive",
        "priority": "Critical",
        "preconditions": "The shopper is browsing as a guest. Accessories such as cables expose Add to cart; phones offer Buy now only.",
        "steps": [
            (HOME, HOME_EXPECT),
            search("usb cable"),
            ("Click the first product card in the search results grid to open its product details page.",
             'The product details page is displayed with "Add to cart".'),
            ('Click "Add to cart" on the product details page.',
             'The "Go to cart" text is displayed.'),
        ],
    },
    {
        "title": "Review the cart after adding a product as a guest",
        "type": "Positive",
        "priority": "Critical",
        "preconditions": "The shopper is browsing as a guest with an empty cart.",
        "steps": [
            (HOME, HOME_EXPECT),
            search("usb cable"),
            ("Click the first product card in the search results grid to open its product details page.",
             'The product details page is displayed with "Add to cart".'),
            ('Click "Add to cart" on the product details page.',
             'The "Go to cart" text is displayed.'),
            ("Open https://www.flipkart.com/viewcart?marketplace=FLIPKART",
             'The cart page is displayed with the "Price Details" summary and the "Place Order" action.'),
        ],
    },
    {
        "title": "Delivery details are shown on a product page for a guest",
        "type": "Positive",
        "priority": "Medium",
        "preconditions": "The shopper is browsing as a guest and has not chosen a delivery location.",
        "steps": [
            (HOME, HOME_EXPECT),
            search("usb cable"),
            OPEN_FIRST_PDP,
            ('Read the delivery information on the product details page.',
             'The "Select delivery location" and "Delivery by" texts are displayed.'),
        ],
    },
    {
        "title": "Exchange-offer pincode messaging is shown on a phone page for a guest",
        "type": "Positive",
        "priority": "Medium",
        "preconditions": "The shopper is browsing as a guest and has not chosen a delivery pincode.",
        "steps": [
            (HOME, HOME_EXPECT),
            search("mobile"),
            ("Click the first product card in the search results grid to open the phone product details page.",
             'The product details page is displayed with the "Exchange offer" section.'),
            ('Read the "Exchange offer" row while no delivery pincode has been chosen.',
             'The "Change pincode to exchange item" text is displayed.'),
        ],
    },
    {
        "title": "Browse a category from the top navigation",
        "type": "Positive",
        "priority": "Medium",
        "preconditions": "The shopper is browsing as a guest on the Flipkart home page.",
        "steps": [
            (HOME, HOME_EXPECT),
            ('Click "Electronics" in the top category navigation bar.',
             'The "Electronics" store page is displayed.'),
            ("Check that the site header survived the category navigation.",
             HOME_EXPECT),
        ],
    },
    {
        "title": "Browse a category via the search results breadcrumb",
        "type": "Positive",
        "priority": "Medium",
        "preconditions": "The shopper is browsing as a guest and has a populated results grid with a category breadcrumb.",
        "steps": [
            (HOME, HOME_EXPECT),
            search("laptop"),
            ('Click the "Laptops" category link in the breadcrumb above the search results.',
             'The "/laptops/pr" page is displayed.'),
        ],
    },
    {
        "title": "Search results respect the maximum display limit (no more than 10000 products)",
        "type": "Boundary",
        "priority": "Low",
        "preconditions": "The shopper is browsing as a guest and searches a very broad keyword.",
        "steps": [
            (HOME, HOME_EXPECT),
            search("shoes"),
            ("Read the results summary line that reports the page range and the total match count.",
             "The summary reports the range of products on the current page out of a total match count that does not exceed 10000."),
        ],
    },
]


def call(method, path, body=None):
    req = urllib.request.Request(
        BASE + path,
        method=method,
        data=json.dumps(body).encode() if body is not None else None,
        headers={"X-Api-Key": API_KEY, "Content-Type": "application/json"},
    )
    try:
        with urllib.request.urlopen(req) as resp:
            raw = resp.read()
            return json.loads(raw) if raw else None
    except urllib.error.HTTPError as exc:
        sys.exit(f"{method} {path} -> {exc.code}: {exc.read().decode()[:500]}")


def main():
    existing = call("GET", f"/projects/{PROJECT_ID}/scenarios") or []
    for scenario in existing:
        call("DELETE", f"/projects/{PROJECT_ID}/scenarios/{scenario['id']}")
    print(f"deleted {len(existing)} scenario(s)")

    for spec in SCENARIOS:
        created = call("POST", f"/projects/{PROJECT_ID}/scenarios/manual", {
            "projectId": PROJECT_ID,
            "featureId": FEATURE_ID,
            "title": spec["title"],
            "type": spec["type"],
            "priority": spec["priority"],
            "risk": spec.get("risk", "Medium"),
            "preconditions": spec["preconditions"],
            "tags": ["flipkart", "guest", "explored"],
            "steps": [{"action": a, "expectedResult": e} for a, e in spec["steps"]],
        })
        print(f"  + {len(spec['steps'])} steps  {created['id']}  {spec['title']}")

    print(f"created {len(SCENARIOS)} scenario(s)")


if __name__ == "__main__":
    main()
