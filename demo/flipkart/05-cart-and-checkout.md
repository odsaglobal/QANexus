# Cart & Checkout

## Features & requirements

### F1. Cart management
- The cart lists added items with image, title, variant, unit price, and quantity.
- Users can **increase/decrease quantity** or **remove** an item.
- The cart shows a price breakup: total MRP, discount, delivery fee, and **total payable**.
- Removing all items shows an **empty cart** state.

### F2. Price & offers
- Applying a valid **coupon/offer** reduces the payable amount and is reflected in the breakup.
- Invalid/expired coupons show an error and do not change the total.
- Delivery fee rules (e.g., free above a threshold) are applied correctly.

### F3. Login gate
- Proceeding to checkout requires the user to be logged in; guests are prompted to log in.

### F4. Address
- The user selects a saved **delivery address** or adds a new one.
- Address form validates required fields (name, 10-digit phone, 6-digit pincode, line, city, state).

### F5. Payment & order placement
- Payment options include UPI, cards, net banking, wallets, and Cash on Delivery (where eligible).
- On successful payment/placement, an **order confirmation** with an order id is shown.
- On payment failure, the user is returned to payment with a clear error and no order is created.

## Acceptance criteria (examples)
- Given a cart with quantity 2 of a ₹500 item, when viewed, then the item subtotal is ₹1000 and the total payable reflects any delivery fee/discount correctly.
- Given a valid coupon, when applied, then the discount line updates and the total payable decreases accordingly.
- Given a guest user, when proceeding to checkout, then they are prompted to log in first.
- Given a successful order, when placed, then an order id and confirmation are displayed and the cart is emptied.

## Negative & edge cases
- Decreasing quantity below 1 removes the item or is blocked with a minimum-of-1 rule.
- Applying an invalid coupon shows an error and keeps the total unchanged.
- Address form rejects a pincode that is not 6 digits or a phone that is not 10 digits.
- Payment failure does not create an order and does not clear the cart.
- Cart total = sum(item unit price × quantity) − discounts + delivery fee (must always balance).
