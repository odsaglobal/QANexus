# Flipkart — Product Overview & Test Scope

## About
Flipkart is a large Indian e-commerce marketplace where customers browse, search,
and buy products across categories (electronics, fashion, home, grocery, and more).
This document set is the **business context** for AI-driven test generation and
autonomous exploration of the Flipkart web application.

- **Target application:** https://www.flipkart.com
- **Primary platforms:** Responsive web (desktop + mobile web), native apps (out of scope here)
- **User types:** Guest (not logged in), Registered customer, Seller (out of scope)

## In-scope modules
1. Authentication (login / signup via mobile OTP)
2. Search & Browse (search, category listing, filters, sort)
3. Product Details (PDP)
4. Cart & Checkout (cart, address, payment, order placement)
5. Orders & Account (order history, returns/cancellations, wishlist, profile)

## High-level user journeys
- **Discover → Buy:** open home → search a product → open PDP → add to cart → checkout → pay → order confirmed.
- **Returning customer:** login via OTP → reorder from order history.
- **Compare & shortlist:** search → apply filters/sort → add multiple items to wishlist.

## Global quality expectations
- Pages load within acceptable time; no broken images or dead links on primary flows.
- Prices, offers, and delivery estimates are consistent between listing and PDP.
- Cart totals (MRP, discount, delivery fee, total payable) are always mathematically correct.
- The app is usable on common viewport sizes (desktop 1280+, mobile 360–414 width).
- Accessibility: interactive elements are keyboard-reachable and have accessible names.

## Key risks to prioritize in testing
- Price/offer mismatch between PLP, PDP, and cart.
- Cart total miscalculation with coupons, delivery fees, and multiple quantities.
- OTP login edge cases (invalid/expired OTP, resend throttling).
- Payment failure handling and order state consistency.
- Out-of-stock and pincode-not-serviceable handling.
