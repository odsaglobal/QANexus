# Flipkart — Guest Shopping Flows (No Login)

All flows are performed as an anonymous guest. Whenever a login/OTP modal appears, close it and continue.

## 1. Global search
- Type a query into the "Search for Products, Brands and More" box and submit (Enter or the search icon).
- The results/listing page shows a **result count** and a product grid.
- Autocomplete suggestions may appear while typing; selecting one runs the search.

## 2. Category browsing
- Hovering / clicking a top-nav category (e.g. Electronics, Home & Furniture) reveals sub-categories.
- Selecting a sub-category opens a listing page scoped to that category.

## 3. Sort results
- Sort options on the listing page: **Relevance, Popularity, Price -- Low to High, Price -- High to Low,
  Newest First**.
- Selecting a sort option must **actually re-order** the products and mark that option as active. The URL
  typically gains a `sort=` parameter (e.g. `price_asc`, `price_desc`).

## 4. Filter results (left rail)
- Filters include **Brand**, **Price** (min/max slider or ranges), **RAM**, **Ratings**, **Categories**.
- Applying a filter narrows the grid; the result count decreases and active filters are shown as chips.

## 5. Open a product (PDP)
- Clicking a product card opens its details page in a new tab/route.
- PDP shows title, price + discount, rating & review counts, highlights, offers, and delivery options.

## 6. Delivery / pincode check
- Entering a 6-digit pincode on the PDP shows estimated delivery date / serviceability.

## 7. Add to cart (guest)
- "Add to Cart" adds the item and the cart count in the header increments.
- Opening the cart shows the line item and a price summary. **Place Order prompts login → stop there.**

## 8. Wishlist / save
- The heart icon on a product attempts to save it; as a guest this usually prompts login → treat the login
  prompt as the expected guest boundary (do not log in).

## Guardrails / expected results worth asserting
- Result counts, and quantitative limits (e.g. "no more than N results/products shown").
- Sort correctness (prices non-increasing for High→Low, non-decreasing for Low→High).
- Applied-filter effects (count decreases; only matching brand/price items shown).
- Cart count increments by exactly 1 per add.
- Login modal is **dismissible** and the guest flow continues without an account.
