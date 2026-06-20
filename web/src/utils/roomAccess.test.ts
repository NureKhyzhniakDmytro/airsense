import { describe, expect, it } from "vitest";
import { canManageRole, isReadOnlyRole } from "./roomAccess";

describe("roomAccess", () => {
  it("treats user role as read-only", () => {
    expect(isReadOnlyRole("user")).toBe(true);
    expect(canManageRole("user")).toBe(false);
  });

  it("allows owner and admin roles to manage", () => {
    expect(isReadOnlyRole("owner")).toBe(false);
    expect(isReadOnlyRole("admin")).toBe(false);
    expect(canManageRole("owner")).toBe(true);
    expect(canManageRole("admin")).toBe(true);
  });

  it("does not mark missing roles as read-only", () => {
    expect(isReadOnlyRole(null)).toBe(false);
    expect(isReadOnlyRole(undefined)).toBe(false);
  });
});
