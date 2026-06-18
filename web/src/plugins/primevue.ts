import PrimeVue from 'primevue/config'
import ConfirmationService from 'primevue/confirmationservice'
import ToastService from 'primevue/toastservice'
import Ripple from 'primevue/ripple'
import Tooltip from 'primevue/tooltip'
import Aura from '@primeuix/themes/aura'
import { definePreset } from '@primeuix/themes'

export default defineNuxtPlugin((nuxtApp) => {
  const preset = definePreset(Aura, {
    semantic: {
      primary: {
        50: '#f0fdfa',
        100: '#ccfbf1',
        200: '#99f6e4',
        300: '#5eead4',
        400: '#2dd4bf',
        500: '#14b8a6',
        600: '#0f766e',
        700: '#0b5f59',
        800: '#0b4f4a',
        900: '#134e4a',
        950: '#042f2e',
      },
    },
  })

  nuxtApp.vueApp.use(PrimeVue, {
    theme: {
      preset,
      options: {
        cssLayer: {
          name: 'primevue',
          order: 'tailwind-base, primevue, tailwind-utilities',
        },
        darkModeSelector: '.app-dark',
      },
    },
    ripple: true,
  })
  nuxtApp.vueApp.directive('ripple', Ripple)
  nuxtApp.vueApp.directive('tooltip', Tooltip)
  nuxtApp.vueApp.use(ToastService)
  nuxtApp.vueApp.use(ConfirmationService)
})
