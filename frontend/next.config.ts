import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Produces a small self-contained server bundle for the shop PC deployment.
  output: "standalone",
  // Next writes assistant instruction files into the project root unless this is turned off.
  agentRules: false,
  // CI/verification can use an isolated directory while a developer has `next dev` running.
  distDir: process.env.NEXT_DIST_DIR ?? ".next",
};

export default nextConfig;
