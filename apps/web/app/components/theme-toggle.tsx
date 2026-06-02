"use client";

import { Moon, Sun } from "lucide-react";
import { useEffect, useState } from "react";

type Theme = "dark" | "light";

const storageKey = "poolpredict-theme";

function applyTheme(theme: Theme) {
  document.documentElement.dataset.theme = theme;
  document.documentElement.style.colorScheme = theme;
  localStorage.setItem(storageKey, theme);
}

export function ThemeToggle() {
  const [theme, setTheme] = useState<Theme>("dark");

  useEffect(() => {
    const current = document.documentElement.dataset.theme === "light" ? "light" : "dark";
    setTheme(current);
  }, []);

  function toggleTheme() {
    const nextTheme = theme === "dark" ? "light" : "dark";
    applyTheme(nextTheme);
    setTheme(nextTheme);
  }

  const isLight = theme === "light";
  const Icon = isLight ? Moon : Sun;

  return (
    <button
      aria-label={isLight ? "Switch to dark theme" : "Switch to light theme"}
      className="themeToggle"
      title={isLight ? "Switch to dark theme" : "Switch to light theme"}
      type="button"
      onClick={toggleTheme}
    >
      <Icon aria-hidden="true" size={18} strokeWidth={2.2} />
    </button>
  );
}
