
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Linq;

class Program
{
    private const string FindFieldScript = """
        const target = arguments[0].toLowerCase();
        const expectedTag = arguments[1].toLowerCase();
        const expectedType = arguments[2].toLowerCase();

        function normalize(s) {
            return (s || "").replace(/[^a-z0-9 ]/gi, " ")
                             .replace(/\s+/g, " ")
                             .trim().toLowerCase();
        }

        function scoreElement(el) {
            let score = 0;

            const labels = Array.from(el.labels || [])
                .map(x => normalize(x.innerText || x.textContent))
                .join(" ");

            const aria = normalize(el.getAttribute("aria-label"));
            const placeholder = normalize(el.getAttribute("placeholder"));
            const labelledBy = normalize(
                (el.getAttribute("aria-labelledby") || "")
                .split(/\s+/)
                .map(id => document.getElementById(id)?.innerText || "")
                .join(" ")
            );

            const attrs = normalize(
                [el.id, el.name, el.getAttribute("autocomplete")]
                .filter(Boolean).join(" ")
            );

            // Collect nearby text without relying on a fixed DOM depth.
            let context = "";
            let parent = el.parentElement;

            for (let i = 0; parent && i < 4; i++) {
                context += " " + normalize(parent.innerText);
                parent = parent.parentElement;
            }

            const exact = s => s === target;
            const contains = s => s.includes(target);

            if (exact(labels)) score += 100;
            else if (contains(labels)) score += 85;

            if (exact(aria) || exact(labelledBy)) score += 95;
            else if (contains(aria) || contains(labelledBy)) score += 75;

            if (contains(placeholder)) score += 35;
            if (contains(attrs)) score += 15;
            if (contains(context)) score += 30;

            // Penalize unrelated or incompatible elements.
            if (expectedTag && el.tagName.toLowerCase() !== expectedTag)
                return -1;

            if (expectedType &&
                (el.getAttribute("type") || "").toLowerCase() !== expectedType)
                return -1;

            if (!el.getClientRects().length) return -1;
            if (el.disabled) return -1;

            return score;
        }

        const candidates = Array.from(
            document.querySelectorAll(
                expectedTag === "select" ? "select" : "input"
            )
        );

        const ranked = candidates
            .map(el => ({ el, score: scoreElement(el) }))
            .sort((a, b) => b.score - a.score);

        if (!ranked.length || ranked[0].score < 25)
            return null;

        // Avoid ambiguous matches rather than silently testing
        // an unrelated field.
        if (ranked.length > 1 &&
            ranked[0].score === ranked[1].score)
            return null;

        return ranked[0].el;
        """;

    private const string FindRadioScript = """
        const groupName = arguments[0].toLowerCase();
        const optionName = arguments[1].toLowerCase();

        function norm(s) {
            return (s || "").replace(/\s+/g, " ")
                             .trim().toLowerCase();
        }

        const radios = Array.from(
            document.querySelectorAll('input[type="radio"]')
        );

        const results = radios.map(radio => {
            let optionText = "";
            let groupText = "";

            if (radio.labels && radio.labels.length) {
                optionText = Array.from(radio.labels)
                    .map(l => norm(l.innerText || l.textContent))
                    .join(" ");
            }

            // Also support labels that wrap the input or use
            // a "for" association.
            let parent = radio.parentElement;
            let depth = 0;

            while (parent && depth < 4) {
                const text = norm(parent.innerText);

                if (text.includes(groupName))
                    groupText = text;

                if (text.includes(optionName) && !optionText)
                    optionText = text;

                parent = parent.parentElement;
                depth++;
            }

            const value = norm(radio.value);
            const aria = norm(radio.getAttribute("aria-label"));

            let score = 0;

            if (optionText === optionName) score += 100;
            else if (optionText.includes(optionName)) score += 70;

            if (value === optionName) score += 35;
            if (aria === optionName) score += 50;

            if (groupText.includes(groupName)) score += 60;

            if (!radio.getClientRects().length || radio.disabled)
                score = -1;

            return { radio, score };
        }).sort((a, b) => b.score - a.score);

        if (!results.length || results[0].score < 60)
            return null;

        if (results.length > 1 &&
            results[0].score === results[1].score)
            return null;

        return results[0].radio;
        """;

