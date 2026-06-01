import "./styles.css";
import type { Metadata } from "next";
import type { Viewport } from "next";
import type { ReactNode } from "react";

export const metadata: Metadata = {
  title: "PoolPredict",
  description: "Prediction pools with virtual points."
};

export const viewport: Viewport = {
  themeColor: "#07110f"
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
