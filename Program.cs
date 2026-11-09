using Microsoft.Extensions.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;

// ---------- 1. Settings: appsettings.json < environment (FORM_Url=...) < command line (--Url ...) ----------
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables("FORM_")
    .AddCommandLine(args)
    .Build();

string url          = config["Url"] ?? "https://app.cloudqa.io/home/AutomationPracticeForm";
string browser      = (config["Browser"] ?? "chrome").ToLower();
bool headless       = bool.Parse(config["Headless"] ?? "false");
int pauseSeconds    = int.Parse(config["PauseSeconds"] ?? "3");
string firstName    = config["FirstName"] ?? "Ragini";
string gender       = config["Gender"] ?? "Male";
string country      = config["Country"] ?? "India";
string state        = config["State"] ?? "";          // empty = just pick the first real option

// You may pass a local file instead of a web address:  --Url test-pages/changed.html
if (!url.Contains("://") && File.Exists(url)) url = "file:///" + Path.GetFullPath(url).Replace('\\', '/');

// Words that describe each field. Add more in code if a page uses other wording.
string[] firstNameWords = { "first name", "firstname", "fname", "given name", "forename" };
string[] genderWords    = { "gender", "sex" };
string[] stateWords     = { "state", "province" };
string[] countryWords   = { "country", "nation" };

// ---------- 2. Open the browser ----------
IWebDriver CreateDriver()
{
    switch (browser)
    {
        case "firefox":
            var ff = new FirefoxOptions();
            if (headless) ff.AddArgument("-headless");
            return new FirefoxDriver(ff);
        case "edge":
            var edge = new EdgeOptions();
            if (headless) edge.AddArgument("--headless=new");
            return new EdgeDriver(edge);
        default:
            var chrome = new ChromeOptions();
            chrome.LeaveBrowserRunning = true;
            chrome.AddArgument("--window-size=1280,1000");
            if (headless) chrome.AddArgument("--headless=new");
            return new ChromeDriver(chrome);
    }
}
IWebDriver driver = CreateDriver();

// ---------- 3. Run the three checks ----------
var results = new List<(string Field, bool Ok, string Detail)>();
void Check(string name, Func<string> action)
{
    try 
    { 
        string detail = action();
        results.Add((name, true, detail)); 
        Console.WriteLine($"[EXECUTED] {name} -> {detail}");
    }
    catch (Exception e) 
    { 
        string error = e.Message.Split('\n')[0];
        results.Add((name, false, error)); 
        Console.WriteLine($"[FAILED] {name} -> {error}");
    }
}

try
{
    Console.WriteLine($"Opening {url} in {browser}...");
    driver.Navigate().GoToUrl(url);
    var form = new FieldActions(driver);

    Check("First Name (text box)", () => form.TypeText(firstNameWords, firstName));
    Check("Gender (radio)",        () => form.ChooseRadio(genderWords, gender));
    Check("State (dropdown)",      () => form.ChooseFromDropdown(stateWords, state, countryWords, country));

    try {
        File.WriteAllBytes("result.png", ((ITakesScreenshot)driver).GetScreenshot().AsByteArray);
    } catch { }

    Thread.Sleep(pauseSeconds * 1000);
}
catch (Exception ex)
{
    Console.WriteLine($"Unexpected error during execution: {ex.Message}");
}

// ---------- 4. Report (Prints PASS/FAIL FIRST) ----------
Console.WriteLine("\n================ TEST RESULTS ================");
foreach (var (field, ok, detail) in results)
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {field}\n      {detail}");
}

bool allPassed = results.Count > 0 && results.All(r => r.Ok);
Console.WriteLine($"\n{(allPassed ? "ALL CHECKS PASSED" : "SOME CHECKS FAILED")}   (screenshot: result.png)");

// ---------- 5. Wait for User before closing ----------
Console.WriteLine("\nAutomation finished! Chrome will stay open.");
Console.WriteLine("Press Enter in this terminal window whenever you want to close Chrome...");
Console.ReadLine();

try { driver.Quit(); } catch { }

return allPassed ? 0 : 1;