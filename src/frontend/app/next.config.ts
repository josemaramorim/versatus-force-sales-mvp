import type { NextConfig } from 'next'

const nextConfig: NextConfig = {
  // Allow cross-origin requests to the .NET 8 backend in development
  async rewrites() {
    return [
      {
        source: '/api/:path*',
        destination: `${process.env.NEXT_PUBLIC_API_URL}/:path*`,
      },
    ]
  },
}

export default nextConfig
