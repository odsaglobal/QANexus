# SauceDemo — Products (Inventory) Requirements

## Overview
After login, the user lands on the Products page (`/inventory.html`), which
displays a fixed catalogue of **six products** in a responsive grid. Each product
card shows an image, a name (link), a short description, a price, and an
**Add to cart** button.

## Catalogue
| # | Product name | Price (USD) |
| --- | --- | --- |
| 1 | Sauce Labs Backpack | 29.99 |
| 2 | Sauce Labs Bike Light | 9.99 |
| 3 | Sauce Labs Bolt T-Shirt | 15.99 |
| 4 | Sauce Labs Fleece Jacket | 49.99 |
| 5 | Sauce Labs Onesie | 7.99 |
| 6 | Test.allTheThings() T-Shirt (Red) | 15.99 |

## Functional requirements

### FR-INV-1: Product listing
- All six products are displayed on load with name, image, description, and price.

### FR-INV-2: Sorting
- A **sort dropdown** (top-right) offers four options:
  - **Name (A to Z)** — default
  - **Name (Z to A)**
  - **Price (low to high)**
  - **Price (high to low)**
- Selecting an option immediately re-orders the grid accordingly.

### FR-INV-3: Add to cart
- Clicking **Add to cart** on a product adds one unit, increments the cart badge,
  and changes the button label to **Remove**.

### FR-INV-4: Remove from cart
- Clicking **Remove** removes the unit, decrements the cart badge (hidden at 0),
  and restores the **Add to cart** label.

### FR-INV-5: Product detail navigation
- Clicking a product name or image opens `/inventory-item.html?id={n}` showing the
  product's full description, price, and an Add to cart / Remove control.
- A **Back to products** control returns to `/inventory.html`.

### FR-INV-6: Cart badge
- The cart icon badge reflects the total number of items currently in the cart and
  is hidden when the cart is empty.

## Acceptance criteria
- Default sort on first load is **Name (A to Z)**.
- Add/Remove state persists while navigating between the list and detail pages.
- For `problem_user`, product images may render incorrectly — this is a known
  defect to be detected, not the expected behaviour.
