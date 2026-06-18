export type PlaceIconOption = {
  value: string;
  label: string;
  description: string;
  symbol: string;
};

export const PLACE_ICON_OPTIONS: PlaceIconOption[] = [
  { value: "building", label: "Office", description: "Administrative or mixed-use space", symbol: "apartment" },
  { value: "industrial", label: "Industrial", description: "Workshop, factory, production floor", symbol: "factory" },
  { value: "residential", label: "Residential", description: "Apartment, house, private room", symbol: "home_work" },
  { value: "laboratory", label: "Laboratory", description: "Testing, validation, calibration", symbol: "science" },
  { value: "warehouse", label: "Warehouse", description: "Storage and logistics zone", symbol: "warehouse" },
  { value: "classroom", label: "Classroom", description: "Teaching and study room", symbol: "school" },
  { value: "retail", label: "Retail", description: "Shop or customer-facing area", symbol: "storefront" },
  { value: "healthcare", label: "Healthcare", description: "Clinic, medical, controlled area", symbol: "local_hospital" },
];

export const ROOM_ICON_OPTIONS: PlaceIconOption[] = [
  { value: "room", label: "Room", description: "General monitored room", symbol: "meeting_room" },
  { value: "production", label: "Production", description: "Production or machine area", symbol: "precision_manufacturing" },
  { value: "living", label: "Living", description: "Residential living space", symbol: "weekend" },
  { value: "lab", label: "Lab", description: "Sensor validation or lab zone", symbol: "science" },
  { value: "office", label: "Office", description: "Desk-based workspace", symbol: "desk" },
  { value: "storage", label: "Storage", description: "Storage or warehouse room", symbol: "inventory_2" },
  { value: "ventilation", label: "Ventilation", description: "Mechanical or HVAC room", symbol: "mode_fan" },
  { value: "server", label: "Server", description: "Server, rack or equipment room", symbol: "dns" },
];

export const getPlaceIconOption = (value?: string | null) => (
  [...PLACE_ICON_OPTIONS, ...ROOM_ICON_OPTIONS].find((option) => option.value === value)
  ?? PLACE_ICON_OPTIONS[0]
);
