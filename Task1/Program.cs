using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Linq;

class Program
{
    private const string FindFieldScript = """
        const target = arguments[0].toLowerCase().trim();
        const expectedTag = (arguments[1] || "").toLowerCase().trim();
        const expectedType = (arguments[2] || "").toLowerCase().trim();

        function normalize(s) {
            return (s || "")
                .replace(/[^a-z0-9 ]/gi, " ")
                .replace(/\s+/g, " ")
                .trim()
                .toLowerCase();
        }

        function escapeRegex(s) {
            return s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
        }

        const targetNorm = normalize(target);
        const wordRegex = new RegExp("\\b" + escapeRegex(targetNorm) + "\\b", "i");

        function isVisible(el) {
            if (!el || el.disabled) return false;
            const style = window.getComputedStyle(el);
            if (style.display === "none" || style.visibility === "hidden") return false;
            return el.getClientRects().length > 0;
        }

        function matchScore(text, exactWeight, wordWeight, subWeight) {
            const n = normalize(text);
            if (!n) return 0;
            if (n === targetNorm) return exactWeight;
            if (wordRegex.test(n)) {
                // Prefer shorter strings that closely wrap the label text
                const extraWords = Math.max(0, n.split(" ").length - targetNorm.split(" ").length);
                return Math.max(subWeight, wordWeight - extraWords * 4);
            }
            if (n.includes(targetNorm)) return subWeight;
            return 0;
        }

        function getClosestLabelText(el) {
            // Check preceding siblings or parent's preceding siblings (e.g., <td><label></td><td><input></td>)
            let curr = el;
            for (let depth = 0; curr && depth < 3; depth++) {
                let prev = curr.previousElementSibling;
                while (prev) {
                    if (!prev.querySelector("input, select, textarea")) {
                        const txt = normalize(prev.innerText || prev.textContent);
                        if (txt) return txt;
                    }
                    prev = prev.previousElementSibling;
                }
                curr = curr.parentElement;
            }
            return "";
        }

        function scoreElement(el) {
            if (!isVisible(el)) return -1;

            const tag = el.tagName.toLowerCase();
            if (expectedTag && tag !== expectedTag) return -1;

            if (tag === "input" && expectedType) {
                const attrType = (el.getAttribute("type") || "text").toLowerCase();
                if (attrType !== expectedType) return -1;
            }

            let score = 0;

            // 1. Explicit HTML <label> associations (for/id or wrapping <label>)
            const labels = Array.from(el.labels || [])
                .map(x => x.innerText || x.textContent)
                .join(" ");
            score += matchScore(labels, 120, 95, 60);

            // 2. ARIA attributes
            const ariaLabel = el.getAttribute("aria-label") || "";
            const ariaLabelledBy = (el.getAttribute("aria-labelledby") || "")
                .split(/\s+/)
                .map(id => document.getElementById(id)?.innerText || "")
                .join(" ");
            score += Math.max(
                matchScore(ariaLabel, 110, 90, 55),
                matchScore(ariaLabelledBy, 110, 90, 55)
            );

            // 3. Closest preceding label/text in DOM when 'for' attribute is missing or broken
            const siblingText = getClosestLabelText(el);
            score += matchScore(siblingText, 95, 75, 40);

            // 4. Placeholder, title, name, id, autocomplete
            const placeholder = el.getAttribute("placeholder") || el.getAttribute("title") || "";
            score += matchScore(placeholder, 65, 50, 30);

            const compactTarget = targetNorm.replace(/\s+/g, "");
            const rawAttrs = [el.id, el.name, el.getAttribute("autocomplete"), el.getAttribute("data-field")]
                .filter(Boolean)
                .join(" ")
                .toLowerCase();
            if (rawAttrs.includes(compactTarget) || normalize(rawAttrs).includes(targetNorm)) {
                score += 25;
            }

            // 5. Distance-weighted ancestor context (only reward ancestors that don't span the entire form)
            let parent = el.parentElement;
            for (let depth = 1; parent && depth <= 4; depth++) {
                if (parent.tagName.toLowerCase() === "form" || parent.tagName.toLowerCase() === "body") break;
                const controlCount = parent.querySelectorAll("input:not([type='hidden']), select, textarea").length;
                const parentText = normalize(parent.innerText);
                if (wordRegex.test(parentText)) {
                    // Strongly prefer containers that wrap only this single field
                    const depthBonus = Math.max(5, (36 - depth * 7) / Math.max(1, controlCount));
                    score += depthBonus;
                    break;
                }
                parent = parent.parentElement;
            }

            return score;
        }

        const selector = expectedTag === "select" ? "select" : "input, textarea";
        const candidates = Array.from(document.querySelectorAll(selector));

        const ranked = candidates
            .map(el => ({ el, score: scoreElement(el) }))
            .filter(x => x.score >= 25)
            .sort((a, b) => b.score - a.score);

        if (!ranked.length) return null;

        return ranked[0].el;
        """;

