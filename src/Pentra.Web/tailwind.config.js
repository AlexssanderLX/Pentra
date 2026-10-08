/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./Views/**/*.cshtml"],
  // Progress-bar widths are chosen at runtime; safelist the bucketed values so
  // we never need inline style attributes (kept out by a strict CSP).
  safelist: Array.from({ length: 21 }, (_, i) => `w-[${i * 5}%]`),
  theme: {
    extend: {
      colors: {
        bg: "#080B09",
        surface: "#18251B",
        primary: {
          DEFAULT: "#76FF45",
          soft: "rgba(118, 255, 69, 0.12)",
        },
        content: "#E6F2E4",
        muted: "#8DA18E",
      },
      fontFamily: {
        sans: [
          "Inter",
          "ui-sans-serif",
          "system-ui",
          "-apple-system",
          "Segoe UI",
          "Roboto",
          "sans-serif",
        ],
        mono: [
          "JetBrains Mono",
          "ui-monospace",
          "SFMono-Regular",
          "Menlo",
          "Consolas",
          "monospace",
        ],
      },
      boxShadow: {
        card: "0 1px 0 0 rgba(255,255,255,0.03) inset, 0 8px 24px -12px rgba(0,0,0,0.7)",
      },
    },
  },
  plugins: [],
};
