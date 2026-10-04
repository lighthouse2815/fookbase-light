export function getSessionDeviceType(userAgent: string | null) {
  if (!userAgent?.trim()) return 'unknownDevice'
  if (/ipad|tablet|kindle|silk|playbook/i.test(userAgent)
    || (/android/i.test(userAgent) && !/mobile/i.test(userAgent))) return 'tabletDevice'
  if (/iphone|ipod|mobile|windows phone/i.test(userAgent)) return 'phoneDevice'
  if (/windows|macintosh|mac os|linux|x11|cros/i.test(userAgent)) return 'computerDevice'
  return 'unknownDevice'
}