    private const string FindRadioScript = """
        const groupName = arguments[0].toLowerCase().trim();
        const optionName = arguments[1].toLowerCase().trim();

        function norm(s) {
            return (s || "")
                .replace(/[^a-z0-9 ]/gi, " ")
                .replace(/\s+/g, " ")
                .trim()
                .toLowerCase();
        }

        function escapeRegex(s) {
            return s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
        }

        const groupNorm = norm(groupName);
        const optionNorm = norm(optionName);
        const optionWordRegex = new RegExp("\\b" + escapeRegex(optionNorm) + "\\b", "i");
        const groupWordRegex = new RegExp("\\b" + escapeRegex(groupNorm) + "\\b", "i");

        function getImmediateOptionText(radio) {
            // 1. Associated <label>
            if (radio.labels && radio.labels.length) {
                return Array.from(radio.labels)
                    .map(l => norm(l.innerText || l.textContent))
                    .join(" ");
            }
            // 2. Next sibling text node or element (e.g., <input type="radio"> Male)
            let node = radio.nextSibling;
            while (node) {
                if (node.nodeType === Node.TEXT_NODE && norm(node.textContent)) {
                    return norm(node.textContent);
                }
                if (node.nodeType === Node.ELEMENT_NODE) {
                    if (node.tagName.toLowerCase() === "input") break;
                    const txt = norm(node.innerText || node.textContent);
                    if (txt) return txt;
                }
                node = node.nextSibling;
            }
            // 3. Immediate parent if it only wraps this single radio
            if (radio.parentElement && radio.parentElement.querySelectorAll('input[type="radio"]').length === 1) {
                return norm(radio.parentElement.innerText || radio.parentElement.textContent);
            }
            return "";
        }

        const radios = Array.from(document.querySelectorAll('input[type="radio"]'));

        const results = radios.map(radio => {
            if (radio.disabled) return { radio, score: -1 };
            const rect = radio.getClientRects();
            const labelRect = radio.labels?.[0]?.getClientRects();
            if (!rect.length && (!labelRect || !labelRect.length)) return { radio, score: -1 };

            let score = 0;
            const optionText = getImmediateOptionText(radio);
            const valText = norm(radio.value);
            const idText = norm(radio.id);
            const ariaText = norm(radio.getAttribute("aria-label"));

            // Strict word-boundary matching prevents "female" from matching "male"
            if (optionText === optionNorm) score += 120;
            else if (optionWordRegex.test(optionText)) score += 95;

            if (valText === optionNorm) score += 60;
            else if (optionWordRegex.test(valText)) score += 45;

            if (ariaText === optionNorm || idText === optionNorm) score += 50;
            else if (optionWordRegex.test(ariaText) || optionWordRegex.test(idText)) score += 35;

            // Require some evidence that this radio represents the target option
            if (score === 0) return { radio, score: -1 };

            // Check group context via ancestor text or name attribute
            if (norm(radio.name).includes(groupNorm)) {
                score += 40;
            }

            let parent = radio.parentElement;
            for (let depth = 1; parent && depth <= 5; depth++) {
                if (parent.tagName.toLowerCase() === "form" || parent.tagName.toLowerCase() === "body") break;
                const text = norm(parent.innerText);
                if (groupWordRegex.test(text)) {
                    score += Math.max(15, 65 - depth * 10);
                    break;
                }
                parent = parent.parentElement;
            }

            return { radio, score };
        })
        .filter(x => x.score >= 50)
        .sort((a, b) => b.score - a.score);

        if (!results.length) return null;

        return results[0].radio;
        """;

