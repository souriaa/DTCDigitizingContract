/** @type {import('next').NextConfig} */
const nextConfig = {
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
