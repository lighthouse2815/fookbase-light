import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import { defineConfig, loadEnv, type HtmlTagDescriptor, type Plugin } from 'vite'
import LandingContent from './src/pages/home/LandingContent.tsx'
import { site } from './src/seo/site.ts'

function seo(siteUrl: string): Plugin {
  const url = new URL(siteUrl)
  if (!['https:', 'http:'].includes(url.protocol) || url.username || url.password || url.pathname !== '/' || url.search || url.hash) {
    throw new Error('VITE_SITE_URL phải là HTTP(S) origin, không có path, query, hash hoặc credential.')
  }
  const homeUrl = url.href
  const imageUrl = new URL('fookbase.png', homeUrl).href
  const robots = `User-agent: *\nAllow: /\nDisallow: /api/\nDisallow: /hubs/\n\nSitemap: ${homeUrl}sitemap.xml\n`
  const sitemap = `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n  <url><loc>${homeUrl}</loc></url>\n</urlset>\n`
  const meta = (name: string, content: string): HtmlTagDescriptor => ({ tag: 'meta', attrs: { name, content } })
  const og = (property: string, content: string): HtmlTagDescriptor => ({ tag: 'meta', attrs: { property, content } })

  return {
    name: 'fookbase-seo',
    transformIndexHtml(html, context) {
      const isHome = !context.server || new URL(context.originalUrl ?? context.path, 'http://localhost').pathname === '/'
      return {
        html: isHome ? html.replace('<div id="root"></div>', `<div id="root"><div data-seo-landing>${renderToStaticMarkup(createElement(LandingContent))}</div></div>`) : html,
        tags: [
          // Static hosting serves this entry for every route. Run before paint,
          // without waiting for the app bundle, and hide only the static landing.
          { tag: 'script', children: `(() => {
            let isAppPage = window.location.pathname !== '/';
            try {
              isAppPage ||= Boolean(JSON.parse(localStorage.getItem('fookbase.session') ?? 'null'));
            } catch {}
            if (isAppPage) {
              const style = document.createElement('style');
              style.textContent = '#root > [data-seo-landing] { display: none; }';
              document.head.appendChild(style);
            }
          })();` },
          { tag: 'title', children: isHome ? site.title : 'Fookbase' },
          meta('description', site.description),
          meta('robots', isHome ? 'index, follow, max-image-preview:large' : 'noindex, follow'),
          { tag: 'link', attrs: { rel: 'canonical', href: homeUrl } },
          og('og:type', 'website'),
          og('og:site_name', site.name),
          og('og:locale', 'vi_VN'),
          og('og:title', site.title),
          og('og:description', site.description),
          og('og:url', homeUrl),
          og('og:image', imageUrl),
          og('og:image:width', '1254'),
          og('og:image:height', '1254'),
          og('og:image:alt', 'Logo Fookbase'),
          meta('twitter:card', 'summary'),
          meta('twitter:title', site.title),
          meta('twitter:description', site.description),
          meta('twitter:image', imageUrl),
          meta('twitter:image:alt', 'Logo Fookbase'),
          { tag: 'script', attrs: { type: 'application/ld+json' }, children: JSON.stringify({
            '@context': 'https://schema.org', '@type': 'WebSite', '@id': `${homeUrl}#website`,
            name: site.name, alternateName: site.alternateNames, url: homeUrl, description: site.description, inLanguage: 'vi',
          }) },
        ].map(tag => ({ ...tag, injectTo: 'head' as const })),
      }
    },
    generateBundle() {
      this.emitFile({ type: 'asset', fileName: 'robots.txt', source: robots })
      this.emitFile({ type: 'asset', fileName: 'sitemap.xml', source: sitemap })
    },
    configureServer(server) {
      server.middlewares.use((request, response, next) => {
        const pathname = new URL(request.url ?? '/', 'http://localhost').pathname
        if (pathname !== '/robots.txt' && pathname !== '/sitemap.xml') return next()
        response.setHeader('Content-Type', pathname === '/robots.txt' ? 'text/plain; charset=utf-8' : 'application/xml; charset=utf-8')
        response.end(pathname === '/robots.txt' ? robots : sitemap)
      })
    },
  }
}

// https://vite.dev/config/
export default defineConfig(({ mode }) => ({
  plugins: [tailwindcss(), react(), seo(loadEnv(mode, process.cwd(), 'VITE_').VITE_SITE_URL ?? site.url)],
  server: {
    host: '0.0.0.0',
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5000',
        changeOrigin: true,
      },
      '/hubs': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5000',
        changeOrigin: true,
        ws: true,
      },
    },
  },
}))
