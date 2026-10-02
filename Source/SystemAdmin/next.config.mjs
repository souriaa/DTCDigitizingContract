import { readFileSync } from "node:fs";

const branding = JSON.parse(readFileSync(new URL("../Shared/application-branding.json", import.meta.url), "utf8"));
if (typeof branding.name !== "string" || !branding.name.trim() || branding.name !== branding.name.trim() || /[\r\n]/.test(branding.name)) {
  throw new Error("Shared application name must be nonblank, trimmed and single-line.");
}

/** @type {import('next').NextConfig} */
const nextConfig = {
  env: { NEXT_PUBLIC_APP_NAME: branding.name },
  output: "standalone",
  async headers() {
    return ["/verify-recovery-email", "/forgot-password", "/reset-password"].map(source => ({ source, headers: [
      { key: "Referrer-Policy", value: "no-referrer" },
      { key: "Cache-Control", value: "no-store" },
    ] }));
  },
  images: {
    unoptimized: true,
  },
}

export default nextConfig
