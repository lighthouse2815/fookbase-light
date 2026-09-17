export function publicProfileHandle(value: string | null | undefined) {
  if (!value || value.includes('@')) return null

  const digits = [...value].filter((character) => /\d/.test(character)).length
  return digits >= 8 && /^[\d\s+().-]+$/.test(value) ? null : value
}
