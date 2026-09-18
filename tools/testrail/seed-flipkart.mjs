#!/usr/bin/env node
/**
 * Seed the 10 Flipkart guest (no-login) test cases into TestRail.
 *
 * Mirrors the scenarios created in QANexus (tools/flipkart/seed-scenarios.py) so the same
 * suite exists in both systems. Uses TestRail's "Test Case (Steps)" template:
 *   custom_preconds           -> preconditions
 *   custom_steps_separated[]  -> { content, expected } ordered steps
 *
 * Usage:
 *   TESTRAIL_BASE_URL="https://assertone.testrail.io" \
 *   TESTRAIL_USER="you@company.com" \
 *   TESTRAIL_APIKEY="your_api_key" \
 *   [TESTRAIL_PROJECT_ID=<id>] [TESTRAIL_SECTION_ID=<id>] \
 *   node tools/testrail/seed-flipkart.mjs
 */

const BASE_URL = normalizeBaseUrl(required('TESTRAIL_BASE_URL'));
const USER = required('TESTRAIL_USER');
const APIKEY = required('TESTRAIL_APIKEY');
const SECTION_ID = process.env.TESTRAIL_SECTION_ID ? Number(process.env.TESTRAIL_SECTION_ID) : null;
const PROJECT_ID = process.env.TESTRAIL_PROJECT_ID ? Number(process.env.TESTRAIL_PROJECT_ID) : null;
const PROJECT_NAME = process.env.TESTRAIL_PROJECT_NAME || 'Flipkart';
const SECTION_NAME = process.env.TESTRAIL_SECTION_NAME || 'Flipkart Guest Flows';

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
  if (!res.ok) throw new Error(`TestRail GET ${pathAndQuery} failed ${res.status}: ${text}`);
  return text ? JSON.parse(text) : {};
}

