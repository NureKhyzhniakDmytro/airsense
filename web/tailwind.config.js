import daisyui from "daisyui";
import primeui from "tailwindcss-primeui";

/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./src/app.vue",
    "./src/components/**/*.{vue,js,ts}",
    "./src/layouts/**/*.{vue,js,ts}",
    "./src/pages/**/*.{vue,js,ts}",
    "./src/plugins/**/*.{js,ts}",
  ],
  theme: {
    extend: {},
  },
  plugins: [
    daisyui,
    primeui,
  ],
};
