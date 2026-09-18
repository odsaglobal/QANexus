# Product Details Page (PDP)

## Features & requirements

### F1. Product information
- PDP shows title, price (selling price + MRP + discount %), images/gallery, rating and review count.
- Key highlights/specifications are listed.
- Offers (bank offers, exchange, EMI) are displayed where applicable.

### F2. Variants
- Products with variants (size, color, storage) let the user select a variant.
- Selecting a variant updates price, availability, and images accordingly.

### F3. Delivery & serviceability
- The user can enter a **pincode** to check delivery availability and ETA.
- Non-serviceable pincodes show a clear message; serviceable ones show an ETA.

### F4. Add to Cart / Buy Now
- **Add to Cart** adds the selected variant/quantity to the cart.
- **Buy Now** takes the user directly into the checkout flow.
- Out-of-stock products disable purchase actions and show an out-of-stock label.

### F5. Reviews & ratings
- Users can read ratings distribution and individual reviews.
- Reviews can be sorted/filtered (e.g., most recent, most helpful).

## Acceptance criteria (examples)
- Given an in-stock product, when "Add to Cart" is clicked, then the item appears in the cart with the selected variant and quantity.
- Given a product with variants, when a different variant is selected, then price and availability update to match that variant.
- Given an out-of-stock product, when the PDP loads, then purchase actions are disabled and an out-of-stock message is shown.
- Given a serviceable pincode, when entered, then a delivery ETA is displayed.

## Negative & edge cases
- Selecting a variant that is out of stock disables Add to Cart for that variant.
- Invalid pincode format is rejected.
- Price shown on PDP matches the price shown on the listing card.
