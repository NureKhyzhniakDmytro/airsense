export const isReadOnlyRole = (role?: string | null) => role === "user";

export const canManageRole = (role?: string | null) => !isReadOnlyRole(role);