async function trPost(pathAndQuery, body) {
  const url = `${BASE_URL}/index.php?/api/v2/${pathAndQuery}`;
  const res = await fetchWithRetry(url, {
    method: 'POST',
    headers: { Authorization: authHeader, 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  const text = await res.text();
  if (!res.ok) throw new Error(`TestRail ${pathAndQuery} failed ${res.status}: ${text}`);
  return text ? JSON.parse(text) : {};
}

const DISMISS = {
  content: 'If a login / OTP / location popup appears, dismiss it (press Escape, or click its ✕ / "Skip" / "Not now" control) and continue as a guest. Do not log in.',
  expected: 'The popup is closed; the storefront is usable without signing in.',
};

// Flipkart guest (no-login) cases — every step below was walked through live via Playwright MCP
// against https://www.flipkart.com on 2026-09-05 and uses the EXACT concrete labels/values seen on
// the page (never a vague placeholder like "the first option" or "a brand"), so an agent or a human
// tester can execute each step unambiguously and independently (no case depends on another).
const cases = [
  {
    title: 'Search for a product by keyword',
    custom_preconds: 'Flipkart home page (https://www.flipkart.com) is open. No user is signed in.',
    steps: [
      DISMISS,
      { content: 'Click the "Search for Products, Brands and More" textbox in the header and type "laptop", then press Enter.', expected: 'The page navigates to a URL containing "search?q=laptop"; the page title starts with "Laptop-".' },
      { content: 'Observe the results header above the product grid.', expected: 'The header reads a line like "Showing 1 – 24 of 652 results for laptop" (a positive total count in place of 652) and a grid of laptop product cards is displayed below it.' },
    ],
  },
  {
    title: 'Sort search results by Price -- High to Low',
    custom_preconds: 'Flipkart home page is open. No user is signed in.',
    steps: [
      DISMISS,
      { content: 'Search for "laptop" (type it into the header search box and press Enter).', expected: 'The laptop search results page is displayed with a "Sort By" bar above the grid showing: Relevance, Popularity, Price -- Low to High, Price -- High to Low, Newest First.' },
      { content: 'Click the "Price -- High to Low" option in the Sort By bar.', expected: 'The option becomes highlighted/active and the grid refreshes.' },
      { content: 'Read the price of each of the first 5 product cards, top to bottom.', expected: 'Each price is less than or equal to the price of the card above it (non-increasing order).' },
    ],
  },
  {
    title: 'Sort search results by Price -- Low to High',
    custom_preconds: 'Flipkart home page is open. No user is signed in.',
    steps: [
      DISMISS,
      { content: 'Search for "laptop" (type it into the header search box and press Enter).', expected: 'The laptop search results page is displayed with the Sort By bar visible.' },
      { content: 'Click the "Price -- Low to High" option in the Sort By bar.', expected: 'The option becomes highlighted/active and the grid refreshes.' },
      { content: 'Read the price of each of the first 5 product cards, top to bottom.', expected: 'Each price is greater than or equal to the price of the card above it (non-decreasing order).' },
    ],
  },
  {
    title: 'Filter search results by Brand',
    custom_preconds: 'Flipkart home page is open. No user is signed in.',
    steps: [
      DISMISS,
      { content: 'Search for "laptop" (type it into the header search box and press Enter).', expected: 'The laptop search results page is displayed with a left-rail Filters panel. The "Brand" section heading is visible but COLLAPSED (no brand checkboxes shown under it yet).' },
      { content: 'Click the "Brand" section heading to expand it.', expected: 'The Brand section expands and shows concrete checkbox options, including "HP", "Lenovo", "ASUS", "DELL", "Acer", "Apple", plus a "19 MORE" link.' },
      { content: 'Click the "HP" checkbox specifically (not the section heading, not any other brand).', expected: 'The page URL gains the query parameter "facets.brand" set to "HP" (URL contains "facets.brand%255B%255D%3DHP"); an active-filter chip reading "✕ HP" appears at the top of the Filters panel next to a "Clear all" link.' },
      { content: 'Read the results header and the titles of the first 5 product cards.', expected: 'The results header still reads "Showing 1 – N of M results for laptop" (positive N and M, M now smaller than the unfiltered total); every one of the first 5 product titles starts with "HP" (e.g. "HP Victus...", "HP 255 G10...", "HP MS Office 2024...") — no other brand appears.' },
      { content: 'Click the "✕" on the "HP" filter chip (or "Clear all").', expected: 'The chip disappears, the "facets.brand" URL parameter is removed, and the grid reverts to the full mixed-brand "laptop" results.' },
    ],
  },
  {
    title: 'Search results respect the maximum display limit (no more than 10000 products)',
    custom_preconds: 'Flipkart home page is open. No user is signed in.',
    steps: [
      DISMISS,
      { content: 'Search for "laptop" (type it into the header search box and press Enter).', expected: 'The laptop search results page is displayed.' },
      { content: 'Read the numeric total in the results header (e.g. "Showing 1 – 24 of 652 results for laptop").', expected: 'A numeric total result count is visible in the header text.' },
      { content: 'Compare the total result count against 10000.', expected: 'The total number of results is 10000 or fewer. (A total greater than 10000 is a defect to report, not an expected outcome.)' },
    ],
  },
  {
    title: 'Open a product details page from the results grid',
    custom_preconds: 'Flipkart home page is open. No user is signed in.',
    steps: [
      DISMISS,
      { content: 'Search for "laptop" (type it into the header search box and press Enter).', expected: 'The laptop search results page is displayed with a grid of product cards.' },
      { content: 'Click the first product card in the results grid (its title link).', expected: 'The browser navigates to that product\'s details page (PDP), at a URL containing "/p/itm".' },
      { content: 'Read the PDP heading, price and rating.', expected: 'The product title (heading, level 1), a current price prefixed with "₹", and a rating value with a ratings count (e.g. "4.3 | 96") are all visible.' },
    ],
  },
  {
    title: 'Browse a category via the search results category breadcrumb',
    custom_preconds: 'Flipkart home page is open. No user is signed in.',
    steps: [
      DISMISS,
      { content: 'Search for "laptop" (type it into the header search box and press Enter).', expected: 'The laptop search results page is displayed; the left-rail Filters panel shows a "CATEGORIES" section with links "Computers" and, nested under it, "Laptops".' },
      { content: 'Click the "Laptops" category link in the CATEGORIES section.', expected: 'The page navigates to a URL containing "/laptops/pr" (the dedicated Laptops category listing), still showing a laptop product grid.' },
    ],
  },
  {
    title: 'Add a product to the cart as a guest',
    custom_preconds: 'Flipkart home page is open. No user is signed in.',
    steps: [
      DISMISS,
      { content: 'Search for "laptop" (type it into the header search box and press Enter), then click the first product card to open its PDP.', expected: 'A product details page is displayed with an "Add to Cart" control.' },
      { content: 'Click "Add to Cart" on the PDP.', expected: 'The header cart badge/link updates to indicate at least 1 item (e.g. the Cart link shows a non-zero count or the page navigates directly into the cart flow).' },
      { content: 'Click the "Cart" link in the header.', expected: 'The cart page (URL containing "/viewcart") lists the product that was just added. Do not proceed to Place Order, which requires login.' },
    ],
  },
  {
    title: 'Search with an invalid query returns no results / empty state',
    custom_preconds: 'Flipkart home page is open. No user is signed in.',
    steps: [
      DISMISS,
      { content: 'Click the header search box, type the gibberish query "zxqwlkjhgf12345", and press Enter.', expected: 'The page navigates to a URL containing "search?q=zxqwlkjhgf12345" without any JavaScript error dialog or blank/broken page.' },
      { content: 'Observe the results area.', expected: 'Either zero product cards are shown, or an explicit "no results" / empty-state message is displayed. The page must not show unrelated products as if the search had matched.' },
    ],
  },
  {
    title: 'Exchange-offer pincode messaging is shown on a product page for a guest',
    custom_preconds: 'Flipkart home page is open. No user is signed in, and no delivery location/pincode has been set.',
    steps: [
      DISMISS,
      { content: 'Search for "laptop" (type it into the header search box and press Enter), then click the first product card to open its PDP.', expected: 'A product details page is displayed with a price/offers panel that includes an "Exchange offer" row.' },
      { content: 'Read the "Exchange offer" row text.', expected: 'It reads "Not available at this Pincode" with a "Change pincode to exchange item" control, reflecting that no delivery pincode is set for this guest session.' },
    ],
  },
];


async function resolveSectionId() {
  if (SECTION_ID) {
    console.log(`Using existing section ${SECTION_ID}.`);
    return { projectId: PROJECT_ID, sectionId: SECTION_ID, reused: true };
  }

  let projectId = PROJECT_ID;
  if (!projectId) {
    const project = await trPost('add_project', {
      name: PROJECT_NAME,
      announcement: 'Seeded Flipkart guest (no-login) test cases for QANexus.',
      show_announcement: true,
      suite_mode: 1,
    });
    projectId = project.id;
    console.log(`Created TestRail project ${PROJECT_NAME} (id ${projectId}).`);
  } else {
    console.log(`Using existing project id ${projectId}.`);
  }

  const suites = await trGet(`get_suites/${projectId}`);
  const suiteId = Array.isArray(suites) && suites.length > 0 ? suites[0].id : undefined;

  const section = await trPost(`add_section/${projectId}`, {
    name: SECTION_NAME,
    ...(suiteId ? { suite_id: suiteId } : {}),
  });
  console.log(`Created section ${SECTION_NAME} (id ${section.id}) in project ${projectId}.`);
  console.log(`\n>>> TestRail Project ID for QANexus sync: ${projectId}\n`);
  return { projectId, sectionId: section.id, reused: false };
}

async function deleteExistingCases(projectId, sectionId) {
  const existing = await trGet(`get_cases/${projectId}&section_id=${sectionId}`);
  const list = Array.isArray(existing) ? existing : existing.cases ?? [];
  if (list.length === 0) {
    console.log('No existing cases to remove.');
    return;
  }
  console.log(`Removing ${list.length} existing case(s) from section ${sectionId}…`);
  for (const c of list) {
    await trPost(`delete_case/${c.id}`, {});
    console.log(`  ✗ removed C${c.id}  ${c.title}`);
  }
}

async function main() {
  const { projectId, sectionId, reused } = await resolveSectionId();
  if (reused && projectId) {
    await deleteExistingCases(Number(projectId), sectionId);
  }
  console.log(`Seeding ${cases.length} Flipkart cases into TestRail section ${sectionId}…`);
  for (const c of cases) {
    const created = await trPost(`add_case/${sectionId}`, {
      title: c.title,
      template_id: 2,
      custom_preconds: c.custom_preconds,
      custom_steps_separated: c.steps,
    });
    console.log(`  ✓ C${created.id}  ${c.title}`);
  }
  console.log('\nDone. In QANexus → Scenarios → "Sync from TestRail", enter the project id above to import.');
}

main().catch((err) => {
  console.error(err.message);
  if (err.cause) console.error('cause:', err.cause?.code ?? '', err.cause?.message ?? err.cause);
  process.exit(1);
});
