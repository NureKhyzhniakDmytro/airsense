export interface PaginationState {
  total: number;
  skip: number;
  count: number;
}

export interface PageChangeEvent {
  first: number;
  rows: number;
} 