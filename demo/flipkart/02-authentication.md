# Authentication (Login & Signup)

Flipkart uses **mobile-number + OTP** as the primary authentication method, with
email/password as a secondary option for some accounts.

## Features & requirements

### F1. Open the login dialog
- A **Login** entry point is visible in the header for guest users.
- Clicking it opens a login form asking for a mobile number (or email).

### F2. Request OTP
- The user enters a 10-digit Indian mobile number and requests an OTP.
- **Validation:** reject numbers that are not 10 digits or contain non-numeric characters.
- On success, an OTP is sent and the UI moves to the OTP entry step.

### F3. Verify OTP
- The user enters the received OTP to authenticate.
- **Invalid OTP** shows a clear error and lets the user retry.
- **Expired OTP** prompts the user to request a new one.
- A **Resend OTP** option appears after a short cooldown; resend is rate-limited.

### F4. Session & logout
- After successful login, the header shows the user's account menu.
- The user can log out; logging out returns them to a guest state.
- The session persists across page reloads until logout or expiry.

## Acceptance criteria (examples)
- Given a valid registered mobile number, when a correct OTP is entered, then the user is logged in and their name/account menu appears.
- Given an invalid OTP, when submitted, then an error message is shown and the user remains on the OTP step.
- Given the OTP field, when a non-numeric value is entered, then submission is blocked.
- Given a logged-in user, when they log out, then protected actions (orders, saved addresses) are no longer accessible.

## Negative & edge cases
- Empty mobile number / empty OTP.
- Mobile number with fewer/greater than 10 digits.
- Repeated resend attempts hit a throttle and show a wait message.
- Attempting to reach checkout while logged out redirects to login.
