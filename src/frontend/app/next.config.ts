import type { NextConfig } from 'next'

const apiUrl = process.env.NEXT_PUBLIC_API_URL

const nextConfig: NextConfig = {
  // Allow cross-origin requests to the .NET 8 backend in development
  ...(apiUrl
    ? {
        async rewrites() {
          return [
            {
              source: '/api/:path*',
              destination: `${apiUrl}/:path*`,
            },
          ]
        },
      }
    : {}),
}

export default nextConfig
