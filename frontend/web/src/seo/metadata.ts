import { site } from './site'

export function updateRouteMetadata(pathname: string, language = 'vi') {
  const isHome = pathname === '/'
  document.documentElement.lang = isHome ? 'vi' : language
  document.title = isHome ? site.title : pathname === '/login' ? 'Đăng nhập | Fookbase' : 'Fookbase'
  const robots = document.querySelector<HTMLMetaElement>('meta[name="robots"]')
  if (robots) robots.content = isHome ? 'index, follow, max-image-preview:large' : 'noindex, follow'
}
