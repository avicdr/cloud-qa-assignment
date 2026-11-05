# Resilient Form Automation (C# + Selenium)

A small Selenium automation project that tests three fields on the
CloudQA Automation Practice Form:

1.  **First Name** --- enters a text value.
2.  **Gender** --- selects a radio-button option.
3.  **State** --- selects an option from a dropdown.

The test is designed to be more resilient than a script based only on
fixed IDs, names, classes, CSS selectors, or positional XPath
expressions. It uses visible labels, accessibility attributes, input
types, and nearby page text to identify likely controls.

## Requirements

-   Windows, macOS, or Linux
-   [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
-   Google Chrome
-   Internet access to open the test page

Selenium Manager (included with recent Selenium versions) normally
handles ChromeDriver setup automatically.

## 1. Create the project

Open a terminal or PowerShell window:

``` bash
dotnet new console -n ResilientFormAutomation
cd ResilientFormAutomation
```

Install Selenium packages:

``` bash
dotnet add package Selenium.WebDriver
dotnet add package Selenium.Support
```

## 2. Add the automation code

Replace the contents of `Program.cs` with the C# implementation supplied
for Task 1.

The project should look like this:

``` text
ResilientFormAutomation/
├── Program.cs
└── ResilientFormAutomation.csproj
```

## 3. Run with default values

The default target is:

``` text
https://app.cloudqa.io/home/AutomationPracticeForm
```

Run:

``` bash
dotnet run
```

By default, the program enters `Avi`, selects `Male`, and selects the
first valid State option available in the dropdown.

## 4. Run with custom values or a modified page URL

The program accepts these command-line arguments in order:

``` text
dotnet run -- "<URL>" "<FirstName>" "<Gender>" "<State>"
```

Example:

``` bash
dotnet run -- "https://app.cloudqa.io/home/AutomationPracticeForm" "John" "Male" "Uttar Pradesh"
```

You can point it at a modified copy of the form without editing the
source code:

``` bash
dotnet run -- "https://your-test-environment.example/modified-form" "Rahul" "Female" "Karnataka"
```

Use a State value that exactly matches an option's visible text
(case-insensitive matching is used).

If the State argument is omitted or left empty, the program selects the
first non-placeholder option with a non-empty value.

## 5. Expected output

A successful run should produce output similar to:

``` text
Testing First Name...
PASS: First Name entered successfully.

Testing Gender...
PASS: Gender selected: Male

Testing State...
PASS: State selected: Uttar Pradesh

All three tests passed!
```

If an element cannot be identified uniquely or an assertion fails, the
program reports a test failure and prints the exception message.

## How the resilient element identification works

The implementation does not rely on one fixed selector for each field.

-   **First Name:** ranks visible text inputs using associated label
    text, ARIA labels, `aria-labelledby`, placeholders, attributes, and
    nearby text.
-   **Gender:** searches radio inputs and evaluates their associated
    labels, option values, ARIA labels, and nearby group text.
-   **State:** ranks visible `<select>` elements using the same semantic
    signals, then uses Selenium's `SelectElement` API to choose an
    option.
-   **Ambiguity handling:** if the best candidates have the same score,
    the locator fails rather than silently selecting an arbitrary field.
-   **Configurable URL:** the target URL is supplied at runtime as the
    first command-line argument.

## Troubleshooting

### Chrome or ChromeDriver error

-   Update Google Chrome.
-   Make sure the machine has internet access on the first Selenium
    Manager run.
-   Run `dotnet restore` and try again.

### Field could not be uniquely identified

The locator intentionally fails when it cannot confidently identify a
field. Check the modified page for visible labels, accessible names, or
nearby text that identifies the intended control. If the page has
removed all meaningful semantic information, improve the locator
strategy rather than adding a fragile positional selector.

### State option not found

Pass the option's visible text exactly as it appears in the dropdown:

``` bash
dotnet run -- "https://example.com/form" "John" "Male" "Karnataka"
```

## Scope and limitations

This is a resilient locator prototype, not a guarantee that automation
can survive every possible page change.

It handles changes such as reordered fields, changed IDs/names/classes,
and moderate DOM restructuring when semantic clues remain available. It
does not currently include automatic iframe or Shadow DOM traversal,
CAPTCHA handling, or authentication flows.

For an assessment, explain that the implementation uses **semantic,
weighted locator selection with ambiguity checks**, rather than claiming
that it can identify any element regardless of the page's markup.
