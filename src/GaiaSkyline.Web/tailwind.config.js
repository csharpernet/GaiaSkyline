/**
 * GaiaSkyline design tokens (Tailwind side).
 * The SAME tokens are mirrored as CSS custom properties in wwwroot/css/tokens.css so a Razor
 * partial can use either the utility classes (e.g. `bg-stone`) or the variables
 * (e.g. `var(--color-stone)`). Keep the two files in sync. See ADR 0001.
 *
 * @type {import('tailwindcss').Config}
 */
module.exports = {
  content: ['./Views/**/*.cshtml', './Pages/**/*.cshtml'],
  theme: {
    extend: {
      colors: {
        ink: '#0F1417',
        stone: '#F5F1EA',
        clay: '#B85D3A',
        river: '#2E4F60',
        fog: '#D9D2C5',
        white: '#FFFFFF',
      },
      fontFamily: {
        // The variable web fonts are loaded from Google Fonts in _Layout.cshtml.
        display: ['Fraunces', 'ui-serif', 'Georgia', 'serif'],
        body: ['Inter', 'ui-sans-serif', 'system-ui', 'sans-serif'],
      },
      borderRadius: {
        // 6 / 12 / 24 px scale.
        sm: '6px',
        md: '12px',
        lg: '24px',
      },
      transitionTimingFunction: {
        // Motion is 200-300ms ease-out throughout.
        out: 'cubic-bezier(0, 0, 0.2, 1)',
      },
      transitionDuration: {
        fast: '200ms',
        slow: '300ms',
      },
    },
    // Tailwind's default spacing scale is already a 4px grid (1 unit = 0.25rem = 4px).
  },
  plugins: [],
};
