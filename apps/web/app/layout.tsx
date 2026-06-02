import "./styles.css";
import type { Metadata } from "next";
import type { Viewport } from "next";
import type { ReactNode } from "react";
import { appName } from "./lib/config";

export const metadata: Metadata = {
  title: appName,
  description: "Sports Prediction with virtual points.",
};

export const viewport: Viewport = {
  themeColor: "#07110f",
};

const themeScript = `
(() => {
  try {
    const storedTheme = localStorage.getItem("poolpredict-theme");
    const theme = storedTheme === "light" ? "light" : "dark";
    document.documentElement.dataset.theme = theme;
    document.documentElement.style.colorScheme = theme;
  } catch {
    document.documentElement.dataset.theme = "dark";
  }
})();
`;

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body>
        <script dangerouslySetInnerHTML={{ __html: themeScript }} />
        {children}
      </body>
    </html>
  );
}
