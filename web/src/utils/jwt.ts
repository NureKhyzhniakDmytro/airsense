import { jwtDecode } from "jwt-decode";

export interface DecodedToken {
  id?: number;
  email?: string;
  name?: string;
  exp?: number;
}

export const decodeToken = (token: string): DecodedToken | null => {
  try {
    return jwtDecode<DecodedToken>(token);
  } catch (error) {
    console.error("Помилка декодування JWT:", error);
    return null;
  }
};
