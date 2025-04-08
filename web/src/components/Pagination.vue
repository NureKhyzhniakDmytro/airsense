<template>
  <nav v-if="totalPages > 1" role="navigation" aria-label="pagination" class="mx-auto flex w-full justify-center">
    <ul class="flex flex-row items-center gap-1">
      <!-- Кнопка "Назад" (скрывается, если нет элементов) -->
      <li v-if="hasPrev">
        <button
            @click="$emit('prev')"
            class="pagination-btn"
            aria-label="Go to previous page"
        >
          <ChevronLeftIcon class="h-5 w-5" />
        </button>
      </li>

      <!-- Номера страниц -->
      <li v-for="page in pages" :key="page">
        <button
            v-if="typeof page === 'number'"
            @click="$emit('page-change', page)"
            :disabled="page === currentPage"
            class="pagination-btn"
            :class="{ 'pagination-active': page === currentPage }"
        >
          {{ page }}
        </button>
        <span v-else class="pagination-ellipsis">
          <EllipsisHorizontalIcon class="h-5 w-5" />
        </span>
      </li>

      <!-- Кнопка "Вперёд" (скрывается, если нет элементов) -->
      <li v-if="hasNext">
        <button
            @click="$emit('next')"
            class="pagination-btn"
            aria-label="Go to next page"
        >
          <ChevronRightIcon class="h-5 w-5" />
        </button>
      </li>
    </ul>
  </nav>
</template>

<script setup lang="ts">
import { computed, defineProps, defineEmits } from "vue";
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  EllipsisHorizontalIcon,
} from "@heroicons/vue/24/outline";

defineEmits(["prev", "next", "page-change"]);

const props = defineProps<{
  totalPages: number;
  currentPage: number;
}>();

const hasPrev = computed(() => props.currentPage > 1);
const hasNext = computed(() => props.currentPage < props.totalPages);

const pages = computed(() => {
  const maxPagesToShow = 5;
  const { totalPages, currentPage } = props;

  if (totalPages <= maxPagesToShow) {
    return Array.from({ length: totalPages }, (_, i) => i + 1);
  }

  const pagesArray = [1];

  if (currentPage > 3) {
    pagesArray.push("...");
  }

  const startPage = Math.max(2, currentPage - 1);
  const endPage = Math.min(totalPages - 1, currentPage + 1);

  for (let i = startPage; i <= endPage; i++) {
    pagesArray.push(i);
  }

  if (currentPage < totalPages - 2) {
    pagesArray.push("...");
  }

  pagesArray.push(totalPages);

  return pagesArray;
});
</script>

<style scoped>
/* Основной стиль кнопок */
.pagination-btn {
  @apply w-10 h-10 flex items-center justify-center rounded-md transition cursor-pointer border bg-gray-200 text-gray-700;
}

.pagination-btn:hover {
  @apply bg-gray-300;
}

/* Активная страница */
.pagination-active {
  @apply bg-blue-600 text-white;
}

/* Эллипсис */
.pagination-ellipsis {
  @apply flex w-10 h-10 items-center justify-center text-gray-400;
}
</style>
