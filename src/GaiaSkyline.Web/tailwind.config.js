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
        // Darkened from #B85D3A to meet WCAG AA (4.5:1) as link/label text on stone/white.
        clay: '#A04A28',
        river: '#2E4F60',
        fog: '#D9D2C5',
        white: '#FFFFFF',
      },
      fontFamily: {
        // Web fonts from Google Fonts; the "*. Fallback" faces (tokens.css) carry size-adjust
        // metrics so the swap from fallback to web font does not shift layout (CLS).
        display: ['Fraunces', 'Fraunces Fallback', 'ui-serif', 'Georgia', 'serif'],
        body: ['Inter', 'Inter Fallback', 'ui-sans-serif', 'system-ui', 'sans-serif'],
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
