#!/usr/bin/env node
/**
 * Seed SauceDemo test cases into TestRail so they can be synced into QANexus and explored.
 *
 * TestRail API: https://support.testrail.com/hc/en-us/articles/7077196481428-Introduction-to-the-TestRail-API
 *
 * Usage:
 *   TESTRAIL_BASE_URL="https://yourcompany.testrail.io" \
 *   TESTRAIL_USER="you@company.com" \
 *   TESTRAIL_APIKEY="your_api_key_or_password" \
 *   node tools/testrail/seed-saucedemo.mjs
 *
 * Required env vars:
 *   TESTRAIL_BASE_URL   Base URL of your TestRail instance (https:// added if missing).
 *   TESTRAIL_USER       TestRail user (email).
 *   TESTRAIL_APIKEY     TestRail API key (Account > API Keys) or password.
 *
 * Optional env vars:
 *   TESTRAIL_SECTION_ID   Add cases under an EXISTING section id. If omitted, the script
 *                         creates a new project + section automatically.
 *   TESTRAIL_PROJECT_ID   Add a section under an EXISTING project id (skips project creation).
 *   TESTRAIL_PROJECT_NAME Name for the created project (default "SauceDemo").
 *   TESTRAIL_SECTION_NAME Name for the created section (default "SauceDemo").
 *
 * The cases use TestRail's "Test Case (Steps)" template fields:
 *   custom_preconds           -> preconditions
 *   custom_steps_separated[]  -> { content, expected } ordered steps
 * These map directly to how QANexus imports TestRail cases (see TestRailClient).
 */

const BASE_URL = normalizeBaseUrl(required('TESTRAIL_BASE_URL'));
const USER = required('TESTRAIL_USER');
const APIKEY = required('TESTRAIL_APIKEY');
const SECTION_ID = process.env.TESTRAIL_SECTION_ID ? Number(process.env.TESTRAIL_SECTION_ID) : null;
const PROJECT_ID = process.env.TESTRAIL_PROJECT_ID ? Number(process.env.TESTRAIL_PROJECT_ID) : null;
const PROJECT_NAME = process.env.TESTRAIL_PROJECT_NAME || 'SauceDemo';
const SECTION_NAME = process.env.TESTRAIL_SECTION_NAME || 'SauceDemo';

function required(name) {
  const v = process.env[name];
  if (!v) {
    console.error(`Missing required env var: ${name}`);
    process.exit(1);
  }
  return v;
}

function normalizeBaseUrl(url) {
  let u = url.trim().replace(/\/+$/, '');
  if (!/^https?:\/\//i.test(u)) {
    u = `https://${u}`;
  }
  return u;
}

const authHeader = 'Basic ' + Buffer.from(`${USER}:${APIKEY}`).toString('base64');

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// undici's default connect timeout (~10s) intermittently trips against the TestRail
// load balancer even though the host is reachable. Retry a few times with backoff.
async function fetchWithRetry(url, init, attempts = 5) {
  let lastErr;
  for (let i = 1; i <= attempts; i++) {
    try {
      return await fetch(url, { ...init, signal: AbortSignal.timeout(30000) });
    } catch (err) {
      lastErr = err;
      const detail = err.cause?.code ?? err.name ?? err.message;
      console.warn(`  … attempt ${i}/${attempts} failed (${detail}); retrying`);
      await sleep(1000 * i);
    }
  }
  throw lastErr;
}

async function trGet(pathAndQuery) {
  const url = `${BASE_URL}/index.php?/api/v2/${pathAndQuery}`;
  const res = await fetchWithRetry(url, {
    method: 'GET',
    headers: { Authorization: authHeader, 'Content-Type': 'application/json' },
  });
  const text = await res.text();
  if (!res.ok) {
    throw new Error(`TestRail GET ${pathAndQuery} failed ${res.status}: ${text}`);
  }
  return text ? JSON.parse(text) : {};
}

