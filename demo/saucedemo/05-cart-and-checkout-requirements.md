# SauceDemo — Cart & Checkout Requirements

## Cart (`/cart.html`)

### FR-CART-1: View cart
- The cart lists each added product with its name, description, price, and
  quantity (always 1 per product in this app).

### FR-CART-2: Remove from cart
- Each line item has a **Remove** button that deletes it from the cart and updates
  the cart badge.

### FR-CART-3: Continue shopping
- A **Continue Shopping** button returns the user to `/inventory.html`.

### FR-CART-4: Checkout entry
- A **Checkout** button navigates to `/checkout-step-one.html`.
- Checkout is allowed with one or more items in the cart.

## Checkout Step One — Your Information (`/checkout-step-one.html`)

### FR-CHK-1: Required fields
- Fields: **First Name**, **Last Name**, **Zip/Postal Code** — all required.

### FR-CHK-2: Validation
- Clicking **Continue** with any field empty shows an error banner:
  - First name missing: "Error: First Name is required"
  - Last name missing: "Error: Last Name is required"
  - Postal code missing: "Error: Postal Code is required"

### FR-CHK-3: Proceed
- With all fields populated, **Continue** navigates to `/checkout-step-two.html`.

### FR-CHK-4: Cancel
- A **Cancel** button returns to the cart.

## Checkout Step Two — Overview (`/checkout-step-two.html`)

### FR-CHK-5: Order summary
- Lists all items being purchased with prices.
- Shows **Payment Information** and **Shipping Information** labels.

### FR-CHK-6: Totals
- **Item total** = sum of item prices (label: "Item total: $X").
- **Tax** = 8% of the item total (label: "Tax: $Y"), rounded to two decimals.
- **Total** = item total + tax (label: "Total: $Z").

### FR-CHK-7: Finish
- **Finish** completes the order and navigates to `/checkout-complete.html`.

### FR-CHK-8: Cancel
- **Cancel** returns to `/inventory.html`.

## Checkout Complete (`/checkout-complete.html`)

### FR-CHK-9: Confirmation
- Displays a **"Thank you for your order!"** header and a dispatch confirmation
  message with a pony-express image.
- A **Back Home** button returns to `/inventory.html`.
- The cart badge is cleared after a completed order.

## Worked example (tax calculation)
Cart: Sauce Labs Backpack ($29.99) + Sauce Labs Bike Light ($9.99)
- Item total = **$39.98**
- Tax (8%) = **$3.20**
- Total = **$43.18**