    static string NormalizeUrl(string rawUrl)
    {
        string trimmed = rawUrl.Trim();
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            return "https://" + trimmed;
        }
        return trimmed;
    }

    static IWebElement FindField(
        IWebDriver driver,
         WebDriverWait wait,
        string label,
        string tag,
        string type = "")
    {
        try
        {
            return wait.Until(d =>
                ((IJavaScriptExecutor)d).ExecuteScript(FindFieldScript, label, tag, type) as IWebElement);
        }
        catch (WebDriverTimeoutException)
        {
            throw new NoSuchElementException($"Could not uniquely identify field '{label}' ({tag}).");
        }
    }

    static IWebElement FindRadio(
        IWebDriver driver,
        WebDriverWait wait,
        string group,
        string option)
    {
        try
        {
            return wait.Until(d =>
                ((IJavaScriptExecutor)d).ExecuteScript(FindRadioScript, group, option) as IWebElement);
        }
        catch (WebDriverTimeoutException)
        {
            throw new NoSuchElementException($"Could not uniquely identify radio option '{option}' in group '{group}'.");
        }
    }

    static void ScrollIntoView(IWebDriver driver, IWebElement element)
    {
        ((IJavaScriptExecutor)driver).ExecuteScript(
            "arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", element);
    }

    static bool IsPlaceholderOption(IWebElement option, int index)
    {
        string text = (option.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (option.GetAttribute("disabled") != null) return true;

        string lower = text.ToLowerInvariant();
        if (lower.StartsWith("--") ||
            lower.Contains("select") ||
            lower.Contains("choose") ||
            lower.Equals("none"))
        {
            return true;
        }

        string? rawValue = option.GetDomAttribute("value");
        if (index == 0 && rawValue != null && string.IsNullOrWhiteSpace(rawValue))
        {
            return true;
        }

        return false;
    }

    static void Main(string[] args)
    {
        string rawUrl = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
            ? args[0]
            : "https://app.cloudqa.io/home/AutomationPracticeForm";

        string url = NormalizeUrl(rawUrl);
        string firstName = args.Length > 1 ? args[1] : "Avi";
        string gender = args.Length > 2 ? args[2] : "Male";
        string requestedState = args.Length > 3 ? args[3] : "";

        var options = new ChromeOptions();
        options.AddArgument("--start-maximized");

        using IWebDriver driver = new ChromeDriver(options);
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));

        try
        {
            Console.WriteLine($"Navigating to: {url}");
            driver.Navigate().GoToUrl(url);

            wait.Until(d =>
                ((IJavaScriptExecutor)d)
                .ExecuteScript("return document.readyState")
                ?.ToString() == "complete");

            // 1. First Name (Text Box)
            Console.WriteLine("Testing First Name...");
            var firstNameField = FindField(driver, wait, "First Name", "input", "text");
            ScrollIntoView(driver, firstNameField);

            firstNameField.Clear();
            firstNameField.SendKeys(firstName);

            string actualFirstName = firstNameField.GetAttribute("value") ?? "";
            if (!string.Equals(actualFirstName, firstName, StringComparison.Ordinal))
                throw new Exception($"First Name verification failed. Expected '{firstName}', got '{actualFirstName}'.");

            Console.WriteLine($"PASS: First Name entered successfully ('{actualFirstName}').");

            // 2. Gender (Radio Button)
            Console.WriteLine("Testing Gender...");
            var genderRadio = FindRadio(driver, wait, "Gender", gender);
            ScrollIntoView(driver, genderRadio);

            if (!genderRadio.Selected)
            {
                try
                {
                    genderRadio.Click();
                }
                catch (ElementDriverException)
                {
                    // Fallback if a styled <label> or custom control overlays the radio input
                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", genderRadio);
                }
            }

            if (!genderRadio.Selected)
                throw new Exception($"Gender selection verification failed for '{gender}'.");

            Console.WriteLine($"PASS: Gender selected ('{gender}').");

            // 3. State (Dropdown)
            Console.WriteLine("Testing State...");
            var stateDropdown = FindField(driver, wait, "State", "select");
            ScrollIntoView(driver, stateDropdown);

            var select = new SelectElement(stateDropdown);
            IWebElement stateOption;

            if (!string.IsNullOrWhiteSpace(requestedState))
            {
                stateOption = select.Options.FirstOrDefault(o =>
                    string.Equals(o.Text.Trim(), requestedState.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? throw new Exception($"Requested state not found: '{requestedState}'.");
            }
            else
            {
                stateOption = select.Options
                    .Select((opt, idx) => new { opt, idx })
                    .FirstOrDefault(x => !IsPlaceholderOption(x.opt, x.idx))?.opt
                    ?? throw new Exception("No selectable non-placeholder state options found.");
            }

            string targetStateText = stateOption.Text.Trim();
            select.SelectByText(targetStateText);

            string selectedStateText = select.SelectedOption.Text.Trim();
            if (!selectedStateText.Equals(targetStateText, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"State verification failed. Expected '{targetStateText}', got '{selectedStateText}'.");

            Console.WriteLine($"PASS: State selected ('{selectedStateText}').");
            Console.WriteLine("\nAll three tests passed!");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("TEST FAILED: " + ex.Message);
            Environment.ExitCode = 1;
        }
        finally
        {
            driver.Quit();
        }
    }
}