async function trPost(pathAndQuery, body) {
  const url = `${BASE_URL}/index.php?/api/v2/${pathAndQuery}`;
  const res = await fetchWithRetry(url, {
    method: 'POST',
    headers: {
      Authorization: authHeader,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(body),
  });
  const text = await res.text();
  if (!res.ok) {
    throw new Error(`TestRail ${pathAndQuery} failed ${res.status}: ${text}`);
  }
  return text ? JSON.parse(text) : {};
}

// SauceDemo (https://www.saucedemo.com) test cases. Steps are written as
// browser actions so the QANexus explorer can execute them against the app.
const cases = [
  {
    title: 'Successful login with standard_user',
    custom_preconds:
      'App is available at https://www.saucedemo.com. Account standard_user exists (password: secret_sauce).',
    steps: [
      { content: 'Navigate to https://www.saucedemo.com', expected: 'The login page is displayed with Username, Password and Login.' },
      { content: 'Enter "standard_user" into the Username field', expected: 'Username field contains standard_user.' },
      { content: 'Enter "secret_sauce" into the Password field', expected: 'Password field is populated (masked).' },
      { content: 'Click the Login button', expected: 'The Products page is shown at /inventory.html with six products.' },
    ],
  },
  {
    title: 'Login rejected for locked_out_user',
    custom_preconds: 'Account locked_out_user exists and is suspended (password: secret_sauce).',
    steps: [
      { content: 'Navigate to https://www.saucedemo.com', expected: 'The login page is displayed.' },
      { content: 'Enter "locked_out_user" into the Username field', expected: 'Username field contains locked_out_user.' },
      { content: 'Enter "secret_sauce" into the Password field', expected: 'Password field is populated.' },
      { content: 'Click the Login button', expected: 'Error banner reads: "Epic sadface: Sorry, this user has been locked out." and the user stays on the login page.' },
    ],
  },
  {
    title: 'Negative login with invalid credentials',
    custom_preconds: 'App is available at https://www.saucedemo.com.',
    steps: [
      { content: 'Navigate to https://www.saucedemo.com', expected: 'The login page is displayed.' },
      { content: 'Enter "no_such_user" into the Username field', expected: 'Username field contains no_such_user.' },
      { content: 'Enter "wrong_password" into the Password field', expected: 'Password field is populated.' },
      { content: 'Click the Login button', expected: 'Error banner reads: "Epic sadface: Username and password do not match any user in this service."' },
    ],
  },
  {
    title: 'Add a product to the cart',
    custom_preconds: 'Logged in as standard_user on the Products page.',
    steps: [
      { content: 'Navigate to https://www.saucedemo.com and log in as standard_user / secret_sauce', expected: 'The Products page is displayed.' },
      { content: 'Click "Add to cart" on Sauce Labs Backpack', expected: 'The button changes to Remove and the cart badge shows 1.' },
      { content: 'Click the cart icon in the top-right', expected: 'The cart page lists Sauce Labs Backpack.' },
    ],
  },
  {
    title: 'Sort products by price low to high',
    custom_preconds: 'Logged in as standard_user on the Products page.',
    steps: [
      { content: 'Navigate to https://www.saucedemo.com and log in as standard_user / secret_sauce', expected: 'The Products page is displayed.' },
      { content: 'Select "Price (low to high)" in the sort dropdown', expected: 'Products reorder by ascending price; Sauce Labs Onesie ($7.99) is first.' },
    ],
  },
  {
    title: 'Complete checkout end to end',
    custom_preconds: 'Logged in as standard_user with at least one item available to add.',
    steps: [
      { content: 'Navigate to https://www.saucedemo.com and log in as standard_user / secret_sauce', expected: 'The Products page is displayed.' },
      { content: 'Add Sauce Labs Backpack and Sauce Labs Bike Light to the cart', expected: 'The cart badge shows 2.' },
      { content: 'Open the cart and click Checkout', expected: 'The Checkout: Your Information page is displayed.' },
      { content: 'Enter First Name "Jordan", Last Name "Rivera", Zip "94107" and click Continue', expected: 'The Checkout: Overview page shows the two items and totals.' },
      { content: 'Verify the item total, 8% tax and total, then click Finish', expected: 'The confirmation page shows "Thank you for your order!".' },
    ],
  },
  {
    title: 'Checkout validation requires customer information',
    custom_preconds: 'Logged in as standard_user with an item in the cart.',
    steps: [
      { content: 'Navigate to https://www.saucedemo.com and log in as standard_user / secret_sauce', expected: 'The Products page is displayed.' },
      { content: 'Add any product to the cart, open the cart and click Checkout', expected: 'The Checkout: Your Information page is displayed.' },
      { content: 'Leave all fields blank and click Continue', expected: 'Error banner reads: "Error: First Name is required".' },
    ],
  },
];

async function resolveSectionId() {
  if (SECTION_ID) {
    console.log(`Using existing section ${SECTION_ID}.`);
    return SECTION_ID;
  }

  // Resolve or create the project.
  let projectId = PROJECT_ID;
  if (!projectId) {
    const project = await trPost('add_project', {
      name: PROJECT_NAME,
      announcement: 'Seeded SauceDemo test cases for QANexus exploration.',
      show_announcement: true,
      suite_mode: 1, // single repository
    });
    projectId = project.id;
    console.log(`Created TestRail project ${PROJECT_NAME} (id ${projectId}).`);
  } else {
    console.log(`Using existing project id ${projectId}.`);
  }

  // Find the default suite (single-suite mode has exactly one).
  const suites = await trGet(`get_suites/${projectId}`);
  const suiteId = Array.isArray(suites) && suites.length > 0 ? suites[0].id : undefined;

  // Create the section.
  const section = await trPost(`add_section/${projectId}`, {
    name: SECTION_NAME,
    ...(suiteId ? { suite_id: suiteId } : {}),
  });
  console.log(`Created section ${SECTION_NAME} (id ${section.id}) in project ${projectId}.`);
  console.log(`\n>>> TestRail Project ID for QANexus sync: ${projectId}\n`);
  return section.id;
}

async function main() {
  const sectionId = await resolveSectionId();
  console.log(`Seeding ${cases.length} SauceDemo cases into TestRail section ${sectionId}…`);
  for (const c of cases) {
    const created = await trPost(`add_case/${sectionId}`, {
      title: c.title,
      template_id: 2, // "Test Case (Steps)" — falls back gracefully if your template differs
      custom_preconds: c.custom_preconds,
      custom_steps_separated: c.steps,
    });
    console.log(`  ✓ C${created.id}  ${c.title}`);
  }
  console.log('Done. In QANexus → Scenarios → "Sync from TestRail", enter the TestRail project id');
  console.log('shown above to import these as scenarios you can explore.');
}

main().catch((err) => {
  console.error(err.message);
  if (err.cause) {
    console.error('cause:', err.cause?.code ?? '', err.cause?.message ?? err.cause);
  }
  process.exit(1);
});
