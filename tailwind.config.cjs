/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.razor",
    "./src/**/*.cshtml",
    "./src/**/*.html",
    "./src/**/*.js",
  ],
  // Safelist is intentionally minimal.
  // As of 2026-03-01 no runtime-generated Tailwind utility class names are used.
  safelist: [],
  theme: {
    extend: {
      colors: {
        ink: {
          50: "#f8fafc",
          100: "#f1f5f9",
          200: "#e2e8f0",
          300: "#cbd5e1",
          400: "#94a3b8",
          500: "#64748b",
          600: "#475569",
          700: "#334155",
          800: "#1f2937",
          900: "#0f172a"
        }
      }
    }
  },
  plugins: []
};
