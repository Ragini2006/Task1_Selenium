# Task 1 - Form automation that keeps working when the page changes (C# + Selenium)

## 1. What is this?

A C# automation tool that opens a web form in Chrome and fills in three target fields:

| Field | Type | What the program does |
|---|---|---|
| **First Name** | text box | types a name |
| **Gender** | radio buttons | clicks "Male" (or configured gender) |
| **State** | dropdown | selects a state from the list |

After each step, it **verifies that the action actually succeeded on the rendered page** (the typed name is present, the radio button is checked, the dropdown displays the selected item) and prints **PASS** or **FAIL**.

The core strength: the program is built to **survive drastic UI changes**—including reversed element order, randomized `id` / `name` / `class` attributes, restyled radio buttons, and the presence of decoy fields.

---

## 2. Why is this needed? (The Problem)

Traditional Selenium scripts find elements using rigid locators like `id="fname"` or hardcoded XPaths. If a developer changes an ID during a redesign, the test breaks immediately—even if the form looks unchanged to a real human.

Humans do not look at underlying DOM IDs; they locate fields by reading **labels**, **placeholders**, and **visual context**. This program mimics that human behavior by evaluating contextual clues across the entire page.

---

## 3. How it works (The Scoring Algorithm)

Instead of relying on a single static selector, the engine executes an evaluation script (`finder.js`) directly in the browser context:

1. It scans **all** interactive input fields, dropdowns, and radio groups on the page.
2. It assigns **marks (scores)** based on multi-attribute relevance:

| Clue | Example | Score Weight |
|---|---|---|
| Exact matching `<label>` text | `<label>First Name</label>` | Highest |
| Partial label match | `<label>First Name *</label>` | High[cite: 5] |
| Placeholder / title attribute | `placeholder="First Name"` | Medium-High[cite: 5] |
| `id` or `name` attribute match | `id="txt_first_name"` | Medium[cite: 5] |
| Surrounding / adjacent DOM text | Preceding text "First Name:" | Medium[cite: 5] |

3. The candidate element with the **highest cumulative score wins**[cite: 5]. This guarantees that a decoy field like "Father's First Name" always loses to the exact match "First Name"[cite: 5].
4. The tool executes the action and reads the value back to verify state changes before concluding[cite: 5].

### Smart Form Handling
- **Radio Buttons:** Evaluates both the specific option text ("Male") and the parent radio group label ("Gender")[cite: 5]. Restyled or custom CSS radio pills with hidden native inputs are triggered via their associated label elements[cite: 5].
- **Cascading Dropdowns:** If a State dropdown is dependent on Country and initially empty, the program automatically selects Country first, waits for the DOM to populate, and then selects State[cite: 5].
- **Dynamic URLs:** Fully configurable via command line parameters, environment variables, or `appsettings.json`[cite: 5].

### Project Structure
| File | Purpose |
|---|---|
| `Program.cs` | Entry point, configuration loader, orchestrator, and test reporter[cite: 5] |
| `FieldActions.cs` | Action execution and DOM state verification[cite: 5] |
| `finder.js` | In-page scoring and candidate ranking engine[cite: 5] |
| `appsettings.json` | Default settings (URL, Name, Gender, Country, State)[cite: 5] |
| `test-pages/original.html` | Clean baseline practice form[cite: 5] |
| `test-pages/changed.html` | Stress-test form with reversed layout, randomized IDs, and decoy fields[cite: 5] |

---

## 4. Prerequisites

1. **.NET SDK** (version 8.0, 9.0, or 10.0) - [Download .NET](https://dotnet.microsoft.com/download)[cite: 5]
2. **Google Chrome** installed (Selenium automatically manages matching ChromeDriver binaries)[cite: 5]

Verify .NET installation in your terminal:
```powershell
dotnet --version

--------------------------------------------------------------------------------
                            HOW TO RUN
--------------------------------------------------------------------------------

1. Open PowerShell and go to your Task 1 folder:
   cd C:\Users\Ragini\Downloads\task1-selenium-csharp

2. Run the baseline original form:
   dotnet run -- --Url "file:///$((Get-Item test-pages/original.html).FullName.Replace('\', '/'))"

3. Run the changed form (stress test with reversed order, random IDs & decoys):
   dotnet run -- --Url "file:///$((Get-Item test-pages/changed.html).FullName.Replace('\', '/'))"

4. Run the live online practice form:
   dotnet run -- --Url "https://app.cloudqa.io/home/AutomationPracticeForm"

5. Run with custom values via command line:
   dotnet run -- --FirstName "Asha" --Gender Female --Country Canada --State Ontario

================================================================================
                           LIMITATIONS 
================================================================================

--- TASK 1: C# Form Automation (finder.js) ---

1. Needs Text Clues:
   - It works by reading words, labels, and hints on the screen[cite: 2, 5]. 
   - If a website uses only pictures/icons with zero text, it cannot guess correctly[cite: 2, 5].

2. Only Basic Web Controls:
   - Designed for standard inputs, regular dropdowns, and radio buttons[cite: 5]. 
   - It does not handle fancy dropdowns made entirely out of <div> tags[cite: 5].

3. No Hidden Frames or Shadow DOM:
   - It only looks at the main page[cite: 2, 5]. 
   - It cannot look inside nested iframes (a webpage inside a webpage) or closed Shadow DOM internals[cite: 2, 5].

4. Cannot Solve CAPTCHAs:
   - It cannot bypass bot puzzles, checkboxes, or security blockers[cite: 5].
