import axios from "axios";

const API_URL = "http://127.0.0.1:8080";
// const API_URL = "http://localhost:8080";
// const API_URL = "https://airsense.yooud.org/api";

const api = axios.create({
  baseURL: API_URL,
  headers: {
    "Content-Type": "application/json",
  },
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem("token");
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export default api;
