# SauceDemo — User Personas & Test Accounts

All accounts share the **same password: `secret_sauce`**. There is no
registration flow; the following usernames are pre-provisioned in the
Production environment. Each account behaves differently and is designed to
surface a specific class of behaviour.

| Username | Persona | Expected behaviour |
| --- | --- | --- |
| `standard_user` | Happy-path customer | Everything works normally; use for baseline/regression flows. |
| `locked_out_user` | Suspended account | Login is rejected with: "Epic sadface: Sorry, this user has been locked out." |
| `problem_user` | Buggy account | Product images are wrong/broken; some inputs and sort behave incorrectly. |
| `performance_glitch_user` | Slow account | Pages load with a noticeable, artificial delay (tests timeouts/waits). |
| `error_user` | Faulty checkout | Certain actions (e.g. cart/checkout fields) fail or throw errors. |
| `visual_user` | Visual defects | Intentional visual/layout differences for visual-regression scenarios. |

## Persona notes for scenario design
- **standard_user** is the reference persona. Any scenario whose expected result
  is "success" should assume `standard_user` unless stated otherwise.
- **locked_out_user** is the canonical **negative login** persona. The expected
  result is an error banner and staying on the login page.
- **performance_glitch_user** is used to validate that the automation **waits**
  correctly rather than failing on slow loads. Do not treat slowness as a defect
  for this persona.
- **problem_user / error_user / visual_user** are used to demonstrate
  **defect detection** — scenarios should assert the *correct* expected behaviour
  so the platform flags the discrepancy.

## Access rules
- Authentication is required for every screen except the login page.
- Navigating directly to an app route (e.g. `/inventory.html`) while
  unauthenticated redirects back to login with an error.
- Logout is available only from the hamburger menu and returns the user to `/`.
