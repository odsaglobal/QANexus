# Flipkart — Business Rules & Test Data

## Pricing & display rules
- All prices are in **INR (₹)**. A discounted item shows the current price, an original struck-through
  price, and a discount percentage (e.g. `₹9,999  ₹12,999  23% off`).
- "Assured" badge / "Bestseller" tags may appear on cards; they are informational.
- Ratings show as `<value>★` with a separate "N Ratings & M Reviews" count.

## Sort rules (listing page)
- **Price -- Low to High**: product prices must be **non-decreasing** top→bottom.
- **Price -- High to Low**: product prices must be **non-increasing** top→bottom.
- **Newest First / Popularity / Relevance**: ordering is site-defined; assert the option becomes active
  and the grid updates.

## Filter rules
- **Brand** filter: only products of the selected brand(s) remain.
- **Price** filter: only products within the chosen min–max remain.
- **Ratings** filter (e.g. "4★ & above"): only products meeting the threshold remain.
- Applying any filter should **not increase** the result count.

## Quantity / limit expectations (examples for QA assertions)
- "Do not show more than 10000 products" → the listing's total result count must be **≤ 10000**.
- "Show at least 1 result for a valid query" → result count **≥ 1**.
- "An invalid/gibberish query shows an empty state or 0 results" → count is 0 or a no-results message.

## Cart rules (guest)
- Adding an item increments the header cart badge by exactly 1.
- Place Order / checkout requires login → **out of scope** for guest testing.

## Authentication rules
- No mobile-number/OTP login is used. Login modals are dismissed and flows continue as guest.

## Test data
### Search queries
- Valid, high-volume: `mobiles under 10000`, `laptop`, `shoes`, `headphones`, `washing machine`
- Category anchor: `Electronics`, `Home & Furniture`
- Invalid / empty-state: `zxqwlkjhgf12345` (expected: no/near-zero results)

### Price ranges
- Budget phones: ₹5,000 – ₹15,000
- Mid laptops: ₹40,000 – ₹70,000

### Pincodes (delivery check)
- `560001` (Bengaluru), `110001` (New Delhi), `400001` (Mumbai)

### Sort values (URL param reference)
- Low to High → `sort=price_asc`
- High to Low → `sort=price_desc`
- Popularity → `sort=popularity`
- Newest First → `sort=recency_desc`
