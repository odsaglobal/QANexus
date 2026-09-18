# Flipkart — Application Overview

## What it is
Flipkart (https://www.flipkart.com) is a large Indian e-commerce marketplace. This project covers the
**public web storefront** exercised as a **guest / anonymous shopper** — i.e. **no mobile-number + OTP
login is required or used**. All flows below can be completed without signing in.

## Primary surfaces (guest-accessible)
- **Home page** — top navigation (Electronics, TVs & Appliances, Men, Women, Baby & Kids, Home &
  Furniture, Sports Books & More, Flights, Offer Zone), promotional banners, category shortcuts, and a
  global **search box** ("Search for Products, Brands and More").
- **Search results / listing page** — product grid, result count ("Showing 1–24 of N results"), sort
  options (Relevance, Popularity, Price -- Low to High, Price -- High to Low, Newest First) and left-rail
  filters (Categories, Brand, RAM, Price, Ratings, etc.).
- **Product Details Page (PDP)** — title, price + discount, ratings & reviews count, highlights, offers,
  delivery/pincode check, seller, "Add to Cart" and "Buy Now".
- **Cart** — line items, quantity, price summary, "Place Order" (which then prompts login — out of scope).
- **Wishlist / Save** — clicking the heart on a product typically prompts login (treat as guest limitation).

## Authentication policy for this project
- **Do NOT perform mobile login / OTP.** Scenarios operate as a guest.
- If a **login modal** appears (it often auto-opens on Flipkart), it must be **dismissed** (close ✕ /
  Escape) and the flow continued as a guest.
- Flows that inherently require an account (place order / payment / saved addresses) are **out of scope**.

## Known site characteristics (important for automation)
- The home page is a **highly dynamic SPA**: auto-rotating carousels, lazy-loaded sections, and element
  references that churn — pause animations and re-locate elements before interacting.
- A **login/OTP modal** frequently intercepts the first click; dismiss it first.
- Sorting/filtering updates the listing via client-side navigation (URL gains `sort=` / filter params).
- Prices are in INR (₹) and often include an original (struck-through) price + discount %.
