export const apiBaseUrl = process.env.NEXT_PUBLIC_API_BASE_URL?.replace(/\/$/, "");

export function apiUrl(path: string) {
  if (!apiBaseUrl) {
    throw new Error("NEXT_PUBLIC_API_BASE_URL is not configured.");
  }

  if (apiBaseUrl.endsWith("/api") && path.startsWith("/api/")) {
    return `${apiBaseUrl}${path.slice(4)}`;
  }

  return `${apiBaseUrl}${path}`;
}

export async function readApiError(response: Response, fallback: string) {
  const error = await response.json().catch(() => ({ error: fallback }));
  return error.error ?? fallback;
}