    static IWebElement FindField(
        IWebDriver driver,
        string label,
        string tag,
        string type = "")
    {
        var element = ((IJavaScriptExecutor)driver)
            .ExecuteScript(FindFieldScript, label, tag, type);

        return element as IWebElement
            ?? throw new Exception($"Could not uniquely identify: {label}");
    }

    static IWebElement FindRadio(
        IWebDriver driver,
        string group,
        string option)
    {
        var element = ((IJavaScriptExecutor)driver)
            .ExecuteScript(FindRadioScript, group, option);

        return element as IWebElement
            ?? throw new Exception(
                $"Could not uniquely identify {option} in {group}");
    }

    static void Main(string[] args)
    {
        // All test inputs can be configured externally.
        string url = args.Length > 0
            ? args[0]
            : "https://app.cloudqa.io/home/AutomationPracticeForm";

        string firstName = args.Length > 1 ? args[1] : "Avi";
        string gender = args.Length > 2 ? args[2] : "Male";
        string requestedState = args.Length > 3 ? args[3] : "";

        var options = new ChromeOptions();
        options.AddArgument("--start-maximized");

        using IWebDriver driver = new ChromeDriver(options);

        var wait = new WebDriverWait(
            driver, TimeSpan.FromSeconds(15));

        try
        {
            driver.Navigate().GoToUrl(url);

            wait.Until(d =>
                ((IJavaScriptExecutor)d)
                .ExecuteScript("return document.readyState")
                ?.ToString() == "complete");

            Console.WriteLine("Testing First Name...");

            var firstNameField = wait.Until(d =>
                FindField(d, "First Name", "input", "text"));

            firstNameField.Clear();
            firstNameField.SendKeys(firstName);

            if (firstNameField.GetAttribute("value") != firstName)
                throw new Exception("First Name test failed.");

            Console.WriteLine("PASS: First Name entered successfully.");

            Console.WriteLine("Testing Gender...");

            var genderRadio = wait.Until(d =>
                FindRadio(d, "Gender", gender));

            if (!genderRadio.Selected)
                genderRadio.Click();

            if (!genderRadio.Selected)
                throw new Exception("Gender selection failed.");

            Console.WriteLine($"PASS: Gender selected: {gender}");

            Console.WriteLine("Testing State...");

            var stateDropdown = wait.Until(d =>
                FindField(d, "State", "select"));

            var select = new SelectElement(stateDropdown);

            IWebElement stateOption;

            if (!string.IsNullOrWhiteSpace(requestedState))
            {
                stateOption = select.Options.FirstOrDefault(o =>
                    string.Equals(
                        o.Text.Trim(),
                        requestedState,
                        StringComparison.OrdinalIgnoreCase))
                    ?? throw new Exception(
                        $"State not found: {requestedState}");
            }
            else
            {
                stateOption = select.Options.FirstOrDefault(o =>
                    !string.IsNullOrWhiteSpace(o.Text) &&
                    !string.IsNullOrWhiteSpace(o.GetAttribute("value")))
                    ?? throw new Exception("No valid state options found.");
            }

            string selectedState = stateOption.Text.Trim();

            select.SelectByText(selectedState);

            if (!select.SelectedOption.Text.Trim()
                .Equals(selectedState, StringComparison.OrdinalIgnoreCase))
                throw new Exception("State selection failed.");

            Console.WriteLine(
                $"PASS: State selected: {selectedState}");

            Console.WriteLine("\nAll three tests passed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST FAILED: " + ex.Message);
            throw;
        }
        finally
        {
            driver.Quit();
        }
    }
}
