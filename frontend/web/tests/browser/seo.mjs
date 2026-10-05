import assert from 'node:assert/strict'
import { postFixtures } from './postInteractionsFixture.mjs'

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const baseUrl = process.env.SEO_BASE_URL ?? 'http://127.0.0.1:5197'
const siteUrl = process.env.SEO_SITE_URL ?? 'https://fookbase.io.vn/'
const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_EXECUTABLE, args: ['--no-sandbox'] })

const escapeExpression = (value) => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
const attribute = (tag, name) => {
  const match = tag.match(new RegExp(`\\b${escapeExpression(name)}\\s*=\\s*(["'])(.*?)\\1`, 'i'))
  return match?.[2] ?? null
}
const matchingTag = (html, tagName, name, value) =>
  (html.match(new RegExp(`<${tagName}\\b[^>]*>`, 'gi')) ?? []).find((tag) => attribute(tag, name) === value) ?? null
const robotsValue = async (page) => page.locator('meta[name="robots"]').getAttribute('content')
const trackPageErrors = (page, errors) => page.on('pageerror', (error) => errors.push(error.stack ?? error.message))
const mockGoogleProvider = async (context) => {
  await context.route('**/api/auth/providers', (route) => route.fulfill({ json: { data: { google: false } } }))
}

try {
  const rawContext = await browser.newContext()
  const rawResponse = await rawContext.request.get(`${baseUrl}/`)
  assert.equal(rawResponse.status(), 200)
  const rawHtml = await rawResponse.text()
  const title = rawHtml.match(/<title>([\s\S]*?)<\/title>/i)?.[1]?.trim()
  const canonicalTag = matchingTag(rawHtml, 'link', 'rel', 'canonical')
  const canonical = canonicalTag && attribute(canonicalTag, 'href')
  const descriptionTag = matchingTag(rawHtml, 'meta', 'name', 'description')
  const description = descriptionTag && attribute(descriptionTag, 'content')
  const structuredData = [...rawHtml.matchAll(/<script\b[^>]*\btype\s*=\s*(["'])application\/ld\+json\1[^>]*>([\s\S]*?)<\/script>/gi)]
    .map((match) => JSON.parse(match[2]))
    .find((value) => value['@type'] === 'WebSite')

  assert.match(rawHtml, /<html\b[^>]*\blang\s*=\s*(["'])vi\1/i)
  assert.match(rawHtml, /<h1\b[^>]*>\s*Fookbase\s*[–-]\s*Mạng xã hội để kết nối, chia sẻ và trò chuyện\.?\s*<\/h1>/i)
  assert.equal(title, 'Fookbase Light | Mạng xã hội kết nối, chia sẻ và trò chuyện')
  assert.ok(canonical, 'Homepage needs a canonical URL')
  assert.equal(canonical, new URL(siteUrl).href, 'Canonical must use the configured public domain')
  assert.equal(new URL(canonical).pathname, '/')
  assert.ok(description?.includes('Fookbase Light (fookbase-light)'), 'Homepage needs the intended Vietnamese description')
  assert.deepEqual(structuredData, {
    '@context': 'https://schema.org',
    '@type': 'WebSite',
    '@id': `${canonical}#website`,
    name: 'Fookbase',
    alternateName: ['Fookbase Light', 'fookbase-light'],
    url: canonical,
    description,
    inLanguage: 'vi',
  })
  console.log('PASS raw homepage includes crawlable title, description, canonical, WebSite JSON-LD and landing H1')

  const robotsResponse = await rawContext.request.get(`${baseUrl}/robots.txt`)
  assert.equal(robotsResponse.status(), 200)
  assert.match(robotsResponse.headers()['content-type'] ?? '', /^text\/plain\b/i)
  const robots = await robotsResponse.text()
  assert.match(robots, new RegExp(`^Sitemap: ${escapeExpression(new URL('sitemap.xml', canonical).href)}$`, 'm'))
  assert.doesNotMatch(robots, /^Disallow:\s*\/assets\/?\s*$/m)
  assert.doesNotMatch(robots, /^Disallow:\s*\/login\/?\s*$/m)

  const sitemapResponse = await rawContext.request.get(`${baseUrl}/sitemap.xml`)
  assert.equal(sitemapResponse.status(), 200)
  const sitemap = await sitemapResponse.text()
  await rawContext.close()
  console.log('PASS robots stays plain text, advertises the canonical sitemap, and keeps public assets and login crawlable')

  const noScriptContext = await browser.newContext({ javaScriptEnabled: false, locale: 'en-US', viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true })
  const noScriptPage = await noScriptContext.newPage()
  await noScriptPage.goto(`${baseUrl}/`)
  await noScriptPage.getByRole('heading', { level: 1, name: /Fookbase.*Mạng xã hội để kết nối, chia sẻ và trò chuyện/ }).waitFor()
  await noScriptPage.getByRole('link', { name: 'Tham gia Fookbase', exact: true }).waitFor()
  assert.equal(await noScriptPage.locator('html').getAttribute('lang'), 'vi')
  const staticGeometry = await noScriptPage.evaluate(() => ({
    viewport: window.innerWidth,
    documentWidth: document.documentElement.scrollWidth,
    bodyWidth: document.body.scrollWidth,
  }))
  assert.ok(staticGeometry.documentWidth <= staticGeometry.viewport, `Static page overflows horizontally: ${staticGeometry.documentWidth}px > ${staticGeometry.viewport}px`)
  assert.ok(staticGeometry.bodyWidth <= staticGeometry.viewport, `Static body overflows horizontally: ${staticGeometry.bodyWidth}px > ${staticGeometry.viewport}px`)
  await noScriptContext.close()
  console.log('PASS 390px no-JavaScript homepage retains the H1 and CTA without horizontal overflow')

  const guestContext = await browser.newContext({ locale: 'en-US', viewport: { width: 1280, height: 900 } })
  await mockGoogleProvider(guestContext)
  const guestPage = await guestContext.newPage()
  const guestErrors = []
  trackPageErrors(guestPage, guestErrors)
  await guestPage.goto(`${baseUrl}/`)
  await guestPage.getByRole('heading', { level: 1, name: /Fookbase.*Mạng xã hội để kết nối, chia sẻ và trò chuyện/ }).waitFor()
  assert.equal(new URL(guestPage.url()).pathname, '/')
  assert.equal(await guestPage.locator('html').getAttribute('lang'), 'vi')
  assert.match(await robotsValue(guestPage) ?? '', /^index, follow\b/)

  await guestPage.locator('header a[href="/login"]').click()
  await guestPage.waitForURL(`${baseUrl}/login`)
  await guestPage.locator('form').waitFor()
  assert.equal(await guestPage.locator('html').getAttribute('lang'), 'en')
  assert.match(await robotsValue(guestPage) ?? '', /^noindex, follow\b/)

  await guestPage.goBack()
  await guestPage.waitForURL(`${baseUrl}/`)
  await guestPage.getByRole('heading', { level: 1, name: /Fookbase.*Mạng xã hội để kết nối, chia sẻ và trò chuyện/ }).waitFor()
  assert.equal(await guestPage.locator('html').getAttribute('lang'), 'vi')
  assert.match(await robotsValue(guestPage) ?? '', /^index, follow\b/)
  console.log('PASS guest homepage remains public, login is noindex, and browser back restores the indexed landing page')

  const protectedPage = await guestContext.newPage()
  trackPageErrors(protectedPage, guestErrors)
  await protectedPage.goto(`${baseUrl}/settings/security`)
  await protectedPage.waitForURL(`${baseUrl}/login`)
  await protectedPage.locator('form').waitFor()
  assert.equal(await protectedPage.locator('html').getAttribute('lang'), 'en')
  assert.match(await robotsValue(protectedPage) ?? '', /^noindex, follow\b/)
  assert.deepEqual(guestErrors, [])
  await guestContext.close()
  console.log('PASS a guest deep link to security redirects to a noindex login page')

  const parserContext = await browser.newContext()
  const parserPage = await parserContext.newPage()
  const parsedSitemap = await parserPage.evaluate((xml) => {
    const document = new DOMParser().parseFromString(xml, 'application/xml')
    return {
      parserError: document.querySelector('parsererror')?.textContent ?? null,
      root: document.documentElement.localName,
      urls: [...document.querySelectorAll('url')].map((entry) => entry.querySelector('loc')?.textContent ?? null),
    }
  }, sitemap)
  assert.equal(parsedSitemap.parserError, null)
  assert.equal(parsedSitemap.root, 'urlset')
  assert.deepEqual(parsedSitemap.urls, [canonical])
  await parserContext.close()
  console.log('PASS sitemap XML is valid and lists only the canonical root page')

  const authenticatedContext = await browser.newContext({ viewport: { width: 1280, height: 900 } })
  await postFixtures(authenticatedContext)
  const authenticatedPage = await authenticatedContext.newPage()
  await authenticatedPage.goto(`${baseUrl}/`)
  await authenticatedPage.waitForURL(`${baseUrl}/feed`)
  await authenticatedPage.getByText('Bảng tin Fookbase', { exact: true }).waitFor()
  await authenticatedContext.close()
  console.log('PASS an authenticated root visit still continues to the feed')
} finally {
  await browser.close()
}
