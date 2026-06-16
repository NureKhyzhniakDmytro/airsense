import PrimeVue from 'primevue/config'
import ConfirmationService from 'primevue/confirmationservice'
import ToastService from 'primevue/toastservice'
import Ripple from 'primevue/ripple'
import Aura from '@primeuix/themes/aura'
import { definePreset } from '@primeuix/themes'

export default defineNuxtPlugin((nuxtApp) => {
  const preset = definePreset(Aura, {
    semantic: {
      primary: {
        50: '{indigo.50}',
        100: '{indigo.100}',
        200: '{indigo.200}',
        300: '{indigo.300}',
        400: '{indigo.400}',
        500: '{indigo.500}',
        600: '{indigo.600}',
        700: '{indigo.700}',
        800: '{indigo.800}',
        900: '{indigo.900}',
        950: '{indigo.950}',
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
  nuxtApp.vueApp.use(ToastService)
  nuxtApp.vueApp.use(ConfirmationService)
})
