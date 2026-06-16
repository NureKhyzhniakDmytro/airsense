export default defineEventHandler((event) => {
  const runtimeConfig = useRuntimeConfig();
  const baseUrl = String(runtimeConfig.apiInternalBaseUrl || "").replace(/\/$/, "");
  const path = String(event.context.params?.path || "").replace(/^\//, "");
  const search = getRequestURL(event).search;

  if (!baseUrl) {
    throw createError({
      statusCode: 500,
      statusMessage: "API internal base URL is not configured.",
    });
  }

  return proxyRequest(event, `${baseUrl}/${path}${search}`);
});
