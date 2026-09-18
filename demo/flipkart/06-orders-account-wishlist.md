# Orders, Account & Wishlist

## Features & requirements

### F1. Order history
- Logged-in users can view a list of past and current orders.
- Each order shows items, order status (Confirmed, Packed, Shipped, Delivered, Cancelled), and totals.
- Users can open an order to see detailed status and tracking.

### F2. Cancellations & returns
- Eligible orders can be **cancelled** before shipping.
- Delivered items within the return window can be **returned/replaced** per policy.
- The UI clearly communicates eligibility and the resulting refund/replacement flow.

### F3. Wishlist / Saved items
- Users can **save products to a wishlist** from PLP and PDP.
- The wishlist lists saved products and allows moving an item to the cart or removing it.

### F4. Profile & addresses
- Users can view/edit profile details (name, email, phone).
- Users can manage saved **addresses** (add, edit, delete, set default).

## Acceptance criteria (examples)
- Given a logged-in user with past orders, when they open order history, then their orders are listed with correct status and totals.
- Given an eligible order, when the user cancels it, then its status changes to Cancelled and a refund flow is initiated where applicable.
- Given a product, when the user taps "save/wishlist", then it appears in the wishlist and can be moved to the cart.
- Given the address book, when a new valid address is added and set as default, then it is pre-selected at checkout.

## Negative & edge cases
- A guest user cannot access order history and is prompted to log in.
- Cancelling an already-shipped order is blocked with an explanatory message.
- Wishlisting the same product twice does not create duplicates.
- Deleting the default address requires choosing a new default (or none remains).
