# SauceDemo — Login Requirements

## Overview
The login page at `/` is the single entry point to the application. It contains
a **Username** field, a **Password** field, and a **Login** button. A logo and a
panel listing accepted usernames and the shared password are shown for
convenience.

## Functional requirements

### FR-LOGIN-1: Successful login
- **Given** a valid, non-locked username (e.g. `standard_user`) and password `secret_sauce`
- **When** the user clicks **Login**
- **Then** the user is redirected to `/inventory.html` and the Products page is displayed.

### FR-LOGIN-2: Locked-out user
- **Given** the username `locked_out_user` with the correct password
- **When** the user clicks **Login**
- **Then** login is rejected and the error banner shows:
  "Epic sadface: Sorry, this user has been locked out."
- **And** the user remains on the login page.

### FR-LOGIN-3: Invalid credentials
- **Given** an unknown username or a wrong password
- **When** the user clicks **Login**
- **Then** the error banner shows:
  "Epic sadface: Username and password do not match any user in this service."

### FR-LOGIN-4: Missing username
- **Given** the Username field is empty
- **When** the user clicks **Login**
- **Then** the error banner shows: "Epic sadface: Username is required."

### FR-LOGIN-5: Missing password
- **Given** a username is entered but the Password field is empty
- **When** the user clicks **Login**
- **Then** the error banner shows: "Epic sadface: Password is required."

### FR-LOGIN-6: Error banner dismissal
- The error banner includes an **✕** control that clears the message when clicked.

## Acceptance criteria
- The error banner is red and appears above the form.
- On success, the URL changes to `/inventory.html` and the cart icon is visible.
- The password is masked in the input field.
- Field validation for empty username takes precedence over empty password.
