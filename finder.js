// finder.js - runs INSIDE the browser page (called from C# with ExecuteScript).
// Instead of one fixed XPath, it looks at EVERY candidate field and gives each a
// score based on several clues. The best-scoring field wins.
//
// arguments: [kind, keywords[], optionText]
//   kind     = "text" | "select" | "radio"
//   keywords = words that describe the field, e.g. ["first name", "fname"]
//   optionText = (radio only) the option we want, e.g. "Male"
// returns: [element, score, description]  or  null if nothing good was found

const kind = arguments[0];
const kws = arguments[1].map(norm);
const wanted = norm(arguments[2] || "");

// ---------- small helpers ----------
function norm(s) { return String(s || "").toLowerCase().replace(/[^a-z0-9]+/g, " ").trim(); }
function squash(s) { return norm(s).replace(/ /g, ""); }
function has(text, k) { return (" " + text + " ").indexOf(" " + k + " ") !== -1; }  // whole-word match

function box(e) {
  const r = e.getBoundingClientRect(), s = getComputedStyle(e);
  return r.width > 0 && r.height > 0 && s.display !== "none" && s.visibility !== "hidden";
}
function visible(el) {
  // fancy radio buttons hide the real <input> but show the label - that counts as visible
  if (el.type === "radio" && el.labels && el.labels.length && box(el.labels[0])) return true;
  return box(el);
}

// Text that is officially attached to the field (<label for>, wrapping <label>, aria-*)
function labelText(el) {
  const t = [];
  if (el.labels) for (const l of el.labels) t.push(l.innerText || l.textContent);
  const by = el.getAttribute("aria-labelledby");
  if (by) by.split(" ").forEach(id => { const n = document.getElementById(id); if (n) t.push(n.textContent); });
  t.push(el.getAttribute("aria-label") || "");
  return norm(t.join(" "));
}

// Plain text right after a radio button:  <input type="radio"> Male
function siblingText(el) {
  let n = el.nextSibling;
  while (n && n.nodeType === 3 && !n.textContent.trim()) n = n.nextSibling;
  if (!n) return "";
  if (n.nodeType === 3) return n.textContent;
  if (n.nodeType === 1 && !/^(INPUT|SELECT|TEXTAREA|BUTTON)$/.test(n.tagName) && n.textContent.length < 40) return n.textContent;
  return "";
}

// All visible text pieces of the page, in order (used to find "the text just before a field")
const textNodes = [];
const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
for (let n; (n = walker.nextNode());) {
  if (!n.textContent.trim()) continue;
  if (n.parentElement && n.parentElement.closest("option,script,style,noscript")) continue;
  textNodes.push(n);
}
function nearby(el, count) {            // the closest `count` text pieces BEFORE the element
  const out = [];
  for (let i = textNodes.length - 1; i >= 0 && out.length < count; i--) {
    if (el.compareDocumentPosition(textNodes[i]) & Node.DOCUMENT_POSITION_PRECEDING) out.push(norm(textNodes[i].textContent));
  }
  return out;
}

// ---------- scoring for text boxes and dropdowns ----------
function fieldScore(el) {
  const label = labelText(el);
  const hint = norm(el.getAttribute("placeholder") || el.title || "");
  const ids = squash((el.name || "") + " " + (el.id || ""));
  const near = nearby(el, 2);
  let best = 0;
  for (const k of kws) {
    const ks = k.replace(/ /g, "");
    if (label === k) best = Math.max(best, 100);               // label says exactly "First Name"
    else if (has(label, k)) best = Math.max(best, 80);         // label contains it ("First Name *")
    if (hint === k) best = Math.max(best, 70);                 // placeholder / title
    else if (has(hint, k)) best = Math.max(best, 60);
    if (ids.indexOf(ks) !== -1) best = Math.max(best, 50);     // id or name contains "firstname"
    if (near[0] === k) best = Math.max(best, 45);              // the text right before it is exactly the keyword
    else if (near[0] && has(near[0], k)) best = Math.max(best, 40);
    else if (near[1] && has(near[1], k)) best = Math.max(best, 15);
  }
  return best;
}

// ---------- pick the candidates ----------
let pool;
if (kind === "text") {
  pool = [...document.querySelectorAll("input")].filter(e => (e.type === "text" || e.type === "search") && !e.readOnly);
} else if (kind === "select") {
  pool = [...document.querySelectorAll("select")];
} else {
  pool = [...document.querySelectorAll("input[type=radio]")];
}
pool = pool.filter(e => !e.disabled && visible(e));

let best = null, bestScore = 0, bestInfo = "";

if (kind === "radio") {
  // A radio button is judged by TWO things: its own option text ("Male")
  // and the group it belongs to (is the group called "Gender"?).
  const allRadios = [...document.querySelectorAll("input[type=radio]")];
  for (const r of pool) {
    const own = labelText(r) || norm(siblingText(r));
    let optionScore = 0;
    if (wanted && own === wanted) optionScore = 100;
    else if (wanted && norm(r.value) === wanted) optionScore = 90;
    else if (wanted && has(own, wanted)) optionScore = 50;

    const group = r.name ? allRadios.filter(x => x.name === r.name) : [r];
    const first = group[0];
    const legend = first.closest("fieldset") ? norm((first.closest("fieldset").querySelector("legend") || {}).textContent) : "";
    const groupText = [squash((first.name || "") + " " + (first.id || "")), legend, ...nearby(first, 2)];
    const groupMatch = kws.some(k => groupText.some(t => t && (has(t, k) || t.indexOf(k.replace(/ /g, "")) !== -1)));
    const total = optionScore + (groupMatch ? 50 : 0);

    if (total > bestScore) { best = r; bestScore = total; bestInfo = `option "${own}", group "${first.name || "?"}"`; }
  }
  if (bestScore < 50) best = null;       // need at least a good option match OR a good group match
} else {
  for (const el of pool) {
    const s = fieldScore(el);
    if (s > bestScore) {
      best = el; bestScore = s;
      bestInfo = `label "${labelText(el)}", id "${el.id}", name "${el.name}"`;
    }
  }
  if (bestScore < 30) best = null;
}

return best ? [best, bestScore, bestInfo] : null;
