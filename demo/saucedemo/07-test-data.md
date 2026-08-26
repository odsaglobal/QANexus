# SauceDemo — Test Data Reference

This document lists reusable test data for the **Production** environment. Values
can be mapped to environment variables (`{{token}}`) for data-driven runs.

## Base URL
| Key | Value |
| --- | --- |
| `baseUrl` | https://www.saucedemo.com |

## Credentials (password is shared)
| Key | Value | Notes |
| --- | --- | --- |
| `password` | secret_sauce | Same for every account |
| `standardUser` | standard_user | Happy-path account |
| `lockedOutUser` | locked_out_user | Always rejected |
| `problemUser` | problem_user | Broken images / buggy inputs |
| `performanceGlitchUser` | performance_glitch_user | Slow loads |
| `errorUser` | error_user | Checkout failures |
| `visualUser` | visual_user | Visual defects |

## Checkout customer information
| Key | Value |
| --- | --- |
| `firstName` | Jordan |
| `lastName` | Rivera |
| `postalCode` | 94107 |

## Reference products & prices (USD)
| Product | Price |
| --- | --- |
| Sauce Labs Backpack | 29.99 |
| Sauce Labs Bike Light | 9.99 |
| Sauce Labs Bolt T-Shirt | 15.99 |
| Sauce Labs Fleece Jacket | 49.99 |
| Sauce Labs Onesie | 7.99 |
| Test.allTheThings() T-Shirt (Red) | 15.99 |

## Expected messages
| Situation | Message |
| --- | --- |
| Locked-out login | Epic sadface: Sorry, this user has been locked out. |
| Bad credentials | Epic sadface: Username and password do not match any user in this service. |
| Missing username | Epic sadface: Username is required. |
| Missing password | Epic sadface: Password is required. |
| Missing first name | Error: First Name is required |
| Missing last name | Error: Last Name is required |
| Missing postal code | Error: Postal Code is required |
| Order complete | Thank you for your order! |

## Suggested sample suite (for the demo)
1. **Successful checkout (standard_user)** — log in, add Backpack + Bike Light, checkout, finish, verify "Thank you for your order!" and Total $43.18.
2. **Locked-out login** — log in as locked_out_user, verify locked-out error.
3. **Empty checkout validation** — proceed to checkout, submit blank form, verify each required-field error.
4. **Sort by price (low to high)** — verify Onesie ($7.99) is first.
5. **Remove from cart** — add two items, remove one, verify cart badge decrements.
