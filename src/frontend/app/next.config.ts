import type { NextConfig } from 'next'
import { execSync } from 'child_process'

// Captura a versão do Git em tempo de build (tag + branch)
let gitVersion = '1.0.0-dev'
try {
  const tag = execSync('git describe --tags --always').toString().trim()
  const branch = execSync('git rev-parse --abbrev-ref HEAD').toString().trim()
  gitVersion = `${tag}+${branch}`
} catch {
  // Fallback: ambiente sem Git disponível (Docker clean build, CI sem .git)
}

const apiUrl = process.env.NEXT_PUBLIC_API_URL

const nextConfig: NextConfig = {
  env: {
    NEXT_PUBLIC_APP_VERSION: gitVersion,
  },
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
