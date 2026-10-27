// The work page's script (ADR 0023, docs/ui.md "Work pages"). Without it the page still works: every selection is a
// real link and the panes keep their default widths.
//  - Navigation: a plain left click on a work link updates the URL (a new history entry, or the same one for
//    data-history="replace") and asks the page to show it; Back and Forward do the same. Modified clicks (new tab,
//    new window) are left to the browser.
//  - Esc closes the step details.
//  - Dividers: drag, or arrow keys when focused, to resize; double-click, Enter or the collapse buttons to collapse.
//    Widths and collapsed panes are remembered in this browser (local storage) as CSS variables on the layout.

const storageKey = "agentledger.work.panes";
const minimum = { left: 200, right: 320 };
const middleMinimum = 480;
const handlers = new WeakMap();

export function init(layout, page) {
  const state = load();
  apply(layout, state);

  const onClick = (event) => {
    const collapse = event.target.closest("[data-collapse]");
    if (collapse) {
      toggle(layout, state, collapse.dataset.collapse);
      return;
    }
    const link = event.target.closest("a[data-work-nav]");
    if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
      return;
    }
    event.preventDefault();
    if (link.dataset.history === "replace") {
      history.replaceState(null, "", link.href);
    } else {
      history.pushState(null, "", link.href);
    }
    page.invokeMethodAsync("GoAsync", location.href);
  };
  const onPopState = () => page.invokeMethodAsync("GoAsync", location.href);
  const onKeyDown = (event) => {
    if (event.key === "Escape") {
      layout.querySelector("a[data-close]")?.click();
      return;
    }
    const divider = event.target.closest?.("[data-divider]");
    if (!divider) {
      return;
    }
    const side = divider.dataset.divider;
    if (event.key === "Enter" || event.key === "Home" || event.key === "End") {
      event.preventDefault();
      toggle(layout, state, side);
    } else if (event.key === "ArrowLeft" || event.key === "ArrowRight") {
      event.preventDefault();
      const step = (event.key === "ArrowRight") === (side === "left") ? 16 : -16;
      resize(layout, state, side, width(layout, side) + step);
    }
  };
  const onDoubleClick = (event) => {
    const divider = event.target.closest("[data-divider]");
    if (divider) {
      toggle(layout, state, divider.dataset.divider);
    }
  };
  const onPointerDown = (event) => {
    const divider = event.target.closest("[data-divider]");
    if (!divider || event.button !== 0) {
      return;
    }
    event.preventDefault();
    const side = divider.dataset.divider;
    divider.setPointerCapture(event.pointerId);
    const move = (moveEvent) => {
      const bounds = layout.getBoundingClientRect();
      resize(layout, state, side, side === "left" ? moveEvent.clientX - bounds.left : bounds.right - moveEvent.clientX);
    };
    const up = () => {
      divider.removeEventListener("pointermove", move);
      divider.removeEventListener("pointerup", up);
      save(state);
    };
    divider.addEventListener("pointermove", move);
    divider.addEventListener("pointerup", up);
  };

  layout.addEventListener("click", onClick);
  layout.addEventListener("dblclick", onDoubleClick);
  layout.addEventListener("pointerdown", onPointerDown);
  document.addEventListener("keydown", onKeyDown);
  window.addEventListener("popstate", onPopState);
  handlers.set(layout, { onClick, onDoubleClick, onPointerDown, onKeyDown, onPopState });
}

export function dispose(layout) {
  const h = handlers.get(layout);
  if (!h) {
    return;
  }
  layout.removeEventListener("click", h.onClick);
  layout.removeEventListener("dblclick", h.onDoubleClick);
  layout.removeEventListener("pointerdown", h.onPointerDown);
  document.removeEventListener("keydown", h.onKeyDown);
  window.removeEventListener("popstate", h.onPopState);
  handlers.delete(layout);
}

// Shows the expanded turn, or the newest turn when none is expanded.
export function scrollToTurn(layout) {
  const pane = layout.querySelector("[data-turns-pane]");
  const target = pane?.querySelector("[data-selected-turn]") ?? [...(pane?.querySelectorAll("[data-turn-row]") ?? [])].pop();
  target?.scrollIntoView({ block: "nearest" });
}

function width(layout, side) {
  return layout.querySelector(`.pane.${side === "left" ? "units" : "details"}`)?.getBoundingClientRect().width ?? 0;
}

function resize(layout, state, side, px) {
  const available = layout.getBoundingClientRect().width - middleMinimum - (side === "left" ? width(layout, "right") : width(layout, "left"));
  state[side] = Math.round(Math.max(minimum[side], Math.min(px, available)));
  state[`${side}Collapsed`] = false;
  apply(layout, state);
  save(state);
}

function toggle(layout, state, side) {
  state[`${side}Collapsed`] = !state[`${side}Collapsed`];
  apply(layout, state);
  save(state);
}

function apply(layout, state) {
  for (const side of ["left", "right"]) {
    if (state[side]) {
      layout.style.setProperty(`--work-${side}`, `${state[side]}px`);
    }
    layout.classList.toggle(`${side}-collapsed`, !!state[`${side}Collapsed`]);
    const divider = layout.querySelector(`[data-divider="${side}"]`);
    divider?.setAttribute("aria-valuenow", String(state[side] ?? ""));
  }
}

function load() {
  try {
    return JSON.parse(localStorage.getItem(storageKey) ?? "{}") ?? {};
  } catch {
    return {};
  }
}

function save(state) {
  try {
    localStorage.setItem(storageKey, JSON.stringify(state));
  } catch {
    // Storage unavailable (private window, blocked): widths just aren't remembered.
  }
}
