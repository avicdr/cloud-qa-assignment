# Plain-English Browser Test Agent

An autonomous browser QA testing agent powered by Playwright and the Google Gemini API (`@google/genai`). It transforms natural language instructions (plain-English test cases) into automated, resilient browser actions and assertions across any web application.

---

## Table of Contents

- [Features](#features)
- [Architecture & How It Works](#architecture--how-it-works)
- [Prerequisites](#prerequisites)
- [Step-by-Step Setup](#step-by-step-setup)
  - [1. Install Dependencies](#1-install-dependencies)
  - [2. Install Playwright Chromium](#2-install-playwright-chromium)
  - [3. Configure Environment Variables](#3-configure-environment-variables)
- [How to Run](#how-to-run)
  - [Interactive Mode](#interactive-mode)
  - [Command-Line (CLI) Mode](#command-line-cli-mode)
  - [Headed Mode (Visual Execution)](#headed-mode-visual-execution)
  - [Debug Mode](#debug-mode)
- [CLI Options & Flags](#cli-options--flags)
- [Credentials & Secret Handling](#credentials--secret-handling)
- [Outputs & Test Artifacts](#outputs--test-artifacts)
- [Project Structure](#project-structure)
- [Troubleshooting & FAQs](#troubleshooting--faqs)

---

## Features

- **Natural Language Test Cases**: Run tests described in plain English without writing hardcoded selectors or custom automation scripts.
- **Dynamic DOM Inspection**: Extracts visible, interactive elements (`<button>`, `<a>`, `<input>`, `<select>`, ARIA controls) and injects unique numeric IDs (`data-ai-id`) for precise targeting.
- **Dual-Layer Locator Strategy**: Targets elements via `data-ai-id` first, with automatic fallback to semantic locators (`getByRole`, `getByLabel`, `getByPlaceholder`, `getByText`, `getByTestId`).
- **Autonomous Replanning & Error Recovery**: If an action fails (e.g., element covered, delayed rendering), the failure is fed back into Gemini to devise an alternate action.
- **Safe Secret Resolution & Masking**: Resolves credentials from `.env` using template syntax (`{{CUSTOMER_PASSWORD}}`) while masking secrets in terminal logs, Gemini prompts, and output reports.
- **Multi-Tab & Notice Handling**: Automatically detects and switches focus to new tabs opened by links (`target="_blank"`) and dismisses cookie/consent banners.
- **Detailed Execution Artifacts**: Generates structured JSON run reports (`report.json`) and captures full-page failure screenshots (`failure.png`) on test failures.

---

## Architecture & How It Works

```
                     ┌───────────────────────────────┐
                     │   Plain-English Test Prompt   │
                     └───────────────┬───────────────┘
                                     │
                                     ▼
 ┌──────────────────────┐    Live DOM Snapshot    ┌───────────────────────────┐
 │                      │ ──────────────────────> │                           │
 │  Playwright Browser  │                         │       Google Gemini       │
 │   (Chromium Engine)  │ <────────────────────── │ (Structured Decision Loop)│
 │                      │      Next Browser       │                           │
 └──────────────────────┘    Action / Assertion   └───────────────────────────┘
            │                                                   │
     Action Result / Error                               Plan Outline &
            │                                            Assumptions
            ▼                                                   │
 ┌──────────────────────────────────────────────────────────────▼─────────────┐
 │                Artifacts: report.json & failure.png                        │
 └────────────────────────────────────────────────────────────────────────────┘
```

1. **Snapshotting**: The agent extracts page metadata (title, URL, headings, visible body text, and up to 200 interactive controls).
2. **Planning & Decision**: Gemini inspects the snapshot, maintains a high-level `planOutline`, and returns a strongly-typed JSON decision (`status`, `reasoning`, `assumptions`, and `nextStep`).
3. **Execution**: Playwright resolves the target element, executes the action (`click`, `fill`, `select`, `press`, `hover`, `goto`, `wait`, or assertion), and monitors page state.
4. **Verification**: The agent verifies conditions with `assertVisible`, `assertText`, or `assertUrl` before declaring `PASS`.
5. **Reporting**: Complete execution telemetry is saved to `report.json`.

---

## Prerequisites

Ensure you have the following installed on your machine:

- **Node.js**: `v18.0.0` or higher (recommended: Node.js `v20+` or `v22+`)
- **npm**: `v9.0.0` or higher (bundled with Node.js)
- **Google Gemini API Key**: Obtain a free or paid API key from [Google AI Studio](https://aistudio.google.com/).

---

## Step-by-Step Setup

### 1. Install Dependencies

In the project root directory, install the required npm packages:

```bash
npm install
```

This installs core runtime packages:

- `@google/genai`: Official Google GenAI SDK for Gemini API integration.
- `playwright`: Cross-browser automation engine.
- `zod`: Schema validation.
- `dotenv`: Environment variable management.
- `tsx` & `typescript`: TypeScript compilation and direct execution.

### 2. Install Playwright Chromium

Download the Chromium browser binary used by Playwright:

```bash
npx playwright install chromium
```

> **Note for Linux / Docker / CI Environments**:
> If running on Ubuntu/Debian or in a headless CI container, also install system dependencies:
>
> ```bash
> npx playwright install-deps chromium
> ```

### 3. Configure Environment Variables

Create your local `.env` configuration file by copying `.env.example`:

**On macOS / Linux:**

```bash
cp .env.example .env
```

**On Windows (PowerShell):**

```powershell
Copy-Item .env.example .env
```

**On Windows (Command Prompt):**

```cmd
copy .env.example .env
```

Open `.env` in your text editor and configure your variables:

```ini
# Required: Your Google Gemini API Key
GEMINI_API_KEY=your_gemini_api_key_here

# Optional: Gemini Model identifier (defaults to gemini-2.5-flash)
GEMINI_MODEL=gemini-2.5-flash

# Optional: Test credentials resolved via {{CUSTOMER_EMAIL}} and {{CUSTOMER_PASSWORD}}
CUSTOMER_EMAIL=user@phptravels.com
CUSTOMER_PASSWORD=demouser

# Optional: Additional test credentials supported by the agent
TEST_USERNAME=testuser
TEST_PASSWORD=testpassword123
ADMIN_EMAIL=admin@example.com
ADMIN_PASSWORD=adminpassword123
```

#### Environment Variables Reference

| Variable            | Required |      Default       | Description                                                                                |
| :------------------ | :------: | :----------------: | :----------------------------------------------------------------------------------------- |
| `GEMINI_API_KEY`    | **Yes**  |         —          | Google Gemini API key from Google AI Studio.                                               |
| `GEMINI_MODEL`      |    No    | `gemini-2.5-flash` | Gemini model to use (e.g., `gemini-2.5-flash`, `gemini-3.5-flash-lite`, `gemini-1.5-pro`). |
| `CUSTOMER_EMAIL`    |    No    |         —          | Customer email injected via `{{CUSTOMER_EMAIL}}`.                                          |
| `CUSTOMER_PASSWORD` |    No    |         —          | Customer password injected via `{{CUSTOMER_PASSWORD}}`.                                    |
| `TEST_USERNAME`     |    No    |         —          | Username injected via `{{TEST_USERNAME}}`.                                                 |
| `TEST_PASSWORD`     |    No    |         —          | Password injected via `{{TEST_PASSWORD}}`.                                                 |
| `ADMIN_EMAIL`       |    No    |         —          | Admin email injected via `{{ADMIN_EMAIL}}`.                                                |
| `ADMIN_PASSWORD`    |    No    |         —          | Admin password injected via `{{ADMIN_PASSWORD}}`.                                          |

---

## How to Run

### Interactive Mode

Run the agent without CLI flags. It will prompt you interactively for the target URL and test case:

```bash
npm start
```

_(or `npm run dev`)_

**Interactive Prompt Example:**

```text
Enter target website URL (e.g., https://phptravels.com/demo): https://phptravels.net/login
Enter plain-English test case: Log in as a customer and verify the dashboard is shown
```

---

### Command-Line (CLI) Mode

Pass the target URL and test description directly via flags:

```bash
npm start -- --url "https://phptravels.net/login" --test "Log in as a customer and verify the dashboard is shown"
```

Or execute directly with `tsx`:

```bash
npx tsx src/index.ts --url "https://phptravels.net/login" --test "Log in as a customer and verify the dashboard is shown"
```

---

### Headed Mode (Visual Execution)

By default, the browser runs in headless mode. Pass `--headed true` to watch the browser actions live in a visible window:

```bash
npm start -- --url "https://phptravels.net/login" --test "Log in as a customer and verify the dashboard is shown" --headed true
```

---

### Debug Mode

Combine `--headed true` with `--debug true`. If an unhandled error occurs or the test fails, Playwright will pause execution (`page.pause()`), opening the Playwright Inspector so you can inspect the DOM:

```bash
npm start -- --url "https://phptravels.net/login" --test "Log in as a customer and verify the dashboard is shown" --headed true --debug true
```

---

## CLI Options & Flags

| Flag          |   Type    | Default  | Description                                                                                     |
| :------------ | :-------: | :------: | :---------------------------------------------------------------------------------------------- |
| `--url`       | `string`  | Prompted | The initial target website URL to navigate to.                                                  |
| `--test`      | `string`  | Prompted | Plain-English test scenario and acceptance criteria.                                            |
| `--headed`    | `boolean` | `false`  | When `true`, opens a visible Chromium browser window.                                           |
| `--debug`     | `boolean` | `false`  | When `true` (with `--headed`), pauses browser execution upon error for inspection.              |
| `--slowmo`    | `number`  |  `250`   | Adds an artificial delay (in milliseconds) between Playwright steps for easier visual tracking. |
| `--max-steps` | `number`  |   `20`   | Maximum number of planning turns/steps allowed before terminating the run.                      |

---

## Credentials & Secret Handling

The agent handles secrets and credentials securely:

1. **On-Page Demo Credentials**:
   - If a demo website displays sample login credentials directly on the page (e.g., _"Email: user@phptravels.com | Password: demouser"_), the agent automatically extracts and uses them.
2. **Template Interpolation**:
   - For authenticated applications, store credentials in `.env` (such as `CUSTOMER_EMAIL` and `CUSTOMER_PASSWORD`).
   - Gemini references them in action payloads using templates like `{{CUSTOMER_EMAIL}}` and `{{CUSTOMER_PASSWORD}}`.
   - The runner resolves these tokens right before filling input fields.
3. **Automatic Redaction**:
   - All secret templates (`{{KEY}}`) and real password values are masked into `[REDACTED SECRET]` before writing to console logs, history entries, Gemini prompt contexts, or `report.json`.

---

## Outputs & Test Artifacts

Every test execution generates structured artifacts in the root directory:

### 1. `report.json`

Contains end-to-end execution metadata, including:

- Test status (`PASS` or `FAIL`).
- Model used and execution timestamps.
- High-level test plan outline generated by Gemini.
- Model assumptions recorded during execution.
- Granular step-by-step history:
  - Action taken (`click`, `fill`, `assertText`, etc.)
  - Target locator and element ID
  - Execution duration in milliseconds
  - URL before and after the action
  - Status and error messages (if any)
- Final page URL and document title.

### 2. `failure.png`

Whenever a test fails or encounters an unrecoverable exception, a full-page screenshot of the browser is automatically captured and saved as `failure.png`.

---

## Project Structure

```text
Task2/
├── src/
│   └── index.ts          # Core agent runner: DOM snapshotting, Gemini planner, & Playwright executor
├── .env.example          # Template for required environment variables and credentials
├── .env                  # Local secret configuration (git-ignored)
├── failure.png           # Auto-generated full-page screenshot on test failure
├── package.json          # Project metadata, dependencies, and execution scripts
├── package-lock.json     # Locked dependency tree
├── README.md             # Project documentation and setup guide
├── report.json           # Detailed JSON execution report from test runs
└── tsconfig.json         # TypeScript compiler configuration (ES2022, NodeNext)
```

---

## Troubleshooting & FAQs

### 1. `Error: GEMINI_API_KEY is missing`

Ensure you copied `.env.example` to `.env` and set `GEMINI_API_KEY` to a valid Google GenAI key. Verify that your `.env` file is located in the root directory.

### 2. `browserType.launch: Executable doesn't exist`

The Chromium browser binaries for Playwright are missing. Run:

```bash
npx playwright install chromium
```

### 3. Test reaches maximum steps without completing

If a complex workflow needs more turns, increase the `--max-steps` argument:

```bash
npm start -- --url "https://example.com" --test "..." --max-steps 35
```

### 4. Overlays, Cookie Banners, or Popups blocking clicks

The agent includes built-in detection for standard cookie and privacy banners (`Accept all`, `Agree & continue`, `Got it`). If a custom popup appears, the agent feeds the action error back into Gemini on the next turn to attempt closing or navigating around the modal.

### 5. Running behind a proxy or custom viewport

The browser viewport is initialized by default to `1440x960` with a 10-second default action timeout. These configurations can be adjusted in `src/index.ts` under the browser context initialization.

# Approach and Limitations

To turn plain-English test cases into reliable automated tests across arbitrary websites, I implemented an iterative Observe $\rightarrow$ Plan $\rightarrow$ Act $\rightarrow$ Verify browser agent using Playwright and the Google Gemini API (@google/genai). Rather than generating a rigid, one-shot script from the initial URL, the runner captures a fresh DOM snapshot before every decision, indexing all visible controls with runtime IDs (data-ai-id) and accessible semantic locators (role, label, text, placeholder). At each turn, Gemini evaluates the active UI state and history to update a high-level test plan and output a single typed action or assertion (click, fill, select, assertVisible, assertText, assertUrl). The executor automatically handles multi-tab switches when links open new windows, retries alternative controls upon interaction errors, and injects sensitive credentials locally from environment variables to prevent prompt exposure. The primary limitations of this approach stem from its DOM-first architecture and LLM dependency. Because the agent relies on structured DOM snapshots, it cannot interact with sub-controls rendered inside closed Shadow DOMs, raw HTML5 <canvas> elements, or WebGL graphics without vision assistance. It is also blocked by aggressive anti-bot mitigations (such as CAPTCHAs or Cloudflare Turnstile) in live staging environments. Additionally, because every step requires a snapshot extraction and an LLM inference round-trip, execution is inherently slower and consumes more network bandwidth and API tokens than a static, hand-coded Playwright test script.
