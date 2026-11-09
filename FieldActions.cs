using OpenQA.Selenium;

/// <summary>
/// Finds a field by MEANING (using finder.js) and then really interacts with it.
/// After every action it reads the page back to prove the action worked.
/// </summary>
public class FieldActions
{
    private readonly IWebDriver _driver;
    private readonly IJavaScriptExecutor _js;
    private readonly string _finderScript;

    public FieldActions(IWebDriver driver)
    {
        _driver = driver;
        _js = (IJavaScriptExecutor)driver;
        _finderScript = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "finder.js"));
    }

    // Ask finder.js for the best field. Retries for a few seconds because pages load slowly.
    private (IWebElement El, int Score, string Info) Find(string kind, string[] keywords, string option = "", int seconds = 10)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (true)
        {
            if (_js.ExecuteScript(_finderScript, kind, keywords, option) is System.Collections.IList r)
                return ((IWebElement)r[0]!, Convert.ToInt32(r[1]), (string)r[2]!);
            if (DateTime.UtcNow > end)
                throw new NoSuchElementException($"No {kind} field found that matches: {string.Join(", ", keywords)}");
            Thread.Sleep(500);
        }
    }

    private void ScrollTo(IWebElement el) =>
        _js.ExecuteScript("arguments[0].scrollIntoView({block:'center'});", el);

    // ---------------- text box ----------------
    public string TypeText(string[] keywords, string value)
    {
        var (el, score, info) = Find("text", keywords);
        ScrollTo(el);
        try { el.Clear(); el.SendKeys(value); }
        catch (WebDriverException)   // e.g. "not interactable" -> set it with JavaScript instead
        {
            _js.ExecuteScript("arguments[0].value = arguments[1];" +
                              "arguments[0].dispatchEvent(new Event('input',{bubbles:true}));" +
                              "arguments[0].dispatchEvent(new Event('change',{bubbles:true}));", el, value);
        }
        var actual = el.GetAttribute("value");
        if (actual != value) throw new Exception($"typed '{value}' but the box contains '{actual}'");
        return $"typed '{value}' -> field [{info}] (score {score})";
    }

    // ---------------- radio button ----------------
    // ---------------- radio button ----------------
    public string ChooseRadio(string[] groupKeywords, string option)
    {
        var (el, score, info) = Find("radio", groupKeywords, option);
        ScrollTo(el);

        // 1. Try native click
        try { el.Click(); } 
        catch (WebDriverException) { }

        // 2. Fallback: JavaScript click & event dispatch if not yet selected
        if (!el.Selected)
        {
            _js.ExecuteScript(
                "arguments[0].checked = true;" +
                "arguments[0].dispatchEvent(new Event('input', { bubbles: true }));" +
                "arguments[0].dispatchEvent(new Event('change', { bubbles: true }));" +
                "arguments[0].click();", el);
        }

        // 3. If still not selected, click its matching label if one exists
        if (!el.Selected)
        {
            string id = el.GetAttribute("id") ?? "";
            if (!string.IsNullOrEmpty(id))
            {
                try
                {
                    var label = _driver.FindElement(By.CssSelector($"label[for='{id}']"));
                    label.Click();
                }
                catch { }
            }
        }

        if (!el.Selected) 
            throw new Exception($"clicked radio [{info}] but it is not selected");

        return $"selected radio -> {info} (score {score})";
    }
    

    // ---------------- dropdown ----------------
    private static bool IsPlaceholder(string text, string? value) =>
        string.IsNullOrWhiteSpace(text) || string.IsNullOrEmpty(value) ||
        text.Trim().StartsWith("--") || text.Trim().StartsWith("select", StringComparison.OrdinalIgnoreCase);

    private static List<IWebElement> RealOptions(IWebElement select) =>
        select.FindElements(By.TagName("option"))
              .Where(o => !IsPlaceholder(o.Text, o.GetAttribute("value"))).ToList();

    // Picks `wanted` (or the first real option if it does not exist). Returns a description.
    private string Pick(IWebElement select, string wanted)
    {
        var options = RealOptions(select);
        if (options.Count == 0) throw new Exception("the dropdown has no selectable options");

        var match = options.FirstOrDefault(o => o.Text.Trim().Equals(wanted.Trim(), StringComparison.OrdinalIgnoreCase))
                 ?? options.FirstOrDefault(o => wanted.Length > 0 && o.Text.Contains(wanted, StringComparison.OrdinalIgnoreCase));
        bool found = match != null;
        match ??= options[0];
        var text = match.Text.Trim();

        try { match.Click(); } catch (WebDriverException) { }
        if (SelectedText(select) != text)    // fallback: set it with JavaScript
            _js.ExecuteScript("arguments[0].value = arguments[1];" +
                              "arguments[0].dispatchEvent(new Event('change',{bubbles:true}));", select, match.GetAttribute("value"));
        if (SelectedText(select) != text) throw new Exception($"tried to choose '{text}' but the dropdown shows '{SelectedText(select)}'");

        return found ? $"'{text}'" : $"'{text}' (requested '{wanted}' is not in the list, used first option)";
    }

    private string SelectedText(IWebElement select) =>
        ((string)_js.ExecuteScript("return arguments[0].options[arguments[0].selectedIndex].text;", select)).Trim();

    /// <param name="parentKeywords">Some forms fill this list only after another dropdown (Country -> State) is chosen.</param>
    public string ChooseFromDropdown(string[] keywords, string wanted, string[]? parentKeywords = null, string parentValue = "")
    {
        var (select, score, info) = Find("select", keywords);
        ScrollTo(select);
        string extra = "";

        if (RealOptions(select).Count == 0 && parentKeywords != null)
        {
            var parent = Find("select", parentKeywords);
            extra = $" (first chose '{Pick(parent.El, parentValue)}' in the parent dropdown)";
            for (int i = 0; i < 10; i++)          // wait up to 5 s for the list to fill
            {
                Thread.Sleep(500);
                (select, score, info) = Find("select", keywords);   // the element may have been re-created
                if (RealOptions(select).Count > 0) break;
            }
        }
        return $"chose {Pick(select, wanted)} -> dropdown [{info}] (score {score}){extra}";
    }
}
