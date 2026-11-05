# Plain-English Browser Test Agent

Turns a plain-English browser test into a constrained Playwright plan using an AI model.

## Setup

```bash
npm install
npx playwright install chromium
cp .env.example .env
```

Add your OpenAI API key to `.env`. For tests requiring customer login, put the demo account credentials in `CUSTOMER_EMAIL` and `CUSTOMER_PASSWORD`. The secrets are resolved locally by the runner and are not sent to the model.

## Run

```bash
npm run dev -- --url "https://example.com" --test "Open the sign-in page, log in as a customer, and verify the dashboard is shown"
```

Show the browser:

```bash
npm run dev -- --url "https://example.com" --test "Verify the home page is shown" --headed true
```

The tool writes `report.json`; on failure it also attempts to save `failure.png`.

## Design

- AI creates a typed plan using a strict structured-output schema.
- The plan is limited to click, fill, select, press, and assertion operations.
- Playwright uses accessible roles, labels, placeholders, and text rather than site-specific CSS/XPath.
- The executor rejects ambiguous locators instead of clicking an arbitrary matching element.
- Credentials use environment-variable placeholders and are never included in the AI prompt.
- Page text is treated as untrusted UI data, not instructions.

## PHPTRAVELS demo

Use the current official demo entry point: https://phptravels.com/demo

The current flow may require requesting demo access before customer credentials are available. Put the customer account's generated email/password into `.env`, then run the login test against the customer login URL shown by the demo. Do not use real customer or payment data in the shared demo.
