import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  env: {
    APP_NAME: process.env.APP_NAME ?? "PoolPredict",
  },
  output: "standalone"
};

export default nextConfig;
