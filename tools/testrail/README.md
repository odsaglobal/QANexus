# TestRail — SauceDemo seed

`seed-saucedemo.mjs` creates a set of SauceDemo test cases in your TestRail
instance so you can then **Sync from TestRail** inside QANexus (Scenarios tab)
and explore/run them.

## 1. Prepare TestRail
1. Create (or pick) a TestRail **project**. Note its numeric **project id** (in the URL, e.g. `.../index.php?/projects/overview/12` → `12`).
2. Inside it, create a **suite** and a **section** (e.g. "SauceDemo"). Note the **section id**.
3. Create an **API key** under *My Settings → API Keys* (or use your password).

## 2. Seed the cases
```bash
TESTRAIL_BASE_URL="https://yourcompany.testrail.io" \
TESTRAIL_USER="you@company.com" \
TESTRAIL_APIKEY="your_api_key" \
TESTRAIL_SECTION_ID=1 \
node tools/testrail/seed-saucedemo.mjs
```
This adds 7 cases (logins, add-to-cart, sort, full checkout, checkout validation).

## 3. Configure the QANexus backend
Set the TestRail connection in `src/ATIP.Api/appsettings.Development.json`:
```json
"TestRail": {
  "BaseUrl": "https://yourcompany.testrail.io",
  "Username": "you@company.com",
  "ApiKey": "your_api_key"
}
```
Restart the API.

## 4. Sync into QANexus
Open the **saucedemo** project → **Scenarios** → **Sync from TestRail**, enter the
TestRail **project id** (and optionally suite/section id), then Sync. The cases
appear as scenarios you can **Explore** (goal-driven) or **Run** (execute steps).
