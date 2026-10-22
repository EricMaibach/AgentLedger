// Progressive enhancement only (docs/ui.md): every page works without this script.

document.documentElement.classList.add("js");

// Copy buttons: <button class="copy-button" data-copy="text">.
document.addEventListener("click", async (event) => {
  const button = event.target instanceof Element ? event.target.closest("[data-copy]") : null;
  if (!button || !navigator.clipboard) {
    return;
  }

  const label = button.getAttribute("aria-label");
  try {
    await navigator.clipboard.writeText(button.dataset.copy ?? "");
    button.setAttribute("aria-label", "Copied");
    button.textContent = "✓";
  } catch {
    button.setAttribute("aria-label", "Copy failed");
    button.textContent = "!";
  }

  setTimeout(() => {
    button.setAttribute("aria-label", label ?? "Copy");
    button.textContent = "⧉";
  }, 1500);
});
