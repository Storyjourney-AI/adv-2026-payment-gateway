const TURNSTILE_SCRIPT_ID = "turnstile-script";
const TURNSTILE_DEFAULT_URL = "https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit";

declare global {
  interface Window {
    turnstile?: {
      render: (container: string | HTMLElement, options: Record<string, unknown>) => string;
      execute: (widgetId: string) => void;
      reset: (widgetId: string) => void;
    };
  }
}

let scriptLoadPromise: Promise<void> | null = null;
let currentExecution: Promise<string> | null = null;

function getSiteKey(): string {
  return (import.meta.env.VITE_TURNSTILE_SITE_KEY || "").trim();
}

function isBrowser(): boolean {
  return typeof window !== "undefined" && typeof document !== "undefined";
}

function ensureTurnstileScript(): Promise<void> {
  if (scriptLoadPromise) {
    return scriptLoadPromise;
  }

  scriptLoadPromise = new Promise((resolve, reject) => {
    if (!isBrowser()) {
      reject(new Error("Turnstile can only run in browser context."));
      return;
    }

    if (window.turnstile) {
      resolve();
      return;
    }

    const existing = document.getElementById(TURNSTILE_SCRIPT_ID) as HTMLScriptElement | null;
    if (existing) {
      existing.addEventListener("load", () => resolve(), { once: true });
      existing.addEventListener("error", () => reject(new Error("Failed to load Turnstile script.")), { once: true });
      return;
    }

    const script = document.createElement("script");
    script.id = TURNSTILE_SCRIPT_ID;
    script.src = TURNSTILE_DEFAULT_URL;
    script.async = true;
    script.defer = true;
    script.onload = () => resolve();
    script.onerror = () => reject(new Error("Failed to load Turnstile script."));
    document.head.appendChild(script);
  });

  return scriptLoadPromise;
}

export function isTurnstileEnabled(): boolean {
  return !!getSiteKey();
}

export async function getTurnstileToken(action: string): Promise<string> {
  const siteKey = getSiteKey();
  if (!siteKey) {
    throw new Error("Turnstile site key is not configured.");
  }

  if (!isBrowser()) {
    throw new Error("Turnstile can only run in browser context.");
  }

  if (currentExecution) {
    return currentExecution;
  }

  currentExecution = new Promise<string>(async (resolve, reject) => {
    try {
      await ensureTurnstileScript();
      if (!window.turnstile) {
        reject(new Error("Turnstile API unavailable."));
        return;
      }

      const container = document.createElement("div");
      container.style.display = "none";
      document.body.appendChild(container);

      const timeout = window.setTimeout(() => {
        container.remove();
        reject(new Error("Turnstile verification timeout."));
      }, 20000);

      let settled = false;
      const widgetId = window.turnstile.render(container, {
        sitekey: siteKey,
        // Use visible Turnstile widget (normal mode).
        // This project previously used "invisible" to avoid UI,
        // but user requested the widget be visible.
        size: "normal",
        action,
        callback: (token: string) => {
          if (!settled) {
            settled = true;
            clearTimeout(timeout);
            container.remove();
            resolve(token);
          }
        },
        "error-callback": () => {
          if (!settled) {
            settled = true;
            clearTimeout(timeout);
            container.remove();
            reject(new Error("Turnstile verification failed."));
          }
        },
        "expired-callback": () => {
          if (!settled) {
            settled = true;
            clearTimeout(timeout);
            container.remove();
            reject(new Error("Turnstile token expired."));
          }
        },
      });

      window.turnstile.execute(widgetId);
    } catch (error) {
      reject(error instanceof Error ? error : new Error("Failed to verify captcha."));
    }
  }).finally(() => {
    currentExecution = null;
  });

  return currentExecution;
}
