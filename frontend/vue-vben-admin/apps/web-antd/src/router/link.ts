import type { RouteLocationRaw } from 'vue-router';

export function createRouteLocationFromLink(
  link: string,
  query?: Record<string, any>,
  state?: Record<string, any>,
): RouteLocationRaw {
  const url = new URL(link, window.location.origin);
  const parsedQuery = Object.fromEntries(url.searchParams.entries());

  return {
    hash: url.hash,
    path: url.pathname,
    query: {
      ...parsedQuery,
      ...(query ?? {}),
    },
    state,
  };
}
