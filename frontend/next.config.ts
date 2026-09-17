import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Next writes assistant instruction files into the project root unless this is turned off.
  agentRules: false,
};

export default nextConfig;
