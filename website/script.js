const tabs = document.querySelectorAll("[data-tab]");
const panels = document.querySelectorAll("[data-panel]");
const themeToggle = document.querySelector(".theme-toggle");
const menuToggle = document.querySelector(".menu-toggle");
const mobileMenu = document.querySelector("#mobile-menu");
const brandLogo = document.querySelector(".brand img");
const revealTargets = document.querySelectorAll(
  ".compare-card, .feature-card, .steps article, .speed-list article, .solution-copy, .solution-map, .install-copy, .terminal"
);

const setTheme = (theme) => {
  const isDark = theme === "dark";

  document.documentElement.dataset.theme = theme;

  try {
    localStorage.setItem("injectlynx-theme", theme);
  } catch {
    // Theme still works for the current page even when storage is blocked.
  }
  document
    .querySelector('meta[name="theme-color"]')
    ?.setAttribute("content", isDark ? "#10171F" : "#8B5CFF");

  if (brandLogo) {
    brandLogo.setAttribute(
      "src",
      isDark ? "assets/injectlynx-logo-dark.svg" : "assets/injectlynx-logo.svg"
    );
  }

  if (themeToggle) {
    themeToggle.setAttribute("aria-pressed", String(isDark));
    themeToggle.setAttribute("aria-label", isDark ? "Switch to light mode" : "Switch to dark mode");
  }
};

const closeMenu = () => {
  if (!menuToggle || !mobileMenu) {
    return;
  }

  menuToggle.setAttribute("aria-expanded", "false");
  menuToggle.setAttribute("aria-label", "Open menu");
  mobileMenu.classList.remove("open");
};

setTheme(document.documentElement.dataset.theme || "light");

themeToggle?.addEventListener("click", () => {
  setTheme(document.documentElement.dataset.theme === "dark" ? "light" : "dark");
});

menuToggle?.addEventListener("click", () => {
  const isOpen = menuToggle.getAttribute("aria-expanded") === "true";

  menuToggle.setAttribute("aria-expanded", String(!isOpen));
  menuToggle.setAttribute("aria-label", isOpen ? "Open menu" : "Close menu");
  mobileMenu?.classList.toggle("open", !isOpen);
});

mobileMenu?.querySelectorAll("a").forEach((link) => {
  link.addEventListener("click", closeMenu);
});

document.addEventListener("keydown", (event) => {
  if (event.key === "Escape") {
    closeMenu();
  }
});

tabs.forEach((tab) => {
  tab.addEventListener("click", () => {
    const target = tab.dataset.tab;

    tabs.forEach((item) => item.classList.toggle("active", item === tab));
    panels.forEach((panel) => {
      panel.classList.toggle("active", panel.dataset.panel === target);
    });
  });
});

revealTargets.forEach((target) => target.classList.add("reveal"));

if ("IntersectionObserver" in window) {
  const observer = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) {
          return;
        }

        entry.target.classList.add("visible");
        observer.unobserve(entry.target);
      });
    },
    { threshold: 0.16 }
  );

  revealTargets.forEach((target) => observer.observe(target));
} else {
  revealTargets.forEach((target) => target.classList.add("visible"));
}
