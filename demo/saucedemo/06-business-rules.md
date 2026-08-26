# SauceDemo — Business Rules & Constraints

## Authentication & access
- **BR-1:** All accounts share the password `secret_sauce`.
- **BR-2:** There is no registration or password-reset flow; accounts are fixed.
- **BR-3:** `locked_out_user` can never sign in and must always be rejected with the locked-out message.
- **BR-4:** Every route except the login page requires an authenticated session; direct access while logged out redirects to login with an error.

## Catalogue & pricing
- **BR-5:** The catalogue is fixed at exactly six products; prices do not change.
- **BR-6:** Product prices are fixed in USD (see Inventory Requirements).
- **BR-7:** Default product sort is **Name (A to Z)**.

## Cart
- **BR-8:** Each product can appear in the cart at most once (quantity is always 1).
- **BR-9:** The cart badge always equals the number of distinct items in the cart and is hidden when empty.
- **BR-10:** Cart contents persist across navigation within a session but are cleared after a completed order or logout.

## Checkout
- **BR-11:** First name, last name, and postal code are all mandatory before continuing.
- **BR-12:** Tax is calculated at a flat **8%** of the item subtotal, rounded to two decimals.
- **BR-13:** The order total equals item subtotal plus tax; there is no shipping fee applied to the total.
- **BR-14:** Finishing checkout empties the cart and shows the confirmation page.

## Known-defect personas (expected discrepancies)
- **BR-15:** `problem_user` renders incorrect/broken product images and mis-handles some inputs.
- **BR-16:** `performance_glitch_user` introduces artificial load delays; correct behaviour is *eventual* success, not failure.
- **BR-17:** `error_user` fails on certain cart/checkout interactions.
- **BR-18:** `visual_user` exhibits intentional visual/layout defects.

> For all defect personas, scenarios must assert the **correct** expected behaviour
> so the platform reports the deviation rather than encoding the bug as expected.
