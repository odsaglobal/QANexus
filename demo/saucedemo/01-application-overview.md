# SauceDemo — Application Overview

## Purpose
SauceDemo is a sample e-commerce web application published by Sauce Labs and used
as a stable, public sandbox for practising UI test automation. It simulates a
minimal online store for outdoor/branded merchandise where a signed-in customer
can browse products, add them to a cart, and complete a checkout.

- **Production URL:** https://www.saucedemo.com
- **Application type:** Single-page web application (desktop and responsive web)
- **Primary domain:** B2C retail / e-commerce
- **Authentication:** Username + password (no self-service registration; accounts are pre-provisioned)

## Core user journey
1. **Log in** with a provided username and the shared password.
2. **Browse the inventory** of six products on the Products page.
3. **Sort** products by name or price.
4. **Add / remove** products to/from the shopping cart.
5. **Review the cart** and proceed to checkout.
6. **Enter customer information** (first name, last name, postal code).
7. **Review the order** (item total, tax, total) on the overview step.
8. **Finish** the order and see the confirmation ("Thank you for your order!").
9. **Log out** via the hamburger menu.

## Primary screens
| Screen | Route (path) | Description |
| --- | --- | --- |
| Login | `/` | Username/password entry; error banner on invalid credentials |
| Products (Inventory) | `/inventory.html` | Grid of 6 products, sort dropdown, add-to-cart buttons |
| Product Detail | `/inventory-item.html?id={n}` | Single product with description, price, add-to-cart |
| Cart | `/cart.html` | Line items, quantities, Continue Shopping / Checkout |
| Checkout: Your Information | `/checkout-step-one.html` | First name, last name, postal code |
| Checkout: Overview | `/checkout-step-two.html` | Item list, payment/shipping info, totals |
| Checkout: Complete | `/checkout-complete.html` | Order confirmation message |

## Global navigation
- **Hamburger menu (top-left):** All Items, About, Logout, Reset App State.
- **Cart icon (top-right):** shows a badge with the number of items in the cart and links to `/cart.html`.

## Why it matters for QA
SauceDemo is intentionally deterministic and includes several "special" user
accounts that reproduce common real-world defects (broken images, slow
performance, UI glitches, locked accounts). This makes it ideal for
demonstrating autonomous exploration, scenario generation, and self-healing test
execution against predictable, reproducible behaviour.
