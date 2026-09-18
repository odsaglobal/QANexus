# Search & Browse (Listing, Filters, Sort)

## Features & requirements

### F1. Search
- A persistent **search box** is available in the header.
- Typing a query shows autocomplete suggestions.
- Submitting a query navigates to a **search results / product listing page (PLP)**.
- **Empty query** should not navigate or should show a gentle prompt.
- Results show product cards with image, title, price, rating, and key offers.

### F2. Category browse
- Users can open a category (e.g., Electronics → Mobiles) from menus.
- Category pages behave like PLPs with the same card layout.

### F3. Filters
- PLP provides filters such as **price range, brand, rating, discount, availability**.
- Applying a filter narrows the result set and reflects in an active-filter summary.
- Multiple filters combine (logical AND across facets).
- Filters can be cleared individually or all at once.

### F4. Sort
- Sort options include **Relevance, Price: Low to High, Price: High to Low, Newest, Popularity**.
- Selecting a sort reorders the results accordingly.

### F5. Pagination / infinite scroll
- Results load additional items via pagination or scroll.
- The result count reflects the active query and filters.

## Acceptance criteria (examples)
- Given a search for "iphone", when submitted, then the PLP shows relevant products with the query echoed on the page.
- Given a PLP, when "Price: Low to High" is selected, then products are ordered by ascending price.
- Given a brand filter, when applied, then only products of that brand remain and the active-filter chip is shown.
- Given two filters (brand + price range), when applied together, then results satisfy both constraints.

## Negative & edge cases
- Search with no results shows a "no results" state (not an error).
- Applying a filter that yields zero products shows an empty state and allows clearing filters.
- Very long queries and special characters are handled without breaking the page.
